using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Models;
using OrderService.Services;
using OrderService.Events;

namespace OrderService.Controllers;

public class CreateOrderRequest
{
    public int CustomerId { get; set; }
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public class CreateOrderItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

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
    public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
    {
        return await _context.Orders.Include(o => o.Items).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> GetOrder(int id)
    {
        var order = await _context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        return order;
    }

    [HttpPost]
    public async Task<ActionResult<Order>> PostOrder(CreateOrderRequest request)
    {
        // 1. Validate Customer
        var customerExists = await _customerServiceClient.CustomerExistsAsync(request.CustomerId);
        if (!customerExists)
        {
            return BadRequest($"Customer with ID {request.CustomerId} not found or Customer Service is unavailable.");
        }

        // 2. Validate Products & Build Order
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

        // 3. Publish OrderCreated event to RabbitMQ
        var orderEvent = new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Items = order.Items.Select(i => new Events.OrderCreatedItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity
            }).ToList()
        };
        await _rabbitMqPublisher.PublishOrderCreatedAsync(orderEvent);

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }
}
