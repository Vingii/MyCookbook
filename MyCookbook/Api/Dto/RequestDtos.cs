namespace MyCookbook.Api.Dto;

public class CreateRecipeRequest
{
    public string Name { get; set; } = "";
    public string? Category { get; set; }
    public int? Duration { get; set; }
    public int Servings { get; set; } = 1;
}

public class UpdateRecipeRequest
{
    public string Name { get; set; } = "";
    public string? Category { get; set; }
    public int? Duration { get; set; }
    public int Servings { get; set; } = 1;
}

public class CreateIngredientRequest
{
    public string Name { get; set; } = "";
    public string? Amount { get; set; }
}

public class UpdateIngredientRequest
{
    public string Name { get; set; } = "";
    public string? Amount { get; set; }
}

public class CreateStepRequest
{
    public string Description { get; set; } = "";
    public int? DurationSeconds { get; set; }
    public string StepType { get; set; } = "Active";
}

public class UpdateStepRequest
{
    public string Description { get; set; } = "";
    public int? DurationSeconds { get; set; }
    public string StepType { get; set; } = "Active";
}

public class CreatePlannedRecipeRequest
{
    public Guid RecipeGuid { get; set; }
    public string Date { get; set; } = "";
    public bool FromFridge { get; set; }
}

public class UpdatePlannedRecipeRequest
{
    public string Date { get; set; } = "";
    public bool FromFridge { get; set; }
}

public class AddTagRequest
{
    public string Name { get; set; } = "";
}
