using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;

namespace Vitorize.Application.Common;

/// <summary>Current Phase-1 verification semantics shared by checkout and post-payment initialization.</summary>
public static class KycVerificationSatisfaction
{
    public static bool IsSatisfied(User user) =>
        user.IsMobileConfirmed && user.VerificationStatus == (byte)VerificationStatus.Verified;
}
