using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
 
using ApiResellerSystem.Data;
using ApiResellerSystem.Models;

namespace SimpleApiReseller.Controllers
{
    [ApiController]
    [Route("api/proxy")]
    public class ApiProxyController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public ApiProxyController(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [HttpGet("{**path}")]
        [HttpPost("{**path}")]
        [HttpPut("{**path}")]
        [HttpDelete("{**path}")]
        [HttpPatch("{**path}")]
        public async Task<IActionResult> ProxyRequest(string path)
        {
            var apiKey = Request.Headers["X-API-Key"].FirstOrDefault();

            if (string.IsNullOrEmpty(apiKey))
            {
                return Unauthorized(new { error = "API key required" });
            }

            var stopwatch = Stopwatch.StartNew();

            // Check credits and get client
            var (clientId, canProceed, remainingCredits) = await CheckCreditAsync(apiKey);

            if (!canProceed)
            {
                stopwatch.Stop();
                await LogRequestAsync(clientId, path, Request.Method, null, 429, "Insufficient credits", stopwatch.ElapsedMilliseconds);
                return StatusCode(429, new { error = "Insufficient credits", remaining = remainingCredits });
            }

            try
            {
                // Get request body before forwarding (since stream can only be read once)
                string requestBody = null;
                if (Request.ContentLength > 0)
                {
                    requestBody = await GetRequestBodyAsync();
                }

                // Forward request to actual API
                var response = await ForwardRequestAsync(path, requestBody);
                var responseContent = await response.Content.ReadAsStringAsync();

                stopwatch.Stop();

                // Log the request
                await LogRequestAsync(clientId, path, Request.Method, requestBody, (int)response.StatusCode, responseContent, stopwatch.ElapsedMilliseconds);

                // Return response with proper content type
                var result = new ContentResult
                {
                    Content = responseContent,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                    StatusCode = (int)response.StatusCode
                };

                return result;
            }
            catch (HttpRequestException httpEx)
            {
                stopwatch.Stop();
                var requestBody = Request.ContentLength > 0 ? await GetRequestBodyAsync() : null;
                await LogRequestAsync(clientId, path, Request.Method, requestBody, 502, $"Target API error: {httpEx.Message}", stopwatch.ElapsedMilliseconds);
                return StatusCode(502, new { error = "Target API unavailable", details = httpEx.Message });
            }
            catch (TaskCanceledException tcEx)
            {
                stopwatch.Stop();
                var requestBody = Request.ContentLength > 0 ? await GetRequestBodyAsync() : null;
                await LogRequestAsync(clientId, path, Request.Method, requestBody, 408, $"Request timeout: {tcEx.Message}", stopwatch.ElapsedMilliseconds);
                return StatusCode(408, new { error = "Request timeout" });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var requestBody = Request.ContentLength > 0 ? await GetRequestBodyAsync() : null;
                await LogRequestAsync(clientId, path, Request.Method, requestBody, 500, ex.Message, stopwatch.ElapsedMilliseconds);
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        // MODIFIED: Credit-based checking instead of quota-based
        private async Task<(int clientId, bool canProceed, decimal remainingCredits)> CheckCreditAsync(string apiKey)
        {
            try
            {
                var client = await _context.Clients
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.ApiKey == apiKey && c.User.IsActive);

                if (client == null)
                {
                    return (0, false, 0);
                }

                // Get per-request cost from system settings
                var requestCost = await GetRequestCostAsync();

                // Check if client has sufficient credits
                if (client.CreditBalance < requestCost)
                {
                    return (client.Id, false, client.CreditBalance);
                }

                // Deduct credits and log transaction
                await DeductCreditAsync(client.Id, requestCost);

                // Return updated balance
                var remainingCredits = client.CreditBalance - requestCost;
                return (client.Id, true, remainingCredits);
            }
            catch (Exception ex)
            {
                // Log the error but don't expose internal details
                Console.WriteLine($"Error checking credits: {ex.Message}");
                return (0, false, 0);
            }
        }

        // NEW: Get per-request cost from system settings
        private async Task<decimal> GetRequestCostAsync()
        {
            try
            {
                var setting = await _context.SystemSettings
                    .FirstOrDefaultAsync(s => s.SettingKey == "REQUEST_COST");

                if (setting != null && decimal.TryParse(setting.SettingValue, out decimal cost))
                {
                    return cost;
                }

                // Default cost if setting not found
                return 1.00m; // Default: 1 cent per request
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting request cost: {ex.Message}");
                return 1.00m; // Fallback to default
            }
        }

        // NEW: Deduct credits from client and log transaction
        private async Task DeductCreditAsync(int clientId, decimal amount)
        {
            try
            {
                // Update client balance
                var client = await _context.Clients.FindAsync(clientId);
                if (client != null)
                {
                    client.CreditBalance -= amount;
                    client.UpdatedAt = DateTime.UtcNow;

                    // Log credit transaction
                    var creditTransaction = new ClientCredit
                    {
                        ClientId = clientId,
                        Amount = -amount, // Negative for usage
                        TransactionType = "Usage",
                        Description = "API request deduction",
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.ClientCredits.Add(creditTransaction);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deducting credits: {ex.Message}");
                // Don't throw exception to avoid breaking the API flow
                // Log the error for monitoring
            }
        }

        // UNCHANGED: Forward request to target API
        private async Task<HttpResponseMessage> ForwardRequestAsync(string path, string? requestBody = null)
        {
            var httpClient = _httpClientFactory.CreateClient();

            // Set timeout
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var targetApiUrl = _configuration["TargetAPI:BaseUrl"];
            if (string.IsNullOrEmpty(targetApiUrl))
            {
                throw new InvalidOperationException("TargetAPI:BaseUrl not configured");
            }

            // Clean up the URL construction
            var cleanPath = path?.TrimStart('/') ?? "";
            var targetUrl = $"{targetApiUrl.TrimEnd('/')}/{cleanPath}";

            var request = new HttpRequestMessage(new HttpMethod(Request.Method), targetUrl);

            // Copy headers (exclude problematic ones)
            var excludedHeaders = new[] { "Host", "Content-Length", "Content-Type", "X-API-Key" };
            foreach (var header in Request.Headers.Where(h => !excludedHeaders.Contains(h.Key, StringComparer.OrdinalIgnoreCase)))
            {
                try
                {
                    // Convert header values to string array to resolve ambiguity
                    var headerValues = header.Value.Where(v => !string.IsNullOrEmpty(v)).ToArray();
                    if (headerValues.Length > 0)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, headerValues);
                    }
                }
                catch
                {
                    // Ignore invalid headers
                }
            }

            // Add authentication for target API
            var targetApiKey = _configuration["TargetAPI:ApiKey"];
            if (!string.IsNullOrEmpty(targetApiKey))
            {
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {targetApiKey}");
            }

            // Add request body if it exists
            if (!string.IsNullOrEmpty(requestBody))
            {
                var contentType = Request.ContentType ?? "application/json";
                request.Content = new StringContent(requestBody, System.Text.Encoding.UTF8, contentType);
            }

            return await httpClient.SendAsync(request);
        }

        // UNCHANGED: Get request body
        private async Task<string> GetRequestBodyAsync()
        {
            try
            {
                Request.EnableBuffering(); // Allow multiple reads
                Request.Body.Position = 0;

                using var reader = new StreamReader(Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();

                Request.Body.Position = 0; // Reset for potential future reads
                return body;
            }
            catch
            {
                return string.Empty;
            }
        }

        // UNCHANGED: Log API requests (no modification needed)
        private async Task LogRequestAsync(int clientId, string endpoint, string method, string? requestData, int responseCode, string? responseData, long processingTimeMs)
        {
            if (clientId == 0) return;

            try
            {
                var apiRequest = new ApiRequest
                {
                    ClientId = clientId,
                    Endpoint = endpoint?.Length > 255 ? endpoint[..255] : endpoint ?? "",
                    Method = method?.Length > 10 ? method[..10] : method ?? "",
                    RequestData = requestData?.Length > 5000 ? requestData[..5000] : requestData, // Limit request data
                    ResponseCode = responseCode,
                    ResponseData = responseData?.Length > 1000 ? responseData[..1000] : responseData, // Truncate long responses
                    ProcessingTimeMs = (int)Math.Min(processingTimeMs, int.MaxValue),
                    IpAddress = GetClientIpAddress(),
                    UserAgent = Request.Headers["User-Agent"].FirstOrDefault()?.Length > 500
                        ? Request.Headers["User-Agent"].FirstOrDefault()?[..500]
                        : Request.Headers["User-Agent"].FirstOrDefault(),
                    CreatedAt = DateTime.UtcNow
                };

                _context.ApiRequests.Add(apiRequest);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log the error but don't let it break the main flow
                Console.WriteLine($"Error logging request: {ex.Message}");
            }
        }

        // UNCHANGED: Get client IP address
        private string? GetClientIpAddress()
        {
            try
            {
                // Try to get real IP from common proxy headers
                var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedFor))
                {
                    var ip = forwardedFor.Split(',')[0].Trim();
                    if (!string.IsNullOrEmpty(ip) && ip.Length <= 45)
                        return ip;
                }

                var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
                if (!string.IsNullOrEmpty(realIp) && realIp.Length <= 45)
                    return realIp;

                // Fallback to connection IP
                var connectionIp = Request.HttpContext.Connection.RemoteIpAddress?.ToString();
                return connectionIp?.Length <= 45 ? connectionIp : null;
            }
            catch
            {
                return null;
            }
        }
    }
}