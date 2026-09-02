using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Vitorize.Infrastructure.Services.Sms;

namespace Vitorize.IntegrationTests;

public sealed class SmsIrSenderTests
{
    [Fact]
    public async Task Verify_send_uses_documented_endpoint_header_and_payload()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK,
            """{"status":1,"message":"موفق","data":{"messageId":89545112,"cost":1}}"""));
        var sender = CreateSender(handler);

        var result = await sender.SendVerifyAsync(
            "api-key-for-test",
            "989120000000",
            123456,
            [new("CODE", "1234")]);

        result.IsSuccess.Should().BeTrue();
        result.ProviderMessageId.Should().Be("89545112");
        handler.Requests.Should().ContainSingle();
        var request = handler.Requests[0];
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri.Should().Be("https://api.sms.ir/v1/send/verify");
        request.ApiKey.Should().Be("api-key-for-test");
        using var payload = JsonDocument.Parse(request.Body);
        payload.RootElement.GetProperty("mobile").GetString().Should().Be("989120000000");
        payload.RootElement.GetProperty("templateId").GetInt32().Should().Be(123456);
        payload.RootElement.GetProperty("parameters")[0].GetProperty("name").GetString().Should().Be("CODE");
        payload.RootElement.GetProperty("parameters")[0].GetProperty("value").GetString().Should().Be("1234");
    }

    [Fact]
    public async Task Bulk_send_uses_the_configured_line_and_text_payload()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK,
            """{"status":1,"message":"موفق","data":{"packId":"2b99e63c-9bf8-4a21-9bfe-3f72dc1b46f1","messageIds":[86522023],"cost":2}}"""));
        var sender = CreateSender(handler);

        var result = await sender.SendBulkAsync("api-key-for-test", 30004505000017, "پیام احراز هویت", "989120000000");

        result.IsSuccess.Should().BeTrue();
        result.ProviderMessageId.Should().Be("86522023");
        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Uri.Should().Be("https://api.sms.ir/v1/send/bulk");
        using var payload = JsonDocument.Parse(request.Body);
        payload.RootElement.GetProperty("lineNumber").GetInt64().Should().Be(30004505000017);
        payload.RootElement.GetProperty("messageText").GetString().Should().Be("پیام احراز هویت");
        payload.RootElement.GetProperty("mobiles")[0].GetString().Should().Be("989120000000");
        payload.RootElement.TryGetProperty("sendDateTime", out _).Should().BeFalse();
    }

    private static SmsIrSender CreateSender(RecordingHandler handler) =>
        new(new TestHttpClientFactory(new HttpClient(handler) { BaseAddress = new Uri("https://api.sms.ir/") }),
            NullLogger<SmsIrSender>.Instance);

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
                request.Headers.GetValues("x-api-key").Single(),
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            Requests.Add(captured);
            return response(captured);
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, string Uri, string ApiKey, string Body);
}
