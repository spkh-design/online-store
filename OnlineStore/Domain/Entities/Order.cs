using Domain.Enums;

namespace Domain.Entities;

public class Order
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; set; }
    public string DeliveryAddress { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; }
    public string? Comment { get; set; }
    
    public virtual User User { get; set; } = null!;
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    
    public void UpdateStatus(OrderStatus newStatus)
    {
        if (Status == OrderStatus.Delivered || Status == OrderStatus.Cancelled)
            throw new InvalidOperationException("Нельзя изменить статус завершённого заказа");
        Status = newStatus;
    }
    
    public decimal CalculateTotal()
    {
        TotalAmount = OrderItems.Sum(i => i.TotalPrice);
        return TotalAmount;
    }
    
    public void Cancel()
    {
        if (Status == OrderStatus.Shipped || Status == OrderStatus.Delivered)
            throw new InvalidOperationException("Нельзя отменить отправленный или доставленный заказ");
        Status = OrderStatus.Cancelled;
    }
}