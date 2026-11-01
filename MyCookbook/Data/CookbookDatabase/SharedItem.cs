namespace MyCookbook.Data.CookbookDatabase
{
    public enum SharedItemKind
    {
        Recipe,
        Cookbook,
    }

    /// <summary>
    /// A recipe or whole cookbook that someone shared with <see cref="UserName"/>, remembered when it was
    /// opened so it can be found again later (MC-71). Entries are live references, not copies: they always
    /// open the owner's current version, and show as unavailable once it is deleted or no longer shared.
    /// </summary>
    public class SharedItem
    {
        public int Id { get; set; }

        /// <summary>The viewer the item was shared with.</summary>
        public string UserName { get; set; } = "";

        public SharedItemKind Kind { get; set; }

        public string OwnerUserName { get; set; } = "";

        /// <summary>For <see cref="SharedItemKind.Recipe"/>. No foreign key, so deleting the recipe leaves the entry.</summary>
        public Guid? RecipeGuid { get; set; }

        /// <summary>For <see cref="SharedItemKind.Cookbook"/>: the token from the share link, needed to reopen it.</summary>
        public string? ShareToken { get; set; }

        public DateTime LastOpened { get; set; }

        public bool Pinned { get; set; }
    }
}
