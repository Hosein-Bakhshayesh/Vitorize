using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Vitorize.Api.Services;
using Xunit;

namespace Vitorize.Tests;

public sealed class TorobTokenInspectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
    private readonly Ed25519PrivateKeyParameters _privateKey;
    private readonly string _publicKeyBase64;

    public TorobTokenInspectorTests()
    {
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var pair = generator.GenerateKeyPair();
        _privateKey = (Ed25519PrivateKeyParameters)pair.Private;
        _publicKeyBase64 = Convert.ToBase64String(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(pair.Public).GetDerEncoded());
    }

    [Fact]
    public void Valid_torob_token_is_fully_verified()
    {
        var token = Sign(Payload("vitorize.com", Now.AddMinutes(10), Now.AddMinutes(-1)));
        var request = new DefaultHttpContext().Request;
        request.Headers["X-Torob-Token"] = token;
        request.Headers["X-Torob-Token-Version"] = "1";

        var info = Authenticator().Inspect(request);

        info.Present.Should().BeTrue();
        info.Length.Should().Be(token.Length);
        info.LooksLikeJwt.Should().BeTrue();
        info.Algorithm.Should().Be("EdDSA");
        info.HeaderVersion.Should().Be("1");
        info.Audiences.Should().Equal("vitorize.com");
        info.AudienceMatches.Should().BeTrue();
        info.TimeValid.Should().BeTrue();
        info.SignatureValid.Should().BeTrue();
        info.VersionHeader.Should().Be("1");
        info.ParseError.Should().BeNull();
        info.ExpiresAt.Should().Be(Now.AddMinutes(10));
        info.NotBefore.Should().Be(Now.AddMinutes(-1));
    }

    [Fact]
    public void Tampered_expired_and_foreign_audience_tokens_are_flagged_but_never_throw()
    {
        var authenticator = Authenticator();
        var valid = Sign(Payload("vitorize.com", Now.AddMinutes(10), Now.AddMinutes(-1)));
        var parts = valid.Split('.');

        var tampered = parts[0] + "." + Base64Url(Encoding.UTF8.GetBytes(Payload("vitorize.com", Now.AddHours(5), Now.AddMinutes(-1)))) + "." + parts[2];
        var tamperedInfo = authenticator.Decode(tampered);
        tamperedInfo.LooksLikeJwt.Should().BeTrue();
        tamperedInfo.SignatureValid.Should().BeFalse();

        var expired = authenticator.Decode(Sign(Payload("vitorize.com", Now.AddMinutes(-10), Now.AddHours(-1))));
        expired.SignatureValid.Should().BeTrue();
        expired.TimeValid.Should().BeFalse();

        var withinSkew = authenticator.Decode(Sign(Payload("vitorize.com", Now.AddMinutes(-4), Now.AddHours(-1))));
        withinSkew.TimeValid.Should().BeTrue();

        var foreign = authenticator.Decode(Sign(Payload("other-shop.example", Now.AddMinutes(10), Now.AddMinutes(-1))));
        foreign.SignatureValid.Should().BeTrue();
        foreign.AudienceMatches.Should().BeFalse();
        foreign.Audiences.Should().Equal("other-shop.example");

        var garbage = authenticator.Decode("not-a-token");
        garbage.Present.Should().BeTrue();
        garbage.LooksLikeJwt.Should().BeFalse();
        garbage.SignatureValid.Should().BeNull();
        garbage.ParseError.Should().Be("not-a-jwt");

        var brokenBase64 = authenticator.Decode("@@@.@@@.@@@");
        brokenBase64.LooksLikeJwt.Should().BeFalse();
        brokenBase64.ParseError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Official_public_key_loads_and_rejects_signatures_from_other_keys()
    {
        var official = new TorobRequestAuthenticator(Configuration(("Torob:ExpectedAudience", "vitorize.com")), new FixedClock(Now));

        var info = official.Decode(Sign(Payload("vitorize.com", Now.AddMinutes(10), Now.AddMinutes(-1))));

        info.LooksLikeJwt.Should().BeTrue();
        info.ParseError.Should().BeNull("the official key must load without errors");
        info.SignatureValid.Should().BeFalse("the token was signed with a test key, not Torob's");
    }

    [Fact]
    public void Missing_header_is_tolerated_unless_enforcement_is_enabled()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers["C-Torob-Token-Version"] = "1";
        var lenient = Authenticator();

        var missing = lenient.Inspect(request);

        missing.Present.Should().BeFalse();
        missing.VersionHeader.Should().Be("1");
        lenient.ShouldReject(missing, out _).Should().BeFalse();

        var strict = Authenticator(("Torob:EnforceToken", "true"));
        strict.ShouldReject(missing, out var error).Should().BeTrue();
        error.Should().NotBeNullOrWhiteSpace();
        strict.ShouldReject(strict.Decode(Sign(Payload("vitorize.com", Now.AddMinutes(10), Now.AddMinutes(-1)))), out _).Should().BeFalse();
        strict.ShouldReject(strict.Decode(Sign(Payload("other", Now.AddMinutes(10), Now.AddMinutes(-1)))), out _).Should().BeTrue();
    }

    private TorobRequestAuthenticator Authenticator(params (string Key, string Value)[] extra)
    {
        var settings = new List<(string Key, string Value)>
        {
            ("Torob:PublicKey", _publicKeyBase64),
            ("Torob:ExpectedAudience", "vitorize.com")
        };
        settings.AddRange(extra);
        return new TorobRequestAuthenticator(Configuration(settings.ToArray()), new FixedClock(Now));
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value)).Build();

    private static string Payload(string audience, DateTimeOffset expires, DateTimeOffset notBefore) =>
        JsonSerializer.Serialize(new { aud = audience, exp = expires.ToUnixTimeSeconds(), nbf = notBefore.ToUnixTimeSeconds() });

    private string Sign(string payloadJson)
    {
        var header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"EdDSA\",\"typ\":\"JWT\",\"v\":1}"));
        var payload = Base64Url(Encoding.UTF8.GetBytes(payloadJson));
        var data = Encoding.ASCII.GetBytes(header + "." + payload);
        var signer = new Ed25519Signer();
        signer.Init(true, _privateKey);
        signer.BlockUpdate(data, 0, data.Length);
        return header + "." + payload + "." + Base64Url(signer.GenerateSignature());
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
