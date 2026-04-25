using ZeroGallery.Shared.Models;
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
            ReadDataFromContext(context);
            await _next(context);
        }

        private static void ReadDataFromContext(HttpContext context)
        {
            var uploadToken = (context?.Request?.Headers?.ContainsKey(UPLOAD_TOKEN_NAME) ?? false)
                ? context.Request.Headers[UPLOAD_TOKEN_NAME].FirstOrDefault()
                : string.Empty;

            var accessToken = (context?.Request?.Headers?.ContainsKey(ACCESS_TOKEN_NAME) ?? false)
                ? context.Request.Headers[ACCESS_TOKEN_NAME].FirstOrDefault()
                : string.Empty;

            var opContext = new OperationContext(Timestamp.UtcNow);
            opContext.SetTokens(accessToken, uploadToken);

            context!.Items["op_context"] = opContext;
        }
    }
}
