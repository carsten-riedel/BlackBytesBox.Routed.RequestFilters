using System;

using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using BlackBytesBox.Routed.RequestFilters.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.IServiceCollectionExtensions
{
    /// <summary>
    /// Provides extension methods for configuring <see cref="IServiceCollection"/>.
    /// </summary>
    public static partial class IServiceCollectionExtensions
    {
        /// <summary>
        /// Registers MyMiddleware options from appsettings and optionally applies additional code configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="manualConfigure">
        /// An optional delegate to override or augment the configuration.
        /// This is applied after binding from appsettings.
        /// </param>
        /// <returns>The updated service collection.</returns>
        public static IServiceCollection AddHttpProtocolFilteringMiddleware(this IServiceCollection services, IConfiguration configuration, Action<HttpProtocolFilteringMiddlewareOptions>? manualConfigure = null)
        {
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Bind configuration from appsettings.json (ensure reloadOnChange is enabled, which is the default).
            services.Configure<HttpProtocolFilteringMiddlewareOptions>(configuration.GetSection(nameof(HttpProtocolFilteringMiddlewareOptions)));

            // Optionally apply additional code configuration.
            if (manualConfigure != null)
            {
                services.PostConfigure<HttpProtocolFilteringMiddlewareOptions>(manualConfigure);
            }

            return services;
        }

        /// <summary>
        /// Registers MyMiddleware options using a direct manual configuration delegate.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="manualConfigure">A delegate for configuring <see cref="HttpProtocolFilteringMiddlewareOptions"/>.</param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>This overload bypasses configuration binding from appsettings.json.</remarks>
        /// <example>
        /// <code>
        /// // Example usage:
        /// services.AddHttpProtocolFilteringMiddleware(options =>
        /// {
        ///     // Apply direct manual configuration.
        ///     options.SomeProperty = "value";
        ///     // Configure additional properties as needed.
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddHttpProtocolFilteringMiddleware(this IServiceCollection services, Action<HttpProtocolFilteringMiddlewareOptions> manualConfigure)
        {
            // Ensure the MiddlewareFailurePointService is registered.
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Directly configure the options using the provided delegate.
            services.Configure(manualConfigure);

            return services;
        }

        /// <summary>
        /// Registers MyMiddleware options with a default configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload applies a default configuration to <see cref="HttpProtocolFilteringMiddlewareOptions"/>.
        /// The default configuration sets the Whitelist, Blacklist, DisallowedStatusCode, DisallowedFailureRating, and ContinueOnDisallowed properties.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage:
        /// builder.Services.AddHttpProtocolFilteringMiddleware();
        /// </code>
        /// </example>
        public static IServiceCollection AddHttpProtocolFilteringMiddleware(this IServiceCollection services)
        {
            return services.AddHttpProtocolFilteringMiddleware(configuration =>
            {
                configuration.Whitelist = new[] { "HTTP/1.1", "HTTP/2", "HTTP/2.0", "HTTP/3", "HTTP/3.0" };
                configuration.Blacklist = new[] { "", "HTTP/1.0", "HTTP/1.?" };
                configuration.DisallowedStatusCode = 400;
                configuration.DisallowedFailureRating = 10;
                configuration.ContinueOnDisallowed = true;
            });
        }
    }
}