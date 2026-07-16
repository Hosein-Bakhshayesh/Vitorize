using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Vitorize.Application.DTOs.Auth;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Infrastructure.Services.Sms;
using Vitorize.Domain.Entities;
using Vitorize.Shared.Common;

namespace Vitorize.IntegrationTests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqlServerIntegrationCollection : ICollectionFixture<IntegrationTestFixture>
{
    public const string Name = "SQL Server API integration";
}

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private const string JwtSecret = "integration-tests-jwt-secret-key-000000000000";
    private const string EncryptionKey = "integration-tests-key-32-bytes!!";
    private readonly string _server = Environment.GetEnvironmentVariable("VITORIZE_INTEGRATION_SQL_SERVER") ?? ".";
    private readonly string _databaseName = $"VitorizeIntegration_{Environment.ProcessId}_{Guid.NewGuid():N}";
    private readonly Dictionary<string, string?> _previousEnvironment = new(StringComparer.Ordinal);

    public string RepositoryRoot { get; } = FindRepositoryRoot();
    public string ConnectionString { get; private set; } = string.Empty;
    public VitorizeApiFactory Factory { get; private set; } = null!;
    public int InitialPrivilegedUserCount { get; private set; }

    public async Task InitializeAsync()
    {
        ConnectionString = new SqlConnectionStringBuilder
        {
            DataSource = _server,
            InitialCatalog = _databaseName,
            IntegratedSecurity = true,
            Encrypt = true,
            TrustServerCertificate = true,
            MultipleActiveResultSets = true
        }.ConnectionString;

        await AssertSqlServer2022OrLaterAsync();
        await RunAsync("sqlpackage", new[]
        {
            "/Action:Publish",
            $"/SourceFile:{Path.Combine(RepositoryRoot, "Database", "Baseline", "VitorizeDb.schema-candidate.dacpac")}",
            $"/TargetConnectionString:{ConnectionString}",
            "/p:BlockOnPossibleDataLoss=True",
            "/p:DropObjectsNotInSource=False",
            "/Quiet:True"
        });

        await RunAsync("powershell", new[]
        {
            "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
            Path.Combine(RepositoryRoot, "Database", "Deploy-Database.ps1"),
            "-ServerInstance", _server,
            "-Database", _databaseName,
            "-Environment", "Development",
            "-ConfirmDatabaseName", _databaseName,
            "-LogDirectory", Path.Combine(RepositoryRoot, ".runtime-verification", "integration-db-logs")
        });

        SetHostEnvironment();
        Factory = new VitorizeApiFactory(ConnectionString);
        _ = Factory.Services;
        await using var db = CreateDbContext();
        InitialPrivilegedUserCount = await db.Users.CountAsync(x =>
            x.Roles.Any(r => r.Name == "Admin" || r.Name == "SuperAdmin"));
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
            await Factory.DisposeAsync();

        var master = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" }.ConnectionString;
        await using var connection = new SqlConnection(master);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{_databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]; END";
        await command.ExecuteNonQueryAsync();
        RestoreHostEnvironment();
    }

    public VitorizeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<VitorizeDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new VitorizeDbContext(options);
    }

    public Task RunSqlFileAsync(string repositoryRelativePath) => RunAsync("sqlcmd", new[]
    {
        "-S", _server, "-d", _databaseName, "-E", "-b", "-I", "-f", "65001",
        "-i", Path.Combine(RepositoryRoot, repositoryRelativePath)
    });

    public HttpClient CreateClient(string? accessToken = null)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        if (!string.IsNullOrWhiteSpace(accessToken))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public async Task<AuthResponseDto> RegisterAsync(HttpClient client, string? mobile = null)
    {
        mobile ??= $"0912{Random.Shared.Next(1000000, 9999999)}";
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FullName = "کاربر تست یکپارچه",
            Mobile = mobile,
            Email = $"integration-{Guid.NewGuid():N}@example.test",
            Password = "Secure-Test-Password-123!"
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResult<AuthResponseDto>>();
        body.Should().NotBeNull();
        body!.IsSuccess.Should().BeTrue();
        return body.Data!;
    }

    public async Task<(User User, string AccessToken)> CreateUserAndTokenAsync(params string[] roles)
    {
        await using var db = CreateDbContext();
        var selectedRoles = await db.Roles.Where(x => roles.Contains(x.Name)).ToListAsync();
        selectedRoles.Select(x => x.Name).Should().BeEquivalentTo(roles);
        var user = new User
        {
            Id = Guid.NewGuid(), FullName = "Integration principal",
            Mobile = $"0935{Random.Shared.Next(1000000, 9999999)}",
            Email = $"principal-{Guid.NewGuid():N}@example.test",
            PasswordHash = PasswordHasher.Hash("Secure-Test-Password-123!"),
            Status = 1, VerificationStatus = 0, IsMobileConfirmed = true,
            IsEmailConfirmed = false, CreatedAt = DateTime.UtcNow
        };
        foreach (var role in selectedRoles)
            user.Roles.Add(role);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        using var scope = Factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(user);
        return (user, token);
    }

    public async Task ConfigureSmsAsync(int otpTemplateId = 1001, int notificationTemplateId = 1002)
    {
        var values = new Dictionary<string, string>
        {
            ["Sms.IsEnabled"] = "true",
            ["Sms.ApiKey"] = "integration-sms-key",
            ["Sms.OtpTemplateId"] = otpTemplateId.ToString(),
            ["Sms.NotificationTemplateId"] = notificationTemplateId.ToString(),
            ["Sms.OtpResendCooldownSeconds"] = "0",
            ["Sms.OtpMaxAttempts"] = "3",
            ["Sms.DailyOtpLimitPerMobile"] = "20"
        };
        await using var db = CreateDbContext();
        var settings = await db.Settings.Where(x => values.Keys.Contains(x.Key)).ToListAsync();
        foreach (var pair in values)
        {
            var setting = settings.SingleOrDefault(x => x.Key == pair.Key);
            if (setting is null)
                db.Settings.Add(new Setting { Id = Guid.NewGuid(), Key = pair.Key, Value = pair.Value, GroupName = "Sms", ValueType = "string" });
            else
                setting.Value = pair.Value;
        }
        await db.SaveChangesAsync();
        Factory.Services.GetRequiredService<ISmsSettingsProvider>().Invalidate();
        ((FakeSmsSender)Factory.Services.GetRequiredService<ISmsSender>()).Clear();
    }

    private async Task AssertSqlServer2022OrLaterAsync()
    {
        var master = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" }.ConnectionString;
        await using var connection = new SqlConnection(master);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CONVERT(int, SERVERPROPERTY('ProductMajorVersion'));";
        var version = Convert.ToInt32(await command.ExecuteScalarAsync());
        if (version < 16)
            throw new InvalidOperationException("The checked-in DACPAC targets SQL Server 2022; integration tests require an isolated SQL Server 2022+ instance.");
    }

    private static async Task RunAsync(string fileName, IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Unable to start {fileName}.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await process.WaitForExitAsync(timeout.Token);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{fileName} failed with exit code {process.ExitCode}.{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Vitorize.sln")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate Vitorize.sln from the integration-test output directory.");
    }

    private void SetHostEnvironment()
    {
        var values = new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Testing",
            ["ConnectionStrings__DefaultConnection"] = ConnectionString,
            ["Jwt__SecretKey"] = JwtSecret,
            ["Jwt__Issuer"] = "Vitorize.Api.IntegrationTests",
            ["Jwt__Audience"] = "Vitorize.IntegrationTests",
            ["Jwt__AccessTokenExpirationMinutes"] = "5",
            ["Jwt__RefreshTokenExpirationDays"] = "1",
            ["Encryption__Key"] = EncryptionKey,
            ["BootstrapAdmin__Enabled"] = "false",
            ["DevelopmentDemoUsers__Enabled"] = "false",
            ["Seq__Enabled"] = "false"
        };
        foreach (var pair in values)
        {
            _previousEnvironment[pair.Key] = Environment.GetEnvironmentVariable(pair.Key);
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }

    private void RestoreHostEnvironment()
    {
        foreach (var pair in _previousEnvironment)
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        _previousEnvironment.Clear();
    }

    internal static IReadOnlyDictionary<string, string?> Configuration(string connectionString) =>
        new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["Jwt:SecretKey"] = JwtSecret,
            ["Jwt:Issuer"] = "Vitorize.Api.IntegrationTests",
            ["Jwt:Audience"] = "Vitorize.IntegrationTests",
            ["Jwt:AccessTokenExpirationMinutes"] = "5",
            ["Jwt:RefreshTokenExpirationDays"] = "1",
            ["Encryption:Key"] = EncryptionKey,
            ["BootstrapAdmin:Enabled"] = "false",
            ["DevelopmentDemoUsers:Enabled"] = "false",
            ["Seq:Enabled"] = "false"
        };
}

public sealed class VitorizeApiFactory : WebApplicationFactory<Vitorize.Api.Program>
{
    private readonly string _connectionString;

    public VitorizeApiFactory(string connectionString) => _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(IntegrationTestFixture.Configuration(_connectionString)));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IZarinpalGatewayService>();
            services.RemoveAll<ISmsSender>();
            services.AddSingleton<IZarinpalGatewayService, FakeZarinpalGateway>();
            services.AddSingleton<ISmsSender, FakeSmsSender>();
        });
    }
}

internal sealed class FakeZarinpalGateway : IZarinpalGatewayService
{
    public Task<(bool Success, string Authority, string PaymentUrl)> CreatePaymentAsync(
        decimal amount, string description, string? mobile = null, string? email = null, string? orderId = null)
    {
        var authority = $"A{Guid.NewGuid():N}";
        return Task.FromResult((true, authority, $"https://payment.test/{authority}"));
    }

    public Task<(bool Success, long RefId)> VerifyPaymentAsync(string authority, decimal amount) =>
        Task.FromResult((true, 123456789L));

    public Task<string> BuildPaymentUrlAsync(string authority) =>
        Task.FromResult($"https://payment.test/{authority}");
}

internal sealed class FakeSmsSender : ISmsSender
{
    public ConcurrentQueue<CapturedSms> Sent { get; } = new();
    public ConcurrentQueue<SmsSendResult> PlannedResults { get; } = new();
    public ConcurrentDictionary<string, SmsSendResult> ResultsByMobile { get; } = new();

    public Task<SmsSendResult> SendVerifyAsync(string apiKey, string mobile, int templateId,
        IReadOnlyList<SmsTemplateParameter> parameters, CancellationToken cancellationToken = default)
    {
        Sent.Enqueue(new CapturedSms(mobile, templateId, parameters.ToArray(), null));
        return Task.FromResult(ResultsByMobile.TryGetValue(mobile, out var targeted)
            ? targeted
            : PlannedResults.TryDequeue(out var result)
            ? result
            : SmsSendResult.Success($"sms-{Guid.NewGuid():N}"));
    }

    public Task<SmsSendResult> SendBulkAsync(string apiKey, long lineNumber, string text, string mobile,
        CancellationToken cancellationToken = default)
    {
        Sent.Enqueue(new CapturedSms(mobile, null, Array.Empty<SmsTemplateParameter>(), text));
        return Task.FromResult(ResultsByMobile.TryGetValue(mobile, out var targeted)
            ? targeted
            : PlannedResults.TryDequeue(out var result)
            ? result
            : SmsSendResult.Success($"sms-{Guid.NewGuid():N}"));
    }

    public Task<SmsAccountStatus> GetAccountStatusAsync(string apiKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SmsAccountStatus { IsSuccess = true, Credit = 1000, Lines = new long[] { 3000 } });

    public void Clear()
    {
        while (Sent.TryDequeue(out _)) { }
        while (PlannedResults.TryDequeue(out _)) { }
        ResultsByMobile.Clear();
    }

    public sealed record CapturedSms(string Mobile, int? TemplateId,
        IReadOnlyList<SmsTemplateParameter> Parameters, string? Text);
}
