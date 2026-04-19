using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace OilChangePOS.API.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
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
        var env = context.RequestServices.GetService<IHostEnvironment>();
        var isDevelopment = env?.IsDevelopment() == true;

        var (status, message, detail) = ex switch
        {
            InvalidOperationException => (HttpStatusCode.BadRequest, ex.Message, (string?)null),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, ex.Message, (string?)null),
            ArgumentException => (HttpStatusCode.BadRequest, ex.Message, (string?)null),
            KeyNotFoundException => (HttpStatusCode.NotFound, ex.Message, (string?)null),
            DbUpdateException dbe => MapDbUpdateException(dbe, isDevelopment),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.", (string?)null)
        };

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json; charset=utf-8";

        object payload = isDevelopment && detail is { Length: > 0 }
            ? new { error = message, type = ex.GetType().Name, detail }
            : new { error = message, type = ex.GetType().Name };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static (HttpStatusCode status, string message, string? detail) MapDbUpdateException(
        DbUpdateException ex,
        bool isDevelopment)
    {
        const string fallback =
            "فشل حفظ البيانات في القاعدة. غالباً لم تُطبَّق ترحيلات قاعدة البيانات (migration) أو قيمة مرتبطة غير صالحة (فرع/صنف/مستخدم). راجع سجل الخادم للتفاصيل.";

        if (ex.GetBaseException() is not SqlException sql)
            return (HttpStatusCode.BadRequest, fallback, isDevelopment ? ex.GetBaseException().Message : null);

        var userMessage = sql.Number switch
        {
            208 => "جدول أو كائن غير موجود في قاعدة البيانات. نفِّذ ترحيلات Entity Framework على نفس قاعدة البيانات التي يتصل بها التطبيق (مثلاً: dotnet ef database update).",
            547 => "البيانات لا تطابق علاقات قاعدة البيانات (مفتاح أجنبي): تأكد أن معرّف الفرع/الصنف/المستخدم موجود فعلاً في الجداول المرتبطة.",
            2627 or 2601 => "تعارض مع قيد تفرّد في القاعدة (لا يمكن تكرار نفس القيمة).",
            _ => fallback
        };

        var detail = isDevelopment ? sql.Message : null;
        return (HttpStatusCode.BadRequest, userMessage, detail);
    }
}
