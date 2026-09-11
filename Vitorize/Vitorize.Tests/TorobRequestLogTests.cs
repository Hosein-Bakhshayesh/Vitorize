using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Vitorize.Api.Services;
using Vitorize.Shared.Logging;
using Xunit;

namespace Vitorize.Tests;

public sealed class TorobRequestLogTests
{
    [Fact]
    public void Template_property_names_survive_the_sensitive_data_redactor()
    {
        var names = Regex.Matches(TorobRequestLog.MessageTemplate, @"\{(\w+)\}").Select(match => match.Groups[1].Value).ToList();

        names.Should().NotBeEmpty();
        names.Should().OnlyHaveUniqueItems();
        names.Where(SensitiveLogData.IsSensitiveProperty).Should().BeEmpty(
            "a redacted property name would hide the very diagnostics this line exists for");
    }

    [Fact]
    public void Every_placeholder_receives_a_value_and_missing_token_is_a_warning()
    {
        var logger = new CapturingLogger();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/json";
        context.Request.Headers["User-Agent"] = "torob.com";
        var parsed = TorobRequestParser.FromJson("{\"page\":1,\"sort\":\"date_added_desc\",\"limit\":100}", "json", ["{\"page\":1}"]);

        TorobRequestLog.Write(logger, context, TorobTokenInfo.Missing, parsed, 200, null, 100, TimeSpan.FromMilliseconds(12.34));

        logger.Level.Should().Be(LogLevel.Warning, "a request without X-Torob-Token must stand out in the log");
        var placeholders = Regex.Matches(TorobRequestLog.MessageTemplate, @"\{(\w+)\}").Select(match => match.Groups[1].Value).ToList();
        logger.State.Should().NotBeNull();
        var provided = logger.State!.Where(pair => pair.Key != "{OriginalFormat}").Select(pair => pair.Key).ToList();
        provided.Should().Equal(placeholders);
        logger.State!.Single(pair => pair.Key == "IgnoredKeys").Value.Should().BeEquivalentTo(new[] { "limit" });
        logger.State!.Single(pair => pair.Key == "EventType").Value.Should().Be(TorobRequestLog.EventType);
        logger.Message.Should().Contain("IgnoredKeys=").And.Contain("limit").And.Contain("EventType=TorobProductsRequest");

        TorobRequestLog.Write(logger, context, TorobTokenInfo.Missing with { Present = true, Length = 10 }, parsed, 200, null, 100, TimeSpan.Zero);
        logger.Level.Should().Be(LogLevel.Information);
        TorobRequestLog.Write(logger, context, TorobTokenInfo.Missing with { Present = true, Length = 10 }, parsed, 400, "x", null, TimeSpan.Zero);
        logger.Level.Should().Be(LogLevel.Warning);
    }

    private sealed class CapturingLogger : ILogger
    {
        public LogLevel Level { get; private set; }
        public string? Message { get; private set; }
        public IReadOnlyList<KeyValuePair<string, object?>>? State { get; private set; }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Level = logLevel;
            Message = formatter(state, exception);
            State = (state as IReadOnlyList<KeyValuePair<string, object?>>)?.ToList();
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
