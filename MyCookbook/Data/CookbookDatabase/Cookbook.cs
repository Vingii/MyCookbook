#nullable disable

namespace MyCookbook.Data.CookbookDatabase
{
    /// <summary>
    /// One of a user's recipe collections (MC-80). Every user has at least one: the first (lowest Id) is their
    /// default cookbook, which also holds every recipe whose <see cref="Recipe.CookbookId"/> is null.
    /// </summary>
    public class Cookbook
    {
        public int Id { get; set; }
        public string UserName { get; set; }

        /// <summary>Empty for a default cookbook nobody has named yet; the UI shows a localized name for it.</summary>
        public string Name { get; set; } = "";

        /// <summary>Whether its recipes are offered when planning (the add dialog and suggestions).</summary>
        public bool ShowInPlanner { get; set; } = true;

        public virtual ICollection<Recipe> Recipes { get; set; }
    }
}
