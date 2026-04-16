namespace Domain.Enums;

public enum OrderStatus
{
    Pending,      // Ожидает обработки
    Paid,         // Оплачен
    Processing,   // В обработке
    Shipped,      // Отправлен
    Delivered,    // Доставлен
    Cancelled     // Отменен
}