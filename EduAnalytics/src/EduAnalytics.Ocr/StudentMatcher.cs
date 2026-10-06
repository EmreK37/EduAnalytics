using System.Text;

namespace EduAnalytics.Ocr;

public record StudentCandidate(int Id, string Number, string Name);

public enum MatchKind
{
    /// <summary>Okunan numara bir öğrenciyle birebir eşleşti.</summary>
    ExactNumber,

    /// <summary>Numara 1-2 karakter hatayla eşleşti (OCR düzeltmesi).</summary>
    FuzzyNumber,

    /// <summary>Numara okunamadı; ad-soyad benzerliğiyle eşleşti — mutlaka kontrol edilmeli.</summary>
    NameOnly,

    /// <summary>Güvenilir eşleşme bulunamadı; öğretmen elle seçmeli.</summary>
    None
}

/// <summary>
/// OCR çıktısını (ham numara + ham ad) derse kayıtlı öğrenci listesiyle eşleştirir.
/// El yazısı OCR'ı hatalı olabilir; bu yüzden birebir eşleşme yoksa küçük düzenleme
/// mesafesine ve ad benzerliğine bakılır, belirsizlikte eşleştirme YAPILMAZ.
/// </summary>
public static class StudentMatcher
{
    public static (int? StudentId, MatchKind Kind) Match(
        string rawNumber, string rawName, IReadOnlyList<StudentCandidate> candidates)
    {
        var number = NormalizeNumber(rawNumber);

        if (number.Length > 0)
        {
            var exact = candidates.Where(c => NormalizeNumber(c.Number) == number).ToList();
            if (exact.Count == 1)
                return (exact[0].Id, MatchKind.ExactNumber);

            if (number.Length >= 4)
            {
                var scored = candidates
                    .Select(c => (Candidate: c, Distance: Levenshtein(number, NormalizeNumber(c.Number))))
                    .Where(x => x.Distance <= 2)
                    .OrderBy(x => x.Distance)
                    .ToList();

                if (scored.Count == 1)
                    return (scored[0].Candidate.Id, MatchKind.FuzzyNumber);

                if (scored.Count > 1 && scored[0].Distance < scored[1].Distance)
                    return (scored[0].Candidate.Id, MatchKind.FuzzyNumber);

                // Aynı mesafede birden fazla aday: ad benzerliği belirgin şekilde ayırıyorsa kullan.
                if (scored.Count > 1 && !string.IsNullOrWhiteSpace(rawName))
                {
                    var byName = scored
                        .Select(x => (x.Candidate, Similarity: NameSimilarity(rawName, x.Candidate.Name)))
                        .OrderByDescending(x => x.Similarity)
                        .ToList();
                    if (byName[0].Similarity >= 0.6 && byName[0].Similarity - byName[1].Similarity >= 0.2)
                        return (byName[0].Candidate.Id, MatchKind.FuzzyNumber);
                }
            }
        }

        // Numara işe yaramadı: sadece ad ile dene (düşük güven — UI'da uyarı gösterilir).
        if (!string.IsNullOrWhiteSpace(rawName))
        {
            var byName = candidates
                .Select(c => (Candidate: c, Similarity: NameSimilarity(rawName, c.Name)))
                .OrderByDescending(x => x.Similarity)
                .ToList();

            if (byName.Count > 0 && byName[0].Similarity >= 0.6
                && (byName.Count == 1 || byName[0].Similarity - byName[1].Similarity >= 0.15))
                return (byName[0].Candidate.Id, MatchKind.NameOnly);
        }

        return (null, MatchKind.None);
    }

    private static string NormalizeNumber(string s)
        => new(s.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    /// <summary>Türkçe karakterleri ASCII'ye indirger; büyük/küçük ve aksan farkını yok eder.</summary>
    private static string FoldName(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            var mapped = ch switch
            {
                'ç' or 'Ç' => 'C',
                'ğ' or 'Ğ' => 'G',
                'ı' or 'İ' or 'i' or 'I' => 'I',
                'ö' or 'Ö' => 'O',
                'ş' or 'Ş' => 'S',
                'ü' or 'Ü' => 'U',
                _ => char.ToUpperInvariant(ch)
            };
            if (char.IsLetter(mapped) || mapped == ' ')
                sb.Append(mapped);
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Karakter ikilisi (bigram) Dice benzerliği: 0–1.</summary>
    private static double NameSimilarity(string a, string b)
    {
        var fa = FoldName(a);
        var fb = FoldName(b);
        if (fa.Length < 2 || fb.Length < 2)
            return fa == fb && fa.Length > 0 ? 1 : 0;

        var bigramsA = Bigrams(fa);
        var bigramsB = Bigrams(fb);
        var overlap = 0;
        foreach (var bg in bigramsA)
        {
            if (bigramsB.TryGetValue(bg.Key, out var count) && count > 0)
            {
                overlap += Math.Min(bg.Value, count);
            }
        }
        return 2.0 * overlap / (fa.Length - 1 + fb.Length - 1);
    }

    private static Dictionary<string, int> Bigrams(string s)
    {
        var map = new Dictionary<string, int>();
        for (var i = 0; i < s.Length - 1; i++)
        {
            var bg = s.Substring(i, 2);
            map[bg] = map.GetValueOrDefault(bg) + 1;
        }
        return map;
    }

    private static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[b.Length];
    }
}
