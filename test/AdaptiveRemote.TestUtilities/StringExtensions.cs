using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace AdaptiveRemote.TestUtilities;

public static class StringExtensions
{
    private static readonly Regex PlaceHolderRegex = new("{\\w+}");

    public static FormattableString AsMessageTemplate(this string format, params object?[] args)
    {
        Dictionary<string, string> placeholders = new();
        format = PlaceHolderRegex.Replace(format, match =>
        {
            if (placeholders.TryGetValue(match.Value, out string? index))
            {
                return index;
            }
            else
            {
                index = $"{{{placeholders.Count}}}";
                placeholders[match.Value] = index;
                return index;
            }
        });

        return FormattableStringFactory.Create(format, args);
    }
}
