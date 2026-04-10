using System.ComponentModel.DataAnnotations;

namespace OrderService.DTOs;

public class CreateOrderItemRequest
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "ProductId must be a positive integer.")]
    public int ProductId { get; set; }

    [Required]
    [Range(1, 10000, ErrorMessage = "Quantity must be between 1 and 10000.")]
    public int Quantity { get; set; }
}
