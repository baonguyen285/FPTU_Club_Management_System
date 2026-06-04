using Microsoft.EntityFrameworkCore;
using Club.Infrastructure.Persistence;
using Club.API.GrpcServices;
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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure DB Context
var connString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=localhost;Database=club_db;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True";
builder.Services.AddDbContext<ClubDbContext>(options =>
    options.UseSqlServer(connString));

// Add gRPC
builder.Services.AddGrpc();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthorization();

app.MapControllers();

// Map gRPC Services
app.MapGrpcService<ClubGrpcServiceImpl>();

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
