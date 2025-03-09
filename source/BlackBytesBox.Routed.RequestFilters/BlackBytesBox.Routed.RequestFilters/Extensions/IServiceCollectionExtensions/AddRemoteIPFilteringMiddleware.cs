using System;

using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using BlackBytesBox.Routed.RequestFilters.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.IServiceCollectionExtensions
{
    /// <summary>
    /// Provides extension methods for configuring the <see cref="IServiceCollection"/> with 
    /// <see cref="RemoteIPFilteringMiddlewareOptions"/>.
    /// </summary>
    public static partial class IServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="RemoteIPFilteringMiddlewareOptions"/> from the application configuration
        /// and optionally applies additional code-based configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">
        /// The application configuration containing a section named "<c>RemoteIPFilteringMiddlewareOptions</c>".
        /// </param>
        /// <param name="manualConfigure">
        /// An optional delegate to modify or augment the bound configuration.
        /// This delegate is applied after binding from the configuration.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// <para>
        /// Ensure that the configuration includes a section with the name 
        /// "<c>RemoteIPFilteringMiddlewareOptions</c>". If the section is missing, default values will be used,
        /// which may not be appropriate for your environment.
        /// </para>
        /// <para>
        /// This method also registers a singleton instance of <see cref="MiddlewareFailurePointService"/> using 
        /// <c>TryAddSingleton</c> to avoid duplicate registrations.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddRemoteIPFilteringMiddleware(this IServiceCollection services, IConfiguration configuration, Action<RemoteIPFilteringMiddlewareOptions>? manualConfigure = null)
        {
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Bind configuration from appsettings.json (reloadOnChange is enabled by default).
            services.Configure<RemoteIPFilteringMiddlewareOptions>(configuration.GetSection(nameof(RemoteIPFilteringMiddlewareOptions)));

            // Optionally apply additional code configuration.
            if (manualConfigure != null)
            {
                services.PostConfigure<RemoteIPFilteringMiddlewareOptions>(manualConfigure);
            }

            return services;
        }

        /// <summary>
        /// Registers <see cref="RemoteIPFilteringMiddlewareOptions"/> using a direct manual configuration delegate.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="manualConfigure">
        /// A delegate for configuring <see cref="RemoteIPFilteringMiddlewareOptions"/> directly.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload bypasses binding from the application configuration.
        /// It is useful in scenarios where configuration is provided entirely in code.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage:
        /// services.AddRemoteIPFilteringMiddleware(options =>
        /// {
        ///     // Configure allowed IP patterns
        ///     options.Whitelist = null;
        ///     
        ///     // Configure disallowed IP patterns
        ///     options.Blacklist = new[] { "8.8.8.8" };
        ///     
        ///     // Set the response status code for disallowed requests
        ///     options.DisallowedStatusCode = 400;
        ///     
        ///     // Set the failure rating that will be recorded
        ///     options.DisallowedFailureRating = 1;
        ///     
        ///     // Determine whether processing should continue when a disallowed IP is encountered
        ///     options.ContinueOnDisallowed = true;
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddRemoteIPFilteringMiddleware(this IServiceCollection services, Action<RemoteIPFilteringMiddlewareOptions> manualConfigure)
        {
            // Ensure the MiddlewareFailurePointService is registered.
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Directly configure the options using the provided delegate.
            services.Configure(manualConfigure);

            return services;
        }

        /// <summary>
        /// Registers <see cref="RemoteIPFilteringMiddlewareOptions"/> with a default configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload applies a default configuration to <see cref="RemoteIPFilteringMiddlewareOptions"/>.
        /// The default settings include values for Whitelist, Blacklist, DisallowedStatusCode, DisallowedFailureRating,
        /// and ContinueOnDisallowed. Ensure these defaults match your application's requirements.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage:
        /// builder.Services.AddRemoteIPFilteringMiddleware();
        /// </code>
        /// </example>
        public static IServiceCollection AddRemoteIPFilteringMiddleware(this IServiceCollection services)
        {
            return services.AddRemoteIPFilteringMiddleware(configuration =>
            {
                configuration.Whitelist = null;
                configuration.Blacklist = new[] { "8.8.8.8" };
                configuration.DisallowedStatusCode = 400;
                configuration.DisallowedFailureRating = 1;
                configuration.ContinueOnDisallowed = true;
            });
        }
    }
}
