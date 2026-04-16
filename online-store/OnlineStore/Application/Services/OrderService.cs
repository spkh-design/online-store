using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services;

public class OrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICartRepository _cartRepository;
    private readonly IDiscountService _discountService;

    public OrderService(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        ICartRepository cartRepository,
        IDiscountService discountService)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
        _discountService = discountService ?? throw new ArgumentNullException(nameof(discountService));
    }

    /// <summary>
    /// Сценарий 3: PlaceOrder - оформление заказа, очистка корзины
    /// </summary>
    public async Task<Order> PlaceOrderAsync(string userId, string deliveryAddress, PaymentMethod paymentMethod, string? promoCode = null)
    {
        // Валидация входных данных
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("Id пользователя не может быть пустым", nameof(userId));
        
        if (string.IsNullOrWhiteSpace(deliveryAddress))
            throw new ArgumentException("Адрес доставки не может быть пустым", nameof(deliveryAddress));

        // Получаем корзину пользователя
        var cart = await _cartRepository.GetCartByUserIdAsync(userId);
        if (cart == null || !cart.Items.Any())
            throw new InvalidOperationException("Корзина пуста. Невозможно оформить заказ");

        var orderItems = new List<OrderItem>();
        decimal subtotal = 0;

        // Проверяем остатки и создаём позиции заказа
        foreach (var cartItem in cart.Items)
        {
            var product = await _productRepository.GetByIdAsync(cartItem.ProductId);
            if (product == null)
                throw new InvalidOperationException($"Товар с id {cartItem.ProductId} не найден");

            if (product.StockQuantity < cartItem.Quantity)
                throw new InvalidOperationException($"Недостаточно товара '{product.Name}'. Доступно: {product.StockQuantity}");

            if (!product.IsAvailable())
                throw new InvalidOperationException($"Товар '{product.Name}' недоступен для заказа");

            // Уменьшаем остаток на складе
            product.ReduceStock(cartItem.Quantity);
            await _productRepository.UpdateAsync(product);

            var orderItem = new OrderItem
            {
                ProductId = cartItem.ProductId,
                Quantity = cartItem.Quantity,
                UnitPrice = product.Price
            };
            
            orderItems.Add(orderItem);
            subtotal += orderItem.TotalPrice;
        }

        // Сценарий 2: CalculateOrderTotal - расчёт итоговой суммы с учётом скидок
        var totalAmount = await CalculateOrderTotalAsync(subtotal, promoCode);

        // Создаём заказ
        var order = new Order
        {
            UserId = userId,
            DeliveryAddress = deliveryAddress,
            PaymentMethod = paymentMethod,
            Status = OrderStatus.Pending,
            TotalAmount = totalAmount,
            OrderItems = orderItems,
            CreatedAt = DateTime.UtcNow
        };

        await _orderRepository.AddAsync(order);
        await _orderRepository.SaveChangesAsync();
        await _productRepository.SaveChangesAsync();

        // Очищаем корзину
        await _cartRepository.ClearCartAsync(userId);

        return order;
    }

    /// <summary>
    /// Сценарий 2: CalculateOrderTotal - расчёт итоговой суммы с учётом скидок
    /// </summary>
    public async Task<decimal> CalculateOrderTotalAsync(decimal subtotal, string? promoCode = null)
    {
        if (subtotal < 0)
            throw new ArgumentException("Сумма заказа не может быть отрицательной", nameof(subtotal));

        decimal total = subtotal;

        // Применяем скидку по промокоду
        if (!string.IsNullOrWhiteSpace(promoCode))
        {
            total = await _discountService.ApplyDiscountAsync(total, promoCode);
        }

        // Применяем автоматические скидки (например, при заказе от определённой суммы)
        total = await _discountService.CalculateDiscountAsync(new Order { TotalAmount = total });

        return Math.Round(total, 2);
    }

    /// <summary>
    /// Получение заказа по ID
    /// </summary>
    public async Task<Order?> GetOrderByIdAsync(int id)
    {
        if (id <= 0)
            throw new ArgumentException("Id заказа должен быть больше 0", nameof(id));

        return await _orderRepository.GetByIdAsync(id);
    }

    /// <summary>
    /// Получение всех заказов пользователя
    /// </summary>
    public async Task<IEnumerable<Order>> GetUserOrdersAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("Id пользователя не может быть пустым", nameof(userId));

        return await _orderRepository.GetByUserIdAsync(userId);
    }

    /// <summary>
    /// Обновление статуса заказа
    /// </summary>
    public async Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus)
    {
        if (orderId <= 0)
            throw new ArgumentException("Id заказа должен быть больше 0", nameof(orderId));

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            throw new InvalidOperationException($"Заказ с id {orderId} не найден");

        order.UpdateStatus(newStatus);
        await _orderRepository.UpdateAsync(order);
        await _orderRepository.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Отмена заказа с возвратом товаров на склад
    /// </summary>
    public async Task<bool> CancelOrderAsync(int orderId)
    {
        if (orderId <= 0)
            throw new ArgumentException("Id заказа должен быть больше 0", nameof(orderId));

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            throw new InvalidOperationException($"Заказ с id {orderId} не найден");

        if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered)
            throw new InvalidOperationException("Нельзя отменить отправленный или доставленный заказ");

        // Возвращаем товары на склад
        foreach (var item in order.OrderItems)
        {
            var product = await _productRepository.GetByIdAsync(item.ProductId);
            if (product != null)
            {
                product.IncreaseStock(item.Quantity);
                await _productRepository.UpdateAsync(product);
            }
        }

        order.Cancel();
        await _orderRepository.UpdateAsync(order);
        await _orderRepository.SaveChangesAsync();
        await _productRepository.SaveChangesAsync();

        return true;
    }
}