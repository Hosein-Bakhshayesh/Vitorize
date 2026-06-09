namespace Vitorize.Application.DTOs.Verification
{
    public class ReviewVerificationRequestDto
    {
        public bool Approve { get; set; }

        public string? AdminNote { get; set; }
    }
}