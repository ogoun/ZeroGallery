using Microsoft.Extensions.FileProviders;
using System.Net;
using ZeroGallery.Shared.Services;
using ZeroGallery.Shared.Services.DB;
using ZeroLevel;
using IConfiguration = ZeroLevel.IConfiguration;

namespace ZeroGalleryApp
{
    public class Program
    {
        const int DEFAULT_PORT = 80;

        private static void UpdateConfigFromEnvironments(AppConfig config, IConfiguration env)
        {
            if (env.Contains("api_master_token"))
            {
                var val = env["api_master_token"].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(val) == false)
                {
                    config.api_master_token = val;
                }
            }

            if (env.Contains("api_write_token"))
            {
                var val = env["api_write_token"].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(val) == false)
                {
                    config.api_write_token = val;
                }
            }

            if (env.Contains("data_folder"))
            {
                var val = env["data_folder"].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(val) == false)
                {
                    config.data_folder = val;
                }
            }

            if (env.Contains("db_path"))
            {
                var val = env["db_path"].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(val) == false)
                {
                    config.db_path = val;
                }
            }

            if (env.Contains("port"))
            {
                config.port = env.First<int>("port");
            }
        }

        public static void Main(string[] args)
        {
            Log.AddConsoleLogger();

            var config = Configuration.ReadFromIniFile("config.ini").Bind<AppConfig>();
            var env_config = Configuration.ReadFromEnvironmentVariables();
            if (env_config != null)
            {
                UpdateConfigFromEnvironments(config, env_config);
            }

            var port = config.port > 0 ? config.port : DEFAULT_PORT;

            var builder = WebApplication.CreateBuilder(args);

            builder.WebHost.ConfigureKestrel((context, serverOptions) =>
            {
                var kestrelSection = context.Configuration.GetSection("Kestrel");
                serverOptions.Configure(kestrelSection)
                    .Endpoint("HTTP", listenOptions =>
                    {
                        serverOptions.Listen(IPAddress.Any, port);
                    });
            });
            builder.WebHost.UseKestrel().UseUrls($"http://0.0.0.0:{port}");

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .WithMethods("GET")
                          .AllowAnyHeader();
                });
            });

            builder.Services.AddSingleton(new DataAlbumRepository(config.db_path));

            builder.Services.AddSingleton(new DataRecordRepository(config.db_path));

            builder.Services.AddSingleton<Scavenger>();

            builder.Services.AddSingleton(config);

            builder.Services.AddSingleton<DataStorage>();

            builder.Services.AddControllers();

            var app = builder.Build();

            app.UseCors("AllowAll");

            app.UseTokenEnrichMiddleware();

            app.UseMiddleware<GlobalExceptionMiddleware>();

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
