﻿using System;

using BlackBytesBox.Routed.RequestFilters.Middleware;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using BlackBytesBox.Routed.RequestFilters.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.IServiceCollectionExtensions
{
    /// <summary>
    /// Provides extension methods for configuring the <see cref="IServiceCollection"/> with
    /// <see cref="HeaderValuesRequiredFilteringMiddlewareOptions"/>.
    /// </summary>
    public static partial class IServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="HeaderValuesRequiredFilteringMiddlewareOptions"/> from the application configuration
        /// and optionally applies additional code-based configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">
        /// The application configuration containing a section named "<c>HeaderValuesRequiredFilteringMiddlewareOptions</c>".
        /// </param>
        /// <param name="manualConfigure">
        /// An optional delegate to modify or augment the bound configuration.
        /// This delegate is applied after binding from the configuration.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// <para>
        /// Ensure that the configuration includes a section with the name
        /// "<c>HeaderValuesRequiredFilteringMiddlewareOptions</c>". If the section is missing, default values will be used,
        /// which may not be appropriate for your environment.
        /// </para>
        /// <para>
        /// This method also registers a singleton instance of <see cref="MiddlewareFailurePointService"/> using
        /// <c>TryAddSingleton</c> to avoid duplicate registrations.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddHeaderValuesRequiredFilteringMiddleware(this IServiceCollection services, IConfiguration configuration, Action<HeaderValuesRequiredFilteringMiddlewareOptions>? manualConfigure = null)
        {
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Bind configuration from appsettings.json (reloadOnChange is enabled by default).
            services.Configure<HeaderValuesRequiredFilteringMiddlewareOptions>(configuration.GetSection(nameof(HeaderValuesRequiredFilteringMiddlewareOptions)));

            // Optionally apply additional code configuration.
            if (manualConfigure != null)
            {
                services.PostConfigure<HeaderValuesRequiredFilteringMiddlewareOptions>(manualConfigure);
            }

            return services;
        }

        /// <summary>
        /// Registers <see cref="HeaderValuesRequiredFilteringMiddlewareOptions"/> using a direct manual configuration delegate.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="manualConfigure">
        /// A delegate for configuring <see cref="HeaderValuesRequiredFilteringMiddlewareOptions"/> directly.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload bypasses binding from the application configuration.
        /// It is useful in scenarios where configuration is provided entirely in code.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage:
        /// services.AddHeaderValuesRequiredFilteringMiddleware(options =>
        /// {
        ///     options.Headers.Add("User-Agent", new HeaderFilterRule() { Allowed = new string[] { "*" } });
        ///     options.DisallowedStatusCode = 400;
        ///     options.DisallowedFailureRating = 1;
        ///     options.ContinueOnDisallowed = true;
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddHeaderValuesRequiredFilteringMiddleware(this IServiceCollection services, Action<HeaderValuesRequiredFilteringMiddlewareOptions> manualConfigure)
        {
            // Ensure the MiddlewareFailurePointService is registered.
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Directly configure the options using the provided delegate.
            services.Configure(manualConfigure);

            return services;
        }

        /// <summary>
        /// Registers <see cref="HeaderValuesRequiredFilteringMiddlewareOptions"/> with a default configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload applies a default configuration to <see cref="HeaderValuesRequiredFilteringMiddlewareOptions"/>.
        /// The default settings include values for Headers, DisallowedStatusCode, DisallowedFailureRating,
        /// and ContinueOnDisallowed. Ensure these defaults match your application's requirements.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage:
        /// builder.Services.AddHeaderValuesRequiredFilteringMiddleware();
        /// </code>
        /// </example>
        public static IServiceCollection AddHeaderValuesRequiredFilteringMiddleware(this IServiceCollection services)
        {
            return services.AddHeaderValuesRequiredFilteringMiddleware(configuration =>
            {
                configuration.Headers.Add("User-Agent", new HeaderFilterRule() { Allowed = new string[] { "*" } });
                configuration.DisallowedStatusCode = 400;
                configuration.DisallowedFailureRating = 1;
                configuration.ContinueOnDisallowed = true;
            });
        }
    }
}