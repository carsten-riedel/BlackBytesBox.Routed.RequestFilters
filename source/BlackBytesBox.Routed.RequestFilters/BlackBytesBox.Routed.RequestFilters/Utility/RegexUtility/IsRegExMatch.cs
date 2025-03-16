using System.Text.RegularExpressions;

namespace BlackBytesBox.Routed.RequestFilters.Utility.RegexUtility
{
    /// <summary>
    /// Provides static methods for regex-based string operations.
    /// </summary>
    public static partial class RegexUtility
    {
        /// <summary>
        /// Determines whether the specified input string matches the given wildcard pattern using regular expressions.
        /// </summary>
        /// <param name="input">
        /// The input string to match. If both <paramref name="input"/> and <paramref name="pattern"/> are null, the method returns true.
        /// </param>
        /// <param name="pattern">
        /// The wildcard pattern to match, where '*' matches any sequence of characters and '?' matches any single character.
        /// If <paramref name="pattern"/> is null while <paramref name="input"/> is not, the method returns false.
        /// </param>
        /// <param name="regexOptions">
        /// Optional regular expression options, with a default of <see cref="RegexOptions.IgnoreCase"/>.
        /// </param>
        /// <returns>
        /// <c>true</c> if both parameters are null or if the input string matches the wildcard pattern; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsRegexMatch(string? input, string? pattern, RegexOptions regexOptions = RegexOptions.IgnoreCase)
        {
            if (input == null && pattern == null)
            {
                return true;
            }

            if (input != null && pattern == null)
            {
                return false;
            }

            if (input == null && pattern != null)
            {
                return false;
            }

            // Convert wildcard pattern to a regular expression pattern.
            var regexPattern = Regex.Escape(pattern!);
            regexPattern = "^" + regexPattern.Replace(@"\*", ".*").Replace(@"\?", ".?") + "$";

            return Regex.IsMatch(input!, regexPattern, regexOptions);
        }
    }
}
