using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add Ocelot configuration file
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// Configure JWT Authentication on Gateway
var secretKey = builder.Configuration["JwtSettings:SecretKey"] ?? "your-super-secret-key-min-32-chars!!";
var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer("Bearer", options =>
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

// Add Ocelot Services
builder.Services.AddOcelot();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseRouting();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

// Enable WebSockets for SignalR
app.UseWebSockets();

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (path is "/health" or "/gateway/health")
    {
        await Results.Ok(new { status = "OK", service = "api-gateway", timestamp = DateTime.UtcNow })
            .ExecuteAsync(context);
        return;
    }

    if (path != null &&
        path.StartsWith("/gateway/dashboard", StringComparison.OrdinalIgnoreCase))
    {
        await Results.Json(new
        {
            success = false,
            message = "This feature has not been implemented yet.",
            data = (object?)null,
            errors = new[]
            {
                new
                {
                    code = "FEATURE_NOT_IMPLEMENTED",
                    field = (string?)null,
                    message = "Backend API for this module is not implemented yet."
                }
            },
            meta = (object?)null,
            traceId = context.TraceIdentifier
        }, statusCode: StatusCodes.Status501NotImplemented).ExecuteAsync(context);
        return;
    }

    await next();
});

// Setup Ocelot
await app.UseOcelot();

app.Run();
