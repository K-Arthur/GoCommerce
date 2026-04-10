using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ApiGateway.Controllers;

[Route("api/aggregate")]
[ApiController]
public class AggregationController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public AggregationController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet("orders/{orderId}")]
    public async Task<IActionResult> GetOrderAggregate(int orderId)
    {
        var orderClient = _httpClientFactory.CreateClient("OrderService");
        var customerClient = _httpClientFactory.CreateClient("CustomerService");
        var productClient = _httpClientFactory.CreateClient("ProductService");

        var orderResponse = await orderClient.GetAsync($"/api/orders/{orderId}");
        if (!orderResponse.IsSuccessStatusCode)
            return NotFound($"Order {orderId} not found.");

        var orderJson = await orderResponse.Content.ReadAsStringAsync();
        var order = JsonSerializer.Deserialize<JsonElement>(orderJson);

        var customerId = order.GetProperty("customerId").GetInt32();
        var customerResponse = await customerClient.GetAsync($"/api/customers/{customerId}");
        JsonElement? customer = null;
        if (customerResponse.IsSuccessStatusCode)
        {
            var customerJson = await customerResponse.Content.ReadAsStringAsync();
            customer = JsonSerializer.Deserialize<JsonElement>(customerJson);
        }

        var products = new List<JsonElement>();
        if (order.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                var productId = item.GetProperty("productId").GetInt32();
                var productResponse = await productClient.GetAsync($"/api/products/{productId}");
                if (productResponse.IsSuccessStatusCode)
                {
                    var productJson = await productResponse.Content.ReadAsStringAsync();
                    products.Add(JsonSerializer.Deserialize<JsonElement>(productJson));
                }
            }
        }

        return Ok(new
        {
            order,
            customer,
            products
        });
    }
}
