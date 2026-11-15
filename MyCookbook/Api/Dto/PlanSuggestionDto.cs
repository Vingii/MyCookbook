namespace MyCookbook.Api.Dto;

/// <summary>A recipe suggested for a day of the planner (GET /api/planner/suggestions).</summary>
public class PlanSuggestionDto
{
    public Guid RecipeGuid { get; set; }
    public string RecipeName { get; set; } = "";
    public string Category { get; set; } = "";
    public double Score { get; set; }
    /// <summary>Side dishes usually planned with this recipe; adding the suggestion adds them too.</summary>
    public List<PlanSuggestionCompanionDto> Companions { get; set; } = [];
    /// <summary>Up to two reasons (not shown in the UI).</summary>
    public List<PlanSuggestionReasonDto> Reasons { get; set; } = [];
}

public class PlanSuggestionCompanionDto
{
    public Guid RecipeGuid { get; set; }
    public string RecipeName { get; set; } = "";
}

/// <summary>
/// Why a recipe is suggested. <see cref="Code"/> is one of: weekdayCategory (Weekday, Category),
/// weekdayTag (Weekday, Tag), seasonCategory (Season, Category), seasonTag (Season, Tag), weekdayDuration / weekendDuration (Duration: quick|medium|long),
/// often, neverCooked, notCookedFor (Days), favorite. Weekday is 0 = Sunday … 6 = Saturday.
/// </summary>
public class PlanSuggestionReasonDto
{
    public string Code { get; set; } = "";
    public int? Days { get; set; }
    public int? Weekday { get; set; }
    /// <summary>winter, spring, summer or autumn.</summary>
    public string? Season { get; set; }
    public string? Category { get; set; }
    public string? Tag { get; set; }
    public string? Duration { get; set; }
}
