using Vitorize.Application.DTOs.Wishlist;

namespace Vitorize.Application.Interfaces
{
    public interface IWishlistService
    {
        Task<List<WishlistItemDto>> GetMyWishlistAsync(Guid userId);

        Task<List<Guid>> GetMyWishlistProductIdsAsync(Guid userId);

        Task<int> GetMyWishlistCountAsync(Guid userId);

        Task<bool> ToggleAsync(Guid userId, Guid productId);

        Task RemoveAsync(Guid userId, Guid productId);
    }
}
