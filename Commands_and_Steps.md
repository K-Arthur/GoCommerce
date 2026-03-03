# Step-by-Step Build Guide: GoCommerce Microservices

This document provides a breakdown of every command and structural step taken to build the `GoCommerce` project from scratch. It is designed to help you understand exactly how the boilerplate, packages, and architecture were generated.

## Phase 1: Solution and Project Scaffolding
First, we created a blank directory and initialized the main `.sln` (Solution) file to group everything together. Then, we generated four separate ASP.NET Core Web API projects.

```bash
# Create the src directory
mkdir -p src 

# Create the Solution file
dotnet new sln -n GoCommerce

# Navigate to src and scaffold 4 independent Web APIs
cd src
dotnet new webapi -n ProductService
dotnet new webapi -n CustomerService
dotnet new webapi -n OrderService
dotnet new webapi -n ShippingService
```

Next, we linked those four new projects to the main Solution file so that building the solution builds all four projects simultaneously.

```bash
# Return to the root folder
cd ..

# Add the projects to the GoCommerce.sln
dotnet sln GoCommerce.slnx add src/ProductService/ProductService.csproj src/CustomerService/CustomerService.csproj src/OrderService/OrderService.csproj src/ShippingService/ShippingService.csproj
```

## Phase 2: Installing Entity Framework Core
Entity Framework (EF) Core is the Object-Relational Mapper (ORM) used to interact with the SQL Server databases. Because this is a **database-per-service** architecture, we had to install the SQL Server provider and EF Core Design tools into **every single project** individually.

```bash
# Product Service
dotnet add src/ProductService/ProductService.csproj package Microsoft.EntityFrameworkCore.SqlServer
dotnet add src/ProductService/ProductService.csproj package Microsoft.EntityFrameworkCore.Design

# Customer Service
dotnet add src/CustomerService/CustomerService.csproj package Microsoft.EntityFrameworkCore.SqlServer
dotnet add src/CustomerService/CustomerService.csproj package Microsoft.EntityFrameworkCore.Design

# Order Service
dotnet add src/OrderService/OrderService.csproj package Microsoft.EntityFrameworkCore.SqlServer
dotnet add src/OrderService/OrderService.csproj package Microsoft.EntityFrameworkCore.Design

# Shipping Service
dotnet add src/ShippingService/ShippingService.csproj package Microsoft.EntityFrameworkCore.SqlServer
dotnet add src/ShippingService/ShippingService.csproj package Microsoft.EntityFrameworkCore.Design
```

*Note: For `OrderService` and `ShippingService`, we also utilized the built-in `HttpClient` factory (`Microsoft.Extensions.Http`), which comes pre-packaged with modern ASP.NET Core.*

## Phase 3: Writing the Code (Models, Data, Controllers)
For each of the four microservices, we followed the same core pattern manually:

1. **Models (`Models/`):** Created the C# classes representing the data (e.g., `Product.cs`, `Order.cs`).
2. **DbContext (`Data/`):** Created the EF Core DbContext class (e.g., `ProductDbContext.cs`) defining the `DbSet` tables.
3. **Configuration (`appsettings.json`):** Added the hardcoded database connection strings pointing to the Docker SQL Server hostnames (e.g., `Server=productdb;User Id=sa;...`).
4. **Controllers (`Controllers/`):** Scaffolded `ApiController` endpoints handling HTTP `GET`, `POST`, `PUT`, `DELETE`.
    - In `OrdersController` and `ShipmentsController`, we added custom logic to make synchronous `HttpClient` calls to the other services to validate existence before writing to the database.
5. **Program.cs (`Program.cs`):** Wired up dependency injection. We added the DbContexts to the services container, configured Swagger/OpenAPI, and wrote custom logic to automatically apply database migrations (`db.Database.MigrateAsync()`) when the app boots up.

## Phase 4: Containerization (Docker)
We needed to ensure the app could be run universally using Docker. We created a `Dockerfile` inside each of the four service directories. These files use a "Multi-Stage Build":
- They pull the heavy `.NET SDK` to compile the code.
- They publish the compiled `.dll` files.
- They pull the lightweight `.NET ASP.NET Runtime` to run the `.dll`.

Then, we tied the 4 Web APIs and 4 separate Microsoft SQL Server 2022 containers together in the root `docker-compose.yml`.

## Phase 5: Entity Framework Migrations
Before we could boot the cluster, we needed to generate the SQL instruction files (Migrations) that tell EF Core how to create the database schema from our C# Models. 

To run these commands, we ensured the EF Core global tool was installed.

```bash
# Install the EF Core CLI tools
dotnet tool install --global dotnet-ef

# Generate an 'InitialCreate' migration for all 4 services
dotnet ef migrations add InitialCreate --project src/ProductService/ProductService.csproj
dotnet ef migrations add InitialCreate --project src/CustomerService/CustomerService.csproj
dotnet ef migrations add InitialCreate --project src/OrderService/OrderService.csproj
dotnet ef migrations add InitialCreate --project src/ShippingService/ShippingService.csproj
```
*These commands generated a `Migrations` folder in every project containing the schema definitions.*

## Phase 6: Orchestration and Testing
With the code written, Dockerfiles established, and EF Migrations generated, the system was ready to deploy.

```bash
# Build and boot the entire 8-container cluster in Detached (-d) mode
docker-compose up --build -d
```

Because of our custom initialization logic in `Program.cs`, the Web APIs booted, waited for SQL Server to become available, and then automatically ran the SQL created in Phase 5 to build the databases on the fly. 

Finally, we used `curl` commands to test the endpoints and ensure traffic routed correctly across the Docker bridge network.
