# GoCommerce - Containerized E-Commerce Backend

This repository contains the source code for a simplified, containerized e-commerce backend platform consisting of four independent microservices. It was built using ASP.NET Core 10, Entity Framework Core, SQL Server, Docker, and Docker Compose.

## Project Architecture

The system follows a strict **database-per-service** microservices architecture. There are four microservices:
1. **Product Service** (Port 5001) - Manages the product catalog, pricing, and stock.
2. **Customer Service** (Port 5002) - Manages customer information.
3. **Order Service** (Port 5003) - Tracks and manages customer orders.
4. **Shipping Service** (Port 5004) - Tracks shipments associated with orders.

### Key Points and Technical Decisions
- **No RabbitMQ/Asynchronous Events:** Based on the red strike-throughs in the exam PDF, RabbitMQ and event-driven architectures were explicitly excluded from the scope. Therefore, communication between services is entirely **synchronous via HTTP**.
- **Data Isolation:** Each service has its own dedicated SQL Server container and its own Entity Framework `DbContext`. Services do not share databases, preventing tight data coupling.
- **Synchronous HTTP Validation:** 
  - When an order is created, the `Order Service` uses strongly-typed `HttpClient`s to synchronously query the `Product Service` and `Customer Service` to ensure the entities exist before allowing the order.
  - When a shipment is created, the `Shipping Service` queries the `Order Service` to ensure the order exists.
- **Automatic EF Core Migrations:** The `Program.cs` files in each service are configured to automatically apply database migrations on startup. They include a robust retry policy to wait for the SQL Server containers to finish initializing.

## Getting Started

### Prerequisites
- Docker
- Docker Compose

### Running the System

To start the entire platform, navigate to the root directory where the `docker-compose.yml` file is located and run:

```bash
docker-compose up --build -d
```

This will spin up 8 containers:
- 4 SQL Server database containers.
- 4 ASP.NET Core web API containers.

*Note: Initial startup may take a minute as SQL Server initializes and the services run their Entity Framework Core migrations.*

### Testing the APIs
You can interact with the APIs using standard REST clients like Postman or `curl`.

**1. Create a Product**
```bash
curl -X POST http://localhost:5001/api/products \
     -H "Content-Type: application/json" \
     -d '{"name":"Widget","description":"A test widget","price":9.99,"stockQuantity":100}'
```

**2. Create a Customer**
```bash
curl -X POST http://localhost:5002/api/customers \
     -H "Content-Type: application/json" \
     -d '{"firstName":"John","lastName":"Doe","email":"john@example.com","address":"123 Main St"}'
```

**3. Create an Order (Validates Product and Customer)**
```bash
curl -X POST http://localhost:5003/api/orders \
     -H "Content-Type: application/json" \
     -d '{"customerId":1,"items":[{"productId":1,"quantity":2}]}'
```

**4. Create a Shipment (Validates Order)**
```bash
curl -X POST http://localhost:5004/api/shipments \
     -H "Content-Type: application/json" \
     -d '{"orderId":1,"shippingAddress":"123 Main St"}'
```
