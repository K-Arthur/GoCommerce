namespace ShippingService.DTOs;

public class CreateShipmentRequest
{
    public int OrderId { get; set; }
    public string ShippingAddress { get; set; } = string.Empty;
}
