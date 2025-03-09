using Microsoft.AspNetCore.Builder;
using System;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.IApplicationBuilderExtensions
{
    /// <summary>
    /// Provides generic extension methods for storing and retrieving properties on IApplicationBuilder.
    /// </summary>
    public static class ApplicationBuilderPropertyExtensions
    {
        /// <summary>
        /// Sets a property on the IApplicationBuilder.Properties dictionary using the full name of type <typeparamref name="T"/> as the key.
        /// </summary>
        /// <typeparam name="T">The type whose full name will be used as the key.</typeparam>
        /// <typeparam name="K">The type of the value to store.</typeparam>
        /// <param name="app">The application builder.</param>
        /// <param name="value">The value to store.</param>
        public static void SetProperty<T, K>(this IApplicationBuilder app, K value)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            string key = typeof(T).FullName;
            app.Properties[key] = value;
        }

        /// <summary>
        /// Retrieves a property from the IApplicationBuilder.Properties dictionary using the full name of type <typeparamref name="T"/> as the key.
        /// </summary>
        /// <typeparam name="T">The type whose full name is used as the key.</typeparam>
        /// <typeparam name="K">The type of the value to retrieve.</typeparam>
        /// <param name="app">The application builder.</param>
        /// <returns>
        /// The stored value if it exists; otherwise, the default value for type <typeparamref name="K"/>.
        /// </returns>
        public static K? GetProperty<T, K>(this IApplicationBuilder app)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            string key = typeof(T).FullName;
            if (app.Properties.TryGetValue(key, out var value) && value is K typedValue)
            {
                return typedValue;
            }
            return default;
        }

        /// <summary>
        /// Sets a property on the IApplicationBuilder.Properties dictionary using the full name of type <typeparamref name="T"/> as the key.
        /// This overload assumes the key and value types are the same.
        /// </summary>
        /// <typeparam name="T">The type used both as the key (via its full name) and the value type.</typeparam>
        /// <param name="app">The application builder.</param>
        /// <param name="value">The value to store.</param>
        public static void SetProperty<T>(this IApplicationBuilder app, T value)
        {
            app.SetProperty<T, T>(value);
        }

        /// <summary>
        /// Retrieves a property from the IApplicationBuilder.Properties dictionary using the full name of type <typeparamref name="T"/> as the key.
        /// This overload assumes the key and value types are the same.
        /// </summary>
        /// <typeparam name="T">The type used both as the key (via its full name) and the value type.</typeparam>
        /// <param name="app">The application builder.</param>
        /// <returns>The stored value if it exists; otherwise, the default value for type <typeparamref name="T"/>.</returns>
        public static T? GetProperty<T>(this IApplicationBuilder app)
        {
            return app.GetProperty<T, T>();
        }
    }
}
