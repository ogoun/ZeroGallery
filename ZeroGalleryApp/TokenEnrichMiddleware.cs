using Microsoft.AspNetCore.Mvc.Controllers;
using System.Net;
using ZeroGallery.Shared.Models;
using ZeroLevel;
using ZeroLevel.Services.Utils;

namespace ZeroGalleryApp
{
    public static class TokenEnrichMiddlewareExtension
    {
        public static IApplicationBuilder UseTokenEnrichMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TokenEnrichMiddleware>();
        }
    }

    public class TokenEnrichMiddleware
    {
        private const string UPLOAD_TOKEN_NAME = "X-ZERO-UPLOAD-TOKEN";
        private const string ACCESS_TOKEN_NAME = "X-ZERO-ACCESS-TOKEN";
        private readonly RequestDelegate _next;

        public TokenEnrichMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            await ReadDataFromContext(context);
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task ReadDataFromContext(HttpContext context)
        {
            var uploadToken = (context?.Request?.Headers?.ContainsKey(UPLOAD_TOKEN_NAME) ?? false) 
                ? context.Request.Headers[UPLOAD_TOKEN_NAME].FirstOrDefault()
                : string.Empty;

            var accessToken = (context?.Request?.Headers?.ContainsKey(ACCESS_TOKEN_NAME) ?? false)
                ? context.Request.Headers[ACCESS_TOKEN_NAME].FirstOrDefault()
                : string.Empty;

            var opContext = new OperationContext(Timestamp.UtcNow);
            opContext.SetTokens(accessToken, uploadToken);
            
            context.Items["op_context"] = opContext;
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            var controllerActionDescriptor = context?.GetEndpoint()?.Metadata?.GetMetadata<ControllerActionDescriptor>();
            var controllerName = controllerActionDescriptor?.ControllerName ?? string.Empty;
            var actionName = controllerActionDescriptor?.ActionName ?? string.Empty;

            Log.Error(ex, $"[{controllerName}.{actionName}]");

            if (context != null)
            {
                context.Response.StatusCode = ex switch
                {
                    KeyNotFoundException or FileNotFoundException => (int)HttpStatusCode.NotFound,
                    UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                    _ => (int)HttpStatusCode.BadRequest,
                };

                await context.Response.WriteAsync(ex.Message ?? "Error");
            }
        }
    }
}
