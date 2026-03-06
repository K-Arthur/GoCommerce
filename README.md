GoCommerce - Containerized E-Commerce Backend

This repository contains the source code for a simple, containerized e-commerce backend made up of four separate microservices. The platform uses ASP.NET Core 10, Entity Framework Core, SQL Server, Docker, Docker Compose, and RabbitMQ.

Project Architecture

The system uses a database-per-service microservices architecture. Services communicate with each other using both HTTP (synchronous) and RabbitMQ (asynchronous) methods. There are four microservices:

1. Product Service (Port 5001): Manages the product catalog, pricing, and stock. It listens for OrderCreated events to update stock levels.
2. Customer Service (Port 5002): Handles customer information.
3. Order Service (Port 5003): Manages customer orders and sends OrderCreated events to RabbitMQ.
4. Shipping Service (Port 5004): Tracks shipments linked to orders.

Key Points and Technical Decisions

* DTO Separation: Each service uses Data Transfer Objects (DTOs) to keep API contracts separate from internal models. Request DTOs are for input, and Response DTOs are for output. Domain entities are never exposed directly.
* Data Isolation: Every service has its own SQL Server container and its own Entity Framework DbContext. Services do not share databases, which helps avoid tight data coupling.
* Synchronous HTTP Validation:
  * When an order is created, the Order Service uses typed HttpClients to check with the Product Service and Customer Service to make sure the entities exist before processing the order.
  * When a shipment is created, the Shipping Service checks with the Order Service to confirm the order exists.
* Asynchronous Event-Based Communication (RabbitMQ):
  * After an order is created, the Order Service sends an OrderCreated event to a RabbitMQ fanout exchange (order_events).
  * The Product Service has a background process that listens to this exchange and automatically reduces the StockQuantity for each product in the order.
* Automatic EF Core Migrations: Each service’s Program.cs file is set up to apply database migrations automatically when starting. There is a retry policy to wait for the SQL Server containers to finish initializing.

Getting Started

Prerequisites

* Docker
* Docker Compose

Running the System

To start the platform, go to the root directory with the docker-compose.yml file and run:

docker compose up --build

This command will start 9 containers:

* 4 SQL Server database containers.
* 4 ASP.NET Core web API containers.
* 1 RabbitMQ message broker container (with management UI at http://localhost:15672, login: guest/guest).

Note: The first startup may take a minute while SQL Server and RabbitMQ initialize and the services run their Entity Framework Core migrations.

Testing the APIs

You can use standard REST clients like Postman or curl to interact with the APIs.

1. Create a Product

curl -X POST http://localhost:5001/api/products \
     -H "Content-Type: application/json" \
     -d '{"name":"Widget","description":"A test widget","price":9.99,"stockQuantity":100}'

2. Create a Customer

curl -X POST http://localhost:5002/api/customers \
     -H "Content-Type: application/json" \
     -d '{"firstName":"John","lastName":"Doe","email":"john@example.com","address":"123 Main St"}'

3. Create an Order (Validates Product and Customer, Publishes Event)

curl -X POST http://localhost:5003/api/orders \
     -H "Content-Type: application/json" \
     -d '{"customerId":1,"items":[{"productId":1,"quantity":2}]}'

After this call, the Product Service will automatically get the OrderCreated event through RabbitMQ and reduce the widget’s stock from 100 to 98.

4. Verify Stock Was Decremented

curl http://localhost:5001/api/products/1

5. Create a Shipment (Validates Order)

curl -X POST http://localhost:5004/api/shipments \
     -H "Content-Type: application/json" \
     -d '{"orderId":1,"shippingAddress":"123 Main St"}'