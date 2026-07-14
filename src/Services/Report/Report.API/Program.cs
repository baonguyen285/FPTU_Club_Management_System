using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using StackExchange.Redis;
using Shared.Kernel.Middlewares;
using Report.Application.Interfaces;
using Report.Application.Features.Reports.Commands.CreateReport;
using Report.Infrastructure.Persistence;
using Report.Infrastructure.GrpcClients;
using Report.Infrastructure.EventBus;
using Shared.Kernel.Grpc;
using Shared.Kernel.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddStandardApiBehavior();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Report.API", Version = "v1" });
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
    ?? "Server=localhost;Database=report_db;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True";
builder.Services.AddDbContext<ReportDbContext>(options =>
    options.UseSqlServer(connString));

// Configure StackExchange.Redis
var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(redisConn));

// Configure gRPC Client
builder.Services.AddGrpcClient<ClubGrpcService.ClubGrpcServiceClient>(options =>
    {
        var url = builder.Configuration["GrpcSettings:ClubServiceUrl"] ?? "http://localhost:5002";
        options.Address = new Uri(url);
    });

// Configure Dependency Injection
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IReportUnitOfWork, ReportUnitOfWork>();
builder.Services.AddScoped<IClubGrpcClient, ClubGrpcClient>();
builder.Services.AddScoped<IEventPublisher, RedisEventPublisher>();

// Register MediatR
builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(typeof(CreateReportCommand).Assembly));

// Register AutoMapper
// Truyền thêm cfg => {} làm tham số đầu tiên
builder.Services.AddAutoMapper(cfg => {}, typeof(Program).Assembly, typeof(CreateReportCommand).Assembly);


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

app.MapGet("/health", () => Results.Ok(new { status = "OK", service = "report-service", timestamp = DateTime.UtcNow }));
app.MapControllers();

// Auto migration
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ReportDbContext>();
        context.Database.EnsureCreated();
        context.Database.ExecuteSqlRaw(@"
IF OBJECT_ID('KpiRules', 'U') IS NULL
BEGIN
    CREATE TABLE KpiRules (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        CreatedAt datetime2 NOT NULL,
        UpdatedAt datetime2 NULL,
        IsActive bit NOT NULL,
        Name nvarchar(150) NOT NULL,
        Description nvarchar(500) NOT NULL,
        MaxPoints int NOT NULL,
        Weight decimal(5,2) NOT NULL
    );
END
");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating or seeding the Report database.");
    }
}

app.Run();
