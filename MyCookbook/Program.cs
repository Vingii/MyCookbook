using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using MudBlazor.Services;
using MyCookbook.Components;
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

            string lokiUri = builder.Configuration["LOKI_URI"] ?? "http://localhost:3100";

            try
            {
                // Add services to the container.
                var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(connectionString, providerOptions => providerOptions.EnableRetryOnFailure()));
                builder.Services.AddDatabaseDeveloperPageExceptionFilter();

                builder.Services.AddRazorPages();
                builder.Services.AddRazorComponents()
                    .AddInteractiveServerComponents();
                builder.Services.AddServerSideBlazor();
                builder.Services.AddMudServices();
                builder.Services.AddHttpClient();
                builder.Services.AddHttpContextAccessor();

                builder.Services.AddSingleton<ChangelogService>();
                builder.Services.AddFeedbackProvider(config);
                builder.Services.AddSingleton<ILanguageDictionary, MemoryLanguageDictionary>();
                builder.Services.AddCultureLocalization(config);
                builder.Services.AddAuth(config, builder.Environment.IsDevelopment());
                builder.Services.AddScoped<UserSettings>();

                Log.Logger = BuildLogger(config);
                builder.Host.UseSerilog(Log.Logger);

                builder.Services.AddTransient<CookbookDatabaseService>();

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

                using (var scope = app.Services.CreateScope())
                {
                    var services = scope.ServiceProvider;
                    try
                    {
                        var cookbookDbContext = services.GetRequiredService<CookbookDatabaseContext>();
                        if (cookbookDbContext.Database.IsRelational())
                        {
                            cookbookDbContext.Database.Migrate();
                        }
                        var applicationDbContext = services.GetRequiredService<ApplicationDbContext>();
                        if (applicationDbContext.Database.IsRelational())
                        {
                            applicationDbContext.Database.Migrate();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "An error occurred while migrating the database.");
                        throw; 
                    }
                }

                if (app.Environment.IsDevelopment())
                {
                    app.UseMigrationsEndPoint();
                }
                else
                {
                    app.UseExceptionHandler("/Error");
                    app.UseHsts();
                }

                app.UseRequestLocalization();

                app.UseSerilogRequestLogging();

                app.UseRouting();

                app.UseStaticFiles();

                app.UseAuthentication();
                app.UseMiddleware<HeaderAuthenticationMiddleware>();
                app.UseAuthorization();
                app.UseAntiforgery();

                app.MapRazorComponents<App>()
                    .AddInteractiveServerRenderMode();
                app.MapBlazorHub().WithOrder(-1);

                app.MapControllers();
                app.MapMethods("/", [HttpMethods.Head], () => Results.StatusCode(200));

                app.MapAdditionalIdentityEndpoints();

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