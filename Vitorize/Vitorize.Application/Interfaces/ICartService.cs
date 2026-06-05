using Vitorize.Application.DTOs.Cart;

namespace Vitorize.Application.Interfaces
{
    public interface ICartService
    {
        Task<CartDto> GetAsync(Guid userId);

        Task<CartDto> AddItemAsync(
            Guid userId,
            AddToCartRequestDto request);

        Task<CartDto> UpdateItemAsync(
            Guid userId,
            Guid cartItemId,
            UpdateCartItemRequestDto request);

        Task<CartDto> RemoveItemAsync(
            Guid userId,
            Guid cartItemId);

        Task ClearAsync(Guid userId);
    }
}