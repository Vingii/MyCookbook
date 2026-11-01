using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Test.Common
{
    public class TestingWebAppFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint> where TEntryPoint : class
    {
        static TestingWebAppFactory()
        {
            // Every test host would otherwise watch appsettings*.json for changes. Each watcher is an inotify
            // instance, and with test classes running in parallel the Linux default limit (128 per user, also on
            // CI runners) is exceeded, failing host startup with "The configured user limit (128) on the number
            // of inotify instances has been reached". Test hosts never need to reload configuration.
            Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var dbGuid = Guid.NewGuid();

            builder.ConfigureServices(services =>
            {
                var cookbookDbContextDescriptor = services.SingleOrDefault(
                   d => d.ServiceType ==
                       typeof(DbContextOptions<CookbookDatabaseContext>));

                if (cookbookDbContextDescriptor != null)
                {
                    services.Remove(cookbookDbContextDescriptor);
                }

                var dbContextFactoryDescriptor = services.SingleOrDefault(
                    d => d.ServiceType ==
                        typeof(IDbContextFactory<CookbookDatabaseContext>));

                if (dbContextFactoryDescriptor != null)
                {
                    services.Remove(dbContextFactoryDescriptor);
                }

                services.AddDbContextFactory<CookbookDatabaseContext>(options =>
                {
                    options.UseInMemoryDatabase($"InMemoryCookbookDbForTesting{dbGuid}");
                    options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                });

                services.AddTransient(_ => new TestDbContextFactory());
            });

            builder.UseEnvironment("Development");
        }
    }
}