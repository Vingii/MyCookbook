using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyCookbook.Data;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Test.Common;

namespace MyCookbook.Test
{
    public class ProgramTests : IClassFixture<TestingWebAppFactory<Program>>
    {
        private readonly TestingWebAppFactory<Program> _factory;

        public ProgramTests(TestingWebAppFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public void Application_CanStart()
        {
            var client = _factory.CreateClient();
            Assert.NotNull(client);
        }

        [Fact]
        public void Services_AreRegistered()
        {
            var scope = _factory.Services.CreateScope();

            var cookbookService = scope.ServiceProvider.GetService<CookbookDatabaseService>();
            Assert.NotNull(cookbookService);

            var applicationDbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
            Assert.NotNull(applicationDbContext);

            var cookbookDbContextFactory = scope.ServiceProvider.GetService<IDbContextFactory<CookbookDatabaseContext>>();
            Assert.NotNull(cookbookDbContextFactory);
        }
    }
}