using Domain.Entities;

namespace Application.Interfaces;

public interface ICartRepository
{
    Task<Cart?> GetCartByUserIdAsync(string userId);
    Task<Cart?> GetCartBySessionIdAsync(string sessionId);
    Task SaveCartAsync(Cart cart);
    Task ClearCartAsync(string userId);
}