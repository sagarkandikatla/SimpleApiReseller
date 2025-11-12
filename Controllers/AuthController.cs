using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using ApiResellerSystem.Data;
using ApiResellerSystem.DTOs;
using ApiResellerSystem.Models;
namespace ApiResellerSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login(LoginDto loginDto)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Client)
                    .FirstOrDefaultAsync(u => u.Username == loginDto.Username && u.IsActive);

                // ADD THIS CHECK
                if (user == null)
                {
                    return Unauthorized(new { message = "Invalid username or password" });
                }

                // ADD THIS CHECK
                if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
                {
                    return Unauthorized(new { message = "Invalid username or password" });
                }

                var token = GenerateJwtToken(user);

                return Ok(new LoginResponseDto
                {
                    Token = token,
                    Username = user.Username,
                    Role = user.Role.ToString(),
                    UserId = user.Id,
                    ClientId = user.Client?.Id
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return StatusCode(500, new { message = "Login failed", error = ex.Message });
            }
        }
        //[HttpPost("register")]
        //public async Task<ActionResult<LoginResponseDto>> Register(CreateClientDto registerDto)
        //{
        //    try
        //    {
        //        // Check if username or email already exists
        //        if (await _context.Users.AnyAsync(u => u.Username == registerDto.Username || u.Email == registerDto.Email))
        //        {
        //            return BadRequest(new { message = "Username or email already exists" });
        //        }

        //        using var transaction = await _context.Database.BeginTransactionAsync();

        //        // Create user
        //        var user = new User
        //        {
        //            Username = registerDto.Username,
        //            Email = registerDto.Email,
        //            PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
        //            Role = UserRole.Client,
        //            IsActive = true,
        //            CreatedAt = DateTime.UtcNow,
        //            UpdatedAt = DateTime.UtcNow
        //        };

        //        _context.Users.Add(user);
        //        await _context.SaveChangesAsync();

        //        // Create client
        //        var client = new Client
        //        {
        //            UserId = user.Id,
        //            CompanyName = registerDto.CompanyName,
        //            ContactPerson = registerDto.ContactPerson,
        //            Phone = registerDto.Phone,
        //            Address = registerDto.Address,
        //            ApiKey = GenerateApiKey(),
        //            ApiSecret = GenerateApiSecret(),
        //            CreatedAt = DateTime.UtcNow,
        //            UpdatedAt = DateTime.UtcNow
        //        };

        //        _context.Clients.Add(client);
        //        await _context.SaveChangesAsync();

        //        await transaction.CommitAsync();

        //        // Set the client reference for token generation
        //        user.Client = client;

        //        var token = GenerateJwtToken(user);

        //        return Ok(new LoginResponseDto
        //        {
        //            Token = token,
        //            Username = user.Username,
        //            Role = user.Role.ToString(),
        //            UserId = user.Id,
        //            ClientId = client.Id
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = "Registration failed", error = ex.Message });
        //    }
        //}

        [HttpPost("refresh-token")]
        public async Task<ActionResult<LoginResponseDto>> RefreshToken()
        {
            try
            {
                // Get current user from JWT token
                var userIdClaim = HttpContext.User.FindFirst("UserId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                var user = await _context.Users
                    .Include(u => u.Client)
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

                if (user == null)
                {
                    return Unauthorized(new { message = "User not found or inactive" });
                }

                var token = GenerateJwtToken(user);

                return Ok(new LoginResponseDto
                {
                    Token = token,
                    Username = user.Username,
                    Role = user.Role.ToString(),
                    UserId = user.Id,
                    ClientId = user.Client?.Id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Token refresh failed", error = ex.Message });
            }
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            try
            {
                // Get current user from JWT token
                var userIdClaim = HttpContext.User.FindFirst("UserId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null || !user.IsActive)
                {
                    return Unauthorized(new { message = "User not found or inactive" });
                }

                // Verify current password
                if (!BCrypt.Net.BCrypt.Verify(changePasswordDto.CurrentPassword, user.PasswordHash))
                {
                    return BadRequest(new { message = "Current password is incorrect" });
                }

                // Update password
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(changePasswordDto.NewPassword);
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Password changed successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Password change failed", error = ex.Message });
            }
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSecret = _configuration["JWT:Secret"];
            if (string.IsNullOrEmpty(jwtSecret))
            {
                throw new InvalidOperationException("JWT Secret not configured");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("UserId", user.Id.ToString()),
                new Claim("Username", user.Username),
                new Claim("Role", user.Role.ToString()),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("ClientId", user.Client?.Id.ToString() ?? "0"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:Issuer"],
                audience: _configuration["JWT:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(24),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateApiKey()
        {
            return $"ak_{Guid.NewGuid():N}";
        }

        private string GenerateApiSecret()
        {
            return $"as_{Guid.NewGuid():N}";
        }
    }
}