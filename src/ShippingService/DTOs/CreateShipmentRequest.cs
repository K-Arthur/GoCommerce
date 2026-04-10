using System.ComponentModel.DataAnnotations;

namespace ShippingService.DTOs;

public class CreateShipmentRequest
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "OrderId must be a positive integer.")]
    public int OrderId { get; set; }

    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string ShippingAddress { get; set; } = string.Empty;
}
