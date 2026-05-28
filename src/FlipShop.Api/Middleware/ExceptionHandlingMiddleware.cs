using FlipShop.Application.Common;
using System.Net;
using System.Text.Json;

namespace FlipShop.Api.Middleware;

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
            logger.LogError(ex, "Unhandled API exception");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            var response = ApiResponse<object>.Fail("Unexpected server error");
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
