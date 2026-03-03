namespace OrderService.Services;

public class CustomerServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CustomerServiceClient> _logger;

    public CustomerServiceClient(HttpClient httpClient, ILogger<CustomerServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> CustomerExistsAsync(int customerId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/customers/{customerId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error communicating with Customer Service for Customer ID {CustomerId}", customerId);
            throw new Exception("Customer Service is unavailable.");
        }
    }
}
