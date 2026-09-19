using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MyCookbook.Utils;

/// <summary>A single <c>[[...]]</c> occurrence: what is shown, and which recipe it points at.</summary>
public readonly record struct RecipeLinkToken(string Display, string Reference);

/// <summary>
/// Parsing helpers for the wiki-style links that can be embedded in step descriptions.
/// <para>
/// The canonical form is <c>[[display text|Recipe name]]</c>: the part after the pipe is the
/// reference that gets resolved, the part before it is what the reader sees. The shorthand
/// <c>[[Recipe name]]</c> means both are the same, and is expanded to the canonical form when a
/// step is saved — that keeps the two-part syntax discoverable without anyone having to be told.
/// </para>
/// <para>
/// Splitting display from reference is what makes renaming a recipe safe: only the reference
/// changes, so an inflected label like <c>[[bramborovou kaší|Bramborová kaše]]</c> keeps reading
/// correctly.
/// </para>
/// </summary>
public static partial class RecipeLinks
{
    [GeneratedRegex(@"\[\[([^\[\]\r\n]*)\]\]")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    /// <summary>Returns every well-formed link in the description, in order.</summary>
    public static IEnumerable<RecipeLinkToken> ExtractLinks(string? description)
    {
        if (string.IsNullOrEmpty(description)) yield break;

        foreach (Match match in LinkRegex().Matches(description))
        {
            var token = Parse(match.Groups[1].Value);
            if (token.Reference.Length > 0) yield return token;
        }
    }

    /// <summary>The recipe names referenced by the description.</summary>
    public static IEnumerable<string> ExtractReferences(string? description) =>
        ExtractLinks(description).Select(l => l.Reference);

    /// <summary>
    /// Rewrites every <c>[[Recipe name]]</c> shorthand into <c>[[Recipe name|Recipe name]]</c>.
    /// Applied when a step is saved, so the display/reference split shows itself to the user.
    /// </summary>
    public static string Expand(string? description) =>
        Rewrite(description, token => token);

    /// <summary>
    /// Points every link that references <paramref name="oldName"/> at <paramref name="newName"/>,
    /// leaving the display text as the user wrote it.
    /// </summary>
    public static string Rename(string? description, string oldName, string newName)
    {
        var oldKey = NormalizeKey(oldName);
        return Rewrite(description, token =>
            NormalizeKey(token.Reference) == oldKey ? token with { Reference = newName } : token);
    }

    /// <summary>
    /// Case-, whitespace- and diacritics-insensitive key used to match a reference against recipe
    /// names, so that <c>[[tzatziki]]</c> finds a recipe named "Tzatziki".
    /// </summary>
    public static string NormalizeKey(string value)
    {
        var stripped = new string(
            value.Normalize(NormalizationForm.FormD)
                 .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                 .ToArray()
        );
        return WhitespaceRegex().Replace(stripped, " ").Trim().ToLowerInvariant();
    }

    /// <summary>Applies <paramref name="transform"/> to every link and writes them back in canonical form.</summary>
    private static string Rewrite(string? description, Func<RecipeLinkToken, RecipeLinkToken> transform)
    {
        if (string.IsNullOrEmpty(description)) return description ?? "";

        return LinkRegex().Replace(description, match =>
        {
            var token = Parse(match.Groups[1].Value);
            // Leave `[[]]` and `[[|]]` as typed — there is nothing to point at.
            if (token.Reference.Length == 0) return match.Value;

            var result = transform(token);
            return $"[[{result.Display}|{result.Reference}]]";
        });
    }

    /// <summary>
    /// Splits the text between the brackets on its last pipe. Either side may be omitted, in which
    /// case it falls back to the other — <c>[[Tzatziki]]</c> and <c>[[|Tzatziki]]</c> are equivalent.
    /// </summary>
    private static RecipeLinkToken Parse(string inner)
    {
        var pipe = inner.LastIndexOf('|');
        if (pipe < 0)
        {
            var both = inner.Trim();
            return new RecipeLinkToken(both, both);
        }

        var display = inner[..pipe].Trim();
        var reference = inner[(pipe + 1)..].Trim();
        if (display.Length == 0) display = reference;
        if (reference.Length == 0) reference = display;
        return new RecipeLinkToken(display, reference);
    }
}
