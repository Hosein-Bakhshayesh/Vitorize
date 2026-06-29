namespace Vitorize.Application.DTOs.Reviews
{
    public class ProductReviewVoteRequestDto
    {
        /// <summary>
        /// نوع رأی: 1 مفید (Helpful)، 2 غیرمفید (Unhelpful).
        /// </summary>
        public byte VoteType { get; set; }
    }
}
