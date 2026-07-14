using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.Responses;

namespace Shared.Kernel.Extensions
{
    public static class ApiBehaviorExtensions
    {
        public static IServiceCollection AddStandardApiBehavior(this IServiceCollection services)
        {
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(entry => entry.Value?.Errors.Count > 0)
                        .SelectMany(entry => entry.Value!.Errors.Select(error =>
                            string.IsNullOrWhiteSpace(error.ErrorMessage)
                                ? "Invalid request value."
                                : error.ErrorMessage))
                        .ToList();

                    var response = new ApiResponse<object>(
                        statusCode: 400,
                        message: "Validation failed",
                        errors: errors,
                        traceId: context.HttpContext.TraceIdentifier);

                    return new BadRequestObjectResult(response);
                };
            });

            return services;
        }
    }
}
