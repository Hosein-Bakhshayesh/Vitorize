using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vitorize.Application.Common;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests;

public sealed class SecureBootstrapSeedTests
{
    private const string BootstrapMobile = "09110000001";
    private const string BootstrapPassword = "Test-only!9xQv7#2026";
    private const string BootstrapName = "Test Bootstrap Administrator";

    [Fact]
    public async Task Production_startup_does_not_create_a_user_by_default()
    {
        await using var fixture = CreateFixture(Environments.Production);

        await fixture.Service.SeedAsync();

        Assert.Empty(await fixture.Db.Users.ToListAsync());
    }

    [Fact]
    public async Task Roles_still_seed_idempotently()
    {
        await using var fixture = CreateFixture(Environments.Production);

        await fixture.Service.SeedReferenceDataAsync();
        await fixture.Service.SeedReferenceDataAsync();

        var roleNames = await fixture.Db.Roles.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync();
        Assert.Equal(new[] { "Admin", "Customer", "SuperAdmin", "Support" }, roleNames);
        Assert.Empty(await fixture.Db.Users.ToListAsync());
    }

    [Fact]
    public async Task Bootstrap_disabled_does_nothing_even_when_values_are_present()
    {
        await using var fixture = CreateFixture(
            Environments.Production,
            new BootstrapAdminOptions
            {
                Enabled = false,
                Mobile = BootstrapMobile,
                Password = BootstrapPassword,
                FullName = BootstrapName
            });

        await fixture.Service.SeedAsync();

        Assert.Empty(await fixture.Db.Users.ToListAsync());
    }

    [Fact]
    public async Task Bootstrap_enabled_with_missing_secrets_fails_without_creating_a_user()
    {
        await using var fixture = CreateFixture(
            Environments.Production,
            new BootstrapAdminOptions { Enabled = true });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.SeedAsync());

        Assert.Contains("not all configured", exception.Message, StringComparison.Ordinal);
        Assert.Empty(await fixture.Db.Users.ToListAsync());
    }

    [Fact]
    public async Task Bootstrap_creates_exactly_one_SuperAdmin_and_a_safe_security_event()
    {
        await using var fixture = CreateFixture(Environments.Production, EnabledBootstrap());

        await fixture.Service.SeedAsync();

        var user = await fixture.Db.Users.Include(x => x.Roles).SingleAsync();
        Assert.Equal(BootstrapMobile, user.Mobile);
        Assert.Single(user.Roles);
        Assert.Equal("SuperAdmin", user.Roles.Single().Name);
        var securityEvent = await fixture.Db.SecurityLogs.SingleAsync();
        Assert.Equal("BootstrapSuperAdminCreated", securityEvent.EventType);
        Assert.Equal(user.Id, securityEvent.UserId);
    }

    [Fact]
    public async Task Re_running_bootstrap_does_not_duplicate_users()
    {
        await using var fixture = CreateFixture(Environments.Production, EnabledBootstrap());

        await fixture.Service.SeedAsync();
        await fixture.Service.SeedAsync();

        Assert.Equal(1, await fixture.Db.Users.CountAsync());
        Assert.Equal(1, await fixture.Db.SecurityLogs.CountAsync(x => x.EventType == "BootstrapSuperAdminCreated"));
    }

    [Fact]
    public async Task Existing_SuperAdmin_prevents_bootstrap_and_is_not_overwritten()
    {
        await using var fixture = CreateFixture(Environments.Production, EnabledBootstrap());
        await fixture.Service.SeedReferenceDataAsync();
        var role = await fixture.Db.Roles.SingleAsync(x => x.Name == "SuperAdmin");
        var existing = ExistingUser("09110000002", "existing-password-hash", role);
        fixture.Db.Users.Add(existing);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.SeedAsync();

        var stored = await fixture.Db.Users.Include(x => x.Roles).SingleAsync();
        Assert.Equal(existing.Id, stored.Id);
        Assert.Equal("existing-password-hash", stored.PasswordHash);
        Assert.Equal(1, await fixture.Db.Users.CountAsync());
        Assert.Empty(await fixture.Db.SecurityLogs.Where(x => x.EventType == "BootstrapSuperAdminCreated").ToListAsync());
    }

    [Fact]
    public async Task Existing_user_with_bootstrap_mobile_is_never_overwritten_or_promoted()
    {
        await using var fixture = CreateFixture(Environments.Production, EnabledBootstrap());
        await fixture.Service.SeedReferenceDataAsync();
        var customerRole = await fixture.Db.Roles.SingleAsync(x => x.Name == "Customer");
        var existing = ExistingUser(BootstrapMobile, "unchanged-password-hash", customerRole);
        fixture.Db.Users.Add(existing);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.SeedAsync());

        var stored = await fixture.Db.Users.Include(x => x.Roles).SingleAsync();
        Assert.Equal(existing.Id, stored.Id);
        Assert.Equal("unchanged-password-hash", stored.PasswordHash);
        Assert.Equal(new[] { "Customer" }, stored.Roles.Select(x => x.Name));
    }

    [Fact]
    public async Task Bootstrap_password_is_BCrypt_hashed()
    {
        await using var fixture = CreateFixture(Environments.Production, EnabledBootstrap());

        await fixture.Service.SeedAsync();

        var storedHash = await fixture.Db.Users.Select(x => x.PasswordHash).SingleAsync();
        Assert.NotEqual(BootstrapPassword, storedHash);
        Assert.True(PasswordHasher.Verify(BootstrapPassword, storedHash));
    }

    [Fact]
    public async Task Bootstrap_password_is_never_written_to_application_or_security_logs()
    {
        await using var fixture = CreateFixture(Environments.Production, EnabledBootstrap());

        await fixture.Service.SeedAsync();

        Assert.DoesNotContain(
            fixture.Logger.Messages,
            message => message.Contains(BootstrapPassword, StringComparison.Ordinal));
        var descriptions = await fixture.Db.SecurityLogs.Select(x => x.Description).ToListAsync();
        Assert.DoesNotContain(
            descriptions,
            description => description?.Contains(BootstrapPassword, StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Demo_customer_is_ignored_outside_Development()
    {
        var demo = EnabledDemo();

        await using var production = CreateFixture(Environments.Production, demo: demo);
        await production.Service.SeedAsync();
        Assert.Empty(await production.Db.Users.ToListAsync());

        await using var staging = CreateFixture(Environments.Staging, demo: demo);
        await staging.Service.SeedAsync();
        Assert.Empty(await staging.Db.Users.ToListAsync());
    }

    [Fact]
    public async Task Explicit_demo_customer_can_only_be_created_in_Development()
    {
        await using var fixture = CreateFixture(Environments.Development, demo: EnabledDemo());

        await fixture.Service.SeedAsync();

        var user = await fixture.Db.Users.Include(x => x.Roles).SingleAsync();
        Assert.Equal("Customer", user.Roles.Single().Name);
        Assert.Equal("DevelopmentDemoUserCreated", (await fixture.Db.SecurityLogs.SingleAsync()).EventType);
    }

    private static BootstrapAdminOptions EnabledBootstrap() => new()
    {
        Enabled = true,
        Mobile = BootstrapMobile,
        Password = BootstrapPassword,
        FullName = BootstrapName
    };

    private static DevelopmentDemoUserOptions EnabledDemo() => new()
    {
        Enabled = true,
        Mobile = "09110000003",
        Password = "Development-only!5rT9",
        FullName = "Development Demo Customer"
    };

    private static User ExistingUser(string mobile, string passwordHash, Role role)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Existing User",
            Mobile = mobile,
            PasswordHash = passwordHash,
            Status = (byte)UserStatus.Active,
            VerificationStatus = (byte)VerificationStatus.Pending,
            IsMobileConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
        user.Roles.Add(role);
        return user;
    }

    private static SeedFixture CreateFixture(
        string environmentName,
        BootstrapAdminOptions? bootstrap = null,
        DevelopmentDemoUserOptions? demo = null)
    {
        var dbOptions = new DbContextOptionsBuilder<VitorizeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new VitorizeDbContext(dbOptions);
        var logger = new ListLogger<VitorizeSeedService>();
        var service = new VitorizeSeedService(
            db,
            Options.Create(bootstrap ?? new BootstrapAdminOptions()),
            Options.Create(demo ?? new DevelopmentDemoUserOptions()),
            new TestHostEnvironment(environmentName),
            logger);

        return new SeedFixture(db, service, logger);
    }

    private sealed class SeedFixture : IAsyncDisposable
    {
        public SeedFixture(
            VitorizeDbContext db,
            VitorizeSeedService service,
            ListLogger<VitorizeSeedService> logger)
        {
            Db = db;
            Service = service;
            Logger = logger;
        }

        public VitorizeDbContext Db { get; }
        public VitorizeSeedService Service { get; }
        public ListLogger<VitorizeSeedService> Logger { get; }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Vitorize.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
