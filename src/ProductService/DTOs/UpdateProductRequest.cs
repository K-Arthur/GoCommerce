using System.ComponentModel.DataAnnotations;

namespace ProductService.DTOs;

public class UpdateProductRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(0.01, 1000000, ErrorMessage = "Price must be greater than zero.")]
    public decimal Price { get; set; }

    [Required]
    [Range(0, 100000, ErrorMessage = "StockQuantity must be between 0 and 100000.")]
    public int StockQuantity { get; set; }
}
