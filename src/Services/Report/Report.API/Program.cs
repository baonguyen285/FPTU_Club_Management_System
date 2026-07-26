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
using Report.Infrastructure.Messaging;
using Shared.Kernel.Grpc;
using Shared.Kernel.Extensions;
using Hangfire;
using Report.API.Jobs;
using Report.Application.Validation;
using Report.Application.Generation;

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
builder.Services.AddGrpcClient<Shared.Kernel.Grpc.ClubAccess.V1.ClubAccessService.ClubAccessServiceClient>(options =>
    {
        var url = builder.Configuration["GrpcSettings:ClubServiceUrl"] ?? "http://localhost:5002";
        options.Address = new Uri(url);
    });
builder.Services.AddGrpcClient<Shared.Kernel.Grpc.SmartReports.V1.ClubReportSnapshotSource.ClubReportSnapshotSourceClient>(options =>
{
    options.Address = new Uri(builder.Configuration["GrpcSettings:ClubServiceUrl"] ?? "http://localhost:9001");
});
builder.Services.AddGrpcClient<Shared.Kernel.Grpc.SmartReports.V1.FinanceReportSnapshotSource.FinanceReportSnapshotSourceClient>(options =>
{
    options.Address = new Uri(builder.Configuration["GrpcSettings:FinanceServiceUrl"] ?? "http://localhost:9003");
});

// Configure Dependency Injection
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IReportUnitOfWork, ReportUnitOfWork>();
builder.Services.AddScoped<ISemesterService, Report.Infrastructure.Services.SemesterService>();
builder.Services.AddScoped<IClubGrpcClient, ClubGrpcClient>();
builder.Services.AddScoped<IClubReportSnapshotSource, ClubReportSnapshotSourceClient>();
builder.Services.AddScoped<IFinanceReportSnapshotSource, FinanceReportSnapshotSourceClient>();
builder.Services.AddScoped<ISmartReportSnapshotService, Report.Infrastructure.Services.SmartReportSnapshotService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IReportValidationRule, ReportCompletenessRule>();
builder.Services.AddScoped<IReportValidationRule, EventConsistencyRule>();
builder.Services.AddScoped<IReportValidationRule, FinanceConsistencyRule>();
builder.Services.AddScoped<IReportValidationRule, MembershipLimitationRule>();
builder.Services.AddScoped<IReportValidationRule, KpiAvailabilityRule>();
builder.Services.AddScoped<IReportValidationEngine, ReportValidationEngine>();
builder.Services.AddScoped<IReportValidationService, Report.Infrastructure.Services.ReportValidationService>();
builder.Services.AddScoped<IReportDraftGenerator, RuleBasedReportDraftGenerator>();
builder.Services.AddScoped<IReportDraftGenerationService, Report.Infrastructure.Services.ReportDraftGenerationService>();
builder.Services.AddSingleton<IRedisStreamProducer, RedisStreamProducer>();
builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddHangfire(configuration => configuration.UseSqlServerStorage(connString));
builder.Services.AddHangfireServer(options => options.ServerName = "report-service");
builder.Services.AddScoped<PendingReportReminderJob>();

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

app.MapGet("/health", () => Results.Ok(new { status = "OK", service = "report-service", timestamp = DateTime.UtcNow }));
app.MapControllers();
if (app.Environment.IsDevelopment())
{
    app.MapPost("/internal/jobs/pending-report-reminder/trigger", (
        IBackgroundJobClient jobs) =>
    {
        var jobId = jobs.Enqueue<PendingReportReminderJob>(
            job => job.ExecuteAsync(CancellationToken.None));
        return Results.Accepted(value: new { jobId });
    });
}

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

IF OBJECT_ID('OutboxMessages', 'U') IS NULL
BEGIN
    CREATE TABLE OutboxMessages (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        EventType nvarchar(200) NOT NULL,
        Payload nvarchar(max) NOT NULL,
        LegacyPayload nvarchar(max) NULL,
        OccurredAtUtc datetime2 NOT NULL,
        PublishedAtUtc datetime2 NULL,
        RetryCount int NOT NULL,
        NextAttemptAtUtc datetime2 NULL,
        LastError nvarchar(2000) NULL,
        LockedUntilUtc datetime2 NULL,
        RowVersion rowversion NOT NULL,
        CreatedAt datetime2 NOT NULL,
        UpdatedAt datetime2 NULL,
        IsActive bit NOT NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OutboxMessages_PublishedAtUtc_NextAttemptAtUtc' AND object_id = OBJECT_ID('OutboxMessages'))
    EXEC('CREATE INDEX IX_OutboxMessages_PublishedAtUtc_NextAttemptAtUtc ON OutboxMessages(PublishedAtUtc, NextAttemptAtUtc)');

IF OBJECT_ID('ReportReminderDispatches', 'U') IS NULL
BEGIN
    CREATE TABLE ReportReminderDispatches (
        ReportId uniqueidentifier NOT NULL,
        ReminderDateUtc date NOT NULL,
        CreatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_ReportReminderDispatches PRIMARY KEY (ReportId, ReminderDateUtc)
    );
END

IF OBJECT_ID('Semesters', 'U') IS NULL
BEGIN
    CREATE TABLE Semesters (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        Code nvarchar(30) NOT NULL,
        Name nvarchar(150) NOT NULL,
        StartDate datetime2 NOT NULL,
        EndDate datetime2 NOT NULL,
        Status int NOT NULL,
        CreatedAt datetime2 NOT NULL,
        UpdatedAt datetime2 NULL,
        IsActive bit NOT NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Semesters_Code' AND object_id = OBJECT_ID('Semesters'))
    EXEC('CREATE UNIQUE INDEX IX_Semesters_Code ON Semesters(Code)');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Semesters_Status_Active' AND object_id = OBJECT_ID('Semesters'))
    EXEC('CREATE UNIQUE INDEX IX_Semesters_Status_Active ON Semesters(Status) WHERE Status = 1');

IF COL_LENGTH('Reports', 'SemesterId') IS NULL ALTER TABLE Reports ADD SemesterId uniqueidentifier NULL;
IF COL_LENGTH('Reports', 'RevisionNumber') IS NULL ALTER TABLE Reports ADD RevisionNumber int NOT NULL CONSTRAINT DF_Reports_RevisionNumber DEFAULT 1;
IF COL_LENGTH('KpiRules', 'SemesterId') IS NULL ALTER TABLE KpiRules ADD SemesterId uniqueidentifier NULL;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Reports_Semesters_SemesterId')
    EXEC('ALTER TABLE Reports ADD CONSTRAINT FK_Reports_Semesters_SemesterId FOREIGN KEY (SemesterId) REFERENCES Semesters(Id)');
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_KpiRules_Semesters_SemesterId')
    EXEC('ALTER TABLE KpiRules ADD CONSTRAINT FK_KpiRules_Semesters_SemesterId FOREIGN KEY (SemesterId) REFERENCES Semesters(Id)');

IF OBJECT_ID('ReportRevisionHistories', 'U') IS NULL
BEGIN
    CREATE TABLE ReportRevisionHistories (
        Id uniqueidentifier NOT NULL PRIMARY KEY, ReportId uniqueidentifier NOT NULL,
        RevisionNumber int NOT NULL, PreviousStatus int NOT NULL, NewStatus int NOT NULL,
        Feedback nvarchar(1000) NULL, ChangedBy uniqueidentifier NOT NULL, ChangedAt datetime2 NOT NULL,
        CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, IsActive bit NOT NULL,
        CONSTRAINT FK_ReportRevisionHistories_Reports_ReportId FOREIGN KEY (ReportId) REFERENCES Reports(Id) ON DELETE CASCADE
    );
END

IF OBJECT_ID('KpiScoreHistories', 'U') IS NULL
BEGIN
    CREATE TABLE KpiScoreHistories (
        Id uniqueidentifier NOT NULL PRIMARY KEY, ClubId uniqueidentifier NOT NULL,
        SemesterId uniqueidentifier NOT NULL, RuleId uniqueidentifier NULL, Points decimal(10,2) NOT NULL,
        Reason nvarchar(500) NOT NULL, SourceType nvarchar(50) NOT NULL, SourceId uniqueidentifier NULL,
        AdjustedBy uniqueidentifier NOT NULL, CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, IsActive bit NOT NULL,
        CONSTRAINT FK_KpiScoreHistories_Semesters_SemesterId FOREIGN KEY (SemesterId) REFERENCES Semesters(Id),
        CONSTRAINT FK_KpiScoreHistories_KpiRules_RuleId FOREIGN KEY (RuleId) REFERENCES KpiRules(Id)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ReportRevisionHistories_ReportId_ChangedAt' AND object_id=OBJECT_ID('ReportRevisionHistories'))
    CREATE INDEX IX_ReportRevisionHistories_ReportId_ChangedAt ON ReportRevisionHistories(ReportId, ChangedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_KpiScoreHistories_SemesterId_ClubId_CreatedAt' AND object_id=OBJECT_ID('KpiScoreHistories'))
    EXEC('CREATE INDEX IX_KpiScoreHistories_SemesterId_ClubId_CreatedAt ON KpiScoreHistories(SemesterId, ClubId, CreatedAt)');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_KpiScoreHistories_SourceType_SourceId' AND object_id=OBJECT_ID('KpiScoreHistories'))
    EXEC('CREATE UNIQUE INDEX IX_KpiScoreHistories_SourceType_SourceId ON KpiScoreHistories(SourceType, SourceId) WHERE SourceId IS NOT NULL');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_KpiRules_SemesterId_Name' AND object_id=OBJECT_ID('KpiRules'))
    EXEC('CREATE UNIQUE INDEX IX_KpiRules_SemesterId_Name ON KpiRules(SemesterId, Name) WHERE SemesterId IS NOT NULL AND IsActive=1');
");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating or seeding the Report database.");
    }
}

var reminderTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
    OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<PendingReportReminderJob>(
    PendingReportReminderJob.RecurringJobId,
    job => job.ExecuteAsync(CancellationToken.None),
    "0 8 * * *",
    new RecurringJobOptions { TimeZone = reminderTimeZone });

app.Run();
