using Vitorize.Application.Cart;
using Vitorize.Application.DTOs.Cart;

namespace Vitorize.Application.Interfaces;

public interface ICartService
{
    Task<CartDto> GetAsync(CartIdentity identity);
    Task<CartDto> AddItemAsync(CartIdentity identity, AddToCartRequestDto request);
    Task<CartDto> UpdateItemAsync(CartIdentity identity, Guid cartItemId, UpdateCartItemRequestDto request);
    Task<CartDto> RemoveItemAsync(CartIdentity identity, Guid cartItemId);
    Task ClearAsync(CartIdentity identity);
    Task<CartDto> MergeGuestCartAsync(Guid userId, string guestToken);

    // Retained for existing authenticated callers and tests.
    Task<CartDto> GetAsync(Guid userId);
    Task<CartDto> AddItemAsync(Guid userId, AddToCartRequestDto request);
    Task<CartDto> UpdateItemAsync(Guid userId, Guid cartItemId, UpdateCartItemRequestDto request);
    Task<CartDto> RemoveItemAsync(Guid userId, Guid cartItemId);
    Task ClearAsync(Guid userId);
}
