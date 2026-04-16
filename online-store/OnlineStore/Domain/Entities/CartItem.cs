namespace Domain.Entities;

public class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    
    public decimal TotalPrice => Price * Quantity;
    
    public virtual Cart Cart { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}