using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Services;

namespace MyCookbook.Test.Services;

// MC-77: combined ingredient list of the planned recipes.
public class IngredientSummaryBuilderTests
{
    private static Recipe MakeRecipe(string name, params (string Name, string? Amount)[] ingredients)
    {
        var recipe = new Recipe { Name = name };
        var order = 1;
        foreach (var (n, a) in ingredients)
            recipe.Ingredients.Add(new Ingredient { Name = n, Amount = a!, Order = order++ });
        return recipe;
    }

    [Theory]
    [InlineData("200 g", 200, "g")]
    [InlineData("1,5 kg", 1.5, "kg")]
    [InlineData("1.5 kg", 1.5, "kg")]
    [InlineData("3", 3, "")]
    [InlineData("2ks", 2, "ks")]
    [InlineData("1/2 hrnku", 0.5, "hrnku")]
    [InlineData("½ lžičky", 0.5, "lžičky")]
    [InlineData("  2 lžíce  ", 2, "lžíce")]
    public void ParseAmount_Numeric(string amount, double quantity, string unit)
    {
        var parsed = IngredientSummaryBuilder.ParseAmount(amount);
        Assert.NotNull(parsed);
        Assert.Equal((decimal)quantity, parsed.Value.Quantity);
        Assert.Equal(unit, parsed.Value.Unit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("špetka")]
    [InlineData("dle chuti")]
    [InlineData("1/0 g")]
    public void ParseAmount_NotNumeric_ReturnsNull(string? amount)
    {
        Assert.Null(IngredientSummaryBuilder.ParseAmount(amount));
    }

    [Fact]
    public void Build_MergesSameNameIgnoringCaseAndSumsPerUnit()
    {
        var result = IngredientSummaryBuilder.Build([
            MakeRecipe("Gyros", ("Cibule", "1 ks"), ("Sůl", "špetka"), ("Mouka", "200 g")),
            MakeRecipe("Guláš", (" cibule ", "2 KS"), ("Mouka", "1 kg"), ("Sůl", null)),
        ]);

        Assert.Equal(["Cibule", "Sůl", "Mouka"], result.Select(r => r.Name));

        var onion = result[0];
        var total = Assert.Single(onion.Totals);
        Assert.Equal(3m, total.Quantity);
        Assert.Equal("ks", total.Unit);
        Assert.Equal(["Gyros", "Guláš"], onion.Recipes);

        var salt = result[1];
        Assert.Empty(salt.Totals);
        Assert.Equal(["špetka"], salt.OtherAmounts);
        Assert.Equal(["Gyros", "Guláš"], salt.Recipes);

        var flour = result[2];
        Assert.Equal([(200m, "g"), (1m, "kg")], flour.Totals.Select(t => (t.Quantity, t.Unit)));
    }

    [Fact]
    public void Build_SameRecipeTwice_CountsAmountTwiceButListsRecipeOnce()
    {
        var gyros = MakeRecipe("Gyros", ("Vepřové", "500 g"));
        var result = IngredientSummaryBuilder.Build([gyros, gyros]);

        var pork = Assert.Single(result);
        Assert.Equal(1000m, Assert.Single(pork.Totals).Quantity);
        Assert.Equal(["Gyros"], pork.Recipes);
    }

    [Fact]
    public void Build_SkipsBlankNames()
    {
        var result = IngredientSummaryBuilder.Build([MakeRecipe("X", ("  ", "1 g"), ("Pepř", ""))]);

        var pepper = Assert.Single(result);
        Assert.Equal("Pepř", pepper.Name);
        Assert.Empty(pepper.Totals);
        Assert.Empty(pepper.OtherAmounts);
    }
}
