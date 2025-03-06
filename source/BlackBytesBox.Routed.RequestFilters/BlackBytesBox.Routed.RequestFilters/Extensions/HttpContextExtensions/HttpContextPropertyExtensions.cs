using System;

using Microsoft.AspNetCore.Http;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.HttpContextExtensions
{

    /// <summary>
    /// Provides generic extension methods for storing and retrieving properties on HttpContext.Items.
    /// </summary>
    public static class HttpContextPropertyExtensions
    {
        /// <summary>
        /// Sets a property on the HttpContext.Items dictionary using the full name of type <typeparamref name="T"/> as the key.
        /// </summary>
        /// <typeparam name="T">The type whose full name will be used as the key.</typeparam>
        /// <typeparam name="K">The type of the value to store.</typeparam>
        /// <param name="context">The current HttpContext.</param>
        /// <param name="value">The value to store.</param>
        public static void SetProperty<T, K>(this HttpContext context, K value)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            string key = typeof(T).FullName!;
            context.Items[key] = value;
        }

        /// <summary>
        /// Retrieves a property from the HttpContext.Items dictionary using the full name of type <typeparamref name="T"/> as the key.
        /// </summary>
        /// <typeparam name="T">The type whose full name is used as the key.</typeparam>
        /// <typeparam name="K">The type of the value to retrieve.</typeparam>
        /// <param name="context">The current HttpContext.</param>
        /// <returns>The stored value if it exists; otherwise, the default value for type <typeparamref name="K"/>.</returns>
        public static K? GetProperty<T, K>(this HttpContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            string key = typeof(T).FullName!;
            if (context.Items.TryGetValue(key, out var value) && value is K typedValue)
            {
                return typedValue;
            }
            return default;
        }

        /// <summary>
        /// Sets a property on the HttpContext.Items dictionary using the full name of type <typeparamref name="T"/> as the key.
        /// This overload assumes the key and value types are the same.
        /// </summary>
        /// <typeparam name="T">The type used both as the key (via its full name) and the value type.</typeparam>
        /// <param name="context">The current HttpContext.</param>
        /// <param name="value">The value to store.</param>
        public static void SetProperty<T>(this HttpContext context, T value)
        {
            context.SetProperty<T, T>(value);
        }

        /// <summary>
        /// Retrieves a property from the HttpContext.Items dictionary using the full name of type <typeparamref name="T"/> as the key.
        /// This overload assumes the key and value types are the same.
        /// </summary>
        /// <typeparam name="T">The type used both as the key (via its full name) and the value type.</typeparam>
        /// <param name="context">The current HttpContext.</param>
        /// <returns>The stored value if it exists; otherwise, the default value for type <typeparamref name="T"/>.</returns>
        public static T? GetProperty<T>(this HttpContext context)
        {
            return context.GetProperty<T, T>();
        }
    }

}
