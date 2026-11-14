using Ecom.API.HostedServices;
using Ecom.Application.Mappings;
using Ecom.Application.Services;
using Ecom.Application.Services.Interfaces;
using Ecom.Application.Payments;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.Repositories;
using Ecom.Infrastructure.Repositories.Interfaces;
using Ecom.Infrastructure.Services;
using Ecom.Domain.Interfaces;
using Ecom.Infrastructure.UnitOfWork;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi;
using Ecom.API.Monitoring;
using Ecom.Application.WhatsApp;
using Ecom.Infrastructure.WhatsApp;
using Ecom.API.Hosting;
using Microsoft.AspNetCore.Builder;
using Ecom.Application.Telegram;
var builder = WebApplication.CreateBuilder(args);


builder.Services.Configure<ForwardedHeadersOptions>(TrustedReverseProxy.Configure);
//builder.Services.AddAIShopping(builder.Configuration);
builder.Services.AddDataProtection();
builder.Services.AddSingleton<ShoppingIdentity>();
builder.Services.AddSingleton<ShoppingRequestGate>();
builder.Services.AddScoped<ShoppingRequestFilter>();

builder.Services.AddOptions<MonitoringOptions>()
    .Bind(builder.Configuration.GetSection("Monitoring"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Directory), "Monitoring:Directory is required.")
    .Validate(o => o.RetentionDays is >= 1 and <= 90 && o.MaxFiles is >= 2 and <= 1000,
        "Monitoring retention must be 1-90 days and MaxFiles 2-1000.")
    .Validate(o => o.MaxFileBytes is >= 65536 and <= 104857600 && o.RecentEventLimit is >= 100 and <= 50000,
        "Monitoring file size must be 64KB-100MB and recent event limit 100-50000.")
    .Validate(o => o.QueueCapacity is >= 100 and <= 100000 && o.HealthIntervalSeconds is >= 10 and <= 300 &&
        o.SlowRequestMilliseconds >= 1, "Invalid monitoring queue, health interval or slow request threshold.")
    .ValidateOnStart();
builder.Services.AddSingleton<MonitoringStore>();
builder.Services.AddSingleton<MonitoringState>();
builder.Services.AddSingleton<ILoggerProvider, MonitoringLoggerProvider>();
builder.Logging.AddFilter<MonitoringLoggerProvider>(null, LogLevel.Information);
builder.Services.AddHostedService<MonitoringPersistenceWorker>();
builder.Services.AddHostedService<MonitoringHealthWorker>();
builder.Services.AddScoped<DatabaseAuditInterceptor>();
builder.Services.AddTransient<MonitoringHttpHandler>();
// Apply safe outbound metadata logging to every factory-created HTTP client.
builder.Services.ConfigureHttpClientDefaults(client => client.AddHttpMessageHandler<MonitoringHttpHandler>());

// Configure FormOptions for large file uploads (up to 50MB)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50 MB
    options.ValueLengthLimit = int.MaxValue;
    options.ValueCountLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

// Add services to the container.
builder.Services.AddControllers(options =>
{
    // Request size limits for security
    options.MaxModelBindingCollectionSize = 100;
})
.AddJsonOptions(options =>
{
    // Performance: Use camelCase for JSON
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.WriteIndented = false; // Smaller payload in production
});

// Add HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Sadad HttpClient with base URL from configuration
var sadadBaseUrl = builder.Configuration["Sadad:BaseUrl"] ?? "https://apisandbox.sadadpay.net";
builder.Services.AddHttpClient("Sadad", client =>
{
    client.BaseAddress = new Uri(sadadBaseUrl);
});

// Add Entity Framework with factory to inject IServiceProvider
// Performance: Enable connection pooling and query optimizations
builder.Services.AddDbContext<EcomDbContext>((serviceProvider, options) =>
{
    var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Database connection is not configured. Set the ConnectionStrings__DefaultConnection environment variable.");
    }

    options.UseNpgsql(connectionString, sqlOptions =>
    {
        // Performance: Enable connection resiliency
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
        // Performance: Enable query splitting for better performance
        sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    });
    options.AddInterceptors(serviceProvider.GetRequiredService<DatabaseAuditInterceptor>());
    // Performance: Connection pooling is enabled by default in EF Core
    // Performance: Disable change tracking for read-only scenarios
    // options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
}, ServiceLifetime.Scoped);

// Override the DbContext registration to inject IServiceProvider for audit fields
builder.Services.AddScoped(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<DbContextOptions<EcomDbContext>>();
    return new EcomDbContext(options, serviceProvider);
});

// Add Identity with enhanced security settings
builder.Services.AddIdentity<AppUsers, IdentityRole>(options =>
{
    // Enhanced password requirements for security
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true; // Require special characters
    options.Password.RequiredLength = 8; // Increased minimum length
    options.Password.RequiredUniqueChars = 1;
    
    // Security: Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
    
    // Security: User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false; // Set to true if email confirmation is implemented
    options.SignIn.RequireConfirmedPhoneNumber = false;
    
    // Security: Token settings
    options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;
    options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultProvider;
    
    // UsernameValidator handles allowed characters, including Arabic names.
    options.User.AllowedUserNameCharacters = string.Empty;
})
.AddEntityFrameworkStores<EcomDbContext>()
.AddUserValidator<UsernameValidator>()
.AddDefaultTokenProviders();

// Add AutoMapper
builder.Services.AddAutoMapper(config => config.AddProfile<MappingProfile>());

// Add repositories
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// Add Unit of Work
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Ecom Badder API", Version = "v2" });
    
    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In =ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\""
    });

    c.AddSecurityRequirement(document =>
      new OpenApiSecurityRequirement
      {
          [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
      });

});
// Add services

builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IShippingService, ShippingService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<AuthSessionService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICatalogObjectStorage, CloudflareR2Service>();
builder.Services.AddScoped<CatalogImageService>();
builder.Services.AddOptions<CatalogImageOptions>().BindConfiguration("CatalogImages")
    .Validate(options => options.IsValid(), "CatalogImages limits are outside their allowed ranges.")
    .ValidateOnStart();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<ISmtpTransport, SmtpTransport>();
builder.Services.AddScoped<IUserManagerService, UserManagerService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddOptions<TelegramOptions>()
    .BindConfiguration("Telegram")
    .Validate(options => options.IsValid(), "Invalid Telegram settings. Configure BotToken and ChatId when Telegram notifications are enabled.")
    .ValidateOnStart();
builder.Services.AddHttpClient<TelegramOrderNotificationService>(client =>
    {
        client.BaseAddress = new Uri("https://api.telegram.org/");
        client.Timeout = TimeSpan.FromSeconds(60);
    })
    .RemoveAllLoggers()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<TelegramOrderNotificationProcessor>();
builder.Services.AddScoped<ISadadInvoiceGateway, SadadInvoiceGateway>();
builder.Services.AddOptions<GupshupOptions>()
    .Configure(options =>
    {
        builder.Configuration.GetSection("Gupshup").Bind(options);
        if (options.Environment == "Sandbox") builder.Configuration.GetSection("Gupshup_sandbox").Bind(options);
        // Gupshup is intentionally disabled for every environment. Telegram now
        // handles paid-order notifications, and no WhatsApp provider calls may run.
        options.Enabled = false;
    })
    .Validate(options => options.IsValid(), "Invalid Gupshup settings. Configure sender, country, webhook secret, public HTTPS URL and template/test allowlist before enabling.")
    .ValidateOnStart();
builder.Services.AddHttpClient<IGupshupWhatsAppService, GupshupWhatsAppService>(client => client.Timeout = TimeSpan.FromSeconds(30))
    .RedactLoggedHeaders(["apikey"])
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<IInvoicePdfRenderer, InvoicePdfRenderer>();
builder.Services.AddScoped<WhatsAppInvoiceProcessor>();
builder.Services.AddScoped<WhatsAppInvoicePreparationRetry>();
builder.Services.AddScoped<WhatsAppConsentService>();
builder.Services.AddScoped<PaymentOrchestrationService>();
builder.Services.AddScoped<IPaymentOrchestrationService>(sp => sp.GetRequiredService<PaymentOrchestrationService>());
builder.Services.AddScoped<IPaymentFinalizationService>(sp => sp.GetRequiredService<PaymentOrchestrationService>());
builder.Services.AddScoped<ISadadPaymentVerificationService>(sp => sp.GetRequiredService<PaymentOrchestrationService>());
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<SadadPaymentStatusCheckerService>();
    builder.Services.AddHostedService<PaymentOrchestrationWorker>();
    builder.Services.AddHostedService<TelegramOrderNotificationWorker>();
}
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IHealthService, HealthService>();
builder.Services.AddScoped<ICarouselService, CarouselService>();

if (builder.Environment.IsProduction() && builder.Configuration.GetValue("DatabaseBackup:Enabled", true))
{
    builder.Services.AddOptions<DatabaseBackupOptions>()
        .Bind(builder.Configuration.GetSection("DatabaseBackup"))
        .Validate(o => Path.IsPathFullyQualified(o.Directory), "DatabaseBackup:Directory must be an absolute path.")
        .Validate(o => o.HourUtc is >= 0 and <= 23, "DatabaseBackup:HourUtc must be between 0 and 23.")
        .Validate(o => o.RetentionCount >= 1 && o.TimeoutMinutes >= 1, "Backup retention count and timeout must be positive.")
        .ValidateOnStart();
    builder.Services.AddHostedService<PostgresBackupService>();
}

// Add Memory Cache for OTP storage
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 4096; // Limit cache size
});

// Add Response Compression for performance
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json", "application/xml", "text/plain", "text/css", "application/javascript" });
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Optimal;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Optimal;
});

// Add JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
if (!builder.Environment.IsEnvironment("Testing"))
{
    var guestDerivationKey = builder.Configuration["CheckoutSecurity:GuestTokenDerivationKey"];
    if (string.IsNullOrWhiteSpace(guestDerivationKey) || Encoding.UTF8.GetByteCount(guestDerivationKey) < 32)
        throw new InvalidOperationException("CheckoutSecurity:GuestTokenDerivationKey must be supplied by the deployment secret store and contain at least 32 bytes.");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var secretKey = jwtSettings["SecretKey"];
    if (string.IsNullOrWhiteSpace(secretKey) || Encoding.UTF8.GetByteCount(secretKey) < 32)
        throw new InvalidOperationException("JwtSettings:SecretKey must be supplied by the deployment secret store and contain at least 32 bytes.");

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey!)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.Name
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var principal = context.Principal!;
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) ||
                !Guid.TryParse(principal.FindFirstValue(JwtService.SessionClaim), out var sessionId) ||
                !await context.HttpContext.RequestServices.GetRequiredService<AuthSessionService>().IsActiveAsync(
                    userId, sessionId, principal.IsInRole("Admin"), context.HttpContext.RequestAborted))
                context.Fail("Session is not active.");
        }
    };
});

// Add Authorization with role-based policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("User", "Admin"));
});

// Add CORS with security best practices
var allowedOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() 
    ?? new[] { "https://aljawharaplus.com","http://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy.WithOrigins(allowedOrigins)
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                   .AllowCredentials();
    });
    
    // Fallback policy for development (remove in production)
    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials(); // Allow credentials for JWT tokens
        });
    }
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

// Add Rate Limiting for security
builder.Services.AddRateLimiter(options =>
{
    // Global rate limiter
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var userId = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
        var key = string.IsNullOrEmpty(userId)
            ? $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}"
            : $"user:{userId}";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100, // Requests per window
                Window = TimeSpan.FromMinutes(1) // Time window
            });
    });

    static RateLimitPartition<string> PerClient(HttpContext context, int permits, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window, AutoReplenishment = true });

    options.AddPolicy("auth-login", context => PerClient(context, 20, TimeSpan.FromMinutes(5)));
    options.AddPolicy("auth-issue", context => PerClient(context, 10, TimeSpan.FromMinutes(5)));
    options.AddPolicy("auth-verify", context => PerClient(context, 20, TimeSpan.FromMinutes(5)));
    options.AddPolicy("auth-refresh", context => PerClient(context, 40, TimeSpan.FromMinutes(1)));
    options.AddPolicy("contact", context => PerClient(context, 3, TimeSpan.FromMinutes(10)));

    // Rejection response
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Rate limit exceeded. Please try again later.", token);
    };
});

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<EcomDbContext>(name: "database");

// Add Request Timeout (if available in .NET 9)
// Note: RequestTimeouts may require additional configuration
// Alternative: Configure Kestrel server options for timeouts
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    // Increase max request body size to 50MB for file uploads
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50 MB
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<MonitoringMiddleware>();

// Configure the HTTP request pipeline.

// Security: Add security headers middleware (must be early in pipeline)
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        // Prices, stock, identity, cart preparation and payment responses must never enter a shared/browser response cache.
        context.Response.Headers.CacheControl = "no-store";
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
    }
    // Security Headers
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    
    // Content Security Policy - Note: CSP is typically handled by the frontend application
    // We only set it in production for additional security. In development, we skip it
    // to avoid conflicts with the frontend's CSP configuration.
    if (!app.Environment.IsDevelopment())
    {
        // Strict CSP for production
        context.Response.Headers.Append("Content-Security-Policy", 
            "default-src 'self'; " +
           
            "script-src 'self'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https:; " +
            "font-src 'self' data:;");
    }
    
    // Remove server header for security
    context.Response.Headers.Remove("Server");
    context.Response.Headers.Remove("X-Powered-By");
    
    // HSTS (HTTP Strict Transport Security) - only for HTTPS
    if (context.Request.IsHttps && !app.Environment.IsDevelopment())
    {
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload");
    }
    
    await next();
});

// Performance: Enable response compression (must be before other middleware)
app.UseResponseCompression();

// Security: Request timeout (configured via Kestrel options above)
// app.UseRequestTimeouts(); // Uncomment if RequestTimeouts package is added

// Security: HTTPS redirection
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Security: CORS (must be before authentication)
// Always use AllowSpecificOrigins to ensure CORS headers are sent
app.UseCors("AllowSpecificOrigins");

// Security: Swagger only in development
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ecom Badder API v2");
        c.RoutePrefix = "swagger";
        // Security: Disable Swagger UI in production
        c.DocumentTitle = "Ecom API - Development";
    });
}

app.UseAuthentication();
// Authentication supplies the stable account ID; sensitive endpoint policies still use the client IP.
app.UseRateLimiter();
app.UseAuthorization();

// Only the empty dashboard shell and assets are public. All monitoring data requires an Admin JWT.
app.UseStaticFiles();
app.MapGet("/admin/monitoring", (HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return Results.File(Path.Combine(app.Environment.WebRootPath, "monitoring", "index.html"), "text/html");
}).AllowAnonymous();

app.Lifetime.ApplicationStarted.Register(() => app.Logger.LogInformation("Application started"));
app.Lifetime.ApplicationStopping.Register(() => app.Logger.LogInformation("Application stopping"));

// Controllers use the global limit and can opt into a stricter endpoint policy.
app.MapControllers();

// Health checks endpoint
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.ToString()
            })
        });
        await context.Response.WriteAsync(result);
    }
});

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();

    var context = scope.ServiceProvider
        .GetRequiredService<EcomDbContext>();

    await context.Database.MigrateAsync();

    await DataSeeder.InitializeIdentityAsync(scope.ServiceProvider);
    //await DataSeeder.SeedAsync(scope.ServiceProvider);

  
}


await app.RunAsync();

public partial class Program;



