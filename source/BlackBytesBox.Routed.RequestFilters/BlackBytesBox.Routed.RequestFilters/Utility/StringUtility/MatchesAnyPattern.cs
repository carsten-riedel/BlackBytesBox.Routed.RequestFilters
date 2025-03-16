using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BlackBytesBox.Routed.RequestFilters.Utility.StringUtility
{
    /// <summary>
    /// Provides static helper methods for common string operations.
    /// </summary>
    public static partial class StringUtility
    {
        public class PatternMatchResult
        {
            public bool IsMatch { get; set; } = false;
            public string MatchedPattern { get; set; } = string.Empty;
        }

        public static PatternMatchResult MatchesAnyPattern(string? input, IEnumerable<string?>? patterns, bool ignoreCase = true)
        {
            // If no patterns are provided, return false.
            if (patterns == null || !patterns.Any())
            {
                return new PatternMatchResult();
            }

            PatternMatchResult retval = new PatternMatchResult();

            var testinput = GetSafeString(input);

            RegexOptions regexOptions = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;

            foreach (var pattern in patterns)
            {
                var testpattern = GetSafeString(pattern);
                if (RegexUtility.RegexUtility.IsRegexMatch(testinput, testpattern, regexOptions))
                {
                    return new PatternMatchResult() { IsMatch = true, MatchedPattern = testpattern };
                }
            }

            return new PatternMatchResult();
        }
    }
}
