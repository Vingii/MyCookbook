namespace MyCookbook.Api.Dto;

/// <summary>One of the user's cookbooks (MC-80).</summary>
public class CookbookDto
{
    public int Id { get; set; }

    /// <summary>Empty for the default cookbook until the user names it; the UI shows a localized name instead.</summary>
    public string Name { get; set; } = "";

    public bool ShowInPlanner { get; set; }

    public bool ShowDuration { get; set; }
    public bool ShowLastCooked { get; set; }
    public bool ShowServings { get; set; }
    public bool ShowStepTimeline { get; set; }

    /// <summary>The first cookbook; recipes that don't name a cookbook belong to it.</summary>
    public bool IsDefault { get; set; }

    public int RecipeCount { get; set; }
}

public class SaveCookbookRequest
{
    public string Name { get; set; } = "";
    public bool ShowInPlanner { get; set; } = true;

    // Display settings (MC-81). Omitted: shown on create, unchanged on update.
    public bool? ShowDuration { get; set; }
    public bool? ShowLastCooked { get; set; }
    public bool? ShowServings { get; set; }
    public bool? ShowStepTimeline { get; set; }
}

/// <summary>What a recipe's cookbook shows (MC-81), sent with a recipe's detail.</summary>
public class RecipeDisplayDto
{
    public bool Duration { get; set; } = true;
    public bool LastCooked { get; set; } = true;
    public bool Servings { get; set; } = true;
    public bool StepTimeline { get; set; } = true;
}
