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
    /// <see cref="SegmentFilteringMiddlewareOptions"/>.
    /// </summary>
    public static partial class IServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="SegmentFilteringMiddlewareOptions"/> from the application configuration
        /// and optionally applies additional code-based configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">
        /// The application configuration containing a section named "<c>SegmentFilteringMiddlewareOptions</c>".
        /// </param>
        /// <param name="manualConfigure">
        /// An optional delegate to modify or augment the bound configuration.
        /// This delegate is applied after binding from the configuration.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// <para>
        /// Ensure that the configuration includes a section with the name
        /// "<c>SegmentFilteringMiddlewareOptions</c>". If the section is missing, default values will be used,
        /// which may not be appropriate for your environment.
        /// </para>
        /// <para>
        /// This method also registers a singleton instance of <see cref="MiddlewareFailurePointService"/>
        /// using <c>TryAddSingleton</c> to avoid duplicate registrations.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddSegmentFilteringMiddleware(this IServiceCollection services, IConfiguration configuration, Action<SegmentFilteringMiddlewareOptions>? manualConfigure = null)
        {
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Bind configuration from appsettings.json (reloadOnChange is enabled by default).
            services.Configure<SegmentFilteringMiddlewareOptions>(configuration.GetSection(nameof(SegmentFilteringMiddlewareOptions)));

            // Optionally apply additional code configuration.
            if (manualConfigure != null)
            {
                services.PostConfigure<SegmentFilteringMiddlewareOptions>(manualConfigure);
            }

            return services;
        }

        /// <summary>
        /// Registers <see cref="SegmentFilteringMiddlewareOptions"/> using a direct manual configuration delegate.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="manualConfigure">
        /// A delegate for configuring <see cref="SegmentFilteringMiddlewareOptions"/> directly.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload bypasses binding from the application configuration.
        /// It is useful in scenarios where configuration is provided entirely in code.
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // Example usage:
        /// services.AddSegmentFilteringMiddleware(options =>
        /// {
        ///     options.Whitelist = new[] { "*" };
        ///     options.Blacklist = new[] { ".git", "cgi-bin", "cgi", "plugins", "fckeditor", "autodiscover", ".env", ".well-known", "HNAP1", "phpmyadmin", "phpunit", "windows", "..." };
        ///     options.DisallowedStatusCode = 400;
        ///     options.DisallowedFailureRating = 10;
        ///     options.ContinueOnDisallowed = true;
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddSegmentFilteringMiddleware(this IServiceCollection services, Action<SegmentFilteringMiddlewareOptions> manualConfigure)
        {
            // Ensure the MiddlewareFailurePointService is registered.
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Directly configure the options using the provided delegate.
            services.Configure(manualConfigure);

            return services;
        }

        /// <summary>
        /// Registers <see cref="SegmentFilteringMiddlewareOptions"/> with a default configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload applies a default configuration to <see cref="SegmentFilteringMiddlewareOptions"/>.
        /// The default settings include values for Whitelist, Blacklist, DisallowedStatusCode, DisallowedFailureRating,
        /// and ContinueOnDisallowed. Ensure these defaults match your application's requirements.
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // Example usage:
        /// builder.Services.AddSegmentFilteringMiddleware();
        /// </code>
        /// </example>
        public static IServiceCollection AddSegmentFilteringMiddleware(this IServiceCollection services)
        {
            return services.AddSegmentFilteringMiddleware(configuration =>
            {
                configuration.FilterPriority = "Blacklist";
                configuration.Whitelist = new[] { "*" };
                configuration.Blacklist = new[] { ".git", "cgi-bin", "cgi", "plugins", "fckeditor", "autodiscover", ".env", ".well-known", "HNAP1", "phpmyadmin", "phpunit", "windows", "..." };
                configuration.CaseSensitive = false;
                configuration.BlacklistStatusCode = StatusCodes.Status403Forbidden;
                configuration.BlacklistFailureRating = 1;
                configuration.BlacklistContinue = true;
                configuration.NotMatchedStatusCode = StatusCodes.Status403Forbidden;
                configuration.NotMatchedFailureRating = 0;
                configuration.NotMatchedContinue = true;
                configuration.NotMatchedLogWarning = true;
                configuration.UnreadableStatusCode = StatusCodes.Status403Forbidden;
                configuration.UnreadableFailureRating = 1;
                configuration.UnreadableContinue = true;
            });
        }
    }
}