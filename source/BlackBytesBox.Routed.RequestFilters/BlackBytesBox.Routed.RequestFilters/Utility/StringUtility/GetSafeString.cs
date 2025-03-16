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
        /// <summary>
        /// Returns the original string if it is not null; otherwise, returns an empty string.
        /// </summary>
        /// <param name="input">The input string to check.</param>
        /// <returns>The original string if not null; otherwise, an empty string.</returns>
        public static string GetSafeString(string? input)
        {
            return input ?? string.Empty;
        }


    }
}
