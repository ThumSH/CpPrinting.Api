using Microsoft.EntityFrameworkCore;
using CpPrinting.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using CpPrinting.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<CpPrinting.Api.Services.ActivityLogger>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("LanCorsPolicy", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey)
    };
});

// Ensure upload directories exist on startup
var wwwroot = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(wwwroot, "uploads", "samples"));
Directory.CreateDirectory(Path.Combine(wwwroot, "uploads", "artworks"));

var app = builder.Build();

app.UseCors("LanCorsPolicy");

// Serve wwwroot — required for artwork and sample images
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await SuperAdminSeeder.SeedAsync(app.Services);

app.Run("http://0.0.0.0:5000");