using System.Collections.Generic;
using System;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.DictionaryExtensions
{
    /// <summary>
    /// Provides extension methods for dictionary operations.
    /// </summary>
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Increments the integer value for a given key in the dictionary by a specified amount.
        /// If the key does not exist, it initializes the key with the provided increment value.
        /// </summary>
        /// <param name="dictionary">The dictionary containing string keys and integer values.</param>
        /// <param name="key">The key whose value to increment.</param>
        /// <param name="increment">The amount to add to the key's value.</param>
        /// <remarks>
        /// This method does not provide thread-safety. For concurrent updates, consider using proper synchronization or a ConcurrentDictionary.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Initialize the dictionary and add the key "test" with an initial value of 0.
        /// var keyValuePairs = new Dictionary&lt;string, int&gt; { ["test"] = 0 };
        ///
        /// // Increase the value for the key "test" by 5.
        /// keyValuePairs.IncrementKeyValue("test", 5);
        /// // If "test" was 0, it becomes 5; if it did not exist, it becomes 5.
        /// </code>
        /// </example>
        public static void IncrementKeyValue(this Dictionary<string, int> dictionary, string key, int increment)
        {
            if (dictionary == null)
                throw new ArgumentNullException(nameof(dictionary));
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (dictionary.TryGetValue(key, out int currentValue))
            {
                dictionary[key] = currentValue + increment;
            }
            else
            {
                dictionary[key] = increment;
            }
        }
    }
}