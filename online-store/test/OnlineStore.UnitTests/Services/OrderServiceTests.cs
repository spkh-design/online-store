using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Moq;
using Xunit;

namespace OnlineStore.UnitTests.Services;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly Mock<IProductRepository> _mockProductRepo;
    private readonly Mock<ICartRepository> _mockCartRepo;
    private readonly Mock<IDiscountService> _mockDiscountService;
    private readonly OrderService _service;
    private const string TestUserId = "user123";

    public OrderServiceTests()
    {
        _mockOrderRepo = new Mock<IOrderRepository>();
        _mockProductRepo = new Mock<IProductRepository>();
        _mockCartRepo = new Mock<ICartRepository>();
        _mockDiscountService = new Mock<IDiscountService>();
        _service = new OrderService(
            _mockOrderRepo.Object,
            _mockProductRepo.Object,
            _mockCartRepo.Object,
            _mockDiscountService.Object);
    }

    // ========== СЦЕНАРИЙ 3: PlaceOrder - ПОЗИТИВНЫЕ ТЕСТЫ ==========

    [Fact]
    public async Task PlaceOrderAsync_ValidData_ShouldCreateOrderAndClearCart()
    {
        // Arrange
        var cart = new Cart
        {
            UserId = TestUserId,
            Items = new List<CartItem>
            {
                new CartItem { ProductId = 1, Quantity = 2, Price = 100 },
                new CartItem { ProductId = 2, Quantity = 1, Price = 200 }
            }
        };
        
        var product1 = new Product { Id = 1, Name = "Товар 1", Price = 100, StockQuantity = 10, IsActive = true };
        var product2 = new Product { Id = 2, Name = "Товар 2", Price = 200, StockQuantity = 5, IsActive = true };

        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(TestUserId)).ReturnsAsync(cart);
        _mockProductRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product1);
        _mockProductRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(product2);
        _mockDiscountService.Setup(r => r.ApplyDiscountAsync(400, null)).ReturnsAsync(400);
        _mockDiscountService.Setup(r => r.CalculateDiscountAsync(It.IsAny<Order>())).ReturnsAsync(0);
        _mockOrderRepo.Setup(r => r.AddAsync(It.IsAny<Order>())).Returns(Task.CompletedTask);
        _mockOrderRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _mockProductRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _mockCartRepo.Setup(r => r.ClearCartAsync(TestUserId)).Returns(Task.CompletedTask);

        // Act
        var result = await _service.PlaceOrderAsync(TestUserId, "г. Москва, ул. Тестовая, д.1", PaymentMethod.Card);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TestUserId, result.UserId);
        Assert.Equal(OrderStatus.Pending, result.Status);
        Assert.Equal(2, result.OrderItems.Count);
        Assert.Equal(400, result.TotalAmount);
        
        // Проверяем, что остатки уменьшились
        Assert.Equal(8, product1.StockQuantity);
        Assert.Equal(4, product2.StockQuantity);
        
        // Проверяем, что корзина очищена
        _mockCartRepo.Verify(r => r.ClearCartAsync(TestUserId), Times.Once);
    }

    // ========== СЦЕНАРИЙ 2: CalculateOrderTotal - ПОЗИТИВНЫЕ ТЕСТЫ ==========

    [Fact]
    public async Task CalculateOrderTotalAsync_WithoutPromo_ShouldReturnSubtotal()
    {
        // Arrange
        _mockDiscountService.Setup(r => r.ApplyDiscountAsync(500, null)).ReturnsAsync(500);
        _mockDiscountService.Setup(r => r.CalculateDiscountAsync(It.IsAny<Order>())).ReturnsAsync(0);

        // Act
        var result = await _service.CalculateOrderTotalAsync(500);

        // Assert
        Assert.Equal(500, result);
    }

    [Fact]
    public async Task CalculateOrderTotalAsync_WithPromoCode_ShouldApplyDiscount()
    {
        // Arrange
        _mockDiscountService.Setup(r => r.ApplyDiscountAsync(500, "WELCOME10")).ReturnsAsync(450);
        _mockDiscountService.Setup(r => r.CalculateDiscountAsync(It.IsAny<Order>())).ReturnsAsync(0);

        // Act
        var result = await _service.CalculateOrderTotalAsync(500, "WELCOME10");

        // Assert
        Assert.Equal(450, result);
    }

    // ========== НЕГАТИВНЫЕ ТЕСТЫ ==========

    [Fact]
    public async Task PlaceOrderAsync_WithEmptyCart_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var emptyCart = new Cart { UserId = TestUserId, Items = new List<CartItem>() };
        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(TestUserId)).ReturnsAsync(emptyCart);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _service.PlaceOrderAsync(TestUserId, "Адрес", PaymentMethod.Card));
    }

    [Fact]
    public async Task PlaceOrderAsync_WithInvalidUserId_ShouldThrowArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.PlaceOrderAsync("", "Адрес", PaymentMethod.Card));
    }

    [Fact]
    public async Task PlaceOrderAsync_WithInvalidAddress_ShouldThrowArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.PlaceOrderAsync(TestUserId, "", PaymentMethod.Card));
    }

    [Fact]
    public async Task PlaceOrderAsync_WithInsufficientStock_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var cart = new Cart
        {
            UserId = TestUserId,
            Items = new List<CartItem> { new CartItem { ProductId = 1, Quantity = 10 } }
        };
        var product = new Product { Id = 1, Name = "Товар", Price = 100, StockQuantity = 2, IsActive = true };

        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(TestUserId)).ReturnsAsync(cart);
        _mockProductRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _service.PlaceOrderAsync(TestUserId, "Адрес", PaymentMethod.Card));
    }

    [Fact]
    public async Task CancelOrderAsync_PendingOrder_ShouldCancelAndReturnStock()
    {
        // Arrange
        var product = new Product { Id = 1, Name = "Товар", Price = 100, StockQuantity = 5 };
        var order = new Order
        {
            Id = 1,
            Status = OrderStatus.Pending,
            OrderItems = new List<OrderItem> { new OrderItem { ProductId = 1, Quantity = 2 } }
        };
        
        _mockOrderRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(order);
        _mockProductRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);
        _mockOrderRepo.Setup(r => r.UpdateAsync(order)).Returns(Task.CompletedTask);
        _mockOrderRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _mockProductRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _service.CancelOrderAsync(1);

        // Assert
        Assert.True(result);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(7, product.StockQuantity); // 5 + 2 = 7
    }

    [Fact]
    public async Task CancelOrderAsync_ShippedOrder_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var order = new Order { Id = 1, Status = OrderStatus.Shipped };
        _mockOrderRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(order);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CancelOrderAsync(1));
    }

    // ========== ТЕСТЫ ГРАНИЧНЫХ УСЛОВИЙ ==========

    [Fact]
    public async Task CalculateOrderTotalAsync_WithZeroSubtotal_ShouldReturnZero()
    {
        _mockDiscountService.Setup(r => r.ApplyDiscountAsync(0, null)).ReturnsAsync(0);
        _mockDiscountService.Setup(r => r.CalculateDiscountAsync(It.IsAny<Order>())).ReturnsAsync(0);

        var result = await _service.CalculateOrderTotalAsync(0);
        
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetOrderByIdAsync_WithInvalidId_ShouldThrowArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetOrderByIdAsync(0));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetOrderByIdAsync(-1));
    }

    [Fact]
    public async Task GetUserOrdersAsync_WithInvalidUserId_ShouldThrowArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetUserOrdersAsync(""));
    }

    [Fact]
    public async Task GetUserOrdersAsync_WithNoOrders_ShouldReturnEmptyList()
    {
        // Arrange
        _mockOrderRepo.Setup(r => r.GetByUserIdAsync(TestUserId)).ReturnsAsync(new List<Order>());

        // Act
        var result = await _service.GetUserOrdersAsync(TestUserId);

        // Assert
        Assert.Empty(result);
    }
}