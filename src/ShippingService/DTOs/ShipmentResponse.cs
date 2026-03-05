namespace ShippingService.DTOs;

public class ShipmentResponse
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string ShippingAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ShippedDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
}
