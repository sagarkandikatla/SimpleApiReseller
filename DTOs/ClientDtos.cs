using System.ComponentModel.DataAnnotations;

namespace ApiResellerSystem.DTOs
{
    public class ChangePasswordDto
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;
    }
    public class CreateClientDto
    {
        [Required, MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        //[Required, MinLength(6)]
        //public string Password { get; set; } = string.Empty;

        public string? CompanyName { get; set; }
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public decimal? InitialCredits { get; set; }
    }

    public class UpdateClientDto
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? CompanyName { get; set; }
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
    }

    public class ClientResponseDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
            public decimal CreditBalance { get; set; }

    }

    //public class ClientQuotaDto
    //{
    //    public int Id { get; set; }
    //    public string PlanName { get; set; } = string.Empty;
    //    public int RequestsUsed { get; set; }
    //    public int RequestsLimit { get; set; }
    //    public int RemainingRequests => RequestsLimit - RequestsUsed;
    //    public DateTime StartDate { get; set; }
    //    public DateTime EndDate { get; set; }
    //    public bool IsActive { get; set; }
    //    public bool IsExpired => EndDate < DateTime.Now.Date;
    //}

    public class ClientStatsDto
    {
        public DateTime RequestDate { get; set; }
        public int TotalRequests { get; set; }
        public double AvgResponseTime { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public double SuccessRate => TotalRequests > 0 ? (double)SuccessCount / TotalRequests * 100 : 0;
    }

    public class ClientCreditDto
    {
        public decimal CreditBalance { get; set; }
        public List<ClientCreditTransactionDto> RecentTransactions { get; set; } = new();
    }

    public class ClientCreditTransactionDto
    {
        public decimal Amount { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class RechargeCreditsDto
    {
        public int ClientId { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }

    //public class PurchaseQuotaDto
    //{
    //    public int QuotaPlanId { get; set; }
    //    public string PaymentMethod { get; set; } = string.Empty;
    //}

    

    public class SetRequestCostDto
    {
        public decimal RequestCost { get; set; }
    }
}