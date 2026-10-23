using System.Globalization;
using System.Text;

namespace Ecom.AIShopping.Infrastructure.Retrieval;

public static class ArabicSearch
{
    // One-to-one ASCII keys let pg_trgm recognize Arabic even on C-locale databases.
    // Internal comparison key only; never a display name, translation or catalog attribute.
    public const string TrigramAlphabet = "ابتثجحخدذرزسشصضطظعغفقكلمنهويءةؤئ";
    public const string TrigramAscii = "abcdefghijklmnopqrstuvwxyz01234";
    public static string TrigramKey(string text) => string.Concat(Normalize(text).Select(c =>
        TrigramAlphabet.IndexOf(c) is var index && index >= 0 ? TrigramAscii[index] : c));
    private static readonly string[][] Concepts =
    [
        ["اكواب", "كاسات", "كوب", "كاس", "cups", "cup"],
        ["صحون", "اطباق", "صحن", "طبق", "plates", "plate"],
        ["ملاعق", "ملعقه", "ملعقة", "spoons", "spoon"],
        ["شوك", "شوكه", "شوكة", "fork", "forks"],
        ["مناديل", "محارم", "napkins", "tissues"],
        ["اكياس", "كياس", "كيس", "bags"],
        ["نفايات", "قمامة", "زباله", "زبالة", "trash", "garbage"],
        ["حاويات", "حافظات", "علب", "containers", "container"]
    ];

    public static bool Suitable(string query, string name, string nameEn, string category)
    {
        var requested = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var title = Normalize(name + " " + nameEn + " " + category);
        var concepts = Concepts.Where(c => c.Any(requested.Contains)).ToArray();
        // Product descriptions cannot create suitability by mentioning unrelated keywords or instructions.
        return concepts.Length > 0 ? concepts.All(c => c.Any(t => title.Contains(t, StringComparison.Ordinal))) :
            requested.Any(t => t.Length > 1 && (title.Contains(t, StringComparison.Ordinal) ||
                title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(word => OneTypo(t, word))));
    }

    private static bool OneTypo(string a, string b)
    {
        if (a.Length < 4 || b.Length < 4 || Math.Abs(a.Length - b.Length) > 1) return false;
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            if (a[i] == b[i]) continue;
            if (a.Length == b.Length)
                return a.AsSpan(i + 1).SequenceEqual(b.AsSpan(i + 1)) ||
                    (i + 1 < a.Length && a[i] == b[i + 1] && a[i + 1] == b[i] && a.AsSpan(i + 2).SequenceEqual(b.AsSpan(i + 2)));
            return a.Length > b.Length ? a.AsSpan(i + 1).SequenceEqual(b.AsSpan(i)) : a.AsSpan(i).SequenceEqual(b.AsSpan(i + 1));
        }
        return true;
    }

    public static string Material(string value) => Normalize(value).Trim() switch
    {
        "ورق" or "ورقي" or "ورقية" or "paper" => "paper",
        "بلاستيك" or "بلاستيكي" or "بلاستيكية" or "plastic" => "plastic",
        "خشب" or "خشبي" or "خشبية" or "wood" or "wooden" => "wood",
        var other => other
    };
    public static string Normalize(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormKC).ToLowerInvariant())
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark || c == 'ـ') continue;
            result.Append(c switch { 'أ' or 'إ' or 'آ' or 'ٱ' => 'ا', 'ى' => 'ي',
                >= '٠' and <= '٩' => (char)('0' + c - '٠'), >= '۰' and <= '۹' => (char)('0' + c - '۰'), _ => c });
        }
        return result.ToString().Trim();
    }

    // These are query translations, never inferred product attributes or claims.
    public static string Expand(string query)
    {
        var text = Normalize(query);
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(12).ToHashSet();
        foreach (var concept in Concepts)
            if (concept.Any(tokens.Contains))
            {
                foreach (var s in concept) tokens.Add(s);
            }
        return string.Join(' ', tokens);
    }

    public static int[] Fuse(IReadOnlyList<int> lexical, IReadOnlyList<int> semantic, int limit) => lexical
        .Select((id, i) => (id, score: 1d / (60 + i + 1)))
        .Concat(semantic.Select((id, i) => (id, score: 1d / (60 + i + 1))))
        .GroupBy(x => x.id).OrderByDescending(g => g.Sum(x => x.score)).ThenBy(g => g.Key).Take(limit).Select(g => g.Key).ToArray();
}
