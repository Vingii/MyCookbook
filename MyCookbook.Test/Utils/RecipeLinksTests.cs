using MyCookbook.Utils;

namespace MyCookbook.Test.Utils;

public class RecipeLinksTests
{
    [Fact]
    public void ExtractReferences_NoLinks_ReturnsEmpty()
    {
        Assert.Empty(RecipeLinks.ExtractReferences("Simmer for 20 minutes."));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ExtractReferences_NullOrEmpty_ReturnsEmpty(string? description)
    {
        Assert.Empty(RecipeLinks.ExtractReferences(description));
    }

    [Fact]
    public void ExtractReferences_MultipleLinks_ReturnsAllInOrder()
    {
        var targets = RecipeLinks.ExtractReferences("Serve with [[Tzatziki]] and [[Pita bread]].").ToList();
        Assert.Equal(["Tzatziki", "Pita bread"], targets);
    }

    [Fact]
    public void ExtractReferences_TrimsSurroundingWhitespace()
    {
        Assert.Equal(["Tzatziki"], RecipeLinks.ExtractReferences("Serve with [[  Tzatziki  ]]."));
    }

    [Theory]
    [InlineData("[[]]")]
    [InlineData("[[   ]]")]
    [InlineData("[[|]]")]
    public void ExtractReferences_EmptyLink_IsIgnored(string description)
    {
        Assert.Empty(RecipeLinks.ExtractReferences(description));
    }

    [Fact]
    public void ExtractReferences_UnclosedBracket_IsIgnored()
    {
        Assert.Empty(RecipeLinks.ExtractReferences("Serve with [[Tzatziki"));
    }

    [Fact]
    public void ExtractReferences_DoesNotSpanLines()
    {
        Assert.Empty(RecipeLinks.ExtractReferences("Serve with [[Tzatziki\nand pita]]"));
    }

    [Fact]
    public void ExtractLinks_Shorthand_UsesTheSameTextForBothHalves()
    {
        var link = Assert.Single(RecipeLinks.ExtractLinks("Serve with [[Tzatziki]]."));
        Assert.Equal("Tzatziki", link.Display);
        Assert.Equal("Tzatziki", link.Reference);
    }

    [Fact]
    public void ExtractLinks_DisplayPipeReference_SplitsOnThePipe()
    {
        var link = Assert.Single(RecipeLinks.ExtractLinks("Podávejte s [[bramborovou kaší|Bramborová kaše]]."));
        Assert.Equal("bramborovou kaší", link.Display);
        Assert.Equal("Bramborová kaše", link.Reference);
    }

    [Fact]
    public void ExtractLinks_MultiplePipes_SplitsOnTheLastOne()
    {
        var link = Assert.Single(RecipeLinks.ExtractLinks("[[a|b|Tzatziki]]"));
        Assert.Equal("a|b", link.Display);
        Assert.Equal("Tzatziki", link.Reference);
    }

    [Fact]
    public void ExtractLinks_MissingDisplay_FallsBackToTheReference()
    {
        var link = Assert.Single(RecipeLinks.ExtractLinks("[[|Tzatziki]]"));
        Assert.Equal("Tzatziki", link.Display);
        Assert.Equal("Tzatziki", link.Reference);
    }

    [Fact]
    public void ExtractLinks_MissingReference_FallsBackToTheDisplay()
    {
        var link = Assert.Single(RecipeLinks.ExtractLinks("[[Tzatziki|]]"));
        Assert.Equal("Tzatziki", link.Display);
        Assert.Equal("Tzatziki", link.Reference);
    }

    [Fact]
    public void Expand_Shorthand_BecomesTheCanonicalTwoPartForm()
    {
        Assert.Equal("Serve with [[Tzatziki|Tzatziki]].", RecipeLinks.Expand("Serve with [[Tzatziki]]."));
    }

    [Fact]
    public void Expand_AlreadyCanonical_IsUnchanged()
    {
        const string description = "Serve with [[tzatziki|Tzatziki]].";
        Assert.Equal(description, RecipeLinks.Expand(description));
    }

    [Fact]
    public void Expand_TrimsWhitespaceInsideTheBrackets()
    {
        Assert.Equal("[[Tzatziki|Tzatziki]]", RecipeLinks.Expand("[[  Tzatziki  ]]"));
    }

    [Fact]
    public void Expand_ExpandsEveryLinkInTheDescription()
    {
        Assert.Equal(
            "[[Tzatziki|Tzatziki]] and [[Pita|Pita]]",
            RecipeLinks.Expand("[[Tzatziki]] and [[Pita]]"));
    }

    [Theory]
    [InlineData("No links here.")]
    [InlineData("Empty [[]] link")]
    [InlineData("")]
    public void Expand_NothingToExpand_IsUnchanged(string description)
    {
        Assert.Equal(description, RecipeLinks.Expand(description));
    }

    [Fact]
    public void Expand_Null_ReturnsEmpty()
    {
        Assert.Equal("", RecipeLinks.Expand(null));
    }

    [Fact]
    public void Rename_UpdatesTheReferenceAndKeepsTheDisplay()
    {
        Assert.Equal(
            "Podávejte s [[bramborovou kaší|Šťouchané brambory]].",
            RecipeLinks.Rename("Podávejte s [[bramborovou kaší|Bramborová kaše]].", "Bramborová kaše", "Šťouchané brambory"));
    }

    [Fact]
    public void Rename_Shorthand_KeepsTheOldTextAsTheDisplay()
    {
        Assert.Equal(
            "Serve with [[Tzatziki|Cucumber dip]].",
            RecipeLinks.Rename("Serve with [[Tzatziki]].", "Tzatziki", "Cucumber dip"));
    }

    [Fact]
    public void Rename_MatchesTheReferenceIgnoringCaseAndDiacritics()
    {
        Assert.Equal(
            "[[dip|Cucumber dip]]",
            RecipeLinks.Rename("[[dip|tzatzíki]]", "Tzatziki", "Cucumber dip"));
    }

    [Fact]
    public void Rename_LeavesLinksToOtherRecipesAlone()
    {
        const string description = "[[Tzatziki|Tzatziki]] and [[Pita|Pita]]";
        Assert.Equal(
            "[[Tzatziki|Cucumber dip]] and [[Pita|Pita]]",
            RecipeLinks.Rename(description, "Tzatziki", "Cucumber dip"));
    }

    [Fact]
    public void Rename_DoesNotTouchPlainTextMentions()
    {
        Assert.Equal(
            "Serve tzatziki with [[Tzatziki|Cucumber dip]].",
            RecipeLinks.Rename("Serve tzatziki with [[Tzatziki]].", "Tzatziki", "Cucumber dip"));
    }

    [Theory]
    [InlineData("Tzatziki", "tzatziki")]
    [InlineData("  Bramborová   KAŠE ", "bramborova kase")]
    [InlineData("Crème Brûlée", "creme brulee")]
    public void NormalizeKey_StripsCaseDiacriticsAndExtraWhitespace(string input, string expected)
    {
        Assert.Equal(expected, RecipeLinks.NormalizeKey(input));
    }
}
