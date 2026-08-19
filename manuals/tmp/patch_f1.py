import io

# ---------- 1. AdminProductService: invariant on create/update ----------
p = "Vitorize.Infrastructure/Services/AdminProductService.cs"
s = io.open(p, encoding="utf-8-sig").read()

helper = '''
        /// <summary>
        /// F1/F3 remediation: inventory, cart validation and paid-time consumption are all
        /// SKU-scoped, so every purchasable non-Instant product must own at least one active
        /// ProductVariant. A product the admin regards as "variantless" receives one implicit
        /// default SKU (Title «پیش‌فرض», IsDefault). The storefront hides the selector when a
        /// product has a single variant, so nothing changes visually for the customer.
        ///
        /// Instant products are exempt: their availability is the gift-code pool, which may be
        /// product-scoped, and forcing a variant id onto Instant order items would break legacy
        /// gift-code allocation.
        ///
        /// The implicit SKU mirrors the product price so the displayed price and the charged
        /// variant price can never drift; the sync deliberately targets only the implicit
        /// default so a real, admin-authored variant price is never overwritten.
        /// </summary>
        private async Task EnsureDefaultVariantAsync(Product product)
        {
            if (ProductAvailabilityRules.IsGiftCodeDriven(product.DeliveryType))
                return;

            var variants = await _dbContext.ProductVariants
                .Where(v => v.ProductId == product.Id)
                .ToListAsync();

            if (variants.Count == 0)
            {
                await _dbContext.ProductVariants.AddAsync(new ProductVariant
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Title = DefaultVariantTitle,
                    Price = product.BasePrice,
                    DiscountPrice = product.DiscountPrice,
                    StockMode = (byte)ProductAvailabilityRules.RequiredStockMode(product.DeliveryType),
                    StockQuantity = 0,          // unknown legacy stock must never become sellable by default
                    IsDefault = true,
                    IsActive = true,
                    SortOrder = 0,
                    CreatedAt = DateTime.UtcNow
                });
                return;
            }

            var implicitDefault = variants.Count == 1 && variants[0].IsDefault && variants[0].Title == DefaultVariantTitle
                ? variants[0]
                : null;
            if (implicitDefault is not null)
            {
                implicitDefault.Price = product.BasePrice;
                implicitDefault.DiscountPrice = product.DiscountPrice;
                implicitDefault.StockMode = (byte)ProductAvailabilityRules.RequiredStockMode(product.DeliveryType);
                implicitDefault.IsActive = true;
            }
        }

        /// <summary>Marker title of the implicit SKU; V0021 seeds migrated products with the same value.</summary>
        internal const string DefaultVariantTitle = "پیش\\u200cفرض";

        public async Task<AdminProductDto> CreateAsync(CreateProductRequestDto request)'''
old_sig = "        public async Task<AdminProductDto> CreateAsync(CreateProductRequestDto request)"
assert s.count(old_sig) == 1
s = s.replace(old_sig, helper, 1)

old_create = """            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            await _dbContext.Products.AddAsync(product);
            await SyncMetadataAsync(product, request.Features, request.InputFields, request.TagIds);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return await GetByIdAsync(product.Id);
        }

        public async Task<AdminProductDto> UpdateAsync("""
new_create = old_create.replace(
    "await SyncMetadataAsync(product, request.Features, request.InputFields, request.TagIds);\n            await _dbContext.SaveChangesAsync();",
    "await SyncMetadataAsync(product, request.Features, request.InputFields, request.TagIds);\n            await EnsureDefaultVariantAsync(product);\n            await _dbContext.SaveChangesAsync();")
assert old_create in s
s = s.replace(old_create, new_create, 1)

old_upd = """            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            await SyncMetadataAsync(product, request.Features, request.InputFields, request.TagIds);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return await GetByIdAsync(product.Id);
        }"""
new_upd = old_upd.replace(
    "await SyncMetadataAsync(product, request.Features, request.InputFields, request.TagIds);\n            await _dbContext.SaveChangesAsync();",
    "await SyncMetadataAsync(product, request.Features, request.InputFields, request.TagIds);\n            await EnsureDefaultVariantAsync(product);\n            await _dbContext.SaveChangesAsync();")
assert old_upd in s
s = s.replace(old_upd, new_upd, 1)
io.open(p, "w", encoding="utf-8-sig", newline="").write(s)
print("AdminProductService patched")

# ---------- 2. CartService: server-side default-variant resolution ----------
p = "Vitorize.Infrastructure/Services/CartService.cs"
s = io.open(p, encoding="utf-8-sig").read()
old = """        ProductVariant? variant = null;
        if (request.ProductVariantId.HasValue)
        {
            variant = product.ProductVariants.FirstOrDefault(x => x.Id == request.ProductVariantId && x.IsActive)
                ?? throw new BusinessException("تنوع محصول معتبر نیست.");
        }
"""
new = old + """        else if (!ProductAvailabilityRules.IsGiftCodeDriven(product.DeliveryType))
        {
            // Managed-stock purchases are SKU-scoped: a request without a variant resolves the
            // product's single (implicit default) SKU server-side, so stock validation at add,
            // checkout revalidation and paid-time consumption all see a concrete variant.
            // Instant products stay variant-optional because their gift codes are product-scoped.
            var actives = product.ProductVariants.Where(x => x.IsActive).ToList();
            variant = actives.Count == 1
                ? actives[0]
                : throw new BusinessException(actives.Count == 0
                    ? "این محصول در حال حاضر قابل خرید نیست."
                    : "لطفاً نسخه محصول را انتخاب کنید.");
        }
        var resolvedVariantId = variant?.Id ?? request.ProductVariantId;
"""
assert old in s
s = s.replace(old, new, 1)
s = s.replace("""            var existing = cart.CartItems.FirstOrDefault(x => x.ProductId == request.ProductId &&
                x.ProductVariantId == request.ProductVariantId && x.InputFingerprint == fingerprint);""",
"""            var existing = cart.CartItems.FirstOrDefault(x => x.ProductId == request.ProductId &&
                x.ProductVariantId == resolvedVariantId && x.InputFingerprint == fingerprint);""", 1)
s = s.replace("""            var resultingQuantity = cart.CartItems
                .Where(x => x.ProductVariantId == request.ProductVariantId)
                .Sum(x => x.Quantity) + request.Quantity;""",
"""            var resultingQuantity = cart.CartItems
                .Where(x => x.ProductVariantId == resolvedVariantId)
                .Sum(x => x.Quantity) + request.Quantity;""", 1)
s = s.replace("""                    Id = Guid.NewGuid(), CartId = cart.Id, ProductId = request.ProductId,
                    ProductVariantId = request.ProductVariantId, InputFingerprint = fingerprint,""",
"""                    Id = Guid.NewGuid(), CartId = cart.Id, ProductId = request.ProductId,
                    ProductVariantId = resolvedVariantId, InputFingerprint = fingerprint,""", 1)
io.open(p, "w", encoding="utf-8-sig", newline="").write(s)
print("CartService patched")
