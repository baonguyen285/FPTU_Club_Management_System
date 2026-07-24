using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Club.Infrastructure.Persistence;
using Club.API.GrpcServices;
using Shared.Kernel.Extensions;
using Shared.Kernel.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to support both HTTP (REST) and HTTP/2 (gRPC)
builder.WebHost.ConfigureKestrel(options =>
{
    // HTTP REST API endpoint
    options.ListenAnyIP(8080);
    
    // gRPC endpoint (HTTP/2 only)
    options.ListenAnyIP(9001, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddStandardApiBehavior();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Club.API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n 👉 Nhập trực tiếp chuỗi Token của bạn vào ô bên dưới (KHÔNG cần gõ chữ 'Bearer' ở trước).",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// Configure DB Context
var connString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=localhost;Database=club_db;Trusted_Connection=True;TrustServerCertificate=True";
builder.Services.AddDbContext<ClubDbContext>(options =>
    options.UseSqlServer(connString));

// Add Repositories
builder.Services.AddScoped<Club.Application.Interfaces.IClubRepository, Club.Infrastructure.Repositories.ClubRepository>();
builder.Services.AddScoped<Club.Application.Interfaces.IUnitOfWork, Club.Infrastructure.Repositories.UnitOfWork>();

// Add MediatR and AutoMapper
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Club.Application.DTOs.ClubDto).Assembly));
builder.Services.AddAutoMapper(typeof(Club.Application.DTOs.MappingProfile).Assembly);

// Add gRPC
builder.Services.AddGrpc();

// Configure JWT Authentication
var secretKey = builder.Configuration["JwtSettings:SecretKey"] ?? "your-super-secret-key-min-32-chars!!";
var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "fptu-club-system",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["JwtSettings:Audience"] ?? "fptu-club-clients",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "OK", service = "club-service", timestamp = DateTime.UtcNow }));
app.MapControllers();

// Map gRPC Services
app.MapGrpcService<ClubGrpcServiceImpl>();
app.MapGrpcService<ClubAccessGrpcServiceImpl>();

// Auto migration
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ClubDbContext>();
        context.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating or seeding the Club database.");
    }
}

app.Run();
