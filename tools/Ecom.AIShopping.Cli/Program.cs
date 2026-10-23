using Ecom.AIShopping.Infrastructure.BackgroundJobs;
using Ecom.AIShopping.Infrastructure.DependencyInjection;
using Ecom.AIShopping.Infrastructure.Persistence;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

if (args.Length != 1 || args[0] is not ("index" or "reindex" or "status"))
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/Ecom.AIShopping.Cli -- index|reindex|status. Indexing may incur embedding charges only when AIShopping__SemanticEnabled=true.");
    return 2;
}
var builder = Host.CreateApplicationBuilder();
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddDbContext<EcomDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddMemoryCache(o => o.SizeLimit = 4096);
builder.Services.AddAIShopping(builder.Configuration);
using var host = builder.Build();
await using var scope = host.Services.CreateAsyncScope();
using var stopping = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };
var db = scope.ServiceProvider.GetRequiredService<AIShoppingDbContext>();
if (args[0] == "status")
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
    {
        documents = await db.SearchDocuments.CountAsync(stopping.Token),
        indexed = await db.SearchDocuments.CountAsync(d => d.IndexedAtUtc != null, stopping.Token),
        failed = await db.SearchDocuments.CountAsync(d => d.LastErrorCode != null, stopping.Token),
        checkpoint = await db.IndexCheckpoints.AsNoTracking().SingleOrDefaultAsync(d => d.Id == 1, stopping.Token)
    }));
    return 0;
}
var index = scope.ServiceProvider.GetRequiredService<ShoppingSearchIndexService>();
var reset = args[0] == "reindex";
do
{
    var result = await index.RunBatchAsync(reset, stopping.Token);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));
    reset = false;
    if (result.SweepCompleted || result.Scanned == 0) break;
} while (!stopping.IsCancellationRequested);
return 0;
