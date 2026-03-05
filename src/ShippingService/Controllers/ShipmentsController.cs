using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShippingService.Data;
using ShippingService.Models;
using ShippingService.DTOs;

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
    public async Task<ActionResult<IEnumerable<ShipmentResponse>>> GetShipments()
    {
        var shipments = await _context.Shipments.ToListAsync();
        return shipments.Select(s => MapToResponse(s)).ToList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ShipmentResponse>> GetShipment(int id)
    {
        var shipment = await _context.Shipments.FindAsync(id);

        if (shipment == null) return NotFound();

        return MapToResponse(shipment);
    }

    [HttpPost]
    public async Task<ActionResult<ShipmentResponse>> PostShipment(CreateShipmentRequest request)
    {
        // Validate Order exists
        try
        {
            var response = await _httpClient.GetAsync($"/api/orders/{request.OrderId}");
            if (!response.IsSuccessStatusCode)
            {
                return BadRequest($"Order with ID {request.OrderId} not found or Order Service is unavailable.");
            }
        }
        catch (Exception)
        {
            return BadRequest("Order Service is unavailable for validation.");
        }

        var shipment = new Shipment
        {
            OrderId = request.OrderId,
            ShippingAddress = request.ShippingAddress,
            Status = "Pending"
        };

        _context.Shipments.Add(shipment);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetShipment), new { id = shipment.Id }, MapToResponse(shipment));
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateShipmentStatusRequest request)
    {
        var shipment = await _context.Shipments.FindAsync(id);
        if (shipment == null) return NotFound();

        shipment.Status = request.Status;
        if (request.Status == "Shipped") shipment.ShippedDate = DateTime.UtcNow;
        else if (request.Status == "Delivered") shipment.DeliveredDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static ShipmentResponse MapToResponse(Shipment shipment)
    {
        return new ShipmentResponse
        {
            Id = shipment.Id,
            OrderId = shipment.OrderId,
            ShippingAddress = shipment.ShippingAddress,
            Status = shipment.Status,
            ShippedDate = shipment.ShippedDate,
            DeliveredDate = shipment.DeliveredDate
        };
    }
}
