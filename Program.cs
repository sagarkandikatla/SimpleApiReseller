using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using SimpleApiReseller.Models;
using ApiResellerSystem.Data;
using ApiResellerSystem.Models;

var builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------
// 1️⃣ Configure Services
// -------------------------------------------------------------
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database (MySQL)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    ));

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JWT:Issuer"],
            ValidAudience = builder.Configuration["JWT:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["JWT:Secret"]!)
            )
        };
    });

builder.Services.AddAuthorization();

// HttpClient (for proxy API requests)
builder.Services.AddHttpClient();

// CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5291", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// -------------------------------------------------------------
// 2️⃣ Middleware Configuration
// -------------------------------------------------------------
app.UseDefaultFiles();
app.UseStaticFiles();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

// Block access to .well-known endpoints
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/.well-known"))
    {
        context.Response.StatusCode = 404;
        return;
    }
    await next();
});

// -------------------------------------------------------------
// 3️⃣ Routing
// -------------------------------------------------------------
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);
app.MapControllers();

// -------------------------------------------------------------
// 4️⃣ Swagger (API docs)
// -------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// -------------------------------------------------------------
// 5️⃣ Database Seeding
// -------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();
    await SeedInitialData(context, app.Environment.IsDevelopment());
}

// -------------------------------------------------------------
// 6️⃣ Run App
// -------------------------------------------------------------
app.Run();


// -------------------------------------------------------------
// 7️⃣ SeedInitialData Method
// -------------------------------------------------------------
static async Task SeedInitialData(ApplicationDbContext context, bool isDevelopment)
{
    try
    {
        // Seed Admin User
        if (!await context.Users.AnyAsync(u => u.Role == UserRole.Admin))
        {
            var adminUser = new User
            {
                Username = "admin",
                Email = "admin@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Users.Add(adminUser);
            await context.SaveChangesAsync();

            Console.WriteLine("✅ Default Admin Created: admin / admin123");
        }

        // Seed System Settings
        if (!await context.SystemSettings.AnyAsync(s => s.SettingKey == "REQUEST_COST"))
        {
            var setting = new SystemSettings
            {
                SettingKey = "REQUEST_COST",
                SettingValue = "0.01",
                UpdatedAt = DateTime.UtcNow
            };
            context.SystemSettings.Add(setting);
            await context.SaveChangesAsync();

            Console.WriteLine("⚙️  Default System Setting Added: REQUEST_COST = 0.01");
        }

        // Seed Demo Client (only in development)
        if (isDevelopment && !await context.Clients.AnyAsync())
        {
            var demoUser = new User
            {
                Username = "democlient",
                Email = "demo@client.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("demo123"),
                Role = UserRole.Client,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Users.Add(demoUser);
            await context.SaveChangesAsync();

            var demoClient = new Client
            {
                UserId = demoUser.Id,
                CompanyName = "Demo Company",
                ContactPerson = "Demo User",
                Phone = "555-0123",
                Address = "123 Demo Street",
                ApiKey = "ak_demo123456789",
                ApiSecret = "as_demo987654321",
                CreditBalance = 10.00m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Clients.Add(demoClient);
            await context.SaveChangesAsync();

            var initialCredit = new ClientCredit
            {
                ClientId = demoClient.Id,
                Amount = 10.00m,
                TransactionType = "Recharge",
                Description = "Initial demo credit",
                CreatedAt = DateTime.UtcNow
            };
            context.ClientCredits.Add(initialCredit);
            await context.SaveChangesAsync();

            Console.WriteLine("👤 Demo Client Created: democlient / demo123 ($10 credit)");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error seeding initial data: {ex.Message}");
    }
}
