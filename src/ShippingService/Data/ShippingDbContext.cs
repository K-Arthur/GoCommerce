using Microsoft.EntityFrameworkCore;
using ShippingService.Models;

namespace ShippingService.Data;

public class ShippingDbContext : DbContext
{
    public ShippingDbContext(DbContextOptions<ShippingDbContext> options) : base(options) { }

    public DbSet<Shipment> Shipments { get; set; } = null!;
}
