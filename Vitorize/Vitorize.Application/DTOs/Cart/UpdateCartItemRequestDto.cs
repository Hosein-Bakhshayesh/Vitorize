namespace Vitorize.Application.DTOs.Cart
{
    public class UpdateCartItemRequestDto
    {
        public int Quantity { get; set; }
        public Dictionary<string, string?>? InputValues { get; set; }
    }
}
