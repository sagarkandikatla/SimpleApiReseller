// Models/User.cs
using System.ComponentModel.DataAnnotations;

namespace ApiResellerSystem.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; } = UserRole.Client;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; } // Added this property

        // Navigation property - This should match what's referenced in ApplicationDbContext
        public virtual Client? Client { get; set; }
    }

    public enum UserRole
    {
        Admin,
        Client
    }
}