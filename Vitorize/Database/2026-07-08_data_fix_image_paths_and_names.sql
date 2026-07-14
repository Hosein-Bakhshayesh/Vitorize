-- ============================================================================
-- Vitorize — Data hygiene fixes (2026-07-08 Production Readiness Review)
-- NO SCHEMA CHANGES. Data-only fixes. Idempotent — safe to run more than once.
--
-- Context:
--   * Historically, uploaded images were written to the Web project's wwwroot
--     while image URLs resolve to the API host. Files have been relocated to
--     Vitorize.Api\wwwroot\uploads (file copy — see README-DEPLOYMENT.md).
--   * One Category row referenced an image file that no longer exists anywhere.
--
-- Already applied to the local development database on 2026-07-08.
-- Run against any other environment that shares this data.
-- ============================================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- 1) Clear the image path of the category whose uploaded file is missing on disk.
--    (The storefront shows a graceful icon placeholder when ImagePath is NULL.)
UPDATE Categories
SET ImagePath = NULL
WHERE ImagePath = '/uploads/categories/ff6e29552f6a4fecae7f107be424cb71.png';
