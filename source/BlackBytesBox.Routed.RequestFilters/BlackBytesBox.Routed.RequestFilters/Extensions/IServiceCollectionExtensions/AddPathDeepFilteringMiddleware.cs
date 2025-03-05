using System;

using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using BlackBytesBox.Routed.RequestFilters.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.IServiceCollectionExtensions
{
    /// <summary>
    /// Provides extension methods for configuring the <see cref="IServiceCollection"/> with
    /// <see cref="PathDeepFilteringMiddlewareOptions"/>.
    /// </summary>
    public static partial class IServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="PathDeepFilteringMiddlewareOptions"/> from the application configuration
        /// and optionally applies additional code-based configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">
        /// The application configuration containing a section named "<c>PathDeepFilteringMiddlewareOptions</c>".
        /// </param>
        /// <param name="manualConfigure">
        /// An optional delegate to modify or augment the bound configuration.
        /// This delegate is applied after binding from the configuration.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// <para>
        /// Ensure that the configuration includes a section with the name
        /// "<c>PathDeepFilteringMiddlewareOptions</c>". If the section is missing, default values will be used,
        /// which may not be appropriate for your environment.
        /// </para>
        /// <para>
        /// This method also registers a singleton instance of <see cref="MiddlewareFailurePointService"/> using
        /// <c>TryAddSingleton</c> to avoid duplicate registrations.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddPathDeepFilteringMiddleware(this IServiceCollection services, IConfiguration configuration, Action<PathDeepFilteringMiddlewareOptions>? manualConfigure = null)
        {
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Bind configuration from appsettings.json (reloadOnChange is enabled by default).
            services.Configure<PathDeepFilteringMiddlewareOptions>(configuration.GetSection(nameof(PathDeepFilteringMiddlewareOptions)));

            // Optionally apply additional code configuration.
            if (manualConfigure != null)
            {
                services.PostConfigure<PathDeepFilteringMiddlewareOptions>(manualConfigure);
            }

            return services;
        }

        /// <summary>
        /// Registers <see cref="PathDeepFilteringMiddlewareOptions"/> using a direct manual configuration delegate.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="manualConfigure">
        /// A delegate for configuring <see cref="PathDeepFilteringMiddlewareOptions"/> directly.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload bypasses binding from the application configuration.
        /// It is useful in scenarios where configuration is provided entirely in code.
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // Example usage:
        /// services.AddPathDeepFilteringMiddleware(options =>
        /// {
        ///     // Set the maximum allowed path depth.
        ///     options.PathDeepLimit = 5;
        /// 
        ///     // Set the response status code for disallowed requests.
        ///     options.DisallowedStatusCode = 400;
        /// 
        ///     // Set the failure rating that will be recorded.
        ///     options.DisallowedFailureRating = 10;
        /// 
        ///     // Determine whether processing should continue when a disallowed path is encountered.
        ///     options.ContinueOnDisallowed = false;
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddPathDeepFilteringMiddleware(this IServiceCollection services, Action<PathDeepFilteringMiddlewareOptions> manualConfigure)
        {
            // Ensure the MiddlewareFailurePointService is registered.
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Directly configure the options using the provided delegate.
            services.Configure(manualConfigure);

            return services;
        }

        /// <summary>
        /// Registers <see cref="PathDeepFilteringMiddlewareOptions"/> with a default configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload applies a default configuration to <see cref="PathDeepFilteringMiddlewareOptions"/>.
        /// The default settings include a path depth limit, response status code for disallowed requests,
        /// failure rating for disallowed attempts, and a flag to determine whether processing should continue.
        /// Ensure these defaults match your application's requirements.
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // Example usage:
        /// builder.Services.AddPathDeepFilteringMiddleware();
        /// </code>
        /// </example>
        public static IServiceCollection AddPathDeepFilteringMiddleware(this IServiceCollection services)
        {
            return services.AddPathDeepFilteringMiddleware(configuration =>
            {
                configuration.PathDeepLimit = 5;
                configuration.DisallowedStatusCode = 400;
                configuration.DisallowedFailureRating = 10;
                configuration.ContinueOnDisallowed = true;
            });
        }
    }
}
