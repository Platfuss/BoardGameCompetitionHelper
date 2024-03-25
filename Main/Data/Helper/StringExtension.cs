using System.Globalization;
using System.Text;

namespace Main.Data.Helper;

public static class StringExtension
{
    public static string RemoveDiacritics(this string text) =>
    string.Concat(
        text.ToLower().Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
    ).Normalize(NormalizationForm.FormC).Replace('ł', 'l');
}
