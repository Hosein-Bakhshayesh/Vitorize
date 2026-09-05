using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
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
    public async Task Fulfillment_ticket_exposes_its_safe_order_item_input_snapshot_only_to_admin()
    {
        var (customer, customerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("Admin");
        var (_, product) = await SeedProductAsync();
        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(), UserId = customer.Id, OrderNumber = $"VT-INPUT-{Guid.NewGuid():N}",
            Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid,
            SubtotalAmount = 10m, FinalAmount = 10m, CurrencyType = (byte)CurrencyType.Toman, CreatedAt = now
        };
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(), UserId = customer.Id, OrderId = order.Id, Subject = "Fulfillment input snapshot",
            Department = (byte)TicketDepartment.Orders, Priority = (byte)TicketPriority.Normal,
            Status = (byte)TicketStatus.WaitingForAdmin, IsFulfillmentTicket = true, CreatedAt = now
        };
        var item = new OrderItem
        {
            Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = product.Title,
            Quantity = 1, UnitPrice = 10m, TotalPrice = 10m, CurrencyType = (byte)CurrencyType.Toman,
            DeliveryType = (byte)DeliveryType.SupportRequired, DeliveryStatus = (byte)DeliveryStatus.Pending,
            SupportTicketId = ticket.Id, CreatedAt = now
        };
        item.InputValues.Add(new OrderItemInputValue
        {
            Id = Guid.NewGuid(), FieldKey = "player_id", FieldLabel = "Player ID", FieldType = 1,
            Value = "player-42", IsSensitive = false, CreatedAt = now
        });
        item.InputValues.Add(new OrderItemInputValue
        {
            Id = Guid.NewGuid(), FieldKey = "account_password", FieldLabel = "Password", FieldType = 12,
            EncryptedValue = "ciphertext-only", IsSensitive = true, CreatedAt = now
        });
        await using (var db = _fixture.CreateDbContext())
        {
            db.Orders.Add(order); db.Tickets.Add(ticket); db.OrderItems.Add(item);
            await db.SaveChangesAsync();
        }

        using var admin = _fixture.CreateClient(adminToken);
        var adminResult = await admin.GetFromJsonAsync<ApiResult<TicketDto>>($"/api/admin/tickets/{ticket.Id}");
        adminResult!.IsSuccess.Should().BeTrue();
        var adminInputs = adminResult.Data!.FulfillmentItems.Single().InputValues;
        adminInputs.Should().Contain(x => x.FieldKey == "player_id" && x.Value == "player-42" && !x.IsSensitive);
        adminInputs.Should().Contain(x => x.FieldKey == "account_password" && x.IsSensitive && x.IsMasked && x.Value != "ciphertext-only");

        using var customerClient = _fixture.CreateClient(customerToken);
        var customerBody = await (await customerClient.GetAsync($"/api/tickets/{ticket.Id}")).Content.ReadAsStringAsync();
        customerBody.Should().NotContain("player-42").And.NotContain("ciphertext-only");
    }

    [Fact]
    public async Task Admin_ticket_message_history_is_paged_stable_and_not_loaded_with_the_header()
    {
        var (owner, ownerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var ownerClient = _fixture.CreateClient(ownerToken);
        using var admin = _fixture.CreateClient(adminToken);
        var ticket = await PostDataAsync<TicketDto>(ownerClient, "/api/tickets", new CreateTicketRequestDto
        {
            Subject = "Paged message history", Department = (byte)TicketDepartment.Technical,
            Priority = (byte)TicketPriority.Normal, Message = "Initial message"
        });

        var createdAt = DateTime.UtcNow.AddMinutes(-54);
        await using (var db = _fixture.CreateDbContext())
        {
            db.TicketMessages.AddRange(Enumerable.Range(1, 54).Select(index => new TicketMessage
            {
                Id = Guid.NewGuid(), TicketId = ticket.Id, SenderUserId = owner.Id,
                Message = $"Paged message {index:000}", IsInternalNote = index % 3 == 0,
                CreatedAt = createdAt.AddMinutes(index)
            }));
            await db.SaveChangesAsync();
        }

        var header = await admin.GetFromJsonAsync<ApiResult<TicketDto>>($"/api/admin/tickets/{ticket.Id}");
        header!.IsSuccess.Should().BeTrue();
        header.Data!.Messages.Should().BeEmpty();
        (await ownerClient.GetAsync($"/api/admin/tickets/{ticket.Id}/messages")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var first = await GetTicketMessagesAsync(admin, ticket.Id, "page=1&pageSize=20&sortDirection=asc");
        var middle = await GetTicketMessagesAsync(admin, ticket.Id, "page=2&pageSize=20&sortDirection=asc");
        var last = await GetTicketMessagesAsync(admin, ticket.Id, "page=3&pageSize=20&sortDirection=asc");
        var beyondLast = await GetTicketMessagesAsync(admin, ticket.Id, "page=4&pageSize=20&sortDirection=asc");
        var defaultSize = await GetTicketMessagesAsync(admin, ticket.Id, "page=1&pageSize=0");
        var cappedSize = await GetTicketMessagesAsync(admin, ticket.Id, "page=1&pageSize=500");
        var externalOnly = await GetTicketMessagesAsync(admin, ticket.Id, "page=1&pageSize=100&includeInternalNotes=false");
        var descending = await GetTicketMessagesAsync(admin, ticket.Id, "page=1&pageSize=20&sortDirection=desc");

        first.TotalCount.Should().Be(55); first.PageSize.Should().Be(20); first.Items.Should().HaveCount(20);
        middle.TotalCount.Should().Be(55); middle.Items.Should().HaveCount(20);
        last.TotalCount.Should().Be(55); last.Items.Should().HaveCount(15);
        beyondLast.TotalCount.Should().Be(55); beyondLast.Items.Should().BeEmpty();
        defaultSize.PageSize.Should().Be(25); defaultSize.Items.Should().HaveCount(25);
        cappedSize.PageSize.Should().Be(100); cappedSize.Items.Should().HaveCount(55);
        externalOnly.Items.Should().OnlyContain(x => !x.IsInternalNote);
        first.Items.Select(x => x.Id).Intersect(middle.Items.Select(x => x.Id)).Should().BeEmpty();
        first.Items.Select(x => x.CreatedAt).Should().BeInAscendingOrder();
        descending.Items.Select(x => x.CreatedAt).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Customer_order_ticket_does_not_consume_the_automatic_fulfillment_item_link()
    {
        var (owner, ownerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, product) = await SeedProductAsync();
        var order = new Order
        {
            Id = Guid.NewGuid(), UserId = owner.Id, OrderNumber = $"VT-TICKET-{Guid.NewGuid():N}",
            Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid,
            SubtotalAmount = 10m, FinalAmount = 10m, CreatedAt = DateTime.UtcNow
        };
        var orderItem = new OrderItem
        {
            Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = product.Title,
            Quantity = 1, UnitPrice = 10m, TotalPrice = 10m, DeliveryType = (byte)DeliveryType.SupportRequired,
            DeliveryStatus = (byte)DeliveryStatus.Pending, CreatedAt = DateTime.UtcNow
        };
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Orders.Add(order);
            seed.OrderItems.Add(orderItem);
            await seed.SaveChangesAsync();
        }

        using var ownerClient = _fixture.CreateClient(ownerToken);
        var ticket = await PostDataAsync<TicketDto>(ownerClient, "/api/tickets", new CreateTicketRequestDto
        {
            OrderId = order.Id, OrderItemId = orderItem.Id, Subject = "Customer order question",
            Department = (byte)TicketDepartment.Orders, Priority = (byte)TicketPriority.Normal,
            Message = "Please help with this order."
        });

        ticket.IsFulfillmentTicket.Should().BeFalse();
        ticket.FulfillmentItems.Should().BeEmpty();
        await using var verify = _fixture.CreateDbContext();
        (await verify.OrderItems.SingleAsync(x => x.Id == orderItem.Id)).SupportTicketId.Should().BeNull();
        (await verify.Tickets.SingleAsync(x => x.Id == ticket.Id)).OrderId.Should().Be(order.Id);
    }

    [Fact]
    public async Task Review_CRUD_moderation_voting_and_duplicate_protection_work()
    {
        var (author, authorToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, voterToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var (category, product) = await SeedProductAsync();
        await SeedBuyerOrderAsync(author, product);
        await SetReviewAutoApproveAsync(true);
        using var authorClient = _fixture.CreateClient(authorToken);
        using var voter = _fixture.CreateClient(voterToken);
        using var admin = _fixture.CreateClient(adminToken);

        var review = await PostDataAsync<ProductReviewDto>(authorClient, "/api/product-reviews",
            new CreateProductReviewRequestDto { ProductId = product.Id, Title = "Good", Comment = "Useful review", Rating = 4 });
        // New customer reviews are published immediately; an administrator can still reject them.
        review.IsApproved.Should().BeTrue();
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
    public async Task Admin_reply_to_review_is_publicly_labeled_management_and_customer_cannot_post_it()
    {
        var (authorUser, authorToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (adminUser, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var (_, product) = await SeedProductAsync();
        await SeedBuyerOrderAsync(authorUser, product);
        await SetReviewAutoApproveAsync(true);
        using var author = _fixture.CreateClient(authorToken);
        using var admin = _fixture.CreateClient(adminToken);

        var review = await PostDataAsync<ProductReviewDto>(author, "/api/product-reviews",
            new CreateProductReviewRequestDto { ProductId = product.Id, Comment = "Does this include support?", Rating = 5 });

        (await author.PostAsJsonAsync($"/api/admin/product-reviews/{review.Id}/replies",
            new CreateAdminProductReviewReplyRequestDto { Comment = "This must be forbidden." }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var reply = await PostDataAsync<AdminProductReviewReplyDto>(admin,
            $"/api/admin/product-reviews/{review.Id}/replies",
            new CreateAdminProductReviewReplyRequestDto { Comment = "بله، پشتیبانی محصول فعال است." });
        reply.Comment.Should().Be("بله، پشتیبانی محصول فعال است.");

        var publicResponse = await _fixture.CreateClient().GetFromJsonAsync<ApiResult<ProductReviewListResultDto>>(
            $"/api/product-reviews/product/{product.Id}");
        var publicReview = publicResponse!.Data!.Reviews.Items.Single(x => x.Id == review.Id);
        publicReview.Replies.Should().ContainSingle();
        publicReview.Replies[0].AuthorLabel.Should().Be("مدیریت");
        publicReview.Replies[0].Comment.Should().Be(reply.Comment);

        await using var db = _fixture.CreateDbContext();
        var storedReply = await db.ProductReviews.SingleAsync(x => x.Id == reply.Id);
        storedReply.ParentId.Should().Be(review.Id);
        storedReply.UserId.Should().Be(adminUser.Id);
        storedReply.IsApproved.Should().BeTrue();
    }

    [Fact]
    public async Task Review_requires_a_purchase_and_waits_for_moderation_when_auto_approval_is_disabled()
    {
        var (customer, customerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var (_, product) = await SeedProductAsync();
        using var customerClient = _fixture.CreateClient(customerToken);
        using var admin = _fixture.CreateClient(adminToken);

        (await customerClient.PostAsJsonAsync("/api/product-reviews",
            new CreateProductReviewRequestDto { ProductId = product.Id, Comment = "No purchase", Rating = 5 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await SeedBuyerOrderAsync(customer, product);
        await SetReviewAutoApproveAsync(false);
        try
        {
            var pending = await PostDataAsync<ProductReviewDto>(customerClient, "/api/product-reviews",
                new CreateProductReviewRequestDto { ProductId = product.Id, Comment = "Needs approval", Rating = 5 });
            pending.IsBuyer.Should().BeTrue();
            pending.IsApproved.Should().BeFalse();

            var eligibility = await customerClient.GetFromJsonAsync<ApiResult<ProductReviewEligibilityDto>>(
                $"/api/product-reviews/product/{product.Id}/eligibility");
            eligibility!.Data!.CanCreateReview.Should().BeFalse();
            eligibility.Data.IsBuyer.Should().BeTrue();
            eligibility.Data.HasExistingReview.Should().BeTrue();

            var beforeApproval = await _fixture.CreateClient().GetAsync($"/api/product-reviews/product/{product.Id}");
            (await beforeApproval.Content.ReadAsStringAsync()).Should().NotContain("Needs approval");

            (await admin.PostAsync($"/api/admin/product-reviews/{pending.Id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.OK);
            var afterApproval = await _fixture.CreateClient().GetAsync($"/api/product-reviews/product/{product.Id}");
            (await afterApproval.Content.ReadAsStringAsync()).Should().Contain("Needs approval");
        }
        finally
        {
            await SetReviewAutoApproveAsync(true);
        }
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

        var approvedProfile = await SubmitKycAsync(approvedClient, approvedUser.Id, "0013546789");
        var rejectedProfile = await SubmitKycAsync(rejectedClient, rejectedUser.Id, "0013546797");
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

    private async Task<VerificationProfileDto> SubmitKycAsync(HttpClient client, Guid userId, string nationalCode)
    {
        await PostDataAsync<VerificationDocumentDto>(client, "/api/verification/documents", new AddVerificationDocumentRequestDto
        {
            DocumentType = 1, FilePath = $"kyc-private:{userId:N}/identity.jpg"
        });
        await PostDataAsync<VerificationDocumentDto>(client, "/api/verification/documents", new AddVerificationDocumentRequestDto
        {
            DocumentType = 4, FilePath = $"kyc-private:{userId:N}/card.jpg"
        });
        return await PostDataAsync<VerificationProfileDto>(client, "/api/verification/submit", new SubmitVerificationRequestDto
        {
            FirstName = "Integration", LastName = "Customer", NationalCode = nationalCode,
            BirthDate = new DateOnly(1990, 1, 1), Address = "Private address", PostalCode = "1234567890",
            RegisteredMobileBelongsToCardHolder = true
        });
    }

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

    private async Task SeedBuyerOrderAsync(User customer, Product product)
    {
        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(), UserId = customer.Id, OrderNumber = $"VT-REVIEW-{Guid.NewGuid():N}",
            Status = (byte)OrderStatus.Completed, PaymentStatus = (byte)PaymentStatus.Paid,
            SubtotalAmount = 10m, FinalAmount = 10m, CurrencyType = (byte)CurrencyType.Toman, CreatedAt = now
        };
        var item = new OrderItem
        {
            Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = product.Title,
            Quantity = 1, UnitPrice = 10m, TotalPrice = 10m, CurrencyType = (byte)CurrencyType.Toman,
            DeliveryType = (byte)DeliveryType.Manual, DeliveryStatus = (byte)DeliveryStatus.Delivered, CreatedAt = now
        };
        await using var db = _fixture.CreateDbContext();
        db.Orders.Add(order);
        db.OrderItems.Add(item);
        await db.SaveChangesAsync();
    }

    private async Task SetReviewAutoApproveAsync(bool enabled)
    {
        await using var db = _fixture.CreateDbContext();
        var setting = await db.Settings.SingleOrDefaultAsync(x => x.Key == ProductReviewSettings.AutoApproveKey);
        if (setting is null)
        {
            setting = new Setting
            {
                Id = Guid.NewGuid(), Key = ProductReviewSettings.AutoApproveKey,
                GroupName = ProductReviewSettings.GroupName, ValueType = "bool",
                Description = "تأیید خودکار نظرات خریداران"
            };
            db.Settings.Add(setting);
        }
        setting.Value = enabled ? "true" : "false";
        setting.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private static async Task<T> PostDataAsync<T>(HttpClient client, string uri, object request)
    {
        var response = await client.PostAsJsonAsync(uri, request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<ApiResult<T>>();
        return result!.Data!;
    }

    private static async Task<PagedResult<TicketMessageDto>> GetTicketMessagesAsync(HttpClient client, Guid ticketId, string query)
    {
        var response = await client.GetFromJsonAsync<ApiResult<PagedResult<TicketMessageDto>>>(
            $"/api/admin/tickets/{ticketId}/messages?{query}");
        response!.IsSuccess.Should().BeTrue();
        return response.Data!;
    }
}
