using Ecom.Domain.Entity;
using Microsoft.EntityFrameworkCore;

namespace Ecom.Infrastructure.Repositories.Interfaces
{
    public interface ITransactionRepository : IBaseRepository<Transaction>
    {
        Task<Transaction?> GetTransactionWithDetailsAsync(int id);
        Task<Transaction?> GetTransactionByReferenceAsync(string reference);
        Task<Transaction?> GetLatestTransactionByOrderAsync(int orderId);
        Task<bool> HasSuccessfulTransactionAsync(int orderId);
        Task<IEnumerable<Transaction>> GetTransactionsByOrderAsync(int orderId);
        Task<IEnumerable<Transaction>> GetTransactionsByStatusAsync(string status, int pageNumber = 1, int pageSize = 20);
        Task<IEnumerable<Transaction>> GetTransactionsByPaymentMethodAsync(Domain.constant.PaymentMethod paymentMethod, int pageNumber = 1, int pageSize = 20);
        Task<IEnumerable<Transaction>> SearchTransactionsAsync(string searchTerm, int limit = 50);
        IQueryable<Transaction> GetTransactionsQuery();
    }
}
