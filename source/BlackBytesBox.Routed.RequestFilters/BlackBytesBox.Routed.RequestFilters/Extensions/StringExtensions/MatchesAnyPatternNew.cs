using System.Collections.Generic;

using BlackBytesBox.Routed.RequestFilters.Utility.StringUtility;

using static BlackBytesBox.Routed.RequestFilters.Utility.StringUtility.StringUtility;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.StringExtensions
{
    /// <summary>
    /// Provides extension methods for the <see cref="string"/> class.
    /// </summary>
    public static partial class StringExtensions
    {
        public static PatternMatchResult MatchesAnyPatternNew(this string? input, IEnumerable<string?>? patterns, bool ignoreCase = true)
        {
            return StringUtility.MatchesAnyPattern(input, patterns, ignoreCase);
        }
    }
}