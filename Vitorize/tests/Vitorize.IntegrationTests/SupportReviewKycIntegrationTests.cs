using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Reviews;
using Vitorize.Application.DTOs.Reviews;
using Vitorize.Application.DTOs.Tickets;
using Vitorize.Application.DTOs.Verification;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class SupportReviewKycIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public SupportReviewKycIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Ticket_reply_close_reopen_and_owner_authorization_work_end_to_end()
    {
        await _fixture.ConfigureSmsAsync();
        var (owner, ownerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, otherToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var ownerClient = _fixture.CreateClient(ownerToken);
        using var otherClient = _fixture.CreateClient(otherToken);
        using var admin = _fixture.CreateClient(adminToken);

        var ticket = await PostDataAsync<TicketDto>(ownerClient, "/api/tickets", new CreateTicketRequestDto
        {
            Subject = "Integration support", Department = (byte)TicketDepartment.Technical,
            Priority = (byte)TicketPriority.High, Message = "Initial customer message"
        });
        (await otherClient.GetAsync($"/api/tickets/{ticket.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await admin.PostAsJsonAsync($"/api/admin/tickets/{ticket.Id}/messages",
            new AdminAddTicketMessageRequestDto { Message = "Support reply" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ownerClient.PostAsJsonAsync($"/api/tickets/{ticket.Id}/messages",
            new AddTicketMessageRequestDto { Message = "Customer follow-up" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsync($"/api/admin/tickets/{ticket.Id}/close", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ownerClient.PostAsJsonAsync($"/api/tickets/{ticket.Id}/messages",
            new AddTicketMessageRequestDto { Message = "Closed message" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync($"/api/admin/tickets/{ticket.Id}/reopen", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = _fixture.CreateDbContext();
        var stored = await db.Tickets.Include(x => x.TicketMessages).SingleAsync(x => x.Id == ticket.Id);
        stored.Status.Should().Be((byte)TicketStatus.WaitingForAdmin);
        stored.TicketMessages.Should().HaveCount(3);
        (await db.OutboxMessages.Where(x => x.MessageType == "SmsSend" && x.Payload.Contains("TK-"))
            .ToListAsync()).Should().NotBeEmpty();
        (await db.Notifications.CountAsync(x => x.UserId == owner.Id)).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Review_CRUD_moderation_voting_and_duplicate_protection_work()
    {
        var (author, authorToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, voterToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var (category, product) = await SeedProductAsync();
        using var authorClient = _fixture.CreateClient(authorToken);
        using var voter = _fixture.CreateClient(voterToken);
        using var admin = _fixture.CreateClient(adminToken);

        var review = await PostDataAsync<ProductReviewDto>(authorClient, "/api/product-reviews",
            new CreateProductReviewRequestDto { ProductId = product.Id, Title = "Good", Comment = "Useful review", Rating = 4 });
        review.IsApproved.Should().BeFalse();
        (await authorClient.PostAsJsonAsync("/api/product-reviews",
            new CreateProductReviewRequestDto { ProductId = product.Id, Comment = "Duplicate", Rating = 3 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await authorClient.PutAsJsonAsync($"/api/product-reviews/{review.Id}",
            new UpdateProductReviewRequestDto { Title = "Updated", Comment = "Updated review", Rating = 5 }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsync($"/api/admin/product-reviews/{review.Id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await voter.PostAsJsonAsync($"/api/product-reviews/{review.Id}/vote",
            new ProductReviewVoteRequestDto { VoteType = (byte)ReviewVoteType.Helpful })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await voter.PostAsJsonAsync($"/api/product-reviews/{review.Id}/vote",
            new ProductReviewVoteRequestDto { VoteType = (byte)ReviewVoteType.Unhelpful })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await voter.DeleteAsync($"/api/product-reviews/{review.Id}/vote")).StatusCode.Should().Be(HttpStatusCode.OK);

        var publicList = await _fixture.CreateClient().GetAsync($"/api/product-reviews/product/{product.Id}");
        publicList.StatusCode.Should().Be(HttpStatusCode.OK);
        (await publicList.Content.ReadAsStringAsync()).Should().Contain("Updated review");
        (await admin.PostAsJsonAsync($"/api/admin/product-reviews/{review.Id}/reject",
            new RejectProductReviewRequestDto { Reason = "Moderation reason" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await authorClient.DeleteAsync($"/api/product-reviews/{review.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = _fixture.CreateDbContext();
        (await db.ProductReviews.SingleAsync(x => x.Id == review.Id)).IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Kyc_submit_review_approve_reject_and_fine_grained_authorization_work()
    {
        await _fixture.ConfigureSmsAsync();
        var (approvedUser, approvedToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (rejectedUser, rejectedToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var approvedClient = _fixture.CreateClient(approvedToken);
        using var rejectedClient = _fixture.CreateClient(rejectedToken);
        using var admin = _fixture.CreateClient(adminToken);

        var approvedProfile = await SubmitKycAsync(approvedClient, "0013546789");
        var rejectedProfile = await SubmitKycAsync(rejectedClient, "0013546797");
        (await approvedClient.GetAsync("/api/admin/verifications")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.GetAsync($"/api/admin/verifications/{approvedProfile.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsJsonAsync($"/api/admin/verifications/{approvedProfile.Id}/review",
            new ReviewVerificationRequestDto { Approve = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsJsonAsync($"/api/admin/verifications/{rejectedProfile.Id}/review",
            new ReviewVerificationRequestDto { Approve = false, AdminNote = "Document mismatch" })).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = _fixture.CreateDbContext();
        (await db.Users.SingleAsync(x => x.Id == approvedUser.Id)).VerificationStatus.Should().Be((byte)VerificationStatus.Verified);
        (await db.Users.SingleAsync(x => x.Id == rejectedUser.Id)).VerificationStatus.Should().Be((byte)VerificationStatus.Rejected);
        var profiles = await db.UserVerificationProfiles.Where(x => x.Id == approvedProfile.Id || x.Id == rejectedProfile.Id).ToListAsync();
        profiles.Should().Contain(x => x.Id == approvedProfile.Id && x.NationalCode != "0013546789");
        (await db.OutboxMessages.CountAsync(x => x.AggregateId == approvedProfile.Id || x.AggregateId == rejectedProfile.Id))
            .Should().Be(2);
    }

    private async Task<VerificationProfileDto> SubmitKycAsync(HttpClient client, string nationalCode) =>
        await PostDataAsync<VerificationProfileDto>(client, "/api/verification/submit", new SubmitVerificationRequestDto
        {
            FirstName = "Integration", LastName = "Customer", NationalCode = nationalCode,
            BirthDate = new DateOnly(1990, 1, 1), Address = "Private address", PostalCode = "1234567890"
        });

    private async Task<(Category Category, Product Product)> SeedProductAsync()
    {
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "Review Category", Slug = $"review-category-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Review Product",
            Slug = $"review-product-{Guid.NewGuid():N}", ProductType = (byte)ProductType.Other,
            DeliveryType = (byte)DeliveryType.Manual, BasePrice = 10, CurrencyType = (byte)CurrencyType.Toman,
            MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        await using var db = _fixture.CreateDbContext();
        db.Categories.Add(category); db.Products.Add(product); await db.SaveChangesAsync();
        return (category, product);
    }

    private static async Task<T> PostDataAsync<T>(HttpClient client, string uri, object request)
    {
        var response = await client.PostAsJsonAsync(uri, request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<ApiResult<T>>();
        return result!.Data!;
    }
}
