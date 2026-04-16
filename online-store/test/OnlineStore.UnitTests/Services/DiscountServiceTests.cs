using Application.Services;
using Domain.Entities;
using Xunit;

namespace OnlineStore.UnitTests.Services;

public class DiscountServiceTests
{
    private readonly DiscountService _service;

    public DiscountServiceTests()
    {
        _service = new DiscountService();
    }

    [Fact]
    public async Task ApplyDiscountAsync_WithValidPromoCode_ShouldApplyPercentageDiscount()
    {
        // Act
        var result = await _service.ApplyDiscountAsync(1000, "WELCOME10");

        // Assert
        Assert.Equal(900, result); // 10% скидка
    }

    [Fact]
    public async Task ApplyDiscountAsync_WithValidPromoCode_ShouldApplyFixedDiscount()
    {
        // Act
        var result = await _service.ApplyDiscountAsync(600, "FREESHIP");

        // Assert
        Assert.Equal(100, result); // 600 - 500 = 100
    }

    [Fact]
    public async Task ApplyDiscountAsync_WithInvalidPromoCode_ShouldReturnOriginalAmount()
    {
        // Act
        var result = await _service.ApplyDiscountAsync(1000, "INVALID");

        // Assert
        Assert.Equal(1000, result);
    }

    [Fact]
    public async Task ApplyDiscountAsync_WithNullPromoCode_ShouldReturnOriginalAmount()
    {
        // Act
        var result = await _service.ApplyDiscountAsync(1000, null);

        // Assert
        Assert.Equal(1000, result);
    }

    [Fact]
    public async Task CalculateDiscountAsync_WithOrderAboveThreshold_ShouldApplyDiscount()
    {
        // Arrange
        var order = new Order { TotalAmount = 1500 };

        // Act
        var result = await _service.CalculateDiscountAsync(order);

        // Assert
        Assert.Equal(75, result); // 5% от 1500
    }

    [Fact]
    public async Task CalculateDiscountAsync_WithOrderBelowThreshold_ShouldApplyNoDiscount()
    {
        // Arrange
        var order = new Order { TotalAmount = 500 };

        // Act
        var result = await _service.CalculateDiscountAsync(order);

        // Assert
        Assert.Equal(0, result);
    }
}