/*
  Safely repair historical KYC items that remained AwaitingSubmission after
  their verification profile was approved.  This is deliberately evidence
  based: it updates only paid, verification-required items whose exact policy
  requirements have a non-empty approved/pending document on the verified
  profile.  Nothing is released merely because a user status says Verified.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.OrderItemKycStates', N'U') IS NULL
   OR OBJECT_ID(N'dbo.UserVerificationProfiles', N'U') IS NULL
   OR OBJECT_ID(N'dbo.VerificationDocuments', N'U') IS NULL
   OR OBJECT_ID(N'dbo.KycPolicyDocumentRequirements', N'U') IS NULL
    THROW 51038, N'The KYC lifecycle tables are required before V0038 can run.', 1;

BEGIN TRANSACTION;

DECLARE @now datetime2 = SYSUTCDATETIME();

UPDATE state
SET state.Status = 2, -- OrderItemKycStatus.Satisfied
    state.UpdatedAt = @now,
    state.CustomerActionDeadlineAt = NULL,
    state.SatisfiedAt = @now,
    state.SatisfiedByVerificationProfileId = profile.Id
FROM dbo.OrderItemKycStates AS state
JOIN dbo.OrderItems AS item ON item.Id = state.OrderItemId
JOIN dbo.Orders AS [order] ON [order].Id = item.OrderId
JOIN dbo.Users AS [user] ON [user].Id = [order].UserId
JOIN dbo.UserVerificationProfiles AS profile ON profile.UserId = [user].Id
WHERE state.Status = 3 -- OrderItemKycStatus.AwaitingSubmission
  AND item.RequiresVerification = 1
  AND item.KycPolicyVersionId IS NOT NULL
  AND [order].PaymentStatus = 2 -- PaymentStatus.Paid
  AND [user].VerificationStatus = 1 -- VerificationStatus.Verified
  AND profile.Status = 1 -- VerificationStatus.Verified
  AND profile.SubmittedAt IS NOT NULL
  AND NULLIF(LTRIM(RTRIM(profile.EncryptedPayload)), N'') IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.KycPolicyDocumentRequirements AS requirement
      WHERE requirement.KycPolicyVersionId = item.KycPolicyVersionId
        AND requirement.IsRequired = 1
        AND NOT EXISTS
        (
            SELECT 1
            FROM dbo.VerificationDocuments AS document
            WHERE document.UserVerificationProfileId = profile.Id
              AND document.KycDocumentTypeId = requirement.KycDocumentTypeId
              AND document.Status IN (0, 1) -- pending during review, or verified
              AND NULLIF(LTRIM(RTRIM(document.FilePath)), N'') IS NOT NULL
        )
  );

COMMIT TRANSACTION;
