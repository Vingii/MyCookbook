using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyCookbook.Migrations
{
    /// <summary>
    /// Sharing a whole collection with a share token (Settings → Share Access) was removed; recipes are shared one
    /// by one with their share link. Drops the stored tokens so none of them lingers in the database.
    /// </summary>
    public partial class RemoveShareTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "UserPreferences" WHERE "Key" = 'ShareToken';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The tokens are gone for good; nothing reads them any more.
        }
    }
}
