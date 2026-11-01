namespace MyCookbook.Data.CookbookDatabase
{
    /// <summary>
    /// A recipe that someone shared with <see cref="UserName"/>, remembered when it was opened so it can be
    /// found again later (MC-71). Entries are live references, not copies: they always open the owner's
    /// current version, and show as unavailable once it is deleted.
    /// </summary>
    public class SharedItem
    {
        public int Id { get; set; }

        /// <summary>The viewer the recipe was shared with.</summary>
        public string UserName { get; set; } = "";

        public string OwnerUserName { get; set; } = "";

        /// <summary>No foreign key, so deleting the recipe leaves the entry (shown as unavailable).</summary>
        public Guid RecipeGuid { get; set; }

        public DateTime LastOpened { get; set; }

        public bool Pinned { get; set; }
    }
}
