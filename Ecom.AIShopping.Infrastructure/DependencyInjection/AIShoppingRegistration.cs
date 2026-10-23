#pragma warning disable OPENAI001
using System.ClientModel;
using System.ClientModel.Primitives;
using Ecom.AIShopping.Abstractions;
using Ecom.AIShopping.Application;
using Ecom.AIShopping.Infrastructure.BackgroundJobs;
using Ecom.AIShopping.Infrastructure.Embeddings;
using Ecom.AIShopping.Infrastructure.OpenAI;
using Ecom.AIShopping.Infrastructure.Persistence;
using Ecom.AIShopping.Infrastructure.ProductCatalog;
using Ecom.AIShopping.Infrastructure.Retrieval;
using Ecom.AIShopping.Infrastructure.ShoppingCart;
using Ecom.AIShopping.Recommendations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;
using OpenAI.Responses;

namespace Ecom.AIShopping.Infrastructure.DependencyInjection;

public static class AIShoppingRegistration
{
    public static IServiceCollection AddAIShopping(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AIShoppingOptions>().Bind(configuration.GetSection("AIShopping"))
            .Validate(o => o.IsValid(), "Invalid AIShopping settings; configure limits, provider credential and model before enabling.")
            .ValidateOnStart();
        services.AddScoped(sp => sp.GetRequiredService<IOptions<AIShoppingOptions>>().Value.Limits);
        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<AIShoppingDbContext>(o => o.UseNpgsql(
            configuration.GetConnectionString("DefaultConnection"), npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "ai_shopping")));
        services.AddSingleton(sp =>
        {
            var o = sp.GetRequiredService<IOptions<AIShoppingOptions>>().Value;
            return new ResponsesClient(new ApiKeyCredential(o.ApiKey ?? throw new InvalidOperationException("AIShopping:ApiKey missing")),
                new ResponsesClientOptions { NetworkTimeout = TimeSpan.FromSeconds(o.TimeoutSeconds),
                    RetryPolicy = new ClientRetryPolicy(0), EnableDistributedTracing = false });
        });
        services.AddSingleton(sp =>
        {
            var o = sp.GetRequiredService<IOptions<AIShoppingOptions>>().Value;
            return new EmbeddingClient(o.EmbeddingModel, new ApiKeyCredential(o.ApiKey ?? throw new InvalidOperationException("AIShopping:ApiKey missing")),
                new global::OpenAI.OpenAIClientOptions { NetworkTimeout = TimeSpan.FromSeconds(o.TimeoutSeconds),
                    RetryPolicy = new ClientRetryPolicy(0), EnableDistributedTracing = false });
        });
        services.AddScoped<IEmbeddingService>(sp => sp.GetRequiredService<IOptions<AIShoppingOptions>>().Value.SemanticEnabled
            ? ActivatorUtilities.CreateInstance<OpenAIEmbeddingService>(sp) : new DisabledEmbeddingService());
        services.AddScoped<IProductCatalogReader, CommerceCatalogReader>();
        services.AddScoped<IProductRetrievalService, PostgresProductRetrieval>();
        services.AddScoped<IShoppingPlanner, OpenAIShoppingPlanner>();
        services.AddScoped<ShoppingToolExecutor>();
        services.AddScoped<IShoppingConversationStore, PostgresConversationStore>();
        services.TryAddScoped<IShoppingCartGateway, UnconfiguredShoppingCartGateway>();
        services.AddScoped<IShoppingConfirmationService, ShoppingConfirmationService>();
        services.AddScoped<ShoppingRecommendationService>();
        services.AddScoped<AIShoppingAgent>();
        services.AddScoped<BrowserCartPreparationService>();
        services.AddScoped<ShoppingUsageLedger>();
        services.AddScoped<ShoppingSearchIndexService>();
        services.AddHostedService<ShoppingIndexWorker>();
        return services;
    }
}
