using Ecom.Domain.Entity;
using Ecom.Domain.constant;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ecom.Infrastructure.Repositories
{
    public class TransactionRepository : BaseRepository<Transaction>, ITransactionRepository
    {
        public TransactionRepository(EcomDbContext context) : base(context)
        {
        }

        public async Task<Transaction?> GetTransactionWithDetailsAsync(int id)
        {
            return await _context.Transactions
                .Include(t => t.Order)
                    .ThenInclude(o => o.AppUser)
                .Include(t => t.Order)
                    .ThenInclude(o => o.ShippingAddress)
                .Include(t => t.AppUser)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<Transaction?> GetTransactionByReferenceAsync(string reference)
        {
            return await _context.Transactions
                .Include(t => t.Order)
                    .ThenInclude(o => o.AppUser)
                .Include(t => t.Order)
                    .ThenInclude(o => o.ShippingAddress)
                .Include(t => t.AppUser)
                .FirstOrDefaultAsync(t => t.TransactionReference == reference);
        }

        public async Task<Transaction?> GetLatestTransactionByOrderAsync(int orderId)
        {
            return await _context.Transactions
                .Include(t => t.Order)
                    .ThenInclude(o => o.AppUser)
                .Include(t => t.Order)
                    .ThenInclude(o => o.ShippingAddress)
                .Include(t => t.AppUser)
                .Where(t => t.OrderId == orderId)
                .OrderByDescending(t => t.TransactionDate)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> HasSuccessfulTransactionAsync(int orderId)
        {
            return await _context.Transactions
                .AnyAsync(t => t.OrderId == orderId && t.Status == "Completed");
        }

        public async Task<IEnumerable<Transaction>> GetTransactionsByOrderAsync(int orderId)
        {
            return await _context.Transactions
                .Include(t => t.Order)
                    .ThenInclude(o => o.AppUser)
                .Include(t => t.Order)
                    .ThenInclude(o => o.ShippingAddress)
                .Include(t => t.AppUser)
                .Where(t => t.OrderId == orderId)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Transaction>> GetTransactionsByStatusAsync(string status, int pageNumber = 1, int pageSize = 20)
        {
            return await _context.Transactions
                .Include(t => t.Order)
                    .ThenInclude(o => o.AppUser)
                .Include(t => t.Order)
                    .ThenInclude(o => o.ShippingAddress)
                .Include(t => t.AppUser)
                .Where(t => t.Status == status)
                .OrderByDescending(t => t.TransactionDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Transaction>> GetTransactionsByPaymentMethodAsync(PaymentMethod paymentMethod, int pageNumber = 1, int pageSize = 20)
        {
            return await _context.Transactions
                .Include(t => t.Order)
                    .ThenInclude(o => o.AppUser)
                .Include(t => t.Order)
                    .ThenInclude(o => o.ShippingAddress)
                .Include(t => t.AppUser)
                .Where(t => t.PaymentMethod == paymentMethod)
                .OrderByDescending(t => t.TransactionDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Transaction>> SearchTransactionsAsync(string searchTerm, int limit = 50)
        {
            return await _context.Transactions
                .Include(t => t.Order)
                    .ThenInclude(o => o.AppUser)
                .Include(t => t.AppUser)
                .Include(t => t.Order)
                    .ThenInclude(o => o.ShippingAddress)
                .Where(t => 
                    t.TransactionReference!.Contains(searchTerm) ||
                    t.Order!.OrderNumber.Contains(searchTerm) ||
                    (t.AppUser != null && (t.AppUser.UserName!.Contains(searchTerm) || t.AppUser.Email!.Contains(searchTerm))) ||
                    (t.Order!.ShippingAddress != null && t.Order.ShippingAddress.FullName.Contains(searchTerm)) ||
                    t.Status.Contains(searchTerm) ||
                    t.PaymentMethod.ToString().Contains(searchTerm))
                .OrderByDescending(t => t.TransactionDate)
                .Take(limit)
                .ToListAsync();
        }

        public IQueryable<Transaction> GetTransactionsQuery()
        {
            return _context.Transactions
                .Include(t => t.Order)
                    .ThenInclude(o => o.AppUser)
                .Include(t => t.Order)
                    .ThenInclude(o => o.ShippingAddress)
                .Include(t => t.AppUser);
        }
    }
}
