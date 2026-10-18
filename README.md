# E-commerce API - Taani

A comprehensive e-commerce API built with .NET 10.0 using Clean Architecture principles.

## 🏗️ Architecture

This solution follows **Clean Architecture** pattern with clear separation of concerns:

- **Ecom.Domain**: Core business entities and domain logic
- **Ecom.Application**: Application services, DTOs, and business rules
- **Ecom.Infrastructure**: Data access, repositories, and external services
- **Ecom.API**: Web API controllers and configuration

## 🚀 Features

### Core Functionality
- **Product Management**: CRUD operations for products with categories and subcategories
- **Order Management**: Complete order lifecycle from creation to delivery
- **User Authentication**: Identity-based authentication system
- **Payment Processing**: Transaction management with multiple payment methods
- **Review System**: Product ratings and reviews

### Technical Features
- **Repository Pattern** with Unit of Work
- **AutoMapper** for object mapping
- **Entity Framework Core** with PostgreSQL
- **ASP.NET Core Identity** for authentication
- **Swagger/OpenAPI** documentation
- **Clean Architecture** principles

## 📋 Prerequisites

- .NET 10.0 SDK or later
- PostgreSQL with matching `pg_dump` / `pg_restore` tools for production backups
- Visual Studio 2024+ or VS Code

## 🔧 Setup Instructions

### 1. Clone and Restore
```bash
git clone <repository-url>
cd Ecom.taani
dotnet restore
```

### 2. Database Setup
Set the database connection through the `ConnectionStrings__DefaultConnection`
environment variable before running the API or Entity Framework migrations.
For example, in PowerShell:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5432;Database=EcomTaaniDb;Username=aljawhara;Password=your-password'
```

For deployments, supply this variable through the service environment or secret
store with your PostgreSQL connection string. Do not commit database credentials
to configuration files. The API and migration tool report a configuration error
when no connection string is supplied.

For your Ubuntu VPS, existing SQL Server data transfer, systemd environment setup,
daily production backups and restore instructions, follow
[the PostgreSQL deployment guide](docs/POSTGRESQL_UBUNTU.md) before deploying.

### 3. Local development secrets

The API loads .NET User Secrets in Development. Before the first run, generate
two independent random keys and store them outside the repository (PowerShell):

```powershell
$guestKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
$jwtKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet user-secrets set "CheckoutSecurity:GuestTokenDerivationKey" $guestKey --project Ecom.API
dotnet user-secrets set "JwtSettings:SecretKey" $jwtKey --project Ecom.API
```

Keep these keys stable between runs. Regenerating them invalidates existing JWTs
and affects recovery of existing guest checkouts. User Secrets are for local
development; deployments must supply `CheckoutSecurity__GuestTokenDerivationKey`
and `JwtSettings__SecretKey` through their secret store or environment variables.
Both values must contain at least 32 UTF-8 bytes.

### 4. Build and Run
```bash
# Build the solution
dotnet build

# Run the API
cd Ecom.API
dotnet run
```

The API will be available at:
- HTTP: `http://localhost:5055`
- HTTPS: `https://localhost:7130`
- Swagger UI: `https://localhost:7130/swagger`

## 📚 API Endpoints

### Authentication
```
POST /api/auth/register     - Register new user
POST /api/auth/login        - User login
POST /api/auth/logout       - User logout
GET  /api/auth/user/{id}    - Get user details
```

### Categories
```
GET    /api/categories                    - Get all categories
GET    /api/categories/{id}               - Get category by ID
POST   /api/categories                    - Create category
PUT    /api/categories/{id}               - Update category
DELETE /api/categories/{id}               - Delete category
GET    /api/categories/{id}/subcategories - Get subcategories
POST   /api/categories/subcategories      - Create subcategory
PUT    /api/categories/subcategories/{id} - Update subcategory
DELETE /api/categories/subcategories/{id} - Delete subcategory
```

### Products
```
GET    /api/products                    - Get products with filters
GET    /api/products/{id}               - Get product by ID
POST   /api/products                    - Create product
PUT    /api/products/{id}               - Update product
DELETE /api/products/{id}               - Delete product
GET    /api/products/featured           - Get featured products
GET    /api/products/category/{id}      - Get products by category
GET    /api/products/subcategory/{id}   - Get products by subcategory
GET    /api/products/{id}/related       - Get related products
GET    /api/products/{id}/ratings       - Get product ratings
POST   /api/products/{id}/ratings       - Add product rating
```

### Orders
```
GET    /api/orders                 - Get orders (admin)
GET    /api/orders/{id}            - Get order by ID
GET    /api/orders/number/{number} - Get order by number
GET    /api/orders/user/{userId}   - Get user orders
GET    /api/orders/my-orders       - Get current user orders
POST   /api/orders                 - Create order
PUT    /api/orders/{id}/status     - Update order status (admin)
PUT    /api/orders/{id}/cancel     - Cancel order
GET    /api/orders/statistics      - Get order statistics (admin)
GET    /api/orders/sales           - Get sales data (admin)
POST   /api/orders/{id}/payment    - Process payment
GET    /api/orders/{id}/transactions - Get order transactions
```

## 🗄️ Database Schema

### Core Entities
- **AppUsers**: User accounts (extends IdentityUser)
- **Category**: Product categories
- **SubCategory**: Product subcategories
- **Product**: Product catalog with pricing and images
- **ProductDetails**: Product specifications (key-value pairs)
- **Order**: Customer orders
- **OrderItem**: Order line items
- **ShippingAddress**: Delivery addresses
- **Transaction**: Payment records
- **Rating**: Product reviews and ratings

### Key Relationships
- Products belong to SubCategories
- SubCategories belong to Categories
- Orders contain multiple OrderItems
- Orders have ShippingAddresses
- Users can have multiple Orders and ShippingAddresses
- Products can have multiple Ratings and ProductDetails

## 🔒 Authentication & Authorization

The API uses ASP.NET Core Identity with cookie-based authentication. Some endpoints require:
- **User authentication**: Order management, reviews
- **Admin/Staff roles**: Order status updates, statistics

## 📝 Sample Usage

### Create a Category
```http
POST /api/categories
Content-Type: application/json

{
  "name": "Electronics",
  "description": "Electronic devices and accessories"
}
```

### Create a Product
```http
POST /api/products
Content-Type: application/json

{
  "title": "Smartphone X1",
  "description": "Latest smartphone with advanced features",
  "oldPrice": 999.99,
  "newPrice": 899.99,
  "images": ["image1.jpg", "image2.jpg"],
  "subCategoryId": 1,
  "productDetails": [
    {
      "label": "Brand",
      "value": "TechCorp"
    },
    {
      "label": "Storage",
      "value": "128GB"
    }
  ]
}
```

### Place an Order
```http
POST /api/orders
Content-Type: application/json

{
  "items": [
    {
      "productId": 1,
      "name": "Smartphone X1",
      "image": "image1.jpg",
      "price": 899.99,
      "quantity": 1
    }
  ],
  "shippingAddress": {
    "fullName": "John Doe",
    "phone": "+1234567890",
    "street": "123 Main St",
    "city": "New York",
    "state": "NY",
    "postalCode": "10001",
    "country": "USA"
  },
  "paymentMethod": 0
}
```

## 🛠️ Development

### Adding New Features
1. Add entities to `Ecom.Domain/Entity`
2. Create DTOs in `Ecom.Application/DTOs`
3. Implement services in `Ecom.Application/Services`
4. Add controllers in `Ecom.API/Controllers`
5. Update AutoMapper profiles if needed

### Database Migrations
```bash
# Add migration
dotnet ef migrations add MigrationName --project Ecom.Infrastructure --startup-project Ecom.API

# Update database
dotnet ef database update --project Ecom.Infrastructure --startup-project Ecom.API
```

## 🧪 Testing

The application includes:
- Comprehensive API endpoints for testing
- Swagger UI for interactive testing
- Clean architecture supporting unit testing

## 📄 License

This project is licensed under the MIT License.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

---

**Built with ❤️ using .NET 10.0 and Clean Architecture**





