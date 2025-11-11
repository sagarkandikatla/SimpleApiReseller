using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
 
using SimpleApiReseller.Models;
using ApiResellerSystem.Data;
using ApiResellerSystem.DTOs;
using ApiResellerSystem.Models;

namespace SimpleApiReseller.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ClientsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ClientsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<ClientResponseDto>>> GetClients()
        {
            var clients = await _context.Clients
                .Include(c => c.User)
                .Select(c => new ClientResponseDto
                {
                    Id = c.Id,
                    Username = c.User.Username,
                    Email = c.User.Email,
                    CompanyName = c.CompanyName,
                    ContactPerson = c.ContactPerson,
                    Phone = c.Phone,
                    ApiKey = c.ApiKey,
                    CreditBalance = c.CreditBalance, // ADDED
                    IsActive = c.User.IsActive,
                    CreatedAt = c.CreatedAt
                })
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return Ok(clients);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<object>> CreateClient(CreateClientDto createClientDto)
        {
            // Check if username or email already exists
            if (await _context.Users.AnyAsync(u => u.Username == createClientDto.Username || u.Email == createClientDto.Email))
            {
                return BadRequest(new { message = "Username or email already exists" });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Generate random password for client
                var generatedPassword = GenerateRandomPassword();

                // Create user
                var user = new User
                {
                    Username = createClientDto.Username,
                    Email = createClientDto.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(generatedPassword),
                    Role = UserRole.Client,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Create client with initial credit balance
                var client = new Client
                {
                    UserId = user.Id,
                    CompanyName = createClientDto.CompanyName,
                    ContactPerson = createClientDto.ContactPerson,
                    Phone = createClientDto.Phone,
                    Address = createClientDto.Address,
                    ApiKey = GenerateApiKey(),
                    ApiSecret = GenerateApiSecret(),
                    CreditBalance = createClientDto.InitialCredits ?? 0, // ADDED
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Clients.Add(client);
                await _context.SaveChangesAsync();

                // Log initial credit if provided
                if (createClientDto.InitialCredits > 0)
                {
                    var creditTransaction = new ClientCredit
                    {
                        ClientId = client.Id,
                        Amount = createClientDto.InitialCredits.Value,
                        TransactionType = "Recharge",
                        Description = "Initial credit allocation",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.ClientCredits.Add(creditTransaction);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                return CreatedAtAction(nameof(GetClient), new { id = client.Id }, new
                {
                    client = new ClientResponseDto
                    {
                        Id = client.Id,
                        Username = user.Username,
                        Email = user.Email,
                        CompanyName = client.CompanyName,
                        ContactPerson = client.ContactPerson,
                        Phone = client.Phone,
                        ApiKey = client.ApiKey,
                        CreditBalance = client.CreditBalance,
                        IsActive = user.IsActive,
                        CreatedAt = client.CreatedAt
                    },
                    credentials = new
                    {
                        username = user.Username,
                        password = generatedPassword, // Return generated password
                        apiKey = client.ApiKey,
                        apiSecret = client.ApiSecret
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Error creating client", error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ClientResponseDto>> GetClient(int id)
        {
            var currentRole = HttpContext.User.FindFirst("Role")?.Value;
            var currentClientId = int.Parse(HttpContext.User.FindFirst("ClientId")?.Value ?? "0");

            // Admin can view any client, clients can only view their own data
            if (currentRole != "Admin" && currentClientId != id)
            {
                return Forbid();
            }

            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
            {
                return NotFound(new { message = "Client not found" });
            }

            return Ok(new ClientResponseDto
            {
                Id = client.Id,
                Username = client.User.Username,
                Email = client.User.Email,
                CompanyName = client.CompanyName,
                ContactPerson = client.ContactPerson,
                Phone = client.Phone,
                ApiKey = client.ApiKey,
                CreditBalance = client.CreditBalance, // ADDED
                IsActive = client.User.IsActive,
                CreatedAt = client.CreatedAt
            });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateClient(int id, UpdateClientDto updateClientDto)
        {
            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
            {
                return NotFound(new { message = "Client not found" });
            }

            // Check if new email is already taken by another user
            if (!string.IsNullOrEmpty(updateClientDto.Email) &&
                updateClientDto.Email != client.User.Email &&
                await _context.Users.AnyAsync(u => u.Email == updateClientDto.Email && u.Id != client.UserId))
            {
                return BadRequest(new { message = "Email already exists" });
            }

            try
            {
                // Update user information
                if (!string.IsNullOrEmpty(updateClientDto.Email))
                    client.User.Email = updateClientDto.Email;

                // Remove password update logic - admin resets password separately
                client.User.UpdatedAt = DateTime.UtcNow;

                // Update client information
                if (!string.IsNullOrEmpty(updateClientDto.CompanyName))
                    client.CompanyName = updateClientDto.CompanyName;

                if (!string.IsNullOrEmpty(updateClientDto.ContactPerson))
                    client.ContactPerson = updateClientDto.ContactPerson;

                if (!string.IsNullOrEmpty(updateClientDto.Phone))
                    client.Phone = updateClientDto.Phone;

                if (!string.IsNullOrEmpty(updateClientDto.Address))
                    client.Address = updateClientDto.Address;

                client.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Client updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating client", error = ex.Message });
            }
        }

        [HttpPut("{id}/toggle-status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleClientStatus(int id)
        {
            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
            {
                return NotFound(new { message = "Client not found" });
            }

            try
            {
                client.User.IsActive = !client.User.IsActive;
                client.User.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"Client {(client.User.IsActive ? "activated" : "deactivated")} successfully",
                    isActive = client.User.IsActive
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating client status", error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteClient(int id)
        {
            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
            {
                return NotFound(new { message = "Client not found" });
            }

            try
            {
                // Check if client has any credit transactions or API requests
                var hasCreditTransactions = await _context.ClientCredits
                    .AnyAsync(cc => cc.ClientId == id);

                var hasApiRequests = await _context.ApiRequests
                    .AnyAsync(ar => ar.ClientId == id);

                if (hasCreditTransactions || hasApiRequests)
                {
                    return BadRequest(new
                    {
                        message = "Cannot delete client with credit history or API request history. Deactivate the client instead."
                    });
                }

                _context.Clients.Remove(client);
                _context.Users.Remove(client.User);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Client deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error deleting client", error = ex.Message });
            }
        }

        [HttpPost("{id}/regenerate-api-key")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<object>> RegenerateApiKey(int id)
        {
            var client = await _context.Clients.FindAsync(id);

            if (client == null)
            {
                return NotFound(new { message = "Client not found" });
            }

            try
            {
                var oldApiKey = client.ApiKey;
                client.ApiKey = GenerateApiKey();
                client.ApiSecret = GenerateApiSecret();
                client.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "API key regenerated successfully",
                    oldApiKey = oldApiKey,
                    newApiKey = client.ApiKey,
                    newApiSecret = client.ApiSecret
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error regenerating API key", error = ex.Message });
            }
        }

        [HttpPost("{id}/reset-password")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<object>> ResetPassword(int id)
        {
            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
            {
                return NotFound(new { message = "Client not found" });
            }

            try
            {
                var newPassword = GenerateRandomPassword();
                client.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                client.User.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Password reset successfully",
                    newPassword = newPassword
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error resetting password", error = ex.Message });
            }
        }

        // NEW: Recharge client credits
        [HttpPost("{id}/recharge")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RechargeCredits(int id, [FromBody] RechargeCreditsDto rechargeDto)
        {
            var client = await _context.Clients.FindAsync(id);

            if (client == null)
            {
                return NotFound(new { message = "Client not found" });
            }

            if (rechargeDto.Amount <= 0)
            {
                return BadRequest(new { message = "Amount must be greater than zero" });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Update client balance
                client.CreditBalance += rechargeDto.Amount;
                client.UpdatedAt = DateTime.UtcNow;

                // Log credit transaction
                var creditTransaction = new ClientCredit
                {
                    ClientId = id,
                    Amount = rechargeDto.Amount,
                    TransactionType = "Recharge",
                    Description = rechargeDto.Description ?? "Manual credit recharge by admin",
                    CreatedAt = DateTime.UtcNow
                };

                _context.ClientCredits.Add(creditTransaction);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Credits recharged successfully",
                    newBalance = client.CreditBalance,
                    amountAdded = rechargeDto.Amount
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Error recharging credits", error = ex.Message });
            }
        }

        // NEW: Get client credit transactions
        [HttpGet("{id}/credits")]
        [Authorize]
        public async Task<ActionResult<object>> GetClientCredits(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var currentRole = HttpContext.User.FindFirst("Role")?.Value;
            var currentClientId = int.Parse(HttpContext.User.FindFirst("ClientId")?.Value ?? "0");

            // Admin can view any client's credits, clients can only view their own
            if (currentRole != "Admin" && currentClientId != id)
            {
                return Forbid();
            }

            var client = await _context.Clients.FindAsync(id);
            if (client == null)
            {
                return NotFound(new { message = "Client not found" });
            }

            var transactions = await _context.ClientCredits
                .Where(cc => cc.ClientId == id)
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

            var totalCount = await _context.ClientCredits.CountAsync(cc => cc.ClientId == id);

            return Ok(new
            {
                currentBalance = client.CreditBalance,
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

        [HttpGet("{id}/statistics")]
        [Authorize]
        public async Task<ActionResult<object>> GetClientStatistics(int id, [FromQuery] int days = 30)
        {
            var currentRole = HttpContext.User.FindFirst("Role")?.Value;
            var currentClientId = int.Parse(HttpContext.User.FindFirst("ClientId")?.Value ?? "0");

            // Admin can view any client's stats, clients can only view their own
            if (currentRole != "Admin" && currentClientId != id)
            {
                return Forbid();
            }

            var startDate = DateTime.Now.AddDays(-days);

            var stats = await _context.ApiRequests
                .Where(ar => ar.ClientId == id && ar.CreatedAt >= startDate)
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
                .Where(ar => ar.ClientId == id && ar.CreatedAt >= startDate)
                .CountAsync();

            var totalSuccessful = await _context.ApiRequests
                .Where(ar => ar.ClientId == id && ar.CreatedAt >= startDate && ar.ResponseCode >= 200 && ar.ResponseCode < 300)
                .CountAsync();

            var averageResponseTime = await _context.ApiRequests
                .Where(ar => ar.ClientId == id && ar.CreatedAt >= startDate && ar.ProcessingTimeMs.HasValue)
                .AverageAsync(ar => (double?)ar.ProcessingTimeMs) ?? 0;

            var totalCreditsSpent = await _context.ClientCredits
                .Where(cc => cc.ClientId == id && cc.CreatedAt >= startDate && cc.TransactionType == "Usage")
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

        // NEW: System settings management
        [HttpPost("system/request-cost")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetRequestCost([FromBody] SetRequestCostDto dto)
        {
            if (dto.RequestCost < 0)
            {
                return BadRequest(new { message = "Request cost cannot be negative" });
            }

            try
            {
                var setting = await _context.SystemSettings
                    .FirstOrDefaultAsync(s => s.SettingKey == "REQUEST_COST");

                if (setting == null)
                {
                    setting = new SystemSettings
                    {
                        SettingKey = "REQUEST_COST",
                        SettingValue = dto.RequestCost.ToString("F4"),
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.SystemSettings.Add(setting);
                }
                else
                {
                    setting.SettingValue = dto.RequestCost.ToString("F4");
                    setting.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Request cost updated successfully",
                    newCost = dto.RequestCost
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating request cost", error = ex.Message });
            }
        }

        [HttpGet("system/settings")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<object>> GetSystemSettings()
        {
            try
            {
                var settings = await _context.SystemSettings.ToListAsync();

                var requestCostSetting = settings.FirstOrDefault(s => s.SettingKey == "REQUEST_COST");
                var requestCost = decimal.TryParse(requestCostSetting?.SettingValue, out var cost) ? cost : 0.01m;

                return Ok(new
                {
                    requestCost,
                    lastUpdated = requestCostSetting?.UpdatedAt,
                    allSettings = settings.Select(s => new
                    {
                        key = s.SettingKey,
                        value = s.SettingValue,
                        updatedAt = s.UpdatedAt
                    })
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving system settings", error = ex.Message });
            }
        }

        // Helper methods
        private string GenerateApiKey()
        {
            return $"ak_{Guid.NewGuid():N}";
        }

        private string GenerateApiSecret()
        {
            return $"as_{Guid.NewGuid():N}";
        }

        private string GenerateRandomPassword()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 12)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}