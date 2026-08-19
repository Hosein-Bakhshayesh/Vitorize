using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Application.DTOs.Admin.Kyc;
using Vitorize.Application.DTOs.Orders;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class CommerceIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public CommerceIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Cart_identity_sensitive_storage_and_checkout_repricing_work_end_to_end()
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(active: true, withSensitiveRequiredField: true, price: 100m);
        using var client = _fixture.CreateClient(token);

        var first = await AddAsync(client, product.Id, "REF-ONE");
        first.Items.Should().ContainSingle();
        first.Items[0].Quantity.Should().Be(1);
        first.Items[0].InputValues.Should().ContainSingle(x => x.FieldKey == "customer_reference" && x.IsMasked);

        var merged = await AddAsync(client, product.Id, "REF-ONE");
        merged.Items.Should().ContainSingle();
        merged.Items[0].Quantity.Should().Be(2);

        var separate = await AddAsync(client, product.Id, "REF-TWO");
        separate.Items.Should().HaveCount(2, "different custom inputs are part of cart identity");

        var editResponse = await client.PutAsJsonAsync($"/api/cart/items/{separate.Items[0].Id}",
            new UpdateCartItemRequestDto
            {
                Quantity = separate.Items[0].Quantity,
                InputValues = new Dictionary<string, string?> { ["customer_reference"] = "REF-EDITED" }
            });
        var editBody = await editResponse.Content.ReadAsStringAsync();
        editResponse.StatusCode.Should().Be(HttpStatusCode.OK, editBody);
        var edited = (await editResponse.Content.ReadFromJsonAsync<ApiResult<CartDto>>())!.Data!;
        edited.Items.Should().HaveCount(2);
        edited.Items.SelectMany(x => x.InputValues).Should().OnlyContain(x => x.IsMasked);

        await using (var db = _fixture.CreateDbContext())
        {
            var values = await db.CartItemInputValues
                .Where(x => x.CartItem.Cart.UserId == user.Id)
                .ToListAsync();
            values.Should().OnlyContain(x => x.Value == null && x.EncryptedValue != null);
            values.Should().NotContain(x => x.EncryptedValue == "REF-ONE" || x.EncryptedValue == "REF-TWO" || x.EncryptedValue == "REF-EDITED");

            // Reprice the way an administrator does: the price lives on the SKU that is actually
            // charged, and AdminProductService keeps the product's implicit default SKU in step
            // with BasePrice. Updating only BasePrice here would test a state the admin UI cannot
            // produce.
            var storedProduct = await db.Products.SingleAsync(x => x.Id == product.Id);
            storedProduct.BasePrice = 175m;
            storedProduct.DiscountPrice = 0m;
            var storedVariant = await db.ProductVariants.SingleAsync(x => x.ProductId == product.Id);
            storedVariant.Price = 175m;
            storedVariant.DiscountPrice = 0m;
            await db.SaveChangesAsync();
        }

        var idempotencyKey = $"checkout-{Guid.NewGuid():N}";
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        var request = new CheckoutRequestDto();
        var checkoutResponse = await client.PostAsJsonAsync("/api/checkout", request);
        var checkoutBody = await checkoutResponse.Content.ReadAsStringAsync();
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK, checkoutBody);
        var checkout = (await checkoutResponse.Content.ReadFromJsonAsync<ApiResult<CheckoutResultDto>>())!.Data!;
        checkout.SubtotalAmount.Should().Be(525m, "three units must be repriced from the current product price");
        checkout.FinalAmount.Should().Be(525m);

        var replay = await client.PostAsJsonAsync("/api/checkout", request);
        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "a completed idempotency key must prevent a second order");

        await using var verify = _fixture.CreateDbContext();
        var order = await verify.Orders.Include(x => x.OrderItems).ThenInclude(x => x.InputValues)
            .SingleAsync(x => x.Id == checkout.OrderId);
        (await verify.Orders.CountAsync(x => x.UserId == user.Id)).Should().Be(1);
        (await verify.IdempotencyKeys.SingleAsync(x => x.Key == idempotencyKey)).Status
            .Should().Be((byte)IdempotencyStatus.Completed);
        order.OrderItems.Should().HaveCount(2);
        order.OrderItems.Sum(x => x.Quantity).Should().Be(3);
        order.OrderItems.Should().OnlyContain(x => x.UnitPrice == 175m);
        order.OrderItems.SelectMany(x => x.InputValues)
            .Should().OnlyContain(x => x.Value == null && x.EncryptedValue != null && x.IsSensitive);
        (await verify.Carts.Include(x => x.CartItems).SingleAsync(x => x.UserId == user.Id))
            .CartItems.Should().BeEmpty();
    }

    /// <summary>
    /// Product information is collected at Checkout, so the cart accepts the line without it. The
    /// refusal moved to order creation — the gate that stands in front of every payment — which this
    /// asserts over HTTP so a crafted request cannot skip the browser and buy without it.
    /// </summary>
    [Fact]
    public async Task Missing_required_dynamic_input_is_rejected_at_checkout_not_at_the_cart()
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(active: true, withSensitiveRequiredField: true);
        using var client = _fixture.CreateClient(token);

        var added = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto
        {
            ProductId = product.Id,
            Quantity = 1
        });
        added.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var cartDb = _fixture.CreateDbContext())
            (await cartDb.CartItems.CountAsync(x => x.Cart.UserId == user.Id)).Should().Be(1);

        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var checkout = await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto());
        checkout.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var db = _fixture.CreateDbContext();
        (await db.Orders.CountAsync(x => x.UserId == user.Id)).Should().Be(0,
            "no order means no payment could have been started");
    }

    [Fact]
    public async Task Currency_is_snapshotted_through_cart_checkout_order_and_payment_and_mixed_cart_is_rejected()
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var rial = await CreateProductAsync(active: true, withSensitiveRequiredField: false, price: 100m,
            currency: CurrencyType.Rial);
        var toman = await CreateProductAsync(active: true, withSensitiveRequiredField: false, price: 100m,
            currency: CurrencyType.Toman);
        using var client = _fixture.CreateClient(token);

        var first = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = rial.Id, Quantity = 1 });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = (await first.Content.ReadFromJsonAsync<ApiResult<CartDto>>())!.Data!;
        cart.CurrencyType.Should().Be((byte)CurrencyType.Rial);
        cart.Items.Should().OnlyContain(x => x.CurrencyType == (byte)CurrencyType.Rial);

        var mixed = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = toman.Id, Quantity = 1 });
        mixed.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        client.DefaultRequestHeaders.Add("Idempotency-Key", $"currency-{Guid.NewGuid():N}");
        var checkoutResponse = await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto());
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkout = (await checkoutResponse.Content.ReadFromJsonAsync<ApiResult<CheckoutResultDto>>())!.Data!;
        checkout.CurrencyType.Should().Be((byte)CurrencyType.Rial);

        await using var verify = _fixture.CreateDbContext();
        var order = await verify.Orders.Include(x => x.OrderItems).Include(x => x.Payments)
            .SingleAsync(x => x.Id == checkout.OrderId);
        order.CurrencyType.Should().Be((byte)CurrencyType.Rial);
        order.OrderItems.Should().OnlyContain(x => x.CurrencyType == (byte)CurrencyType.Rial);
        order.Payments.Should().OnlyContain(x => x.CurrencyType == (byte)CurrencyType.Rial);
    }

    [Fact]
    public async Task Free_checkout_is_rejected_before_an_order_or_inventory_reservation_is_created()
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(active: true, withSensitiveRequiredField: false, price: 0m);
        using var client = _fixture.CreateClient(token);

        (await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = product.Id, Quantity = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"free-{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto());
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var verify = _fixture.CreateDbContext();
        (await verify.Orders.CountAsync(x => x.UserId == user.Id)).Should().Be(0);
        (await verify.GiftCodeReservations.CountAsync(x => x.UserId == user.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Checkout_stage_required_input_is_allowed_in_cart_but_rejected_by_server_checkout()
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(active: true, withSensitiveRequiredField: false, price: 100m);
        await using (var db = _fixture.CreateDbContext())
        {
            db.ProductInputFields.Add(new ProductInputField
            {
                Id = Guid.NewGuid(), ProductId = product.Id, Key = "checkout_player_id", Label = "شناسه بازی",
                FieldType = (byte)ProductInputFieldType.Text, IsRequired = true,
                DisplayStage = (byte)ProductInputStage.Checkout, IsActive = true,
                SortOrder = 0, CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var client = _fixture.CreateClient(token);
        var add = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = product.Id, Quantity = 1 });
        add.StatusCode.Should().Be(HttpStatusCode.OK, "stage-two values are intentionally completed from the cart");

        client.DefaultRequestHeaders.Add("Idempotency-Key", $"fix02-stage-two-{Guid.NewGuid():N}");
        var checkout = await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto());
        checkout.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the UI guard must complement, never replace, server validation");

        await using var verify = _fixture.CreateDbContext();
        (await verify.Orders.CountAsync(x => x.UserId == user.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Inactive_product_is_hidden_and_cannot_be_added_to_cart()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(active: false, withSensitiveRequiredField: false);
        using var client = _fixture.CreateClient(token);

        (await client.GetAsync($"/api/products/{product.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var add = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto
        {
            ProductId = product.Id,
            Quantity = 1
        });
        add.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Product_threshold_kyc_is_snapshotted_at_checkout_without_pre_payment_rejection()
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(active: true, withSensitiveRequiredField: false, price: 250m);
        Guid policyVersionId;
        await using (var db = _fixture.CreateDbContext())
        {
            policyVersionId = await db.KycPolicyVersions
                .Where(x => x.KycPolicy.Code == "legacy-profile-verification")
                .Select(x => x.Id).SingleAsync();
            var stored = await db.Products.SingleAsync(x => x.Id == product.Id);
            stored.KycRequirementMode = (byte)KycRequirementMode.AboveThreshold;
            stored.KycThresholdAmount = 500m;
            stored.KycPolicyVersionId = policyVersionId;
            stored.RequiresVerification = true;
            await db.SaveChangesAsync();
        }

        using var client = _fixture.CreateClient(token);
        (await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = product.Id, Quantity = 2 }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        client.DefaultRequestHeaders.Add("Idempotency-Key", $"kyc-threshold-{Guid.NewGuid():N}");
        var response = await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto());
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the KYC threshold is reached at UnitPrice × Quantity before a payment attempt is created");

        var checkout = (await response.Content.ReadFromJsonAsync<ApiResult<CheckoutResultDto>>())!.Data!;

        await using var verify = _fixture.CreateDbContext();
        var item = await verify.OrderItems.SingleAsync(x => x.OrderId == checkout.OrderId);
        item.RequiresVerification.Should().BeTrue();
        item.KycRequirementMode.Should().Be((byte)KycRequirementMode.AboveThreshold);
        item.KycThresholdAmount.Should().Be(500m);
        item.KycEvaluatedAmount.Should().Be(500m);
        item.KycPolicyVersionId.Should().Be(policyVersionId);
    }

    [Fact]
    public async Task Kyc_policy_admin_returns_only_published_active_policy_versions()
    {
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var client = _fixture.CreateClient(adminToken);

        var response = await client.GetAsync("/api/admin/kyc/policy-versions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<List<AdminKycPolicyVersionOptionDto>>>())!;
        body.IsSuccess.Should().BeTrue();
        body.Data.Should().Contain(x => x.PolicyCode == "legacy-profile-verification" && x.Status == (byte)KycPolicyVersionStatus.Published);
    }

    [Fact]
    public async Task Kyc_policy_versions_are_draft_editable_then_immutable_with_historical_requirements()
    {
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var client = _fixture.CreateClient(adminToken);
        var suffix = Guid.NewGuid().ToString("N");
        var documentResponse = await client.PostAsJsonAsync("/api/admin/kyc/document-types", new UpsertKycDocumentTypeRequestDto
        {
            Code = $"id-{suffix}", Title = "Integration identity", IsActive = true, AllowedExtensions = "jpg,jpeg,png,webp", MaxFileSizeBytes = 1024
        });
        documentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var document = (await documentResponse.Content.ReadFromJsonAsync<ApiResult<AdminKycDocumentTypeDto>>())!.Data!;

        var createResponse = await client.PostAsJsonAsync("/api/admin/kyc/policies", new UpsertKycPolicyRequestDto
        {
            Code = $"policy-{suffix}", Name = "Integration policy", CustomerTitle = "V1 title", CustomerInstructions = "V1 instructions"
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var policy = (await createResponse.Content.ReadFromJsonAsync<ApiResult<AdminKycPolicyDto>>())!.Data!;
        var v1 = policy.Versions.Should().ContainSingle().Which;

        (await client.PutAsJsonAsync($"/api/admin/kyc/policy-versions/{v1.Id}/document-requirements", new SetKycPolicyDocumentRequirementsRequestDto
        {
            Requirements = [new KycPolicyDocumentRequirementRequestDto { KycDocumentTypeId = document.Id, IsRequired = true, SortOrder = 10, CustomerInstructions = "V1 document" }]
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"/api/admin/kyc/policy-versions/{v1.Id}/publish", new { })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.PutAsJsonAsync($"/api/admin/kyc/policy-versions/{v1.Id}", new UpdateKycPolicyVersionRequestDto { CustomerTitle = "mutated" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "published semantic fields must be immutable");
        (await client.PutAsJsonAsync($"/api/admin/kyc/policy-versions/{v1.Id}/document-requirements", new SetKycPolicyDocumentRequirementsRequestDto()))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "published requirements must be immutable");

        var v2Response = await client.PostAsJsonAsync($"/api/admin/kyc/policies/{policy.Id}/versions", new CreateKycPolicyVersionRequestDto { CustomerTitle = "V2 title", CustomerInstructions = "V2 instructions" });
        v2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var v2 = (await v2Response.Content.ReadFromJsonAsync<ApiResult<AdminKycPolicyVersionOptionDto>>())!.Data!;
        v2.Version.Should().Be(2);
        (await client.PostAsJsonAsync($"/api/admin/kyc/policy-versions/{v2.Id}/publish", new { })).StatusCode.Should().Be(HttpStatusCode.OK);

        var v1Read = (await (await client.GetAsync($"/api/admin/kyc/policy-versions/{v1.Id}")).Content.ReadFromJsonAsync<ApiResult<AdminKycPolicyVersionOptionDto>>())!.Data!;
        v1Read.CustomerTitle.Should().Be("V1 title");
        v1Read.CustomerInstructions.Should().Be("V1 instructions");
        v1Read.DocumentRequirements.Should().ContainSingle(x => x.KycDocumentTypeId == document.Id && x.IsRequired && x.CustomerInstructions == "V1 document");
    }

    [Fact]
    public async Task Instant_multi_unit_checkout_reserves_pays_and_delivers_distinct_codes_end_to_end()
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var (product, plaintextCodes) = await CreateInstantProductWithCodesAsync(codeCount: 3, price: 150m);
        using var client = _fixture.CreateClient(token);

        // Buy TWO units of an instant-delivery product in a single order (the defect returned HTTP 500 here).
        var add = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = product.Id, Quantity = 2 });
        add.StatusCode.Should().Be(HttpStatusCode.OK, await add.Content.ReadAsStringAsync());

        client.DefaultRequestHeaders.Add("Idempotency-Key", $"instant-multi-{Guid.NewGuid():N}");
        var checkoutResponse = await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto());
        var checkoutBody = await checkoutResponse.Content.ReadAsStringAsync();
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK, checkoutBody);
        var checkout = (await checkoutResponse.Content.ReadFromJsonAsync<ApiResult<CheckoutResultDto>>())!.Data!;
        checkout.ReservationIds.Should().HaveCount(2, "two units reserve two distinct codes");
        checkout.ReservationIds.Distinct().Should().HaveCount(2);
        checkout.FinalAmount.Should().Be(300m);

        // Complete payment through the real DI payment + delivery pipeline.
        Guid paymentId;
        await using (var db = _fixture.CreateDbContext())
            paymentId = await db.Payments.Where(x => x.OrderId == checkout.OrderId).Select(x => x.Id).SingleAsync();
        using (var scope = _fixture.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IPaymentService>().VerifyMockPaymentAsync(user.Id, paymentId);

        await using var verify = _fixture.CreateDbContext();
        var order = await verify.Orders.Include(x => x.OrderItems).SingleAsync(x => x.Id == checkout.OrderId);
        order.Status.Should().Be((byte)OrderStatus.Completed);
        order.OrderItems.Single().Quantity.Should().Be(2, "order item quantity equals the purchased units");
        var deliveries = await verify.OrderItemDeliveries.Where(x => x.OrderItem.OrderId == checkout.OrderId).ToListAsync();
        deliveries.Should().HaveCount(2);
        deliveries.Select(x => x.GiftCodeId).Distinct().Should().HaveCount(2, "each unit gets a distinct code");
        (await verify.GiftCodeReservations.CountAsync(x => x.OrderId == checkout.OrderId)).Should().Be(2);
        (await verify.GiftCodes.CountAsync(x => x.ProductId == product.Id && x.Status == (byte)GiftCodeStatus.Delivered)).Should().Be(2);
        (await verify.GiftCodes.CountAsync(x => x.ProductId == product.Id && x.Status == (byte)GiftCodeStatus.Available)).Should().Be(1, "the third code stays available");

        // Customer gift-code library returns BOTH distinct codes over HTTP.
        var library = await client.GetAsync("/api/orders/deliveries");
        library.EnsureSuccessStatusCode();
        var libraryBody = await library.Content.ReadAsStringAsync();
        foreach (var code in plaintextCodes.Take(2))
            libraryBody.Should().Contain(code);

        // Admin order detail shows the correct delivered quantity over HTTP.
        using var adminClient = _fixture.CreateClient(adminToken);
        var adminResponse = await adminClient.GetAsync($"/api/admin/orders/{checkout.OrderId}");
        adminResponse.EnsureSuccessStatusCode();
        var adminOrder = (await adminResponse.Content.ReadFromJsonAsync<ApiResult<OrderDto>>())!.Data!;
        var adminItem = adminOrder.Items.Should().ContainSingle().Subject;
        adminItem.Quantity.Should().Be(2);
        adminItem.Deliveries.Should().HaveCount(2);
    }

    [Fact]
    public async Task Instant_checkout_beyond_available_inventory_fails_cleanly_without_side_effects()
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (product, _) = await CreateInstantProductWithCodesAsync(codeCount: 1, price: 150m); // only one code
        using var client = _fixture.CreateClient(token);

        (await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = product.Id, Quantity = 3 }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"instant-short-{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto());
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "insufficient inventory is a controlled business error, not HTTP 500");

        await using var verify = _fixture.CreateDbContext();
        (await verify.Orders.CountAsync(x => x.UserId == user.Id)).Should().Be(0);
        (await verify.GiftCodeReservations.CountAsync(x => x.UserId == user.Id)).Should().Be(0);
        (await verify.GiftCodes.CountAsync(x => x.ProductId == product.Id && x.Status == (byte)GiftCodeStatus.Available)).Should().Be(1);
    }

    private async Task<(Product Product, List<string> Codes)> CreateInstantProductWithCodesAsync(int codeCount, decimal price)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var crypto = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        await using var db = _fixture.CreateDbContext();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "Instant category", Slug = $"instant-cat-{Guid.NewGuid():N}",
            SortOrder = 0, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Instant gift card",
            Slug = $"instant-{Guid.NewGuid():N}", ProductType = (byte)ProductType.GiftCard,
            DeliveryType = (byte)DeliveryType.Instant, BasePrice = price,
            CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var plaintexts = new List<string>();
        var codes = new List<GiftCode>();
        for (var i = 0; i < codeCount; i++)
        {
            var plain = $"GIFT-{Guid.NewGuid():N}";
            plaintexts.Add(plain);
            codes.Add(new GiftCode
            {
                Id = Guid.NewGuid(), ProductId = product.Id, EncryptedCode = crypto.Encrypt(plain),
                MaskedCode = "****" + plain[^4..], Status = (byte)GiftCodeStatus.Available, EncryptionVersion = 2,
                CodeHashFingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(plain))),
                CreatedAt = DateTime.UtcNow.AddSeconds(i)
            });
        }
        db.Categories.Add(category);
        db.Products.Add(product);
        db.GiftCodes.AddRange(codes);
        await db.SaveChangesAsync();
        return (product, plaintexts);
    }

    private async Task<CartDto> AddAsync(HttpClient client, Guid productId, string reference)
    {
        var response = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto
        {
            ProductId = productId,
            Quantity = 1,
            InputValues = new Dictionary<string, string?> { ["customer_reference"] = reference }
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return (await response.Content.ReadFromJsonAsync<ApiResult<CartDto>>())!.Data!;
    }

    private async Task<Product> CreateProductAsync(bool active, bool withSensitiveRequiredField, decimal price = 100m,
        CurrencyType currency = CurrencyType.Toman)
    {
        await using var db = _fixture.CreateDbContext();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "Integration category", Slug = $"integration-{Guid.NewGuid():N}",
            SortOrder = 0, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Integration product",
            Slug = $"product-{Guid.NewGuid():N}", ProductType = (byte)ProductType.Other,
            DeliveryType = (byte)DeliveryType.Manual, BasePrice = price,
            CurrencyType = (byte)currency, MinOrderQuantity = 1,
            IsActive = active, CreatedAt = DateTime.UtcNow
        };
        if (withSensitiveRequiredField)
            product.ProductInputFields.Add(new ProductInputField
            {
                Id = Guid.NewGuid(), Key = "customer_reference", Label = "شناسه مشتری",
                FieldType = (byte)ProductInputFieldType.Text, IsRequired = true,
                MinLength = 3, MaxLength = 50, IsSensitive = true,
                DisplayStage = (byte)ProductInputStage.ProductPage, IsActive = true,
                SortOrder = 0, CreatedAt = DateTime.UtcNow
            });
        // Inventory is SKU-scoped, so a purchasable non-Instant product always owns a canonical
        // variant. Stock is set well above anything these tests order, keeping the subject of the
        // test the cart/checkout behaviour rather than the stock ceiling.
        product.ProductVariants.Add(new ProductVariant
        {
            Id = Guid.NewGuid(), ProductId = product.Id, Title = "پیش‌فرض", Price = price,
            StockMode = (byte)ProductVariantStockMode.Manual, StockQuantity = 1000,
            IsDefault = true, IsActive = true, SortOrder = 0, CreatedAt = DateTime.UtcNow
        });
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }
}
