using Domain.Entities;

namespace Application.Interfaces;

public interface IDiscountService
{
    Task<decimal> CalculateDiscountAsync(Order order);
    Task<decimal> ApplyDiscountAsync(decimal totalAmount, string? promoCode);
}