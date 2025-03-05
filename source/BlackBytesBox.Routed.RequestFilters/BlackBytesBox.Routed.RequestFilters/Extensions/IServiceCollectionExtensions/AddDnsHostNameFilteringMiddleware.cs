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
    /// <see cref="DnsHostNameFilteringMiddlewareOptions"/>.
    /// </summary>
    public static partial class IServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="DnsHostNameFilteringMiddlewareOptions"/> from the application configuration
        /// and optionally applies additional code-based configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">
        /// The application configuration containing a section named "<c>DnsHostNameFilteringMiddlewareOptions</c>".
        /// </param>
        /// <param name="manualConfigure">
        /// An optional delegate to modify or augment the bound configuration.
        /// This delegate is applied after binding from the configuration.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// <para>
        /// Ensure that the configuration includes a section with the name "<c>DnsHostNameFilteringMiddlewareOptions</c>".
        /// If the section is missing, default values will be used, which may not be appropriate for your environment.
        /// </para>
        /// <para>
        /// Also, this method registers a singleton instance of <see cref="MiddlewareFailurePointService"/> 
        /// using <c>TryAddSingleton</c> to avoid duplicate registrations.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddDnsHostNameFilteringMiddleware(this IServiceCollection services, IConfiguration configuration, Action<DnsHostNameFilteringMiddlewareOptions>? manualConfigure = null)
        {
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Bind configuration from appsettings.json (ensure reloadOnChange is enabled, which is the default).
            services.Configure<DnsHostNameFilteringMiddlewareOptions>(configuration.GetSection(nameof(DnsHostNameFilteringMiddlewareOptions)));

            // Optionally apply additional code configuration.
            if (manualConfigure != null)
            {
                services.PostConfigure<DnsHostNameFilteringMiddlewareOptions>(manualConfigure);
            }

            return services;
        }

        /// <summary>
        /// Registers <see cref="DnsHostNameFilteringMiddlewareOptions"/> using a direct manual configuration delegate.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="manualConfigure">
        /// A delegate for configuring <see cref="DnsHostNameFilteringMiddlewareOptions"/> directly.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload bypasses binding from the application configuration.
        /// It is useful in scenarios where configuration is provided entirely in code.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage:
        /// services.AddDnsHostNameFilteringMiddleware(options =>
        /// {
        ///     options.Whitelist = new[] { "example.com", "*.example.com" };
        ///     options.Blacklist = new[] { "badexample.com" };
        ///     options.DisallowedStatusCode = 400;
        ///     options.DisallowedFailureRating = 10;
        ///     options.ContinueOnDisallowed = true;
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddDnsHostNameFilteringMiddleware(this IServiceCollection services, Action<DnsHostNameFilteringMiddlewareOptions> manualConfigure)
        {
            // Ensure the MiddlewareFailurePointService is registered.
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Directly configure the options using the provided delegate.
            services.Configure(manualConfigure);

            return services;
        }

        /// <summary>
        /// Registers <see cref="DnsHostNameFilteringMiddlewareOptions"/> with a default configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload applies a default configuration to <see cref="DnsHostNameFilteringMiddlewareOptions"/>.
        /// The default settings include values for Whitelist, Blacklist, DisallowedStatusCode, DisallowedFailureRating,
        /// and ContinueOnDisallowed. Ensure these defaults match your application's requirements.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage:
        /// builder.Services.AddDnsHostNameFilteringMiddleware();
        /// </code>
        /// </example>
        public static IServiceCollection AddDnsHostNameFilteringMiddleware(this IServiceCollection services)
        {
            return services.AddDnsHostNameFilteringMiddleware(configuration =>
            {
                configuration.Whitelist = null;
                configuration.Blacklist = new[] { "*amazonaws*", "*googleusercontent*", "*googlebot*", "*stretchoid*", "*binaryedge.ninja*", "*shodan.io*", "*shadowserver.org*", "*aeza.network*", "*datapacket*", "*masterinter*", "*shadowbrokers*", "*beget.com*", "*.ru*", "*marsdatacenter*", "*.onyphe.net" };
                configuration.DisallowedStatusCode = 400;
                configuration.DisallowedFailureRating = 10;
                configuration.ContinueOnDisallowed = true;
            });
        }
    }
}
