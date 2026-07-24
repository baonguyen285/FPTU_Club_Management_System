using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Middlewares;

namespace Shared.Kernel.Tests;

public sealed class ExceptionHandlingMiddlewareTests
{
    public static TheoryData<Exception, int> StatusCodeCases => new()
    {
        { new UnauthorizedException("Unauthorized"), StatusCodes.Status401Unauthorized },
        { new ForbiddenException("Forbidden"), StatusCodes.Status403Forbidden },
        { new NotFoundException("Not found"), StatusCodes.Status404NotFound },
        { new BadRequestException("Bad request"), StatusCodes.Status400BadRequest },
        { new InvalidDomainException("Invalid domain"), StatusCodes.Status400BadRequest },
        { new ConflictException("Conflict"), StatusCodes.Status409Conflict },
        { new InvalidOperationException("Unexpected"), StatusCodes.Status500InternalServerError }
    };

    [Theory]
    [MemberData(nameof(StatusCodeCases))]
    public async Task InvokeAsync_MapsKnownExceptionsToCanonicalStatusCodes(
        Exception exception,
        int expectedStatusCode)
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(exception, Environments.Production);

        await middleware.InvokeAsync(context);

        Assert.Equal(expectedStatusCode, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        using var body = await ReadBodyAsync(context);
        var root = body.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(exception.Message, root.GetProperty("message").GetString());
        Assert.Equal("trace-test", root.GetProperty("traceId").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("data").ValueKind);
        Assert.Equal(0, root.GetProperty("errors").GetArrayLength());
    }

    [Fact]
    public async Task InvokeAsync_InDevelopment_IncludesStructuredExceptionDetail()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(
            new InvalidOperationException("Development detail"),
            Environments.Development);

        await middleware.InvokeAsync(context);

        using var body = await ReadBodyAsync(context);
        var error = body.RootElement.GetProperty("errors")[0];
        Assert.Equal("EXCEPTION_DETAIL", error.GetProperty("code").GetString());
        Assert.Equal("Development detail", error.GetProperty("message").GetString());
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-test"
        };
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static ExceptionHandlingMiddleware CreateMiddleware(
        Exception exception,
        string environmentName)
    {
        return new ExceptionHandlingMiddleware(
            _ => Task.FromException(exception),
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            new TestHostEnvironment(environmentName));
    }

    private static async Task<JsonDocument> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(context.Response.Body);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Shared.Kernel.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
