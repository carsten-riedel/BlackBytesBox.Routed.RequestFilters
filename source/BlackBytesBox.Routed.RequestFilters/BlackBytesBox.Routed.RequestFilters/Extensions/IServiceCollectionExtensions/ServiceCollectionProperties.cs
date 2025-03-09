using System;
using System.Collections.Concurrent;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.IServiceCollectionExtensions
{
    /// <summary>
    /// A simple container to hold arbitrary properties for the <see cref="IServiceCollection"/>.
    /// </summary>
    public class ServiceCollectionProperties
    {
        /// <summary>
        /// Gets the thread-safe dictionary of properties.
        /// </summary>
        /// <remarks>
        /// This dictionary is thread-safe and can be accessed concurrently from multiple threads.
        /// </remarks>
        public ConcurrentDictionary<string, object?> Properties { get; } = new ConcurrentDictionary<string, object?>();
    }

    /// <summary>
    /// Provides generic extension methods for storing and retrieving properties on <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ServiceCollectionPropertyExtensions
    {
        /// <summary>
        /// Ensures that a <see cref="ServiceCollectionProperties"/> instance is registered in the service collection and returns it.
        /// </summary>
        /// <param name="services">The service collection to check or add the properties container to.</param>
        /// <returns>The <see cref="ServiceCollectionProperties"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="services"/> is null.</exception>
        private static ServiceCollectionProperties GetOrCreateProperties(IServiceCollection services)
        {
            // Try to find an existing registration.
            var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ServiceCollectionProperties));
            if (descriptor != null)
            {
                return (ServiceCollectionProperties)descriptor.ImplementationInstance!;
            }
            else
            {
                var props = new ServiceCollectionProperties();
                services.AddSingleton(props);
                return props;
            }
        }

        /// <summary>
        /// Sets a property in the <see cref="ServiceCollectionProperties"/> container using the full name of type <typeparamref name="T"/> as the key.
        /// </summary>
        /// <typeparam name="T">The type whose full name will be used as the key.</typeparam>
        /// <typeparam name="K">The type of the value to store.</typeparam>
        /// <param name="services">The service collection to store the property in.</param>
        /// <param name="value">The value to store.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="services"/> is null.</exception>
        public static void SetProperty<T, K>(this IServiceCollection services, K value)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            var props = GetOrCreateProperties(services);
            string key = typeof(T).FullName!;
            props.Properties[key] = value;
        }

        /// <summary>
        /// Retrieves a property from the <see cref="ServiceCollectionProperties"/> container using the full name of type <typeparamref name="T"/> as the key.
        /// </summary>
        /// <typeparam name="T">The type whose full name is used as the key.</typeparam>
        /// <typeparam name="K">The type of the value to retrieve.</typeparam>
        /// <param name="services">The service collection to retrieve the property from.</param>
        /// <returns>The stored value if it exists and can be cast to type <typeparamref name="K"/>; otherwise, default(<typeparamref name="K"/>).</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="services"/> is null.</exception>
        public static K? GetProperty<T, K>(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            var props = GetOrCreateProperties(services);
            string key = typeof(T).FullName!;
            if (props.Properties.TryGetValue(key, out var value) && value is K typedValue)
            {
                return typedValue;
            }
            return default;
        }

        /// <summary>
        /// Sets a property in the <see cref="ServiceCollectionProperties"/> container using the full name of type <typeparamref name="T"/> as the key.
        /// This overload assumes the key and value types are the same.
        /// </summary>
        /// <typeparam name="T">The type used both as the key (via its full name) and the value type.</typeparam>
        /// <param name="services">The service collection to store the property in.</param>
        /// <param name="value">The value to store.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="services"/> is null.</exception>
        public static void SetProperty<T>(this IServiceCollection services, T value)
        {
            services.SetProperty<T, T>(value);
        }

        /// <summary>
        /// Retrieves a property from the <see cref="ServiceCollectionProperties"/> container using the full name of type <typeparamref name="T"/> as the key.
        /// This overload assumes the key and value types are the same.
        /// </summary>
        /// <typeparam name="T">The type used both as the key (via its full name) and the value type.</typeparam>
        /// <param name="services">The service collection to retrieve the property from.</param>
        /// <returns>The stored value if it exists; otherwise, default(<typeparamref name="T"/>).</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="services"/> is null.</exception>
        public static T? GetProperty<T>(this IServiceCollection services)
        {
            return services.GetProperty<T, T>();
        }
    }
}