namespace MyCookbook.Api.Dto;

/// <summary>One ingredient of the planner's combined shopping list.</summary>
public class IngredientSummaryDto
{
    public string Name { get; set; } = "";
    /// <summary>Numeric amounts summed per unit (e.g. 300 g, 2 ks). Unit is "" for bare numbers.</summary>
    public List<IngredientQuantityDto> Totals { get; set; } = [];
    /// <summary>Amounts that are not a number with a unit (e.g. "špetka"), listed as written.</summary>
    public List<string> OtherAmounts { get; set; } = [];
    /// <summary>Names of the planned recipes using this ingredient.</summary>
    public List<string> Recipes { get; set; } = [];
}

public class IngredientQuantityDto
{
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
}
