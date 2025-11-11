using ApiResellerSystem.Data;
using ApiResellerSystem.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SimpleApiReseller.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CreditController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CreditController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ------------------------------------------------------------
        // 1️⃣  GET /api/credit/my-balance
        // ------------------------------------------------------------
        [HttpGet("my-balance")]
        public async Task<ActionResult<ClientCreditDto>> GetMyBalance()
        {
            var currentClientId = int.Parse(HttpContext.User.FindFirst("ClientId")?.Value ?? "0");

            if (currentClientId == 0)
            {
                return BadRequest(new { message = "Invalid client" });
            }

            try
            {
                var client = await _context.Clients
                    .FirstOrDefaultAsync(c => c.Id == currentClientId);

                if (client == null)
                {
                    return NotFound(new { message = "Client not found" });
                }

                var recentTransactions = await _context.ClientCredits
                    .Where(cc => cc.ClientId == currentClientId)
                    .OrderByDescending(cc => cc.CreatedAt)
                    .Take(10)
                    .Select(cc => new ClientCreditTransactionDto
                    {
                        Amount = cc.Amount,
                        TransactionType = cc.TransactionType,
                        Description = cc.Description,
                        CreatedAt = cc.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new ClientCreditDto
                {
                    CreditBalance = client.CreditBalance,
                    RecentTransactions = recentTransactions
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving balance", error = ex.Message });
            }
        }

        // ------------------------------------------------------------
        // 2️⃣  GET /api/credit/my-transactions
        // ------------------------------------------------------------
        [HttpGet("my-transactions")]
        public async Task<ActionResult<IEnumerable<ClientCreditTransactionDto>>> GetMyTransactions(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? transactionType = null)
        {
            var currentClientId = int.Parse(HttpContext.User.FindFirst("ClientId")?.Value ?? "0");
            if (currentClientId == 0)
                return BadRequest(new { message = "Invalid client" });

            var query = _context.ClientCredits.Where(cc => cc.ClientId == currentClientId);

            if (!string.IsNullOrEmpty(transactionType))
                query = query.Where(cc => cc.TransactionType == transactionType);

            var transactions = await query
                .OrderByDescending(cc => cc.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(cc => new ClientCreditTransactionDto
                {
                    Amount = cc.Amount,
                    TransactionType = cc.TransactionType,
                    Description = cc.Description,
                    CreatedAt = cc.CreatedAt
                })
                .ToListAsync();

            var totalCount = await query.CountAsync();

            return Ok(new
            {
                transactions,
                pagination = new
                {
                    currentPage = page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                }
            });
        }

        // ------------------------------------------------------------
        // 3️⃣  GET /api/credit/statistics
        // ------------------------------------------------------------
        [HttpGet("statistics")]
        public async Task<ActionResult<IEnumerable<ClientStatsDto>>> GetStatistics([FromQuery] int days = 30)
        {
            var currentClientId = int.Parse(HttpContext.User.FindFirst("ClientId")?.Value ?? "0");
            if (currentClientId == 0)
                return BadRequest(new { message = "Invalid client" });

            var startDate = DateTime.Now.AddDays(-days);

            var stats = await _context.ApiRequests
                .Where(ar => ar.ClientId == currentClientId && ar.CreatedAt >= startDate)
                .GroupBy(ar => ar.CreatedAt.Date)
                .Select(g => new ClientStatsDto
                {
                    RequestDate = g.Key,
                    TotalRequests = g.Count(),
                    AvgResponseTime = g.Average(ar => ar.ProcessingTimeMs ?? 0),
                    SuccessCount = g.Count(ar => ar.ResponseCode >= 200 && ar.ResponseCode < 300),
                    ErrorCount = g.Count(ar => ar.ResponseCode >= 400)
                })
                .OrderByDescending(s => s.RequestDate)
                .ToListAsync();

            var totalRequests = await _context.ApiRequests
                .Where(ar => ar.ClientId == currentClientId && ar.CreatedAt >= startDate)
                .CountAsync();

            var totalSuccessful = await _context.ApiRequests
                .Where(ar => ar.ClientId == currentClientId && ar.CreatedAt >= startDate &&
                       ar.ResponseCode >= 200 && ar.ResponseCode < 300)
                .CountAsync();

            var averageResponseTime = await _context.ApiRequests
                .Where(ar => ar.ClientId == currentClientId && ar.CreatedAt >= startDate &&
                       ar.ProcessingTimeMs.HasValue)
                .AverageAsync(ar => (double?)ar.ProcessingTimeMs) ?? 0;

            var totalCreditsSpent = await _context.ClientCredits
                .Where(cc => cc.ClientId == currentClientId &&
                       cc.CreatedAt >= startDate &&
                       cc.TransactionType == "Usage")
                .SumAsync(cc => Math.Abs(cc.Amount));

            return Ok(new
            {
                dailyStats = stats,
                summary = new
                {
                    totalRequests,
                    totalSuccessful,
                    successRate = totalRequests > 0 ? (double)totalSuccessful / totalRequests * 100 : 0,
                    averageResponseTime,
                    totalCreditsSpent,
                    period = $"Last {days} days"
                }
            });
        }

        // ------------------------------------------------------------
        // 4️⃣  GET /api/credit/usage-summary
        // ------------------------------------------------------------
        [HttpGet("usage-summary")]
        public async Task<ActionResult<object>> GetUsageSummary()
        {
            var currentClientId = int.Parse(HttpContext.User.FindFirst("ClientId")?.Value ?? "0");
            if (currentClientId == 0)
                return BadRequest(new { message = "Invalid client" });

            var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == currentClientId);
            if (client == null)
                return NotFound(new { message = "Client not found" });

            var today = DateTime.Now.Date;
            var todayRequests = await _context.ApiRequests
                .Where(ar => ar.ClientId == currentClientId && ar.CreatedAt.Date == today)
                .CountAsync();
            var todayCreditsUsed = await _context.ClientCredits
                .Where(cc => cc.ClientId == currentClientId && cc.CreatedAt.Date == today && cc.TransactionType == "Usage")
                .SumAsync(cc => Math.Abs(cc.Amount));

            var thisMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var monthlyRequests = await _context.ApiRequests
                .Where(ar => ar.ClientId == currentClientId && ar.CreatedAt >= thisMonth)
                .CountAsync();
            var monthlyCreditsUsed = await _context.ClientCredits
                .Where(cc => cc.ClientId == currentClientId && cc.CreatedAt >= thisMonth && cc.TransactionType == "Usage")
                .SumAsync(cc => Math.Abs(cc.Amount));

            var totalRequests = await _context.ApiRequests
                .Where(ar => ar.ClientId == currentClientId)
                .CountAsync();
            var totalCreditsUsed = await _context.ClientCredits
                .Where(cc => cc.ClientId == currentClientId && cc.TransactionType == "Usage")
                .SumAsync(cc => Math.Abs(cc.Amount));

            return Ok(new
            {
                currentBalance = client.CreditBalance,
                today = new
                {
                    requests = todayRequests,
                    creditsUsed = todayCreditsUsed
                },
                thisMonth = new
                {
                    requests = monthlyRequests,
                    creditsUsed = monthlyCreditsUsed
                },
                lifetime = new
                {
                    requests = totalRequests,
                    creditsUsed = totalCreditsUsed
                }
            });
        }
    }
}
