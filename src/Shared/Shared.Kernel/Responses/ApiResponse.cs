using System.Collections.Generic;

namespace Shared.Kernel.Responses
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public T? Data { get; set; }
        public List<string> Errors { get; set; } = new();
        public object? Meta { get; set; }
        public string? TraceId { get; set; }

        public ApiResponse()
        {
        }

        public ApiResponse(T? data, string? message = null, int statusCode = 200, object? meta = null, string? traceId = null)
        {
            Success = true;
            Message = message ?? "Success";
            StatusCode = statusCode;
            Data = data;
            Meta = meta;
            TraceId = traceId;
        }

        public ApiResponse(int statusCode, string message, List<string>? errors = null, object? meta = null, string? traceId = null)
        {
            Success = false;
            Message = message;
            StatusCode = statusCode;
            Errors = errors ?? new List<string>();
            Meta = meta;
            TraceId = traceId;
        }
    }
}
