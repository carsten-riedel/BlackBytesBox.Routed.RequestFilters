using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using BlackBytesBox.Routed.RequestFilters.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using System;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.IServiceCollectionExtensions
{
    /// <summary>
    /// Provides extension methods for configuring the <see cref="IServiceCollection"/> with
    /// <see cref="RequestUrlFilteringMiddlewareOptions"/>.
    /// </summary>
    public static partial class IServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="RequestUrlFilteringMiddlewareOptions"/> from the application configuration
        /// and optionally applies additional code-based configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">
        /// The application configuration containing a section named "<c>RequestUrlFilteringMiddlewareOptions</c>".
        /// </param>
        /// <param name="manualConfigure">
        /// An optional delegate to modify or augment the bound configuration.
        /// This delegate is applied after binding from the configuration.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// <para>
        /// Ensure that the configuration includes a section with the name
        /// "<c>RequestUrlFilteringMiddlewareOptions</c>". If the section is missing, default values will be used,
        /// which may not be appropriate for your environment.
        /// </para>
        /// <para>
        /// This method also registers a singleton instance of <see cref="MiddlewareFailurePointService"/> using
        /// <c>TryAddSingleton</c> to avoid duplicate registrations.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddRequestUrlFilteringMiddleware(this IServiceCollection services, IConfiguration configuration, Action<RequestUrlFilteringMiddlewareOptions>? manualConfigure = null)
        {
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Bind configuration from appsettings.json (reloadOnChange is enabled by default).
            services.Configure<RequestUrlFilteringMiddlewareOptions>(configuration.GetSection(nameof(RequestUrlFilteringMiddlewareOptions)));

            // Optionally apply additional code configuration.
            if (manualConfigure != null)
            {
                services.PostConfigure<RequestUrlFilteringMiddlewareOptions>(manualConfigure);
            }

            return services;
        }

        /// <summary>
        /// Registers <see cref="RequestUrlFilteringMiddlewareOptions"/> using a direct manual configuration delegate.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="manualConfigure">
        /// A delegate for configuring <see cref="RequestUrlFilteringMiddlewareOptions"/> directly.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload bypasses binding from the application configuration.
        /// It is useful in scenarios where configuration is provided entirely in code.
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // Example usage:
        /// services.AddRequestUrlFilteringMiddleware(options =>
        /// {
        ///     // Configure allowed URL patterns.
        ///     options.Whitelist = new[] { "/api/*", "/home/*" };
        /// 
        ///     // Configure disallowed URL patterns.
        ///     options.Blacklist = new[] { "*.php*", "*sitemap.xml*", "*robots.txt*" };
        /// 
        ///     // Set the response status code for disallowed requests.
        ///     options.DisallowedStatusCode = 400;
        /// 
        ///     // Set the failure rating that will be recorded.
        ///     options.DisallowedFailureRating = 10;
        /// 
        ///     // Determine whether processing should continue when a disallowed URL is encountered.
        ///     options.ContinueOnDisallowed = false;
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddRequestUrlFilteringMiddleware(this IServiceCollection services, Action<RequestUrlFilteringMiddlewareOptions> manualConfigure)
        {
            // Ensure the MiddlewareFailurePointService is registered.
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Directly configure the options using the provided delegate.
            services.Configure(manualConfigure);

            return services;
        }

        /// <summary>
        /// Registers <see cref="RequestUrlFilteringMiddlewareOptions"/> with a default configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload applies a default configuration to <see cref="RequestUrlFilteringMiddlewareOptions"/>.
        /// The default settings include values for Whitelist, Blacklist, DisallowedStatusCode, DisallowedFailureRating,
        /// and ContinueOnDisallowed. Ensure these defaults match your application's requirements.
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // Example usage:
        /// builder.Services.AddRequestUrlFilteringMiddleware();
        /// </code>
        /// </example>
        public static IServiceCollection AddRequestUrlFilteringMiddleware(this IServiceCollection services)
        {
            return services.AddRequestUrlFilteringMiddleware(configuration =>
            {
                configuration.Whitelist = null;
                configuration.Blacklist = new[]
                {
                    "*.php*", "*sitemap.xml*", "*robots.txt*", "*XDEBUG_SESSION_START*", "*usr/local*", "*bin/sh*", "*,/*", "*:///*", "*...*", "*../*", "*.ashx*"
                };
                configuration.DisallowedStatusCode = 400;
                configuration.DisallowedFailureRating = 10;
                configuration.ContinueOnDisallowed = true;
            });
        }
    }
}
