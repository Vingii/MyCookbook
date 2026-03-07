using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Test.Common
{
    public class TestingWebAppFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint> where TEntryPoint : class
    {
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
                });

                services.AddTransient(_ => new TestDbContextFactory());
            });

            builder.UseEnvironment("Development");
        }
    }
}