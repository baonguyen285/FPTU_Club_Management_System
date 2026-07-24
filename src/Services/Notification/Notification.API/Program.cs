using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using Notification.Infrastructure.EventBus;
using Notification.Infrastructure.GrpcClients;
using Notification.Infrastructure.Hubs;
using Notification.Infrastructure.Persistence;
using Notification.Application.Interfaces;
using Notification.Application.Mappings;
using Shared.Kernel.Extensions;
using Shared.Kernel.Middlewares;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddStandardApiBehavior();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Notification API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] {}
        }
    });
});

// 2. Configure Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// 3. Configure Redis & Hosted Service
var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(redisConn));

builder.Services.AddGrpcClient<Shared.Kernel.Grpc.IdentityDirectory.V1.IdentityDirectoryService.IdentityDirectoryServiceClient>(options =>
{
    options.Address = new Uri(builder.Configuration["GrpcSettings:IdentityDirectoryUrl"] ?? "http://localhost:9002");
});
builder.Services.AddScoped<IIdentityDirectoryClient, IdentityDirectoryClient>();
builder.Services.AddHostedService<RedisStreamsConsumer>();

// 4. Configure AutoMapper & MediatR
builder.Services.AddAutoMapper(typeof(NotificationMappingProfile).Assembly);
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Notification.Application.DTOs.NotificationDto).Assembly));

// 5. Configure SignalR
builder.Services.AddSignalR();

// 6. Configure CORS for WebSockets
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy",
        builder => builder
        .AllowAnyMethod()
        .AllowAnyHeader()
        .SetIsOriginAllowed((host) => true) // allow any origin
        .AllowCredentials());
});

// 7. Configure JWT Authentication
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
        ValidateLifetime = true
    };

    // Support JWT over WebSocket (SignalR sends token in query string)
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && (path.StartsWithSegments("/hubs/notification")))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

var app = builder.Build();

// Ensure DB Created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    context.Database.EnsureCreated();
    context.Database.ExecuteSqlRaw(@"
IF COL_LENGTH('Notifications', 'SourceEventId') IS NULL ALTER TABLE Notifications ADD SourceEventId uniqueidentifier NULL;
IF COL_LENGTH('Notifications', 'TargetUrl') IS NULL ALTER TABLE Notifications ADD TargetUrl nvarchar(500) NULL;
IF COL_LENGTH('Notifications', 'ReadAt') IS NULL ALTER TABLE Notifications ADD ReadAt datetime2 NULL;
IF COL_LENGTH('Notifications', 'IsDeleted') IS NULL ALTER TABLE Notifications ADD IsDeleted bit NOT NULL CONSTRAINT DF_Notifications_IsDeleted DEFAULT(0);
IF COL_LENGTH('Notifications', 'DeletedAt') IS NULL ALTER TABLE Notifications ADD DeletedAt datetime2 NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notifications_SourceEventId_UserId' AND object_id = OBJECT_ID('Notifications'))
    EXEC('CREATE UNIQUE INDEX IX_Notifications_SourceEventId_UserId ON Notifications(SourceEventId, UserId) WHERE SourceEventId IS NOT NULL');
IF OBJECT_ID('StreamProcessingFailures', 'U') IS NULL
    CREATE TABLE StreamProcessingFailures (Id uniqueidentifier NOT NULL PRIMARY KEY, StreamEntryId nvarchar(100) NOT NULL, RetryCount int NOT NULL, LastErrorCode nvarchar(100) NOT NULL, LastErrorMessage nvarchar(2000) NOT NULL, LastFailedAtUtc datetime2 NOT NULL, CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, IsActive bit NOT NULL);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StreamProcessingFailures_StreamEntryId' AND object_id = OBJECT_ID('StreamProcessingFailures'))
    EXEC('CREATE UNIQUE INDEX IX_StreamProcessingFailures_StreamEntryId ON StreamProcessingFailures(StreamEntryId)');
");
}

app.UseCors("CorsPolicy");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "OK", service = "notification-service", timestamp = DateTime.UtcNow }));
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notification");

app.Run();
