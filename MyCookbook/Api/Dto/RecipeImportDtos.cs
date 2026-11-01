namespace MyCookbook.Api.Dto;

// MC-69: importing a recipe from a web page with AI.

public class RecipeImportStatusDto
{
    /// <summary>False when no LLM is configured; the UI then hides the feature.</summary>
    public bool Enabled { get; set; }
}

public class RecipeImportPreviewRequest
{
    public string Url { get; set; } = "";

    /// <summary>The app language ("cs" or "en") the suggested version is translated into.</summary>
    public string Language { get; set; } = "cs";
}

public class RecipeImportPreviewDto
{
    public string SourceUrl { get; set; } = "";
    public List<RecipeImportVariantDto> Variants { get; set; } = [];
}

/// <summary>One version of the extracted recipe the user can choose from.</summary>
public class RecipeImportVariantDto
{
    /// <summary>"suggested" (translated and adapted to the user's conventions) or "original" (as on the page).</summary>
    public string Key { get; set; } = "";
    public RecipeDraftDto Recipe { get; set; } = new();
}

/// <summary>A complete recipe that has not been saved yet.</summary>
public class RecipeDraftDto
{
    public string Name { get; set; } = "";
    public string? Category { get; set; }
    public int Servings { get; set; } = 1;
    public int? Duration { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<RecipeDraftIngredientDto> Ingredients { get; set; } = [];
    public List<RecipeDraftStepDto> Steps { get; set; } = [];
}

public class RecipeDraftIngredientDto
{
    public string Name { get; set; } = "";
    public string? Amount { get; set; }
}

public class RecipeDraftStepDto
{
    public string Description { get; set; } = "";
    public int? DurationMinutes { get; set; }
    public string StepType { get; set; } = "Active";
}
