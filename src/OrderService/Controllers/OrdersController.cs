using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.DTOs;
using OrderService.Models;
using OrderService.Services;
using OrderService.Events;

namespace OrderService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OrdersController : ControllerBase
{
    private readonly OrderDbContext _context;
    private readonly CustomerServiceClient _customerServiceClient;
    private readonly ProductServiceClient _productServiceClient;
    private readonly RabbitMqPublisher _rabbitMqPublisher;

    public OrdersController(
        OrderDbContext context,
        CustomerServiceClient customerServiceClient,
        ProductServiceClient productServiceClient,
        RabbitMqPublisher rabbitMqPublisher)
    {
        _context = context;
        _customerServiceClient = customerServiceClient;
        _productServiceClient = productServiceClient;
        _rabbitMqPublisher = rabbitMqPublisher;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderResponse>>> GetOrders()
    {
        var orders = await _context.Orders.Include(o => o.Items).ToListAsync();
        return Ok(orders.Select(MapToResponse));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderResponse>> GetOrder(int id)
    {
        var order = await _context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        return Ok(MapToResponse(order));
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> PostOrder(CreateOrderRequest request)
    {
        var customerExists = await _customerServiceClient.CustomerExistsAsync(request.CustomerId);
        if (!customerExists)
        {
            return BadRequest($"Customer with ID {request.CustomerId} not found or Customer Service is unavailable.");
        }

        var order = new Order
        {
            CustomerId = request.CustomerId,
            OrderDate = DateTime.UtcNow,
            Status = "Created",
            Items = new List<OrderItem>(),
            TotalAmount = 0
        };

        foreach (var itemReq in request.Items)
        {
            var productResult = await _productServiceClient.GetProductDetailsAsync(itemReq.ProductId);
            if (!productResult.Exists)
            {
                return BadRequest($"Product with ID {itemReq.ProductId} not found or Product Service is unavailable.");
            }

            var orderItem = new OrderItem
            {
                ProductId = itemReq.ProductId,
                ProductName = productResult.Name,
                Quantity = itemReq.Quantity,
                UnitPrice = productResult.Price
            };

            order.TotalAmount += (orderItem.Quantity * orderItem.UnitPrice);
            order.Items.Add(orderItem);
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var orderCreatedEvent = new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Items = order.Items.Select(i => new OrderCreatedItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity
            }).ToList()
        };
        await _rabbitMqPublisher.PublishOrderCreatedAsync(orderCreatedEvent);

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, MapToResponse(order));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var order = await _context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        if (order.Status == "Cancelled")
            return BadRequest("Order is already cancelled.");

        order.Status = "Cancelled";
        await _context.SaveChangesAsync();

        var orderCancelledEvent = new OrderCancelledEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId
        };
        await _rabbitMqPublisher.PublishOrderCancelledAsync(orderCancelledEvent);

        return NoContent();
    }

    private static OrderResponse MapToResponse(Order order) => new()
    {
        Id = order.Id,
        CustomerId = order.CustomerId,
        OrderDate = order.OrderDate,
        Status = order.Status,
        TotalAmount = order.TotalAmount,
        Items = order.Items.Select(i => new OrderItemResponse
        {
            Id = i.Id,
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice
        }).ToList()
    };
}
