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
    /// <see cref="HeaderPresentsFilteringMiddlewareOptions"/>.
    /// </summary>
    public static partial class IServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="HeaderPresentsFilteringMiddlewareOptions"/> from the application configuration
        /// and optionally applies additional code-based configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">
        /// The application configuration containing a section named "<c>HeaderPresentsFilteringMiddlewareOptions</c>".
        /// </param>
        /// <param name="manualConfigure">
        /// An optional delegate to modify or augment the bound configuration.
        /// This delegate is applied after binding from the configuration.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// <para>
        /// Ensure that the configuration includes a section with the name
        /// "<c>HeaderPresentsFilteringMiddlewareOptions</c>". If the section is missing, default values will be used,
        /// which may not be appropriate for your environment.
        /// </para>
        /// <para>
        /// This method also registers a singleton instance of <see cref="MiddlewareFailurePointService"/> using
        /// <c>TryAddSingleton</c> to avoid duplicate registrations.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddHeaderValuesFilteringMiddleware(this IServiceCollection services, IConfiguration configuration, Action<HeaderValuesFilteringMiddlewareOptions>? manualConfigure = null)
        {
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Bind configuration from appsettings.json (reloadOnChange is enabled by default).
            services.Configure<HeaderValuesFilteringMiddlewareOptions>(configuration.GetSection(nameof(HeaderValuesFilteringMiddlewareOptions)));

            // Optionally apply additional code configuration.
            if (manualConfigure != null)
            {
                services.PostConfigure<HeaderValuesFilteringMiddlewareOptions>(manualConfigure);
            }

            return services;
        }

        /// <summary>
        /// Registers <see cref="HeaderPresentsFilteringMiddlewareOptions"/> using a direct manual configuration delegate.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="manualConfigure">
        /// A delegate for configuring <see cref="HeaderPresentsFilteringMiddlewareOptions"/> directly.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload bypasses binding from the application configuration.
        /// It is useful in scenarios where configuration is provided entirely in code.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage:
        /// services.AddHeaderPresentsFilteringMiddleware(options =>
        /// {
        ///     options.Whitelist = new[] { "*" };
        ///     options.Blacklist = new[] { "*strangeoptions*" };
        ///     options.DisallowedStatusCode = 400;
        ///     options.DisallowedFailureRating = 10;
        ///     options.ContinueOnDisallowed = true;
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddHeaderValuesFilteringMiddleware(this IServiceCollection services, Action<HeaderValuesFilteringMiddlewareOptions> manualConfigure)
        {
            // Ensure the MiddlewareFailurePointService is registered.
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Directly configure the options using the provided delegate.
            services.Configure(manualConfigure);

            return services;
        }

        /// <summary>
        /// Registers <see cref="HeaderPresentsFilteringMiddlewareOptions"/> with a default configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload applies a default configuration to <see cref="HeaderPresentsFilteringMiddlewareOptions"/>.
        /// The default settings include values for Whitelist, Blacklist, DisallowedStatusCode, DisallowedFailureRating,
        /// and ContinueOnDisallowed. Ensure these defaults match your application's requirements.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage:
        /// builder.Services.AddHeaderPresentsFilteringMiddleware();
        /// </code>
        /// </example>
        public static IServiceCollection AddHeaderValuesFilteringMiddleware(this IServiceCollection services)
        {
            return services.AddHeaderValuesFilteringMiddleware(configuration =>
            {
                configuration.Headers.Add("X-Frame-Options", new HeaderFilterRule() { Blacklist = new string[] { "" }, Whitelist = new string[] { "sd" } });
                configuration.DisallowedStatusCode = 400;
                configuration.DisallowedFailureRating = 1;
                configuration.ContinueOnDisallowed = true;
            });
        }
    }
}