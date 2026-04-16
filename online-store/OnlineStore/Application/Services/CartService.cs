using Application.Interfaces;
using Domain.Entities;

namespace Application.Services;

public class CartService
{
    private readonly ICartRepository _cartRepository;
    private readonly IProductRepository _productRepository;

    public CartService(ICartRepository cartRepository, IProductRepository productRepository)
    {
        _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    }

    /// <summary>
    /// Сценарий 1: AddToCart - добавление товара в корзину, проверка остатка
    /// </summary>
    public async Task<Cart> AddToCartAsync(string userId, int productId, int quantity)
    {
        // Валидация входных данных
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("Id пользователя не может быть пустым", nameof(userId));
        
        if (productId <= 0)
            throw new ArgumentException("Id товара должен быть больше 0", nameof(productId));
        
        if (quantity <= 0)
            throw new ArgumentException("Количество должно быть больше 0", nameof(quantity));

        // Получаем товар и проверяем остаток
        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null)
            throw new InvalidOperationException($"Товар с id {productId} не найден");

        if (product.StockQuantity < quantity)
            throw new InvalidOperationException($"Недостаточно товара на складе. Доступно: {product.StockQuantity}");

        if (!product.IsAvailable())
            throw new InvalidOperationException($"Товар '{product.Name}' недоступен для заказа");

        // Получаем корзину пользователя
        var cart = await _cartRepository.GetCartByUserIdAsync(userId);
        if (cart == null)
        {
            cart = new Cart
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                Items = new List<CartItem>()
            };
        }

        // Добавляем или обновляем товар в корзине
        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (existingItem != null)
        {
            var newQuantity = existingItem.Quantity + quantity;
            if (product.StockQuantity < newQuantity)
                throw new InvalidOperationException($"Общее количество ({newQuantity}) превышает остаток на складе ({product.StockQuantity})");
            
            existingItem.Quantity = newQuantity;
            existingItem.Price = product.Price;
            existingItem.ProductName = product.Name;
        }
        else
        {
            cart.Items.Add(new CartItem
            {
                ProductId = productId,
                ProductName = product.Name,
                Price = product.Price,
                Quantity = quantity,
                AddedAt = DateTime.UtcNow
            });
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _cartRepository.SaveCartAsync(cart);
        
        return cart;
    }

    /// <summary>
    /// Удаление товара из корзины
    /// </summary>
    public async Task<Cart> RemoveFromCartAsync(string userId, int productId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("Id пользователя не может быть пустым", nameof(userId));
        
        if (productId <= 0)
            throw new ArgumentException("Id товара должен быть больше 0", nameof(productId));

        var cart = await _cartRepository.GetCartByUserIdAsync(userId);
        if (cart == null)
            throw new InvalidOperationException("Корзина не найдена");

        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item == null)
            throw new InvalidOperationException($"Товар с id {productId} отсутствует в корзине");

        cart.Items.Remove(item);
        cart.UpdatedAt = DateTime.UtcNow;
        
        await _cartRepository.SaveCartAsync(cart);
        return cart;
    }

    /// <summary>
    /// Обновление количества товара в корзине
    /// </summary>
    public async Task<Cart> UpdateQuantityAsync(string userId, int productId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("Id пользователя не может быть пустым", nameof(userId));
        
        if (productId <= 0)
            throw new ArgumentException("Id товара должен быть больше 0", nameof(productId));
        
        if (quantity < 0)
            throw new ArgumentException("Количество не может быть отрицательным", nameof(quantity));

        var cart = await _cartRepository.GetCartByUserIdAsync(userId);
        if (cart == null)
            throw new InvalidOperationException("Корзина не найдена");

        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item == null)
            throw new InvalidOperationException($"Товар с id {productId} отсутствует в корзине");

        if (quantity == 0)
        {
            cart.Items.Remove(item);
        }
        else
        {
            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null)
                throw new InvalidOperationException($"Товар с id {productId} не найден");

            if (product.StockQuantity < quantity)
                throw new InvalidOperationException($"Недостаточно товара на складе. Доступно: {product.StockQuantity}");

            item.Quantity = quantity;
            item.Price = product.Price;
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _cartRepository.SaveCartAsync(cart);
        
        return cart;
    }

    /// <summary>
    /// Получение корзины пользователя
    /// </summary>
    public async Task<Cart?> GetCartAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("Id пользователя не может быть пустым", nameof(userId));

        return await _cartRepository.GetCartByUserIdAsync(userId);
    }

    /// <summary>
    /// Очистка корзины
    /// </summary>
    public async Task ClearCartAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("Id пользователя не может быть пустым", nameof(userId));

        await _cartRepository.ClearCartAsync(userId);
    }
}