using Shared.Kernel.Responses;

namespace Shared.Kernel.Tests;

public sealed class ApiResponseTests
{
    [Fact]
    public void SuccessResponse_PreservesDataMetadataAndTraceId()
    {
        var metadata = new { Page = 2, PageSize = 20 };

        var response = new ApiResponse<string>(
            data: "payload",
            message: "Created",
            meta: metadata,
            traceId: "trace-success");

        Assert.True(response.Success);
        Assert.Equal("Created", response.Message);
        Assert.Equal("payload", response.Data);
        Assert.Same(metadata, response.Meta);
        Assert.Equal("trace-success", response.TraceId);
        Assert.Null(response.Errors);
    }

    [Fact]
    public void FailureResponse_UsesStructuredErrorsAndEmptyData()
    {
        var errors = new[]
        {
            new ApiError("VALIDATION_ERROR", "Email is required.", "email")
        };

        var response = new ApiResponse<object>(
            message: "Validation failed",
            errors: errors,
            traceId: "trace-failure");

        Assert.False(response.Success);
        Assert.Equal("Validation failed", response.Message);
        Assert.Null(response.Data);
        Assert.Same(errors, response.Errors);
        Assert.Equal("trace-failure", response.TraceId);
    }

    [Fact]
    public void FailureResponse_DefaultsErrorsToAnEmptyCollection()
    {
        var response = new ApiResponse<object>("Request failed");

        Assert.NotNull(response.Errors);
        Assert.Empty(response.Errors);
    }
}
