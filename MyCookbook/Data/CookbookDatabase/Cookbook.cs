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

        // What recipes in this cookbook show (MC-81). Hiding a field keeps its data; it only stops showing it.
        public bool ShowDuration { get; set; } = true;
        public bool ShowLastCooked { get; set; } = true;
        public bool ShowServings { get; set; } = true;
        public bool ShowStepTimeline { get; set; } = true;

        public virtual ICollection<Recipe> Recipes { get; set; }
    }
}
