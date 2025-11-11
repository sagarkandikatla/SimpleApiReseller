// Models/Client.cs
using System.ComponentModel.DataAnnotations;

namespace ApiResellerSystem.Models
{
    public class Client
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        [MaxLength(100)]
        public string? CompanyName { get; set; }

        [MaxLength(100)]
        public string? ContactPerson { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        public string? Address { get; set; }

        [Required, MaxLength(255)]
        public string ApiKey { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string ApiSecret { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
     //   public virtual ICollection<ClientQuota> ClientQuotas { get; set; } = new List<ClientQuota>();
        public virtual ICollection<ApiRequest> ApiRequests { get; set; } = new List<ApiRequest>();
        //  public virtual ICollection<QuotaPurchase> QuotaPurchases { get; set; } = new List<QuotaPurchase>();
        public decimal CreditBalance { get; set; } = 0;
    }
}