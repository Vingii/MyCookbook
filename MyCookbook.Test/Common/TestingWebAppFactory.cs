using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using MyCookbook.Data;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Services;

namespace MyCookbook.Test.Common
{
    public class TestingWebAppFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint> where TEntryPoint : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var dbGuid = Guid.NewGuid();

            builder.ConfigureServices(services =>
            {
                var dbContextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType ==
                        typeof(DbContextOptions<ApplicationDbContext>));

                if (dbContextDescriptor != null)
                {
                    services.Remove(dbContextDescriptor);
                }

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

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase($"InMemoryApplicationDbForTesting{dbGuid}");
                });

                services.AddDbContextFactory<CookbookDatabaseContext>(options =>
                {
                    options.UseInMemoryDatabase($"InMemoryCookbookDbForTesting{dbGuid}");
                });

                services.AddTransient(_ => new TestDbContextFactory());

                var dictionaryServiceDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ILanguageDictionary));
                if (dictionaryServiceDescriptor != null)
                {
                    services.Remove(dictionaryServiceDescriptor);
                }

                var mockDictionary = new Mock<ILanguageDictionary>();

                mockDictionary
                    .Setup(d => d.WordInflections(It.IsAny<string>()))
                    .Returns((string word) =>
                    {
                        if (string.IsNullOrEmpty(word)) return new List<string>();
                        return new List<string> { word, word + "s" };
                    });

                services.AddSingleton(mockDictionary.Object);
            });

            builder.UseEnvironment("Development");
        }
    }
}