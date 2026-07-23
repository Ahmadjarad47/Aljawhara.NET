using Ecom.API.Controllers.Extensions;
using Ecom.Application.DTOs.Common;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TransactionsController : ControllerBase
    {
        private readonly ITransactionService _transactionService;

        public TransactionsController(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        /// <summary>
        /// Get current user's transactions with filtering and pagination
        /// </summary>
        [HttpGet("my")]
        public async Task<ActionResult<PagedResult<TransactionAdvancedDto>>> GetMyTransactions([FromQuery] TransactionFilterDto filter)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId.ToString()))
            {
                return Unauthorized();
            }

            // Enforce current user
            filter.AppUserId = userId.ToString();

            try
            {
                var result = await _transactionService.GetTransactionsAsync(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving user transactions: {ex.Message}");
            }
        }
    }
}


