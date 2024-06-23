using Microsoft.EntityFrameworkCore;
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

            try
            {
                // Add services to the container.
                var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(connectionString, providerOptions => providerOptions.EnableRetryOnFailure()));
                builder.Services.AddDatabaseDeveloperPageExceptionFilter();

                new DbHeartbeatProvider(connectionString).Start();

                builder.Services.AddRazorPages();
                builder.Services.AddRazorComponents()
                    .AddInteractiveServerComponents();
                builder.Services.AddServerSideBlazor();
                builder.Services.AddMudServices();

                var secretsProvider = builder.Services.AddSecretsProvider(builder);
                builder.Services.AddFeedbackProvider(secretsProvider, config);
                builder.Services.AddLanguageDictionary();
                builder.Services.AddCultureLocalization(config);
                builder.Services.AddAuth(secretsProvider, builder.Environment.IsDevelopment());

                Log.Logger = BuildLogger(secretsProvider);
                builder.Host.UseSerilog(Log.Logger);

                builder.Services.AddScoped<CookbookDatabaseService>();

                builder.Services.AddDbContext<CookbookDatabaseContext>(options =>
                    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

                var app = builder.Build();

                // Configure the HTTP request pipeline.
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

                app.UseHttpsRedirection();

                app.UseRouting();
                app.UseAntiforgery();

                app.UseStaticFiles();

                app.UseAuthentication();
                app.UseAuthorization();

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
                Log.Fatal(ex, "Host Terminated Unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static Serilog.ILogger BuildLogger(ISecretsProvider secretsProvider)
        {
            var appSettingsConfiguration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", true)
                .Build();

            var grafanaSections = appSettingsConfiguration.GetSection("Serilog").GetSection("WriteTo")
                .GetChildren().Where(x => x.GetSection("Name").Value == "GrafanaLoki");

            var credentials = CreateGrafanaCredentials(secretsProvider);

            var grafanaLoginSettings = new Dictionary<string, string>();
            foreach (var section in grafanaSections)
            {
                var basePath = section.Path;
                grafanaLoginSettings[$"{basePath}:args:credentials:login"] = credentials.Login;
                grafanaLoginSettings[$"{basePath}:args:credentials:password"] = credentials.Password;
            }

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", true)
                .AddInMemoryCollection(grafanaLoginSettings)
                .Build();

            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            return logger;
        }

        private static LokiCredentials CreateGrafanaCredentials(ISecretsProvider secretsProvider)
        {
            var grafanaToken = secretsProvider.GetSecret("GrafanaKey");
            return new LokiCredentials { Login = "912173", Password = grafanaToken };
        }
    }
}