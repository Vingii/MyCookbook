namespace MyCookbook.Api.Dto;

/// <summary>One of the user's cookbooks (MC-80).</summary>
public class CookbookDto
{
    public int Id { get; set; }

    /// <summary>Empty for the default cookbook until the user names it; the UI shows a localized name instead.</summary>
    public string Name { get; set; } = "";

    public bool ShowInPlanner { get; set; }

    /// <summary>The first cookbook; recipes that don't name a cookbook belong to it.</summary>
    public bool IsDefault { get; set; }

    public int RecipeCount { get; set; }
}

public class SaveCookbookRequest
{
    public string Name { get; set; } = "";
    public bool ShowInPlanner { get; set; } = true;
}
