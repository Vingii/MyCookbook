using System.Globalization;
using System.Text.RegularExpressions;
using MyCookbook.Api.Dto;
using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Services;

/// <summary>
/// Combines the ingredients of several recipes into one shopping list: ingredients with the same name
/// (ignoring case and surrounding spaces) are merged, and amounts like "200 g" or "1,5 kg" are summed per unit.
/// Units are not converted (g and kg stay separate).
/// </summary>
public static partial class IngredientSummaryBuilder
{
    [GeneratedRegex(@"^(?<num>\d+/\d+|\d+(?:[.,]\d+)?|½|¼|¾)\s*(?<unit>.*)$")]
    private static partial Regex AmountRegex();

    public static List<IngredientSummaryDto> Build(IEnumerable<Recipe> recipes)
    {
        var items = new Dictionary<string, Accumulator>(StringComparer.OrdinalIgnoreCase);
        var order = new List<Accumulator>();

        foreach (var recipe in recipes)
        {
            foreach (var ingredient in recipe.Ingredients.OrderBy(i => i.Order))
            {
                var name = (ingredient.Name ?? "").Trim();
                if (name.Length == 0) continue;

                if (!items.TryGetValue(name, out var acc))
                {
                    acc = new Accumulator(name);
                    items[name] = acc;
                    order.Add(acc);
                }
                acc.Add(ingredient.Amount, recipe.Name);
            }
        }

        return order.Select(a => a.ToDto()).ToList();
    }

    /// <summary>Parses "200 g", "1,5 kg", "1/2 hrnku" or "3" into a quantity and a unit. Null if not numeric.</summary>
    public static (decimal Quantity, string Unit)? ParseAmount(string? amount)
    {
        if (string.IsNullOrWhiteSpace(amount)) return null;
        var match = AmountRegex().Match(amount.Trim());
        if (!match.Success) return null;

        var num = match.Groups["num"].Value;
        decimal quantity;
        switch (num)
        {
            case "½": quantity = 0.5m; break;
            case "¼": quantity = 0.25m; break;
            case "¾": quantity = 0.75m; break;
            default:
                if (num.Contains('/'))
                {
                    var parts = num.Split('/');
                    var denominator = decimal.Parse(parts[1], CultureInfo.InvariantCulture);
                    if (denominator == 0) return null;
                    quantity = decimal.Parse(parts[0], CultureInfo.InvariantCulture) / denominator;
                }
                else
                {
                    quantity = decimal.Parse(num.Replace(',', '.'), CultureInfo.InvariantCulture);
                }
                break;
        }

        return (quantity, match.Groups["unit"].Value.Trim());
    }

    private sealed class Accumulator(string name)
    {
        // Unit (case-insensitive) → running total; keeps the first spelling of the unit.
        private readonly Dictionary<string, IngredientQuantityDto> _totals = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<IngredientQuantityDto> _totalsOrder = [];
        private readonly List<string> _other = [];
        private readonly List<string> _recipes = [];

        public void Add(string? amount, string recipeName)
        {
            if (!_recipes.Contains(recipeName)) _recipes.Add(recipeName);

            if (string.IsNullOrWhiteSpace(amount)) return;
            var parsed = ParseAmount(amount);
            if (parsed is not { } p)
            {
                _other.Add(amount.Trim());
                return;
            }

            if (!_totals.TryGetValue(p.Unit, out var total))
            {
                total = new IngredientQuantityDto { Unit = p.Unit };
                _totals[p.Unit] = total;
                _totalsOrder.Add(total);
            }
            total.Quantity += p.Quantity;
        }

        public IngredientSummaryDto ToDto() => new()
        {
            Name = name,
            Totals = _totalsOrder,
            OtherAmounts = _other,
            Recipes = _recipes,
        };
    }
}
