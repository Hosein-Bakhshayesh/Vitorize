using FluentAssertions;
using Vitorize.Shared.Logging;
using Xunit;

namespace Vitorize.Tests;

public sealed class SensitiveLogDataJsonTests
{
    [Fact]
    public void Json_quoted_sensitive_keys_are_redacted_and_ordinary_keys_survive()
    {
        const string body = "{\"page\":1,\"sort\":\"date_added_desc\",\"password\":\"demo-value-only\",\"api_key\":\"k-123\"," +
                            "\"Authorization\":\"Bearer abc.def\",\"otp\":123456,\"refreshToken\":[\"r1\",\"r2\"],\"note\":\"keep me\"}";

        var safe = SensitiveLogData.RedactFreeText(body, 4096);

        safe.Should().Contain("\"page\":1").And.Contain("\"sort\":\"date_added_desc\"").And.Contain("\"note\":\"keep me\"");
        safe.Should().NotContain("demo-value-only").And.NotContain("k-123").And.NotContain("abc.def")
            .And.NotContain("123456").And.NotContain("r1");
        safe.Should().Contain("\"password\":\"[REDACTED]\"").And.Contain("\"api_key\":\"[REDACTED]\"")
            .And.Contain("\"otp\":\"[REDACTED]\"").And.Contain("\"refreshToken\":\"[REDACTED]\"");
    }

    [Fact]
    public void Spaced_json_and_key_value_forms_are_both_redacted()
    {
        SensitiveLogData.RedactFreeText("{ \"Password\" : \"x y z\" }", 4096).Should().NotContain("x y z");
        SensitiveLogData.RedactFreeText("password=NeverLogMe", 4096).Should().NotContain("NeverLogMe");
    }
}
