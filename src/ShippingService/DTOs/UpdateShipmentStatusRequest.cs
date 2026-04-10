using System.ComponentModel.DataAnnotations;

namespace ShippingService.DTOs;

public class UpdateShipmentStatusRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Status { get; set; } = string.Empty;
}
