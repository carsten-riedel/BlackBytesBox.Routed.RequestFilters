using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.StringExtensions
{
    /// <summary>
    /// Provides extension methods for string operations, enhancing the built-in string manipulation capabilities.
    /// </summary>
    public static partial class ValidationsStringExtensions
    {
        /// <summary>
        /// Attempts to match the given input against the specified pattern using regular expressions.
        /// Returns true if the input matches the pattern, false otherwise.
        /// </summary>
        /// <param name="input">The input string to be matched.</param>
        /// <param name="pattern">The pattern string, where '*' matches any number of characters and '?' matches one.</param>
        /// <returns>True if the input matches the pattern; otherwise, false.</returns>
        private static bool IsRegExMatch(string? input, string? pattern)
        {
            if (input == null || pattern == null)
            {
                return input == null && pattern == null;
            }

            int minInputLength = pattern.Replace("*", "").Replace("?", "").Length;

            if (input.Length < minInputLength)
            {
                return false;
            }

            var regexPattern = "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".?") + "$";
            return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Checks if the input matches any of the provided patterns.
        /// </summary>
        /// <param name="input">The input string to be matched.</param>
        /// <param name="patterns">A list of patterns to match against the input.</param>
        /// <returns>True if any pattern matches the input; otherwise, false.</returns>
        public static bool MatchesAnyPattern(string? input, List<string>? patterns)
        {
            if (patterns == null || patterns.Count == 0)
            {
                return false;
            }

            foreach (var pattern in patterns)
            {
                if (IsRegExMatch(input, pattern))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the input matches any of the provided patterns.
        /// </summary>
        /// <param name="input">The input string to be matched.</param>
        /// <param name="patterns">A list of patterns to match against the input.</param>
        /// <returns>True if any pattern matches the input; otherwise, false.</returns>
        public static bool MatchesAnyPattern(string? input, string[]? patterns)
        {
            if (patterns == null || patterns.Length == 0)
            {
                return false;
            }

            foreach (var pattern in patterns)
            {
                if (IsRegExMatch(input, pattern))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Extension method to evaluate this string instance against optional whitelist and blacklist patterns to determine
        /// whether the operation should continue.
        /// </summary>
        /// <param name="input">The string instance to be evaluated.</param>
        /// <param name="whitelist">An optional list of whitelist patterns. If the string matches any of these patterns,
        /// the method immediately returns true, indicating that the operation should continue.</param>
        /// <param name="blacklist">An optional list of blacklist patterns. If the string matches any of these patterns,
        /// the method returns false, indicating that the operation should be halted.</param>
        /// <returns>True if the operation should continue (either by passing a whitelist check or not failing a blacklist check),
        /// or false if the string matches a blacklist pattern and should be halted.</returns>
        public static bool ValidateWhitelistBlacklist(this string input, List<string>? whitelist = null, List<string>? blacklist = null)
        {
            // Utilize the ValidateWhitelistBlacklist method directly
            return InternalValidateWhitelistBlacklist(input, whitelist, blacklist);
        }

        public static bool ValidateWhitelistBlacklist(this string input, IEnumerable<string>? whitelist = null, IEnumerable<string>? blacklist = null)
        {
            // Utilize the ValidateWhitelistBlacklist method directly
            return InternalValidateWhitelistBlacklist(input, whitelist?.ToList(), blacklist?.ToList());
        }

        /// <summary>
        /// Evaluates an input string against optional whitelist and blacklist to determine whether the operation should continue.
        /// This method returns true to signal that the operation should continue, either because the input matches a whitelist
        /// pattern, does not match a blacklist pattern, or no lists are provided.
        /// </summary>
        /// <param name="input">The input string to be evaluated.</param>
        /// <param name="whitelist">An optional list of whitelist patterns. If the input matches any of these patterns,
        /// the function immediately returns true, indicating that the operation should continue.</param>
        /// <param name="blacklist">An optional list of blacklist patterns. If the input matches any of these patterns,
        /// the function returns false, indicating that the operation should be halted.</param>
        /// <returns>True if the operation should continue (either by passing a whitelist check or not failing a blacklist check),
        /// or false if the input matches a blacklist pattern and should be halted.</returns>
        private static bool InternalValidateWhitelistBlacklist(string input, List<string>? whitelist = null, List<string>? blacklist = null)
        {
            // Check against the whitelist if it is provided
            if (whitelist != null)
            {
                var whitelistResult = MatchesAnyPattern(input, whitelist);
                if (whitelistResult)
                {
                    // The input matches a whitelist pattern; continue the operation
                    return true;
                }
            }

            // If the input does not match any whitelist pattern or no whitelist is provided, check the blacklist
            if (blacklist != null)
            {
                var blacklistResult = MatchesAnyPattern(input, blacklist);
                // If the input matches a blacklist pattern, halt the operation
                return !blacklistResult;
            }

            // If no lists are provided or no matches found in blacklist, continue the operation
            return true;
        }

    }
}
