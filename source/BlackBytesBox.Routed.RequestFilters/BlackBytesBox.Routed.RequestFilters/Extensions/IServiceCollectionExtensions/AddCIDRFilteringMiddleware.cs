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
    /// Extension methods for configuring <see cref="CIDRFilteringMiddlewareOptions"/>.
    /// </summary>
    public static partial class IServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="CIDRFilteringMiddlewareOptions"/> from configuration with optional code configuration.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configuration">Configuration with "<c>CIDRFilteringMiddlewareOptions</c>" section.</param>
        /// <param name="manualConfigure">Optional delegate for further configuration.</param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// Also registers <see cref="MiddlewareFailurePointService"/> as singleton.
        /// </remarks>
        public static IServiceCollection AddCIDRFilteringMiddleware(this IServiceCollection services, IConfiguration configuration, Action<CIDRFilteringMiddlewareOptions>? manualConfigure = null)
        {
            services.TryAddSingleton<MiddlewareFailurePointService>();

            services.Configure<CIDRFilteringMiddlewareOptions>(configuration.GetSection(nameof(CIDRFilteringMiddlewareOptions)));

            if (manualConfigure != null)
            {
                services.PostConfigure<CIDRFilteringMiddlewareOptions>(manualConfigure);
            }

            return services;
        }

        /// <summary>
        /// Registers <see cref="CIDRFilteringMiddlewareOptions"/> using a manual configuration delegate.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="manualConfigure">Delegate to configure the options.</param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// Bypasses configuration binding.
        /// </remarks>
        public static IServiceCollection AddCIDRFilteringMiddleware(this IServiceCollection services, Action<CIDRFilteringMiddlewareOptions> manualConfigure)
        {
            services.TryAddSingleton<MiddlewareFailurePointService>();

            services.Configure(manualConfigure);

            return services;
        }

        /// <summary>
        /// Registers <see cref="CIDRFilteringMiddlewareOptions"/> with default settings.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// Defaults include Whitelist, Blacklist, status codes, failure ratings, and logging options.
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// builder.Services.AddCIDRFilteringMiddleware();
        /// </code>
        /// </example>
        public static IServiceCollection AddCIDRFilteringMiddleware(this IServiceCollection services)
        {
            return services.AddCIDRFilteringMiddleware(configuration =>
            {
                configuration.FilterPriority = "Whitelist";
                configuration.Whitelist = new[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16" };
                configuration.Blacklist = new[] { "*" };
                configuration.BlacklistStatusCode = StatusCodes.Status403Forbidden;
                configuration.BlacklistFailureRating = 1;
                configuration.BlacklistContinue = true;
                configuration.NotMatchedStatusCode = StatusCodes.Status403Forbidden;
                configuration.NotMatchedFailureRating = 0;
                configuration.NotMatchedContinue = true;
                configuration.NotMatchedLogWarning = true;
            });
        }
    }
}
