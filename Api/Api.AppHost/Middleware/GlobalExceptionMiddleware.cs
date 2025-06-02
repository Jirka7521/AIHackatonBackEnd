using LLM.Models;
using System.Net;
using System.Text.Json;

namespace LLM.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var response = exception switch
            {
                InvalidOperationException ex when ex.Message.Contains("already exists") =>
                    new { statusCode = HttpStatusCode.Conflict, message = ex.Message },
                InvalidOperationException ex when ex.Message.Contains("does not exist") =>
                    new { statusCode = HttpStatusCode.NotFound, message = ex.Message },
                ArgumentException ex =>
                    new { statusCode = HttpStatusCode.BadRequest, message = ex.Message },
                UnauthorizedAccessException ex =>
                    new { statusCode = HttpStatusCode.Unauthorized, message = "Unauthorized access" },
                _ =>
                    new { statusCode = HttpStatusCode.InternalServerError, message = "An error occurred while processing your request" }
            };

            context.Response.StatusCode = (int)response.statusCode;

            var apiResponse = ApiResponse<object>.ErrorResponse(response.message);
            var jsonResponse = JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }
    }
} 