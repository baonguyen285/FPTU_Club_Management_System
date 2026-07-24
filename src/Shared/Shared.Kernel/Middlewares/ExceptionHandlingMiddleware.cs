using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Responses;

namespace Shared.Kernel.Middlewares
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IHostEnvironment _environment;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex, _logger, _environment);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception, ILogger logger, IHostEnvironment environment)
        {
            context.Response.ContentType = "application/json";
            
            var statusCode = exception switch
            {
                UnauthorizedException => StatusCodes.Status401Unauthorized,
                UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                ForbiddenException => StatusCodes.Status403Forbidden,
                NotFoundException => StatusCodes.Status404NotFound,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                ClubNotFoundException => StatusCodes.Status404NotFound,
                BadRequestException => StatusCodes.Status400BadRequest,
                ArgumentException => StatusCodes.Status400BadRequest,
                InvalidDomainException => StatusCodes.Status400BadRequest,
                ConflictException => StatusCodes.Status409Conflict,
                ServiceUnavailableException => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status500InternalServerError
            };

            context.Response.StatusCode = statusCode;
            logger.LogError(exception, "Unhandled request error. TraceId: {TraceId}", context.TraceIdentifier);

            var errors = new List<ApiError>();
            if (environment.IsDevelopment())
            {
                errors.Add(new ApiError(
                    code: "EXCEPTION_DETAIL",
                    message: exception.InnerException?.Message ?? exception.Message));
            }

            var response = new ApiResponse<object>(
                message: exception.Message,
                errors: errors,
                traceId: context.TraceIdentifier
            );

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            return context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
        }
    }
}
