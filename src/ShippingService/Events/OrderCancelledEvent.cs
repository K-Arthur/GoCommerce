namespace ShippingService.Events;

public class OrderCancelledEvent
{
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
}
