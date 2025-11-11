using Microsoft.EntityFrameworkCore;
using ApiResellerSystem.Models;
using SimpleApiReseller.Models;

namespace ApiResellerSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<ApiRequest> ApiRequests { get; set; }
        public DbSet<ClientCredit> ClientCredits { get; set; }
        public DbSet<SystemSettings> SystemSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure table and column names to match your database schema
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Username).HasColumnName("username");
                entity.Property(e => e.Email).HasColumnName("email");
                entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
                entity.Property(e => e.Role).HasColumnName("role").HasConversion<string>();
                entity.Property(e => e.IsActive).HasColumnName("is_active");

                // FIX: Add proper timestamp configuration
                entity.Property(e => e.CreatedAt).HasColumnName("created_at")
                      .HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at")
                      .HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

                // Add indexes
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
            });

            modelBuilder.Entity<Client>(entity =>
            {
                entity.ToTable("clients");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.CompanyName).HasColumnName("company_name");
                entity.Property(e => e.ContactPerson).HasColumnName("contact_person");
                entity.Property(e => e.Phone).HasColumnName("phone");
                entity.Property(e => e.Address).HasColumnName("address");
                entity.Property(e => e.ApiKey).HasColumnName("api_key");
                entity.Property(e => e.ApiSecret).HasColumnName("api_secret");
                entity.Property(e => e.CreditBalance).HasColumnName("credit_balance")
                      .HasColumnType("decimal(10,2)").HasDefaultValue(0);

                // FIX: Add proper timestamp configuration
                entity.Property(e => e.CreatedAt).HasColumnName("created_at")
                      .HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at")
                      .HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

                // Add indexes and relationships
                entity.HasIndex(e => e.ApiKey).IsUnique();
                entity.HasOne(e => e.User).WithOne(e => e.Client).HasForeignKey<Client>(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ApiRequest>(entity =>
            {
                entity.ToTable("api_requests");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ClientId).HasColumnName("client_id");
                entity.Property(e => e.Endpoint).HasColumnName("endpoint").HasMaxLength(255);
                entity.Property(e => e.Method).HasColumnName("method").HasMaxLength(10);
                entity.Property(e => e.RequestData).HasColumnName("request_data");
                entity.Property(e => e.ResponseCode).HasColumnName("response_code");
                entity.Property(e => e.ResponseData).HasColumnName("response_data");
                entity.Property(e => e.ProcessingTimeMs).HasColumnName("processing_time_ms");
                entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(500);

                // FIX: Add proper timestamp configuration
                entity.Property(e => e.CreatedAt).HasColumnName("created_at")
                      .HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Add indexes and relationships
                entity.HasIndex(e => new { e.ClientId, e.CreatedAt });
                entity.HasIndex(e => e.CreatedAt);
                entity.HasOne(e => e.Client).WithMany(e => e.ApiRequests).HasForeignKey(e => e.ClientId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ClientCredit>(entity =>
            {
                entity.ToTable("client_credits");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ClientId).HasColumnName("client_id");
                entity.Property(e => e.Amount).HasColumnName("amount").HasColumnType("decimal(10,2)");
                entity.Property(e => e.TransactionType).HasColumnName("transaction_type").HasMaxLength(20);
                entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);

                // FIX: Add proper timestamp configuration
                entity.Property(e => e.CreatedAt).HasColumnName("created_at")
                      .HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Add indexes and relationships
                entity.HasIndex(e => new { e.ClientId, e.CreatedAt });
                entity.HasIndex(e => e.TransactionType);
                entity.HasOne(e => e.Client)
                      .WithMany()
                      .HasForeignKey(e => e.ClientId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // SystemSettings configuration
            modelBuilder.Entity<SystemSettings>(entity =>
            {
                entity.ToTable("system_settings");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.SettingKey).HasColumnName("setting_key").HasMaxLength(100);
                entity.Property(e => e.SettingValue).HasColumnName("setting_value");

                // FIX: Add proper timestamp configuration
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at")
                      .HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

                // Add indexes
                entity.HasIndex(e => e.SettingKey).IsUnique();
            });
        }
    }
}