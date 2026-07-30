using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Vitorize.Api.Controllers;
using Vitorize.Api.Services;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Common.Zarinpal;
using Vitorize.Infrastructure.Persistence;
using Xunit;

namespace Vitorize.Tests;

public sealed class HealthProbeTests
{
    [Fact]
    public async Task Legacy_and_explicit_readiness_routes_report_unavailable_dependency_without_details()
    {
        var readiness = Substitute.For<IReadinessProbe>();
        readiness.IsReadyAsync(Arg.Any<CancellationToken>()).Returns(false);
        var controller = CreateController(readiness);

        var legacy = Assert.IsType<ObjectResult>(await controller.Check(CancellationToken.None));
        var explicitReadiness = Assert.IsType<ObjectResult>(await controller.Readiness(CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, legacy.StatusCode);
        Assert.Equal("{\"Status\":\"Unhealthy\"}", JsonSerializer.Serialize(legacy.Value));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, explicitReadiness.StatusCode);
        Assert.Equal("{\"Status\":\"NotReady\"}", JsonSerializer.Serialize(explicitReadiness.Value));
    }

    [Fact]
    public async Task Protected_diagnostics_do_not_echo_provider_validation_errors_or_merchant_identifiers()
    {
        var readiness = Substitute.For<IReadinessProbe>();
        readiness.IsReadyAsync(Arg.Any<CancellationToken>()).Returns(true);
        var payment = Substitute.For<IZarinpalPaymentConfigurationProvider>();
        payment.GetAsync(Arg.Any<CancellationToken>()).Returns(new ZarinpalPaymentConfiguration(
            "merchant-secret-that-must-not-be-returned", false, null, null, null, null));
        payment.ValidateAsync(Arg.Any<CancellationToken>()).Returns(
            new ZarinpalConfigurationValidation(false, ["provider-error-secret-that-must-not-be-returned"]));
        var controller = CreateController(readiness, payment);

        var result = Assert.IsType<OkObjectResult>(await controller.CheckDatabaseOnly(CancellationToken.None));
        var body = JsonSerializer.Serialize(result.Value).ToLowerInvariant();

        Assert.Contains("payment configuration is invalid", body);
        Assert.DoesNotContain("merchant-secret-that-must-not-be-returned", body);
        Assert.DoesNotContain("provider-error-secret-that-must-not-be-returned", body);
    }

    [Fact]
    public void Liveness_has_no_dependency_check_and_returns_a_minimal_healthy_status()
    {
        var result = Assert.IsType<OkObjectResult>(new LivenessController().Check());
        Assert.Equal("{\"Status\":\"Healthy\"}", JsonSerializer.Serialize(result.Value));
    }

    private static HealthController CreateController(
        IReadinessProbe readiness,
        IZarinpalPaymentConfigurationProvider? payment = null)
    {
        var options = new DbContextOptionsBuilder<VitorizeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var settings = Substitute.For<ISettingService>();
        settings.GetValueAsync("SiteName").Returns("Vitorize");
        if (payment is null)
        {
            payment = Substitute.For<IZarinpalPaymentConfigurationProvider>();
            payment.GetAsync(Arg.Any<CancellationToken>()).Returns(new ZarinpalPaymentConfiguration("", null, null, null, null, null));
            payment.ValidateAsync(Arg.Any<CancellationToken>()).Returns(ZarinpalConfigurationValidation.Valid);
        }

        return new HealthController(
            new VitorizeDbContext(options), settings, payment, readiness,
            Substitute.For<ILogger<HealthController>>());
    }
}
