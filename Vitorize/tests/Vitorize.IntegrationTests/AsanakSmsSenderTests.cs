using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Vitorize.Application.Models.Sms;
using Vitorize.Infrastructure.Services.Sms;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

public sealed class AsanakSmsSenderTests
{
    [Fact]
    public async Task Template_send_uses_documented_endpoint_credentials_and_positional_parameters()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK,
            """{"meta":{"status":200,"message":"success"},"data":[89545112]}"""));
        var sender = CreateSender(handler);

        var result = await sender.SendVerifyAsync(Options(), "989120000000", 123456,
            [new("CODE", "1234"), new("EXPIRE", "3")]);

        result.IsSuccess.Should().BeTrue();
        result.ProviderMessageId.Should().Be("89545112");
        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Uri.Should().Be("https://sms.asanak.ir/webservice/v2rest/template");
        using var payload = JsonDocument.Parse(request.Body);
        payload.RootElement.GetProperty("username").GetString().Should().Be("asanak-user");
        payload.RootElement.GetProperty("password").GetString().Should().Be("asanak-password");
        payload.RootElement.GetProperty("template_id").GetInt32().Should().Be(123456);
        payload.RootElement.GetProperty("destination").GetString().Should().Be("989120000000");
        payload.RootElement.GetProperty("parameters")[0].GetString().Should().Be("1234");
        payload.RootElement.GetProperty("parameters")[1].GetString().Should().Be("3");
    }

    [Fact]
    public async Task Text_send_and_credit_status_use_documented_asanak_contract()
    {
        var handler = new RecordingHandler(request => request.Uri.EndsWith("getcredit", StringComparison.Ordinal)
            ? Json(HttpStatusCode.OK, """{"meta":{"status":200,"message":"success"},"data":{"credit":928}}""")
            : Json(HttpStatusCode.OK, """{"meta":{"status":200,"message":"success"},"data":[123456]}"""));
        var sender = CreateSender(handler);
        var options = Options();

        var result = await sender.SendBulkAsync(options, "پیام آزمایشی", "989120000000");
        var status = await sender.GetAccountStatusAsync(options);

        result.IsSuccess.Should().BeTrue();
        result.ProviderMessageId.Should().Be("123456");
        status.IsSuccess.Should().BeTrue();
        status.Credit.Should().Be(928m);
        status.Lines.Should().ContainSingle().Which.Should().Be(982100000000L);
        var sendRequest = handler.Requests.First();
        sendRequest.Uri.Should().Be("https://sms.asanak.ir/webservice/v2rest/sendsms");
        using var payload = JsonDocument.Parse(sendRequest.Body);
        payload.RootElement.GetProperty("source").GetString().Should().Be("982100000000");
        payload.RootElement.GetProperty("message").GetString().Should().Be("پیام آزمایشی");
    }

    [Fact]
    public async Task Provider_credit_error_is_mapped_without_exposing_provider_message()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.PaymentRequired,
            """{"meta":{"status":1006,"message":"Credit is not enough"},"data":[]}"""));
        var sender = CreateSender(handler);

        var result = await sender.SendBulkAsync(Options(), "پیام", "989120000000");

        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Be(SmsFailureReason.InsufficientCredit);
    }

    private static SmsOptions Options() => new()
    {
        Provider = "Asanak",
        AsanakUsername = "asanak-user",
        AsanakPassword = "asanak-password",
        AsanakSource = "982100000000"
    };

    private static AsanakSmsSender CreateSender(RecordingHandler handler) =>
        new(new TestHttpClientFactory(new HttpClient(handler) { BaseAddress = new Uri("https://sms.asanak.ir/") }),
            NullLogger<AsanakSmsSender>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private sealed class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(Func<CapturedRequest, HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var captured = new CapturedRequest(
                request.Method,
                request.RequestUri!.ToString(),
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            Requests.Add(captured);
            return response(captured);
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, string Uri, string Body);
}
