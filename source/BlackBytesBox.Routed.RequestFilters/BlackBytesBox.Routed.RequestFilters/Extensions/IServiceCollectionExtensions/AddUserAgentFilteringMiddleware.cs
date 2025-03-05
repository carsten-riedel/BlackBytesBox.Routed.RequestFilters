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
    /// <see cref="UserAgentFilteringMiddlewareOptions"/>.
    /// </summary>
    public static partial class IServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="UserAgentFilteringMiddlewareOptions"/> from the application configuration
        /// and optionally applies additional code-based configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">
        /// The application configuration containing a section named "<c>UserAgentFilteringMiddlewareOptions</c>".
        /// </param>
        /// <param name="manualConfigure">
        /// An optional delegate to modify or augment the bound configuration.
        /// This delegate is applied after binding from the configuration.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// <para>
        /// Ensure that the configuration includes a section with the name 
        /// "<c>UserAgentFilteringMiddlewareOptions</c>". If the section is missing, default values will be used,
        /// which may not be appropriate for your environment.
        /// </para>
        /// <para>
        /// This method also registers a singleton instance of <see cref="MiddlewareFailurePointService"/> using 
        /// <c>TryAddSingleton</c> to avoid duplicate registrations.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddUserAgentFilteringMiddleware(this IServiceCollection services, IConfiguration configuration, Action<UserAgentFilteringMiddlewareOptions>? manualConfigure = null)
        {
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Bind configuration from appsettings.json (reloadOnChange is enabled by default).
            services.Configure<UserAgentFilteringMiddlewareOptions>(configuration.GetSection(nameof(UserAgentFilteringMiddlewareOptions)));

            // Optionally apply additional code configuration.
            if (manualConfigure != null)
            {
                services.PostConfigure<UserAgentFilteringMiddlewareOptions>(manualConfigure);
            }

            return services;
        }

        /// <summary>
        /// Registers <see cref="UserAgentFilteringMiddlewareOptions"/> using a direct manual configuration delegate.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="manualConfigure">
        /// A delegate for configuring <see cref="UserAgentFilteringMiddlewareOptions"/> directly.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload bypasses binding from the application configuration.
        /// It is useful in scenarios where configuration is provided entirely in code.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage:
        /// services.AddUserAgentFilteringMiddleware(options =>
        /// {
        ///     // Configure allowed User-Agent patterns for typical browsers.
        ///     options.Whitelist = new[] { "Mozilla/5.0*", "Chrome/90.0*", "Safari/605.1.15*" };
        ///     
        ///     // Configure disallowed User-Agent patterns for non-browser clients.
        ///     options.Blacklist = new[] { "*curl*", "*wget*", "*python-requests*", "*HttpClient-Test*" };
        ///     
        ///     // Set the response status code for disallowed requests.
        ///     options.DisallowedStatusCode = 400;
        ///     
        ///     // Set the failure rating that will be recorded.
        ///     options.DisallowedFailureRating = 10;
        ///     
        ///     // Determine whether processing should continue when a disallowed User-Agent is encountered.
        ///     options.ContinueOnDisallowed = false;
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddUserAgentFilteringMiddleware(this IServiceCollection services, Action<UserAgentFilteringMiddlewareOptions> manualConfigure)
        {
            // Ensure the MiddlewareFailurePointService is registered.
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Directly configure the options using the provided delegate.
            services.Configure(manualConfigure);

            return services;
        }

        /// <summary>
        /// Registers <see cref="UserAgentFilteringMiddlewareOptions"/> with a default configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload applies a default configuration to <see cref="UserAgentFilteringMiddlewareOptions"/>.
        /// The default settings include values for Whitelist, Blacklist, DisallowedStatusCode, DisallowedFailureRating,
        /// and ContinueOnDisallowed. Ensure these defaults match your application's requirements.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage:
        /// builder.Services.AddUserAgentFilteringMiddleware();
        /// </code>
        /// </example>
        public static IServiceCollection AddUserAgentFilteringMiddleware(this IServiceCollection services)
        {
            return services.AddUserAgentFilteringMiddleware(configuration =>
            {
                configuration.Whitelist = null;
                configuration.Blacklist = new[]
                {
                    "?????", "*curl*", "*https://*", "*http://*", "*python*", "*AsyncHttpClient*",
                    "*Googlebot*", "*google.com*", "*researchscan.com*", "*NetAPI*", "*Go-http-client*",
                    "*ALittle*", "*Root Slut*", "*zgrab*", "*Palo Alto Networks*", "*WebSearch*",
                    "*YaBrowser*", "*UCBrowser*", "*panscient*", "*Firefox/45.0*", "*Firefox/81.0*"
                };
                configuration.DisallowedStatusCode = 400;
                configuration.DisallowedFailureRating = 10;
                configuration.ContinueOnDisallowed = true;
            });
        }
    }
}
