using Ecom.AIShopping.Contracts;

namespace Ecom.AIShopping.Abstractions;

public interface IShoppingPlanner
{
    Task<PlanningResult> PlanAsync(ShoppingConversation? previous, string message, CancellationToken cancellationToken);
}

public interface IProductCatalogReader
{
    Task<IReadOnlyList<CatalogProduct>> GetAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken);
}

public interface IProductRetrievalService
{
    Task<IReadOnlyList<CatalogProduct>> SearchAsync(string query, string? material, int limit, CancellationToken cancellationToken);
}

public interface IShoppingConversationStore
{
    Task<ShoppingConversation?> FindAsync(Guid id, string ownerHash, CancellationToken cancellationToken);
    Task<bool> SaveAsync(ShoppingConversation conversation, int expectedVersion, CancellationToken cancellationToken);
}

/// <summary>
/// Implement with the REAL cart service. Must bind ownerHash to the existing cart identity, check stock/rules,
/// and atomically persist operationId and the mutation. Replay returns the identical result even after a crash.
/// No order/payment operation belongs here. A browser-only cart needs its actual frontend adapter first.
/// </summary>
public interface IShoppingCartGateway
{
    Task<CartConfirmation> AddApprovedAsync(string ownerHash, Guid operationId,
        IReadOnlyList<CartLine> lines, CancellationToken cancellationToken);
}

public interface IShoppingConfirmationService
{
    Task<CartConfirmation> ConfirmAsync(Guid conversationId, Guid recommendationId, string ownerHash,
        ConfirmRequest request, CancellationToken cancellationToken);
}

public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken);
}
