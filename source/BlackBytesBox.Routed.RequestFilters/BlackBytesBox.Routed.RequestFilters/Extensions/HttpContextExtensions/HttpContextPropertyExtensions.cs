using Microsoft.AspNetCore.Http;
using System;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.HttpContextExtensions
{
    /// <summary>
    /// Provides extension methods for storing and retrieving items from <see cref="HttpContext.Items"/> using a string key.
    /// </summary>
    public static class HttpContextDictionaryExtensions
    {
        /// <summary>
        /// Stores an object in the <see cref="HttpContext.Items"/> dictionary using the specified key.
        /// </summary>
        /// <param name="context">The current HTTP context.</param>
        /// <param name="key">The key under which the value will be stored.</param>
        /// <param name="value">The value to store.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="context"/> or <paramref name="key"/> is <see langword="null"/>.
        /// </exception>
        public static void SetItem(this HttpContext context, string key, object value)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));
            if (key is null)
                throw new ArgumentNullException(nameof(key));

            context.Items[key] = value;
        }

        /// <summary>
        /// Retrieves an item of type <typeparamref name="T"/> from the <see cref="HttpContext.Items"/> dictionary using the specified key.
        /// </summary>
        /// <typeparam name="T">The expected type of the stored value.</typeparam>
        /// <param name="context">The current HTTP context.</param>
        /// <param name="key">The key under which the value is stored.</param>
        /// <returns>
        /// The stored value cast to type <typeparamref name="T"/> if found and valid.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="context"/> or <paramref name="key"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the key does not exist in <see cref="HttpContext.Items"/> or the value cannot be cast to type <typeparamref name="T"/>.
        /// </exception>
        public static T GetItem<T>(this HttpContext context, string key)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));
            if (key is null)
                throw new ArgumentNullException(nameof(key));

            if (context.Items.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }

            throw new InvalidOperationException($"No valid entry found for key '{key}' in HttpContext.Items.");
        }
    }
}
