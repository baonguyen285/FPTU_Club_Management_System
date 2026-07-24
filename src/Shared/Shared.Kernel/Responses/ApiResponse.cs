using System.Collections.Generic;
using System.Diagnostics;

namespace Shared.Kernel.Responses
{
    public sealed class ApiError
    {
        public string Code { get; set; } = string.Empty;
        public string? Field { get; set; }
        public string Message { get; set; } = string.Empty;

        public ApiError()
        {
        }

        public ApiError(string code, string message, string? field = null)
        {
            Code = code;
            Field = field;
            Message = message;
        }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public IReadOnlyList<ApiError>? Errors { get; set; }
        public object? Meta { get; set; }
        public string? TraceId { get; set; }

        public ApiResponse()
        {
        }

        public ApiResponse(T? data, string? message = null, object? meta = null, string? traceId = null)
        {
            Success = true;
            Message = message ?? "Success";
            Data = data;
            Meta = meta;
            TraceId = traceId ?? Activity.Current?.Id;
        }

        public ApiResponse(string message, IReadOnlyList<ApiError>? errors = null, object? meta = null, string? traceId = null)
        {
            Success = false;
            Message = message;
            Data = default;
            Errors = errors ?? new List<ApiError>();
            Meta = meta;
            TraceId = traceId ?? Activity.Current?.Id;
        }
    }
}
