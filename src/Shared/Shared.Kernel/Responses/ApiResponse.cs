using System.Collections.Generic;

namespace Shared.Kernel.Responses
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int StatusCode { get; set; }
        public T Data { get; set; }
        public List<string> Errors { get; set; } = new();

        public ApiResponse()
        {
        }

        public ApiResponse(T data, string message = null, int statusCode = 200)
        {
            Success = true;
            Message = message;
            StatusCode = statusCode;
            Data = data;
        }

        public ApiResponse(int statusCode, string message, List<string> errors = null)
        {
            Success = false;
            Message = message;
            StatusCode = statusCode;
            Errors = errors ?? new List<string>();
        }
    }
}
