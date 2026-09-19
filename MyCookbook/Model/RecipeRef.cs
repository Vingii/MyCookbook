namespace MyCookbook.Model;

/// <summary>A lightweight recipe reference, used when resolving wiki-style links between recipes.</summary>
public record RecipeRef(Guid Guid, string Name);

/// <summary>A step description together with the recipe it belongs to.</summary>
public record StepDescriptionRef(Guid RecipeGuid, string RecipeName, string Description);
