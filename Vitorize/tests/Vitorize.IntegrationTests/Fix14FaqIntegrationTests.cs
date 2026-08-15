using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Content;
using Vitorize.Application.DTOs.Storefront;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;

namespace Vitorize.IntegrationTests;

/// <summary>
/// FIX-14 Admin FAQ management over the existing structured Faq entity. The public
/// <c>/api/faqs</c> contract is unchanged: active items only, ordered by SortOrder.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix14FaqIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public Fix14FaqIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A_customer_cannot_mutate_faqs()
    {
        var (_, customerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var client = _fixture.CreateClient(customerToken);

        foreach (var response in new[]
                 {
                     await client.GetAsync("/api/admin/faqs"),
                     await client.PostAsJsonAsync("/api/admin/faqs", New("q", "a", 10)),
                     await client.DeleteAsync($"/api/admin/faqs/{Guid.NewGuid()}")
                 })
        {
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task Admin_crud_drives_the_public_faq_list_with_ordering_and_activation()
    {
        await ClearAsync();
        using var admin = await AdminClientAsync();

        var first = await CreateAsync(admin, New("پرسش اول", "پاسخ اول", sortOrder: 10));
        var second = await CreateAsync(admin, New("پرسش دوم", "پاسخ دوم", sortOrder: 20));
        var hidden = await CreateAsync(admin, New("پرسش پنهان", "پاسخ پنهان", sortOrder: 5, isActive: false));

        using var publicClient = _fixture.CreateClient();
        var visible = await PublicFaqsAsync(publicClient);

        visible.Select(x => x.Question).Should().Equal("پرسش اول", "پرسش دوم");
        visible.Should().NotContain(x => x.Question == "پرسش پنهان",
            "an inactive item must never reach the storefront even with the lowest SortOrder");

        // Reordering is reflected immediately.
        (await admin.PutAsJsonAsync($"/api/admin/faqs/{second.Id}",
            New("پرسش دوم", "پاسخ دوم", sortOrder: 1))).EnsureSuccessStatusCode();
        (await PublicFaqsAsync(publicClient)).Select(x => x.Question).Should().Equal("پرسش دوم", "پرسش اول");

        // Activating the hidden item publishes it.
        (await admin.PutAsJsonAsync($"/api/admin/faqs/{hidden.Id}",
            New("پرسش پنهان", "پاسخ پنهان", sortOrder: 30, isActive: true))).EnsureSuccessStatusCode();
        (await PublicFaqsAsync(publicClient)).Should().HaveCount(3);

        // Deleting removes it again.
        (await admin.DeleteAsync($"/api/admin/faqs/{first.Id}")).EnsureSuccessStatusCode();
        (await PublicFaqsAsync(publicClient)).Should().NotContain(x => x.Question == "پرسش اول");
    }

    [Fact]
    public async Task A_faq_answer_is_stored_and_returned_as_plain_text()
    {
        await ClearAsync();
        using var admin = await AdminClientAsync();
        const string answer = "<b>پاسخ</b> با نویسه‌های < و > و \"نقل قول\"";

        var created = await CreateAsync(admin, New("پرسش متنی", answer, 10));

        // No sanitizer, no markup handling: the answer round-trips verbatim and the storefront
        // renders it HTML-encoded, so the angle brackets are shown, never interpreted.
        await using (var db = _fixture.CreateDbContext())
        {
            (await db.Faqs.AsNoTracking().SingleAsync(x => x.Id == created.Id)).Answer.Should().Be(answer);
        }

        using var publicClient = _fixture.CreateClient();
        var published = await PublicFaqsAsync(publicClient);
        published.Single(x => x.Question == "پرسش متنی").Answer.Should().Be(answer);
    }

    [Theory]
    [InlineData("", "پاسخ")]
    [InlineData("پرسش", "")]
    [InlineData("   ", "   ")]
    public async Task Blank_question_or_answer_is_refused(string question, string answer)
    {
        using var admin = await AdminClientAsync();

        var response = await admin.PostAsJsonAsync("/api/admin/faqs", New(question, answer, 10));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task ClearAsync()
    {
        await using var db = _fixture.CreateDbContext();
        db.Faqs.RemoveRange(await db.Faqs.ToListAsync());
        await db.SaveChangesAsync();
    }

    private static async Task<List<FaqDto>> PublicFaqsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/faqs");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResult<List<FaqDto>>>())!.Data!;
    }

    private static async Task<AdminFaqDto> CreateAsync(HttpClient admin, CreateFaqRequestDto request)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/faqs", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResult<AdminFaqDto>>())!.Data!;
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        return _fixture.CreateClient(token);
    }

    private static CreateFaqRequestDto New(string question, string answer, int sortOrder, bool isActive = true) =>
        new() { Question = question, Answer = answer, SortOrder = sortOrder, IsActive = isActive };
}
