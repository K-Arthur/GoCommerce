GoCommerce - Containerized E-Commerce Backend

This repository contains the source code for a containerized e-commerce backend made up of five microservices behind a single API Gateway. The platform uses ASP.NET Core 8, Entity Framework Core, SQL Server, Docker, Docker Compose, and RabbitMQ.

Project Architecture

The system uses a database-per-service microservices architecture. Services communicate with each other using both HTTP (synchronous) and RabbitMQ (asynchronous) methods. All client traffic enters through a single API Gateway on port 5000. Each service also exposes its own port and Swagger UI for direct testing.

1. API Gateway (Port 5000): A YARP reverse proxy that routes all client requests to the appropriate downstream service. It also hosts a custom aggregation endpoint that merges order, customer, and product data into a single response.
2. Product Service (Port 5001): Manages the product catalog, pricing, and stock. It listens for OrderCreated events to update stock levels.
3. Customer Service (Port 5002): Handles customer information.
4. Order Service (Port 5003): Manages customer orders and publishes OrderCreated and OrderCancelled events to RabbitMQ.
5. Shipping Service (Port 5004): Tracks shipments linked to orders. It listens for OrderCancelled events to automatically cancel shipments.

Key Points and Technical Decisions

* API Gateway (YARP): The gateway is the single entry point for all client requests. It uses YARP (Yet Another Reverse Proxy), a first-party Microsoft library, to forward requests to downstream services based on URL path prefix. Route configuration is defined in appsettings.json. The gateway also hosts an aggregation controller that calls three services and returns a merged response at GET /api/aggregate/orders/{orderId}.
* Swagger UI: Every service registers Swashbuckle middleware and exposes a Swagger UI at /swagger on its respective port. This lets you browse all available endpoints, view request/response schemas, and execute test calls directly from the browser without needing curl or Postman.
* DTO Separation: Each service uses Data Transfer Objects (DTOs) to keep API contracts separate from internal models. Request DTOs are for input, and Response DTOs are for output. Domain entities are never exposed directly. All request DTOs carry data annotation attributes ([Required], [Range], [EmailAddress], [StringLength]) that the framework enforces automatically, returning structured 400 errors for invalid input.
* Data Isolation: Every service has its own SQL Server container and its own Entity Framework DbContext. Services do not share databases, which helps avoid tight data coupling.
* Synchronous HTTP Validation:
  * When an order is created, the Order Service uses typed HttpClients to check with the Product Service and Customer Service to make sure the entities exist before processing the order.
  * When a shipment is created, the Shipping Service checks with the Order Service to confirm the order exists.
* Asynchronous Event-Based Communication (RabbitMQ):
  * After an order is created, the Order Service sends an OrderCreated event to a RabbitMQ fanout exchange (order_events). The Product Service has a background process that listens to this exchange and automatically reduces the StockQuantity for each product in the order.
  * After an order is cancelled, the Order Service sends an OrderCancelled event to a separate fanout exchange (order_cancelled_events). The Shipping Service has a background process that listens to this exchange and automatically sets the shipment status to "Cancelled".
  * Both consumers use durable queues, manual acknowledgement, and a retry loop with 10 attempts to handle the Docker startup race condition where RabbitMQ may not be ready yet.
* Automatic EF Core Migrations: Each service's Program.cs file is set up to apply database migrations automatically when starting. There is a retry policy to wait for the SQL Server containers to finish initializing.

Getting Started

Prerequisites

* Docker Desktop or Podman (Fedora/RHEL users should use Podman)
* Docker Compose or podman-compose

Running the System

To start the platform, go to the root directory with the docker-compose.yml file and run:

docker compose up --build

For Fedora/RHEL (Podman):

podman pull docker.io/library/rabbitmq:3-management
podman-compose up --build

This command will start 10 containers:

* 4 SQL Server database containers.
* 4 ASP.NET Core web API containers (internal, no host ports).
* 1 RabbitMQ message broker container (with management UI at http://localhost:15672, login: guest/guest).
* 1 API Gateway container (port 5000, the only exposed API endpoint).

Note: The first startup may take a minute while SQL Server and RabbitMQ initialize and the services run their Entity Framework Core migrations.

Stopping the System

docker compose down

For Fedora/RHEL (Podman):

podman-compose down

To wipe all data for a clean slate:

docker compose down -v

Verifying Services via Swagger UI

Each service exposes its own Swagger UI for browsing and testing endpoints directly from the browser:

* API Gateway: http://localhost:5000/swagger — shows the aggregation endpoint only (YARP proxy routes are config-based and do not appear here)
* Product Service: http://localhost:5001/swagger — CRUD endpoints for products
* Customer Service: http://localhost:5002/swagger — CRUD endpoints for customers
* Order Service: http://localhost:5003/swagger — order creation, listing, and cancellation
* Shipping Service: http://localhost:5004/swagger — shipment creation, listing, and status updates

To test an endpoint, expand it in the Swagger UI, click "Try it out", fill in the request body or parameters, and click "Execute". A successful 200 or 201 response with a JSON body confirms the service and its database are operational.

You can also verify GET endpoints directly in the browser address bar:
* http://localhost:5001/api/products — returns a JSON array (empty [] if no data yet, which is expected before seeding)
* http://localhost:5002/api/customers — returns a JSON array of customers
* http://localhost:5003/api/orders — returns a JSON array of orders
* http://localhost:5004/api/shipments — returns a JSON array of shipments

Common error signs:
* ERR_CONNECTION_REFUSED — the container is not running.
* 502 Bad Gateway — the gateway is up but the downstream service is unreachable.
* 503 Service Unavailable — YARP could not forward the request. Check gateway logs with: docker compose logs apigateway

Testing the APIs

All requests go through the API Gateway at http://localhost:5000. You can use Swagger UI, Postman, or curl to interact with the APIs.

1. Create a Product

curl -X POST http://localhost:5000/api/products \
     -H "Content-Type: application/json" \
     -d '{"name":"Widget","description":"A test widget","price":9.99,"stockQuantity":100}'

2. Create a Customer

curl -X POST http://localhost:5000/api/customers \
     -H "Content-Type: application/json" \
     -d '{"firstName":"John","lastName":"Doe","email":"john@example.com","address":"123 Main St"}'

3. Create an Order (Validates Product and Customer, Publishes OrderCreated Event)

curl -X POST http://localhost:5000/api/orders \
     -H "Content-Type: application/json" \
     -d '{"customerId":1,"items":[{"productId":1,"quantity":2}]}'

After this call, the Product Service will automatically get the OrderCreated event through RabbitMQ and reduce the widget's stock from 100 to 98.

4. Verify Stock Was Decremented

curl http://localhost:5000/api/products/1

5. Create a Shipment (Validates Order)

curl -X POST http://localhost:5000/api/shipments \
     -H "Content-Type: application/json" \
     -d '{"orderId":1,"shippingAddress":"123 Main St"}'

6. Test the Aggregation Endpoint

curl http://localhost:5000/api/aggregate/orders/1

This returns a single JSON response merging the order, customer, and product data from three different services.

7. Cancel the Order (Publishes OrderCancelled Event)

curl -X DELETE http://localhost:5000/api/orders/1

After this call, the Shipping Service will automatically get the OrderCancelled event through RabbitMQ and set the shipment status to "Cancelled".

8. Verify Shipment Was Cancelled

curl http://localhost:5000/api/shipments/1

9. Test DTO Validation

curl -X POST http://localhost:5000/api/customers \
     -H "Content-Type: application/json" \
     -d '{"firstName":"John"}'

This returns a 400 Bad Request with structured error messages for each missing required field.