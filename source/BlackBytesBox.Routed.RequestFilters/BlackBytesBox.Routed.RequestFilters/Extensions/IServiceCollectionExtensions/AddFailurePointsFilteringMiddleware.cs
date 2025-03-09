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
    /// <see cref="FailurePointsFilteringMiddlewareOptions"/>.
    /// </summary>
    public static partial class IServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="FailurePointsFilteringMiddlewareOptions"/> from the application configuration
        /// and optionally applies additional code-based configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">
        /// The application configuration containing a section named "<c>FailurePointsFilteringMiddlewareOptions</c>".
        /// </param>
        /// <param name="manualConfigure">
        /// An optional delegate to modify or augment the bound configuration.
        /// This delegate is applied after binding from the configuration.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// <para>
        /// Ensure that the configuration includes a section with the name
        /// "<c>FailurePointsFilteringMiddlewareOptions</c>". If the section is missing, default values will be used,
        /// which may not be appropriate for your environment.
        /// </para>
        /// <para>
        /// This method also registers a singleton instance of <see cref="MiddlewareFailurePointService"/> using
        /// <c>TryAddSingleton</c> to avoid duplicate registrations.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddFailurePointsFilteringMiddleware(this IServiceCollection services, IConfiguration configuration, Action<FailurePointsFilteringMiddlewareOptions>? manualConfigure = null)
        {
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Bind configuration from the application configuration.
            services.Configure<FailurePointsFilteringMiddlewareOptions>(configuration.GetSection(nameof(FailurePointsFilteringMiddlewareOptions)));

            // Optionally apply additional code configuration.
            if (manualConfigure != null)
            {
                services.PostConfigure<FailurePointsFilteringMiddlewareOptions>(manualConfigure);
            }

            return services;
        }

        /// <summary>
        /// Registers <see cref="FailurePointsFilteringMiddlewareOptions"/> using a direct manual configuration delegate.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="manualConfigure">
        /// A delegate for configuring <see cref="FailurePointsFilteringMiddlewareOptions"/> directly.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload bypasses binding from the application configuration.
        /// It is useful in scenarios where configuration is provided entirely in code.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage:
        /// services.AddFailurePointsFilteringMiddleware(options =>
        /// {
        ///     options.FailurePointsLimit = 0;
        ///     options.DisallowedStatusCode = 400;
        ///     options.ContinueOnDisallowed = false;
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddFailurePointsFilteringMiddleware(this IServiceCollection services, Action<FailurePointsFilteringMiddlewareOptions> manualConfigure)
        {
            // Ensure the MiddlewareFailurePointService is registered.
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Directly configure the options using the provided delegate.
            services.Configure(manualConfigure);

            return services;
        }

        /// <summary>
        /// Registers <see cref="FailurePointsFilteringMiddlewareOptions"/> with a default configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload applies a default configuration to <see cref="FailurePointsFilteringMiddlewareOptions"/>.
        /// The default settings include values for <see cref="FailurePointsFilteringMiddlewareOptions.FailurePointsLimit"/>,
        /// <see cref="FailurePointsFilteringMiddlewareOptions.DisallowedStatusCode"/>,
        /// and <see cref="FailurePointsFilteringMiddlewareOptions.ContinueOnDisallowed"/>.
        /// Ensure these defaults match your application's requirements.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage:
        /// builder.Services.AddFailurePointsFilteringMiddleware();
        /// </code>
        /// </example>
        public static IServiceCollection AddFailurePointsFilteringMiddleware(this IServiceCollection services)
        {
            return services.AddFailurePointsFilteringMiddleware(configuration =>
            {
                configuration.DumpFilePath = System.IO.Path.Combine(AppContext.BaseDirectory, "FailurePointsFilteringMiddleware.json");
                configuration.FailurePointsLimit = 0;
                configuration.DisallowedStatusCode = 400;
                configuration.ContinueOnDisallowed = false;
            });
        }
    }
}
