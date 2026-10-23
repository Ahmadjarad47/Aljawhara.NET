using System.Text.Json;
using Ecom.AIShopping.Abstractions;
using Ecom.AIShopping.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecom.AIShopping.Infrastructure.Persistence;

public sealed class PostgresConversationStore(AIShoppingDbContext db) : IShoppingConversationStore
{
    public async Task<ShoppingConversation?> FindAsync(Guid id, string ownerHash, CancellationToken ct)
    {
        var payload = await db.Conversations.AsNoTracking().Where(x => x.Id == id && x.OwnerHash == ownerHash)
            .Select(x => x.Payload).SingleOrDefaultAsync(ct);
        return payload is null ? null : JsonSerializer.Deserialize<ShoppingConversation>(payload, ShoppingJson.Options);
    }

    public async Task<bool> SaveAsync(ShoppingConversation conversation, int expectedVersion, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(conversation, ShoppingJson.Options);
        if (expectedVersion > 0)
            return await db.Conversations.Where(x => x.Id == conversation.Id && x.OwnerHash == conversation.OwnerHash &&
                    x.Version == expectedVersion && x.ExpiresAtUtc > DateTime.UtcNow)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Payload, json).SetProperty(x => x.Version, conversation.Version), ct) == 1;
        db.Conversations.Add(new() { Id = conversation.Id, OwnerHash = conversation.OwnerHash,
            Version = conversation.Version, Payload = json, ExpiresAtUtc = conversation.ExpiresAtUtc });
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { db.ChangeTracker.Clear(); return false; }
    }
}
