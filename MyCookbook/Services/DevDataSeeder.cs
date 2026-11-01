using Microsoft.EntityFrameworkCore;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Model;

namespace MyCookbook.Services;

/// <summary>
/// Fills a local development database with sample data, so the app is not empty on localhost.
/// Runs on startup only when both DEV_AUTO_LOGIN and DEV_SEED_DATA=true are set (never in CI or tests).
/// <para>
/// Each user is seeded once, tracked by a "DevSeeded" preference: recipes you delete later do not come
/// back on restart, and recipes whose names you already use are skipped.
/// </para>
/// </summary>
public static class DevDataSeeder
{
    /// <summary>A second user who shares her cookbook, for trying out sharing and cloning.</summary>
    public const string SharingUser = "dev-anna";
    public const string SharingUserDisplayName = "Anna Nováková";
    public const string SharingUserToken = "dev-share-token";

    private const string SeededMarker = "DevSeeded";

    private record SeedStep(string Text, int Minutes, StepType Type = StepType.Active);

    private record SeedRecipe(
        string Name, string Category, int Duration, int Servings, string[] Tags,
        (string Name, string Amount)[] Ingredients, SeedStep[] Steps);

    public static async Task SeedAsync(CookbookDatabaseContext context, string devUser, DateOnly today)
    {
        if (await SeedUserAsync(context, devUser, DevUserRecipes))
        {
            await AddPlanAndHistoryAsync(context, devUser, today);
        }

        if (await SeedUserAsync(context, SharingUser, SharingUserRecipes))
        {
            context.UserPreferences.Add(new UserPreference { UserName = SharingUser, Key = "DisplayName", Value = SharingUserDisplayName });
            if (!await context.UserPreferences.AnyAsync(p => p.UserName == SharingUser && p.Key == "ShareToken"))
                context.UserPreferences.Add(new UserPreference { UserName = SharingUser, Key = "ShareToken", Value = SharingUserToken });
            await context.SaveChangesAsync();
        }
    }

    /// <returns>True when the user was seeded now, false when already seeded earlier.</returns>
    private static async Task<bool> SeedUserAsync(CookbookDatabaseContext context, string user, SeedRecipe[] recipes)
    {
        if (await context.UserPreferences.AnyAsync(p => p.UserName == user && p.Key == SeededMarker))
            return false;

        var existingNames = await context.Recipes.Where(r => r.UserName == user).Select(r => r.Name).ToListAsync();
        foreach (var seed in recipes.Where(r => !existingNames.Contains(r.Name)))
        {
            var recipe = new Recipe
            {
                UserName = user,
                Name = seed.Name,
                Category = seed.Category,
                Duration = seed.Duration,
                Servings = seed.Servings,
                Tags = seed.Tags.Select(t => new Tag { UserName = user, Name = t }).ToList(),
            };
            for (var i = 0; i < seed.Ingredients.Length; i++)
            {
                var (name, amount) = seed.Ingredients[i];
                recipe.Ingredients.Add(new Ingredient { UserName = user, Name = name, Amount = amount, Order = i + 1 });
            }
            for (var i = 0; i < seed.Steps.Length; i++)
            {
                var step = seed.Steps[i];
                recipe.Steps.Add(new Step
                {
                    UserName = user,
                    Description = step.Text,
                    Order = i + 1,
                    Duration = TimeSpan.FromMinutes(step.Minutes),
                    StepType = step.Type,
                });
            }
            context.Recipes.Add(recipe);
        }

        context.UserPreferences.Add(new UserPreference { UserName = user, Key = SeededMarker, Value = "1" });
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>Favorites, last-cooked dates and a meal plan around today, so those views have content too.</summary>
    private static async Task AddPlanAndHistoryAsync(CookbookDatabaseContext context, string user, DateOnly today)
    {
        var recipes = await context.Recipes.Where(r => r.UserName == user).ToDictionaryAsync(r => r.Name);
        Recipe? Get(string name) => recipes.GetValueOrDefault(name);

        foreach (var name in new[] { "Svíčková na smetaně", "Palačinky" })
        {
            if (Get(name) is { } recipe)
                context.FavoriteRecipes.Add(new FavoriteRecipe { UserName = user, RecipeId = recipe.Id });
        }

        if (Get("Palačinky") is { } pancakes) pancakes.LastCooked = today.AddDays(-2).ToDateTime(TimeOnly.MinValue);
        if (Get("Svíčková na smetaně") is { } svickova) svickova.LastCooked = today.AddDays(-14).ToDateTime(TimeOnly.MinValue);

        foreach (var (offset, name) in new[]
                 {
                     (-2, "Palačinky"), (0, "Kuřecí gyros"), (1, "Česnečka"), (2, "Svíčková na smetaně"), (4, "Kuře na paprice"),
                 })
        {
            if (Get(name) is { } recipe)
                context.PlannedRecipes.Add(new PlannedRecipe { UserName = user, RecipeId = recipe.Id, Date = today.AddDays(offset) });
        }

        await context.SaveChangesAsync();
    }

    private static readonly SeedRecipe[] DevUserRecipes =
    [
        new("Svíčková na smetaně", "Hlavní jídla", 180, 4, ["tradiční", "neděle"],
            [("hovězí svíčková", "800 g"), ("mrkev", "2 ks"), ("petržel", "1 ks"), ("celer", "1/4 ks"), ("cibule", "1 ks"),
             ("smetana ke šlehání", "250 ml"), ("nové koření", "5 ks"), ("bobkový list", "3 ks"), ("citron", "1 ks"), ("cukr", "1 lžíce")],
            [new("Svíčkovou osolte, opepřete a prudce opečte ze všech stran.", 10),
             new("Na stejném tuku orestujte nakrájenou mrkev, petržel, celer a cibuli.", 15),
             new("Přidejte nové koření, bobkový list a maso, podlijte vodou a duste pod pokličkou.", 120, StepType.Passive),
             new("Maso vyjměte, zeleninu rozmixujte se smetanou a dochuťte citronem a cukrem.", 15),
             new("Podávejte s [[houskovým knedlíkem|Houskový knedlík]] a brusinkami.", 5)]),

        new("Houskový knedlík", "Přílohy", 90, 6, ["tradiční"],
            [("hladká mouka", "500 g"), ("mléko", "300 ml"), ("vejce", "1 ks"), ("droždí", "20 g"), ("rohlíky", "3 ks"), ("sůl", "1 lžička")],
            [new("Droždí rozmíchejte ve vlažném mléce se lžičkou mouky a nechte vzejít.", 10, StepType.SemiPassive),
             new("Z mouky, vejce, soli a kvásku vypracujte těsto a vmíchejte nakrájené rohlíky.", 10),
             new("Těsto nechte kynout na teplém místě.", 40, StepType.Passive),
             new("Vytvarujte dva šišky a vařte je v osolené vodě, v polovině otočte.", 25, StepType.SemiPassive)]),

        new("Kuřecí gyros", "Hlavní jídla", 40, 4, ["grilování"],
            [("kuřecí stehna", "600 g"), ("česnek", "3 stroužky"), ("bílý jogurt", "150 g"), ("sladká paprika", "2 lžičky"),
             ("oregano", "1 lžička"), ("pita chléb", "4 ks"), ("rajče", "2 ks"), ("červená cibule", "1 ks")],
            [new("Kuřecí stehna nakrájejte na nudličky a smíchejte s jogurtem, česnekem, paprikou a oreganem.", 10),
             new("Nechte marinovat v lednici.", 30, StepType.Passive),
             new("Maso opečte na pánvi nebo grilu dozlatova.", 15),
             new("Podávejte v pita chlebu s rajčetem, cibulí a [[Tzatziki]].", 5)]),

        new("Tzatziki", "Omáčky", 15, 4, ["rychlé", "vegetariánské"],
            [("okurka", "1 ks"), ("řecký jogurt", "400 g"), ("česnek", "2 stroužky"), ("olivový olej", "2 lžíce"), ("kopr", "1 hrst")],
            [new("Okurku nastrouhejte, osolte a nechte okapat.", 10, StepType.SemiPassive),
             new("Okurku vymačkejte a smíchejte s jogurtem, prolisovaným česnekem, olejem a koprem.", 5)]),

        new("Česnečka", "Polévky", 30, 4, ["rychlé"],
            [("brambory", "4 ks"), ("česnek", "6 stroužků"), ("kmín", "1 lžička"), ("majoránka", "1 lžíce"),
             ("eidam", "100 g"), ("chléb", "4 krajíce")],
            [new("Brambory oloupejte, nakrájejte na kostičky a vařte s kmínem v osolené vodě.", 20, StepType.SemiPassive),
             new("Přidejte prolisovaný česnek a majoránku a nechte chvíli projít varem.", 5),
             new("Podávejte s opečeným chlebem a nastrouhaným eidamem.", 5)]),

        new("Bramborový salát", "Přílohy", 75, 6, ["Vánoce", "vegetariánské"],
            [("brambory", "1 kg"), ("vejce", "3 ks"), ("kyselé okurky", "4 ks"), ("mrkev", "2 ks"), ("cibule", "1 ks"), ("majonéza", "200 g")],
            [new("Brambory a mrkev uvařte ve slupce, vejce natvrdo.", 30, StepType.SemiPassive),
             new("Vše nechte vychladnout.", 30, StepType.Passive),
             new("Brambory, mrkev, vejce, okurky a cibuli nakrájejte na kostičky a smíchejte s majonézou.", 15)]),

        new("Palačinky", "Dezerty", 30, 4, ["sladké", "rychlé"],
            [("hladká mouka", "250 g"), ("mléko", "500 ml"), ("vejce", "2 ks"), ("cukr", "1 lžíce"), ("sůl", "1 špetka"),
             ("olej", "na smažení"), ("meruňková marmeláda", "na potření")],
            [new("Mouku, mléko, vejce, cukr a sůl prošlehejte na hladké těsto.", 5),
             new("Na rozpálené pánvi s olejem smažte tenké palačinky z obou stran.", 20),
             new("Potřete marmeládou a zarolujte.", 5)]),

        new("Kuře na paprice", "Hlavní jídla", 50, 4, ["rychlé"],
            [("kuřecí stehna", "4 ks"), ("cibule", "2 ks"), ("sladká paprika", "2 lžíce"), ("smetana ke šlehání", "200 ml"),
             ("hladká mouka", "1 lžíce"), ("máslo", "30 g")],
            [new("Na másle zpěňte nakrájenou cibuli.", 10),
             new("Přidejte stehna, osolte, posypte paprikou, podlijte vodou a duste.", 35, StepType.Passive),
             new("Smetanu rozmíchejte s moukou, vlijte do omáčky a krátce povařte.", 5)]),
    ];

    private static readonly SeedRecipe[] SharingUserRecipes =
    [
        new("Lasagne", "Hlavní jídla", 90, 6, ["neděle"],
            [("lasagne", "250 g"), ("mleté hovězí", "500 g"), ("rajčatová passata", "700 ml"), ("mléko", "500 ml"),
             ("máslo", "50 g"), ("hladká mouka", "50 g"), ("parmazán", "80 g")],
            [new("Mleté maso orestujte, přidejte passatu a duste.", 30, StepType.SemiPassive),
             new("Z másla, mouky a mléka uvařte bešamel.", 10),
             new("Střídejte vrstvy lasagní, masa a bešamelu, navrch posypte parmazánem.", 10),
             new("Pečte při 180 °C.", 40, StepType.Passive)]),

        new("Hummus", "Předkrmy", 15, 4, ["vegetariánské", "rychlé"],
            [("cizrna", "400 g"), ("tahini", "3 lžíce"), ("citron", "1 ks"), ("česnek", "1 stroužek"), ("olivový olej", "3 lžíce")],
            [new("Cizrnu slijte a rozmixujte s tahini, citronovou šťávou, česnekem a olejem do hladka.", 10),
             new("Dochuťte solí a pokapejte olejem.", 5)]),

        new("Mramorová bábovka", "Dezerty", 70, 8, ["sladké"],
            [("polohrubá mouka", "300 g"), ("cukr", "200 g"), ("vejce", "4 ks"), ("máslo", "200 g"), ("mléko", "150 ml"),
             ("kakao", "2 lžíce"), ("prášek do pečiva", "1 ks")],
            [new("Máslo utřete s cukrem a žloutky, přidejte mléko a mouku s práškem do pečiva.", 15),
             new("Vmíchejte sníh z bílků, třetinu těsta obarvěte kakaem.", 10),
             new("Těsta střídavě nalijte do formy a pečte při 170 °C.", 45, StepType.Passive)]),
    ];
}
