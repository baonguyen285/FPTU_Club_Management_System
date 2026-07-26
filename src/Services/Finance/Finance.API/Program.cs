using System.Text;
using Finance.Infrastructure.Persistence;
using Finance.Infrastructure.GrpcClients;
using Finance.Application.Interfaces;
using Finance.Application.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Shared.Kernel.Extensions;
using Shared.Kernel.Middlewares;
using Shared.Kernel.Grpc;
using Finance.Infrastructure.Messaging;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddStandardApiBehavior();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Finance.API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        Description = "Enter a valid JWT access token."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Database=finance_db;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True";
builder.Services.AddDbContext<FinanceDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<IBudgetProposalRepository, BudgetProposalRepository>();
builder.Services.AddScoped<IClubAccessService, ClubAccessService>();
builder.Services.AddScoped<IBudgetProposalService, BudgetProposalService>();
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));
builder.Services.AddScoped<IFinanceEventPublisher, RedisFinanceEventPublisher>();
builder.Services.AddGrpcClient<Shared.Kernel.Grpc.ClubAccess.V1.ClubAccessService.ClubAccessServiceClient>(options =>
{
    options.Address = new Uri(builder.Configuration["GrpcSettings:ClubServiceUrl"] ?? "http://localhost:9001");
});

var secretKey = builder.Configuration["JwtSettings:SecretKey"]
    ?? "your-super-secret-key-min-32-chars!!";
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "fptu-club-system",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["JwtSettings:Audience"] ?? "fptu-club-clients",
            ValidateLifetime = true,
            NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Name,
            RoleClaimType = "role",
            ClockSkew = TimeSpan.Zero
        };
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "OK",
    service = "finance-service",
    timestamp = DateTime.UtcNow
}));
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        context.Database.Migrate();
    }
    catch (Exception exception)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "An error occurred while migrating the Finance database.");
    }
}

app.Run();
