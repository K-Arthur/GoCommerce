using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShippingService.Data;
using ShippingService.Models;

namespace ShippingService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ShipmentsController : ControllerBase
{
    private readonly ShippingDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public ShipmentsController(ShippingDbContext context, HttpClient httpClient, IConfiguration configuration)
    {
        _context = context;
        _httpClient = httpClient;
        _configuration = configuration;
        _httpClient.BaseAddress = new Uri(_configuration["ServiceUrls:OrderService"]!);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Shipment>>> GetShipments()
    {
        return await _context.Shipments.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Shipment>> GetShipment(int id)
    {
        var shipment = await _context.Shipments.FindAsync(id);

        if (shipment == null) return NotFound();

        return shipment;
    }

    [HttpPost]
    public async Task<ActionResult<Shipment>> PostShipment(Shipment shipment)
    {
        // Validate Order exists
        try
        {
            var response = await _httpClient.GetAsync($"/api/orders/{shipment.OrderId}");
            if (!response.IsSuccessStatusCode)
            {
                return BadRequest($"Order with ID {shipment.OrderId} not found or Order Service is unavailable.");
            }
        }
        catch (Exception)
        {
            return BadRequest("Order Service is unavailable for validation.");
        }

        shipment.Status = "Pending";
        _context.Shipments.Add(shipment);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetShipment), new { id = shipment.Id }, shipment);
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] string status)
    {
        var shipment = await _context.Shipments.FindAsync(id);
        if (shipment == null) return NotFound();

        shipment.Status = status;
        if (status == "Shipped") shipment.ShippedDate = DateTime.UtcNow;
        else if (status == "Delivered") shipment.DeliveredDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }
}
