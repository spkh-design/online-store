using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Moq;
using Xunit;

namespace OnlineStore.UnitTests.Services;

public class CartServiceTests
{
    private readonly Mock<ICartRepository> _mockCartRepo;
    private readonly Mock<IProductRepository> _mockProductRepo;
    private readonly CartService _service;
    private const string TestUserId = "user123";

    public CartServiceTests()
    {
        _mockCartRepo = new Mock<ICartRepository>();
        _mockProductRepo = new Mock<IProductRepository>();
        _service = new CartService(_mockCartRepo.Object, _mockProductRepo.Object);
    }

    // ========== СЦЕНАРИЙ 1: AddToCart - ПОЗИТИВНЫЕ ТЕСТЫ ==========

    [Fact]
    public async Task AddToCartAsync_NewItem_ShouldAddToCart()
    {
        // Arrange
        var product = new Product { Id = 1, Name = "Тестовый товар", Price = 100, StockQuantity = 10, IsActive = true };
        _mockProductRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);
        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(TestUserId)).ReturnsAsync((Cart?)null);
        _mockCartRepo.Setup(r => r.SaveCartAsync(It.IsAny<Cart>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.AddToCartAsync(TestUserId, 1, 2);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(2, result.Items.First().Quantity);
        Assert.Equal(100, result.Items.First().Price);
        _mockCartRepo.Verify(r => r.SaveCartAsync(It.IsAny<Cart>()), Times.Once);
    }

    [Fact]
    public async Task AddToCartAsync_ExistingItem_ShouldIncreaseQuantity()
    {
        // Arrange
        var product = new Product { Id = 1, Name = "Тестовый товар", Price = 100, StockQuantity = 10, IsActive = true };
        var existingCart = new Cart
        {
            UserId = TestUserId,
            Items = new List<CartItem>
            {
                new CartItem { ProductId = 1, Quantity = 1, Price = 100, ProductName = "Тестовый товар" }
            }
        };
        
        _mockProductRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);
        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(TestUserId)).ReturnsAsync(existingCart);
        _mockCartRepo.Setup(r => r.SaveCartAsync(It.IsAny<Cart>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.AddToCartAsync(TestUserId, 1, 2);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(3, result.Items.First().Quantity);
    }

    [Fact]
    public async Task RemoveFromCartAsync_ExistingItem_ShouldRemoveItem()
    {
        // Arrange
        var existingCart = new Cart
        {
            UserId = TestUserId,
            Items = new List<CartItem>
            {
                new CartItem { ProductId = 1, Quantity = 2 },
                new CartItem { ProductId = 2, Quantity = 1 }
            }
        };
        
        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(TestUserId)).ReturnsAsync(existingCart);
        _mockCartRepo.Setup(r => r.SaveCartAsync(It.IsAny<Cart>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.RemoveFromCartAsync(TestUserId, 1);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(2, result.Items.First().ProductId);
    }

    [Fact]
    public async Task UpdateQuantityAsync_ValidQuantity_ShouldUpdate()
    {
        // Arrange
        var product = new Product { Id = 1, Name = "Тестовый товар", Price = 100, StockQuantity = 10, IsActive = true };
        var existingCart = new Cart
        {
            UserId = TestUserId,
            Items = new List<CartItem>
            {
                new CartItem { ProductId = 1, Quantity = 1 }
            }
        };
        
        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(TestUserId)).ReturnsAsync(existingCart);
        _mockProductRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);
        _mockCartRepo.Setup(r => r.SaveCartAsync(It.IsAny<Cart>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateQuantityAsync(TestUserId, 1, 5);

        // Assert
        Assert.Equal(5, result.Items.First().Quantity);
    }

    [Fact]
    public async Task ClearCartAsync_ShouldRemoveAllItems()
    {
        // Arrange
        _mockCartRepo.Setup(r => r.ClearCartAsync(TestUserId)).Returns(Task.CompletedTask);

        // Act
        await _service.ClearCartAsync(TestUserId);

        // Assert
        _mockCartRepo.Verify(r => r.ClearCartAsync(TestUserId), Times.Once);
    }

    // ========== НЕГАТИВНЫЕ ТЕСТЫ ==========

    [Fact]
    public async Task AddToCartAsync_WithInvalidUserId_ShouldThrowArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.AddToCartAsync("", 1, 1));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.AddToCartAsync(null!, 1, 1));
    }

    [Fact]
    public async Task AddToCartAsync_WithInvalidProductId_ShouldThrowArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.AddToCartAsync(TestUserId, 0, 1));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.AddToCartAsync(TestUserId, -1, 1));
    }

    [Fact]
    public async Task AddToCartAsync_WithInvalidQuantity_ShouldThrowArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.AddToCartAsync(TestUserId, 1, 0));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.AddToCartAsync(TestUserId, 1, -5));
    }

    [Fact]
    public async Task AddToCartAsync_WithNonExistentProduct_ShouldThrowInvalidOperationException()
    {
        _mockProductRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Product?)null);
        
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.AddToCartAsync(TestUserId, 999, 1));
    }

    [Fact]
    public async Task AddToCartAsync_WithInsufficientStock_ShouldThrowInvalidOperationException()
    {
        var product = new Product { Id = 1, Name = "Товар", Price = 100, StockQuantity = 2, IsActive = true };
        _mockProductRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);
        
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.AddToCartAsync(TestUserId, 1, 10));
    }

    [Fact]
    public async Task AddToCartAsync_WithUnavailableProduct_ShouldThrowInvalidOperationException()
    {
        var product = new Product { Id = 1, Name = "Товар", Price = 100, StockQuantity = 10, IsActive = false };
        _mockProductRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);
        
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.AddToCartAsync(TestUserId, 1, 1));
    }

    [Fact]
    public async Task RemoveFromCartAsync_WithNonExistentCart_ShouldThrowInvalidOperationException()
    {
        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(TestUserId)).ReturnsAsync((Cart?)null);
        
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RemoveFromCartAsync(TestUserId, 1));
    }

    // ========== ТЕСТЫ ГРАНИЧНЫХ УСЛОВИЙ ==========

    [Fact]
    public async Task AddToCartAsync_MaximumQuantity_ShouldWork()
    {
        var product = new Product { Id = 1, Name = "Товар", Price = 100, StockQuantity = 100, IsActive = true };
        _mockProductRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);
        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(TestUserId)).ReturnsAsync((Cart?)null);
        _mockCartRepo.Setup(r => r.SaveCartAsync(It.IsAny<Cart>())).Returns(Task.CompletedTask);

        var result = await _service.AddToCartAsync(TestUserId, 1, 100);
        
        Assert.Equal(100, result.Items.First().Quantity);
    }

    [Fact]
    public async Task UpdateQuantityAsync_SetToZero_ShouldRemoveItem()
    {
        var product = new Product { Id = 1, Name = "Товар", Price = 100, StockQuantity = 10, IsActive = true };
        var existingCart = new Cart
        {
            UserId = TestUserId,
            Items = new List<CartItem> { new CartItem { ProductId = 1, Quantity = 5 } }
        };
        
        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(TestUserId)).ReturnsAsync(existingCart);
        _mockProductRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);
        _mockCartRepo.Setup(r => r.SaveCartAsync(It.IsAny<Cart>())).Returns(Task.CompletedTask);

        var result = await _service.UpdateQuantityAsync(TestUserId, 1, 0);
        
        Assert.Empty(result.Items);
    }
}