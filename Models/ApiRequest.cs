using System.ComponentModel.DataAnnotations;

namespace ApiResellerSystem.Models
{
    public class ApiRequest
    {
        public long Id { get; set; }
        public int ClientId { get; set; }

        [Required, MaxLength(255)]
        public string Endpoint { get; set; } = string.Empty;

        [Required, MaxLength(10)]
        public string Method { get; set; } = string.Empty;

        public string? RequestData { get; set; }
        public int? ResponseCode { get; set; }
        public string? ResponseData { get; set; }
        public int? ProcessingTimeMs { get; set; }

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation property
        public virtual Client Client { get; set; } = null!;
    }
}