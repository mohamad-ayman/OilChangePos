using System.Net;
using System.Text.Json;

namespace OilChangePOS.API.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Request failed: {Path}", context.Request.Path);
            await WriteErrorAsync(context, ex);
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, Exception ex)
    {
        var (status, message) = ex switch
        {
            InvalidOperationException => (HttpStatusCode.BadRequest, ex.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, ex.Message),
            ArgumentException => (HttpStatusCode.BadRequest, ex.Message),
            KeyNotFoundException => (HttpStatusCode.NotFound, ex.Message),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            error = message,
            type = ex.GetType().Name
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
