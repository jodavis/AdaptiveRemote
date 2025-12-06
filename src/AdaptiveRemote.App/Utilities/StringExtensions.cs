using System.Text;

namespace AdaptiveRemote.Utilities;

internal static class StringExtensions
{
    public static string ToPascalCase(this string input)
    {
        StringBuilder builder = new();

        bool nextUpper = true;
        foreach (char c in input)
        {
            if (char.IsWhiteSpace(c))
            {
                nextUpper = true;
            }
            else if (nextUpper)
            {
                nextUpper = false;
                builder.Append(char.ToUpperInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
