using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.StaticFiles;
using MyCookbook.Data;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Services;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

namespace MyCookbook
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var config = builder.Configuration;

            try
            {
                // Add services to the container.
                var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

                builder.Services.AddControllers()
                    .AddJsonOptions(o =>
                    {
                        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    });
                builder.Services.AddHttpClient();
                builder.Services.AddHttpContextAccessor();

                builder.Services.AddSingleton<ChangelogService>();
                builder.Services.AddFeedbackProvider(config);
                builder.Services.AddApiKeyAuth();

                Log.Logger = BuildLogger(config);
                builder.Host.UseSerilog(Log.Logger);

                builder.Services.AddTransient<CookbookDatabaseService>();
                builder.Services.AddHostedService<DailyLastCookedWorker>();

                builder.Services.AddDbContextFactory<CookbookDatabaseContext>(options =>
                    options.UseSqlServer(connectionString + ";MultipleActiveResultSets=True", providerOptions => providerOptions.EnableRetryOnFailure()));

                builder.Services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders =
                        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    options.KnownNetworks.Clear();
                    options.KnownProxies.Clear();
                });

                var app = builder.Build();

                app.UseForwardedHeaders();

                app.Use((context, next) =>
                {
                    if (context.Request.Headers.TryGetValue("X-Forwarded-Proto", out StringValues proto))
                    {
                        context.Request.Scheme = proto;
                    }

                    return next();
                });

                var migrationAttempts = 0;
                while (true)
                {
                    using var scope = app.Services.CreateScope();
                    var services = scope.ServiceProvider;
                    try
                    {
                        var cookbookDbContext = services.GetRequiredService<CookbookDatabaseContext>();
                        if (cookbookDbContext.Database.IsRelational())
                            cookbookDbContext.Database.Migrate();
                        break;
                    }
                    catch (Exception ex) when (migrationAttempts++ < 5)
                    {
                        Log.Warning(ex, "Migration attempt {Attempt} failed, retrying in 5s...", migrationAttempts);
                        Thread.Sleep(5000);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "An error occurred while migrating the database.");
                        throw;
                    }
                }

                if (!app.Environment.IsDevelopment())
                {
                    app.UseExceptionHandler("/error");
                    app.UseHsts();
                }

                app.UseSerilogRequestLogging();

                app.UseRouting();

                var provider = new FileExtensionContentTypeProvider();
                provider.Mappings[".jsonl"] = "application/jsonlines+json"; 
                app.UseStaticFiles(new StaticFileOptions
                {
                    ContentTypeProvider = provider
                });

                app.UseAuthentication();
                app.UseAuthorization();

                // Persist display name so ?user= share links can use it instead of raw UID
                app.Use(async (context, next) =>
                {
                    var uid = context.User.Identity?.Name;
                    var displayName = context.User.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value;
                    if (uid != null && displayName != null)
                    {
                        _ = Task.Run(async () =>
                        {
                            using var scope = context.RequestServices.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<CookbookDatabaseService>();
                            await db.UpdateUserPreference("DisplayName", displayName, uid);
                        });
                    }
                    await next();
                });

                app.MapControllers();
                app.MapMethods("/", [HttpMethods.Head], () => Results.StatusCode(200));
                app.MapFallbackToFile("index.html");

                app.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HOST TERMINATED UNEXPECTEDLY: {ex}");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static Serilog.ILogger BuildLogger(IConfiguration config)
        {
            var grafanaSections = config.GetSection("Serilog").GetSection("WriteTo")
                .GetChildren().Where(x => x.GetSection("Name").Value == "GrafanaLoki");

            var grafanaSettings = new Dictionary<string, string>();
            foreach (var section in grafanaSections)
            {
                var basePath = section.Path;
                grafanaSettings[$"{basePath}:args:uri"] = config["Grafana:Url"];
            }

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", true)
                .AddInMemoryCollection(grafanaSettings)
                .Build();

            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            return logger;
        }
    }
}