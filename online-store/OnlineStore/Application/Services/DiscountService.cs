using Application.Interfaces;
using Domain.Entities;

namespace Application.Services;

public class DiscountService : IDiscountService
{
    private static readonly Dictionary<string, decimal> _promoCodes = new()
    {
        { "WELCOME10", 0.10m },  // 10% скидка
        { "SAVE20", 0.20m },     // 20% скидка
        { "FREESHIP", 500 }       // бесплатная доставка при заказе от 500
    };

    public Task<decimal> CalculateDiscountAsync(Order order)
    {
        decimal discount = 0;
        
        // Автоматическая скидка при заказе от 1000
        if (order.TotalAmount >= 1000)
        {
            discount = order.TotalAmount * 0.05m; // 5% скидка
        }
        
        return Task.FromResult(discount);
    }

    public Task<decimal> ApplyDiscountAsync(decimal totalAmount, string? promoCode)
    {
        if (string.IsNullOrWhiteSpace(promoCode))
            return Task.FromResult(totalAmount);

        if (_promoCodes.TryGetValue(promoCode.ToUpper(), out var discountValue))
        {
            if (discountValue < 1)
            {
                // Процентная скидка
                totalAmount -= totalAmount * discountValue;
            }
            else if (discountValue > 0 && totalAmount >= discountValue)
            {
                // Фиксированная скидка (например, бесплатная доставка)
                totalAmount -= discountValue;
            }
        }
        
        return Task.FromResult(Math.Max(0, totalAmount));
    }
}