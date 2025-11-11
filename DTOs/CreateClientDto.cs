public class CreateClientDto
{
    public string Username { get; set; }
    public string Email { get; set; }
    public string Password { get; set; } // Added this property to fix the error  
    public string? CompanyName { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public decimal? InitialCredits { get; set; }
}
