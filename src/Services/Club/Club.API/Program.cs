using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Club.Infrastructure.Persistence;
using Club.API.GrpcServices;
using Shared.Kernel.Extensions;
using Shared.Kernel.Middlewares;
using StackExchange.Redis;

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
builder.Services.AddScoped<Club.Application.Interfaces.IClubEventPublisher, Club.Infrastructure.Messaging.RedisClubEventPublisher>();
builder.Services.AddScoped<Club.Application.Interfaces.IClubApplicationService, Club.Infrastructure.Services.ClubApplicationService>();
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));

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
    options.MapInboundClaims = false;
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
        NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Name,
        RoleClaimType = "role",
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
app.MapGrpcService<ClubReportSnapshotGrpcService>();

// Auto migration
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ClubDbContext>();
        context.Database.EnsureCreated();
        context.Database.ExecuteSqlRaw("""
            IF OBJECT_ID(N'[ClubApplications]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ClubApplications] (
                    [Id] uniqueidentifier NOT NULL,
                    [ApplicantUserId] uniqueidentifier NOT NULL,
                    [ProposedClubName] nvarchar(150) NOT NULL,
                    [Description] nvarchar(4000) NOT NULL,
                    [Objectives] nvarchar(4000) NOT NULL,
                    [EvidenceUrlsJson] nvarchar(max) NULL,
                    [Status] int NOT NULL,
                    [ReviewFeedback] nvarchar(2000) NULL,
                    [SubmittedAt] datetime2 NOT NULL,
                    [ReviewedAt] datetime2 NULL,
                    [ReviewedByUserId] uniqueidentifier NULL,
                    [CreatedClubId] uniqueidentifier NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [IsActive] bit NOT NULL,
                    CONSTRAINT [PK_ClubApplications] PRIMARY KEY ([Id])
                );
                CREATE INDEX [IX_ClubApplications_Status] ON [ClubApplications] ([Status]);
                CREATE UNIQUE INDEX [IX_ClubApplications_CreatedClubId]
                    ON [ClubApplications] ([CreatedClubId]) WHERE [CreatedClubId] IS NOT NULL;
            END
            """);
        context.Database.ExecuteSqlRaw("""
            UPDATE Clubs
            SET Status = 1
            WHERE Id = '99999999-9999-9999-9999-999999999999' AND Status = 0;
            """);

        var treasurerUserId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var defaultClubId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        if (!context.ClubMembers.Any(member =>
            member.ClubId == defaultClubId && member.UserId == treasurerUserId))
        {
            context.ClubMembers.Add(new Club.Domain.Entities.ClubMember
            {
                Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
                ClubId = defaultClubId,
                UserId = treasurerUserId,
                Role = Club.Domain.Enums.ClubRole.Treasurer,
                Status = Club.Domain.Enums.MembershipStatus.Approved,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            });
            context.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating or seeding the Club database.");
    }
}

app.Run();
