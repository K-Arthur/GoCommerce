namespace ShippingService.Models;

public class Shipment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string ShippingAddress { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime? ShippedDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
}
