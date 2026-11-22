using Microsoft.EntityFrameworkCore;
using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Services;

/// <summary>
/// A user's cookbooks (MC-80). The first one (lowest Id) is the default: it is created on demand and also holds
/// every recipe without a <see cref="Recipe.CookbookId"/>, so recipes created before cookbooks existed, or by any
/// code path that doesn't pick a cookbook, still belong somewhere.
/// </summary>
public class CookbookService(IDbContextFactory<CookbookDatabaseContext> contextFactory)
{
    public const int MaxNameLength = 50;

    public enum DeleteResult { Deleted, NotFound, NotEmpty, LastCookbook }

    public record CookbookInfo(Cookbook Cookbook, bool IsDefault, int RecipeCount);

    /// <summary>The user's cookbooks, default first, creating the default one if the user has none yet.</summary>
    public async Task<List<CookbookInfo>> GetAllAsync(string user)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var cookbooks = await LoadOrCreateAsync(context, user);
        var defaultId = cookbooks[0].Id;

        var counts = await context.Recipes
            .Where(r => r.UserName == user)
            .GroupBy(r => r.CookbookId)
            .Select(g => new { CookbookId = g.Key, Count = g.Count() })
            .ToListAsync();
        int CountOf(int id) =>
            counts.Where(c => c.CookbookId == id || (id == defaultId && c.CookbookId == null)).Sum(c => c.Count);

        return cookbooks.Select(c => new CookbookInfo(c, c.Id == defaultId, CountOf(c.Id))).ToList();
    }

    /// <summary>
    /// The id to store on a new or moved recipe: <paramref name="cookbookId"/> if it is one of the user's cookbooks,
    /// null (the default cookbook) if it is omitted. Null result with <c>Found = false</c> means it isn't theirs.
    /// </summary>
    public async Task<(bool Found, int? CookbookId)> ResolveAsync(string user, int? cookbookId)
    {
        if (cookbookId == null) return (true, null);
        await using var context = await contextFactory.CreateDbContextAsync();
        var exists = await context.Cookbooks.AnyAsync(c => c.Id == cookbookId && c.UserName == user);
        return exists ? (true, cookbookId) : (false, null);
    }

    /// <summary>
    /// A predicate for "the recipe is in cookbook <paramref name="cookbookId"/>", or null when that is not one of
    /// the user's cookbooks.
    /// </summary>
    public async Task<Func<Recipe, bool>?> InCookbookAsync(string user, int cookbookId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var cookbooks = await LoadOrCreateAsync(context, user);
        if (cookbooks.All(c => c.Id != cookbookId)) return null;
        var isDefault = cookbooks[0].Id == cookbookId;
        return r => r.CookbookId == cookbookId || (isDefault && r.CookbookId == null);
    }

    /// <summary>A predicate for "the recipe's cookbook is shown in the planner".</summary>
    public async Task<Func<Recipe, bool>> ShownInPlannerAsync(string user)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var cookbooks = await context.Cookbooks
            .Where(c => c.UserName == user)
            .OrderBy(c => c.Id)
            .AsNoTracking()
            .ToListAsync();
        // No cookbooks yet means only the implicit default one, which is shown.
        var hidden = cookbooks.Where(c => !c.ShowInPlanner).Select(c => c.Id).ToHashSet();
        var defaultHidden = cookbooks.Count > 0 && hidden.Contains(cookbooks[0].Id);
        return r => r.CookbookId is { } id ? !hidden.Contains(id) : !defaultHidden;
    }

    public async Task<Cookbook> CreateAsync(string user, string name, bool showInPlanner)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        // Make sure the default exists first, so the new cookbook never becomes the default by accident.
        await LoadOrCreateAsync(context, user);
        var cookbook = new Cookbook { UserName = user, Name = name, ShowInPlanner = showInPlanner };
        context.Cookbooks.Add(cookbook);
        await context.SaveChangesAsync();
        return cookbook;
    }

    public async Task<Cookbook?> UpdateAsync(string user, int id, string name, bool showInPlanner)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var cookbook = await context.Cookbooks.FirstOrDefaultAsync(c => c.Id == id && c.UserName == user);
        if (cookbook == null) return null;
        cookbook.Name = name;
        cookbook.ShowInPlanner = showInPlanner;
        await context.SaveChangesAsync();
        return cookbook;
    }

    /// <summary>Deletes an empty cookbook. The last one can't be deleted: a user always has a cookbook.</summary>
    public async Task<DeleteResult> DeleteAsync(string user, int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var cookbooks = await LoadOrCreateAsync(context, user);
        var cookbook = cookbooks.FirstOrDefault(c => c.Id == id);
        if (cookbook == null) return DeleteResult.NotFound;
        if (cookbooks.Count == 1) return DeleteResult.LastCookbook;

        var isDefault = cookbooks[0].Id == id;
        var hasRecipes = await context.Recipes.AnyAsync(r =>
            r.UserName == user && (r.CookbookId == id || (isDefault && r.CookbookId == null)));
        if (hasRecipes) return DeleteResult.NotEmpty;

        context.Cookbooks.Remove(cookbook);
        await context.SaveChangesAsync();
        return DeleteResult.Deleted;
    }

    private static async Task<List<Cookbook>> LoadOrCreateAsync(CookbookDatabaseContext context, string user)
    {
        var cookbooks = await context.Cookbooks
            .Where(c => c.UserName == user)
            .OrderBy(c => c.Id)
            .ToListAsync();
        if (cookbooks.Count > 0) return cookbooks;

        var created = new Cookbook { UserName = user, Name = "", ShowInPlanner = true };
        context.Cookbooks.Add(created);
        await context.SaveChangesAsync();
        return [created];
    }
}
