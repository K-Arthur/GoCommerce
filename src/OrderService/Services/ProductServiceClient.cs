namespace OrderService.Services;

public class ProductServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductServiceClient> _logger;

    public ProductServiceClient(HttpClient httpClient, ILogger<ProductServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(bool Exists, string Name, decimal Price)> GetProductDetailsAsync(int productId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/products/{productId}");
            if (response.IsSuccessStatusCode)
            {
                var product = await response.Content.ReadFromJsonAsync<ProductDto>();
                if (product != null)
                {
                    return (true, product.Name, product.Price);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error communicating with Product Service for Product ID {ProductId}", productId);
            throw new Exception("Product Service is unavailable.");
        }

        return (false, string.Empty, 0);
    }
}

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
