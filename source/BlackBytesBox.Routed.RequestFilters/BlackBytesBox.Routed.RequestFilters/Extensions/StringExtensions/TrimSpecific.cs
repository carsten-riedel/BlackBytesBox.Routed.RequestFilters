namespace BlackBytesBox.Routed.RequestFilters.Extensions.StringExtensions
{
    /// <summary>
    /// Provides extension methods for the <see cref="string"/> class.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Trims occurrences of a specified character from the start and end of the string up to a defined count.
        /// </summary>
        /// <remarks>
        /// This extension method removes the <paramref name="charToTrim"/> from the beginning of the string up to <paramref name="countFromStart"/> times and
        /// from the end up to <paramref name="countFromEnd"/> times. If the input is <c>null</c> or empty, it returns the input unchanged.
        /// If all characters are trimmed, an empty string is returned.
        /// </remarks>
        /// <param name="input">The string to trim. May be <c>null</c> or empty.</param>
        /// <param name="charToTrim">The character to remove from both ends.</param>
        /// <param name="countFromStart">The maximum number of occurrences to remove from the start.</param>
        /// <param name="countFromEnd">The maximum number of occurrences to remove from the end.</param>
        /// <returns>
        /// A new string with the specified character trimmed from the beginning and end according to the provided counts.
        /// Returns the original string if it is <c>null</c> or empty, or an empty string if all characters are trimmed.
        /// </returns>
        /// <example>
        /// <code language="csharp">
        /// // Example usage:
        /// string original = "###Hello World###";
        /// string trimmed = original.TrimSpecific('#', 3, 3);
        /// Console.WriteLine(trimmed); // Outputs: "Hello World"
        /// </code>
        /// </example>
        public static string TrimSpecific(this string input, char charToTrim, int countFromStart, int countFromEnd)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            int start = 0;
            int end = input.Length;

            // Trim from start
            while (start < end && input[start] == charToTrim && countFromStart > 0)
            {
                start++;
                countFromStart--;
            }

            // Trim from end
            while (end > start && input[end - 1] == charToTrim && countFromEnd > 0)
            {
                end--;
                countFromEnd--;
            }

            if (start >= end)
                return string.Empty;

            return input.Substring(start, end - start);
        }
    }
}
