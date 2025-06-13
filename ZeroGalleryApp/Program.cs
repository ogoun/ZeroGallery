using Microsoft.Extensions.FileProviders;
using System.Net;
using ZeroGallery.Shared.Services;
using ZeroLevel;

namespace ZeroGalleryApp
{
    public class Program
    {
        const int PORT = 80;
        public static void Main(string[] args)
        {
            Log.AddConsoleLogger();
            
            var config = Configuration.ReadFromIniFile("config.ini").Bind<AppConfig>();

            var builder = WebApplication.CreateBuilder(args);

            builder.WebHost.ConfigureKestrel((context, serverOptions) =>
            {
                var kestrelSection = context.Configuration.GetSection("Kestrel");
                serverOptions.Configure(kestrelSection)
                    .Endpoint("HTTP", listenOptions =>
                    {
                        serverOptions.Listen(IPAddress.Any, PORT);
                    });
            });
            builder.WebHost.UseKestrel().UseUrls($"http://0.0.0.0:{PORT}");

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .WithMethods("GET")
                          .AllowAnyHeader();
                });
            });

            builder.Services.AddSingleton(config);

            builder.Services.AddSingleton<DataStorage>();

            builder.Services.AddControllers();

            var app = builder.Build();

            app.UseCors("AllowAll");

            app.UseTokenEnrichMiddleware();

            var fileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "web"));
            DefaultFilesOptions defoptions = new();
            defoptions.FileProvider = fileProvider;
            defoptions.DefaultFileNames.Clear();
            defoptions.DefaultFileNames.Add("index.html");
            app.UseDefaultFiles(defoptions);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = fileProvider,
                RequestPath = ""
            });

            app.MapControllers();

            app.Run();
        }
    }
}
