// Simple credit transaction log
using ApiResellerSystem.Models;

public class ClientCredit
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public decimal Amount { get; set; } // +ve for recharge, -ve for usage
    public string TransactionType { get; set; } // "Recharge" or "Usage"
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Client Client { get; set; } = null!;
}