import io, re, os

os.chdir(r"D:\Vitorize\Vitorize\tests\Vitorize.IntegrationTests")

EDITS = [
    # (file, anchor to insert before, occurrence index)
    ("Fix13VatCheckoutIntegrationTests.cs",
     "        db.Categories.Add(category);\n        db.Products.Add(product);"),
    ("Fix13VatSnapshotImmutabilityIntegrationTests.cs",
     "        db.Categories.Add(category);\n        db.Products.Add(product);"),
    ("Fix09Phase2HProductionReadinessIntegrationTests.cs",
     "        await using var db = _fixture.CreateDbContext();\n        db.AddRange(category, product);"),
    ("GuestCartSqlIntegrationTests.cs",
     "        await using var db = _fixture.CreateDbContext();\n        db.AddRange(category, product);"),
    ("Phase4ConcurrencyIntegrationTests.cs",
     "        await using var seed = _fixture.CreateDbContext();\n        seed.Categories.Add(category);"),
    ("Fix11ProductInputRequiredOptionalIntegrationTests.cs",
     "        db.Categories.Add(category);\n        db.Products.Add(product);"),
]

CALL = "        product.WithCanonicalVariant();\n"

for fname, anchor in EDITS:
    s = io.open(fname, encoding="utf-8-sig").read()
    if "WithCanonicalVariant" in s:
        print("skip (already patched):", fname); continue
    n = s.count(anchor)
    if n == 0:
        print("!! anchor missing:", fname); continue
    s = s.replace(anchor, CALL + anchor, 1)
    io.open(fname, "w", encoding="utf-8-sig", newline="").write(s)
    print("patched %s (anchor occurrences=%d)" % (fname, n))

# Phase3A / Phase2G are single-line constructions -> chain the call.
one_liners = [
    ("Fix09Phase3ARedactedUploadIntegrationTests.cs",
     'IsActive = true, CreatedAt = now };',
     'IsActive = true, CreatedAt = now }.WithCanonicalVariant();'),
    ("Fix09Phase3ARedactedUploadIntegrationTests.cs",
     'IsActive = true, CreatedAt = DateTime.UtcNow };',
     'IsActive = true, CreatedAt = DateTime.UtcNow }.WithCanonicalVariant();'),
    ("Fix09Phase2GRealPostPaymentKycIntegrationTests.cs",
     'KycPolicyVersionId = mode == KycRequirementMode.None ? null : setup.PolicyVersion.Id };',
     'KycPolicyVersionId = mode == KycRequirementMode.None ? null : setup.PolicyVersion.Id }.WithCanonicalVariant();'),
]
for fname, old, new in one_liners:
    s = io.open(fname, encoding="utf-8-sig").read()
    if new in s:
        print("skip (already patched):", fname, old[:40]); continue
    # only patch the "var product = new Product" lines
    lines = s.split("\n")
    hit = False
    for i, ln in enumerate(lines):
        if "var product = new Product" in ln and ln.rstrip().endswith(old):
            lines[i] = ln.rstrip()[: -len(old)] + new
            hit = True
            break
    if not hit:
        print("!! one-liner not found:", fname, old[:50]); continue
    io.open(fname, "w", encoding="utf-8-sig", newline="").write("\n".join(lines))
    print("patched one-liner:", fname)

# ensure using directive
for fname in set([e[0] for e in EDITS] + [o[0] for o in one_liners]):
    s = io.open(fname, encoding="utf-8-sig").read()
    if "using Vitorize.IntegrationTests.Infrastructure;" in s:
        continue
    m = re.search(r"^using .+;\r?\n(?!using)", s, re.M)
    s = s[: m.end()] + "using Vitorize.IntegrationTests.Infrastructure;\n" + s[m.end():]
    io.open(fname, "w", encoding="utf-8-sig", newline="").write(s)
    print("added using:", fname)
