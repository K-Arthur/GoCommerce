namespace ProductService.Events;

public class OrderCreatedEvent
{
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public List<OrderCreatedItem> Items { get; set; } = new();
}

public class OrderCreatedItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}
