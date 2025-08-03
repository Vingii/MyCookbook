using Microsoft.EntityFrameworkCore;
using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Test.Common
{
    public class TestDbContextFactory : IDbContextFactory<CookbookDatabaseContext>
    {
        private readonly CookbookDatabaseContext _context;

        public TestDbContextFactory()
        {
            var options = new DbContextOptionsBuilder<CookbookDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new CookbookDatabaseContext(options);
            _context.Database.EnsureCreated();
        }

        public CookbookDatabaseContext CreateDbContext() => _context;
    }
}