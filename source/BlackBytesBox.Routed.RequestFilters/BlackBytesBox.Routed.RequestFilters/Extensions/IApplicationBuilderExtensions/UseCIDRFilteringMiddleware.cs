using System;

using BlackBytesBox.Routed.RequestFilters.Middleware;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.IApplicationBuilderExtensions
{
    /// <summary>
    /// Provides extension methods for registering <see cref="CIDRFilteringMiddleware"/> in the application's request pipeline.
    /// </summary>
    public static partial class IApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds <see cref="CIDRFilteringMiddleware"/> to the application's request pipeline.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The updated application builder.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="app"/> is null.</exception>
        public static IApplicationBuilder UseCIDRFilteringMiddleware(this IApplicationBuilder app)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            // If RemoteIPFilteringMiddleware hasn't been added yet, add it.
            // This middleware must be registered first to ensure that basic IP filtering is applied before CIDR filtering.
            if (!app.GetProperty<RemoteIPFilteringMiddleware, bool>())
            {
                app.SetProperty<RemoteIPFilteringMiddleware, bool>(true);
                app.UseMiddleware<RemoteIPFilteringMiddleware>();
            }

            return app.UseMiddleware<CIDRFilteringMiddleware>();
        }

        /// <summary>
        /// Adds <see cref="CIDRFilteringMiddleware"/> to the application's request pipeline with additional custom configuration.
        /// The additional configuration supplements the dependency injection–registered options, which are auto‑refreshed when the configuration changes.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="additionalConfigure">
        /// A delegate to apply extra configuration to <see cref="CIDRFilteringMiddlewareOptions"/>.
        /// </param>
        /// <returns>The updated application builder.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="app"/> or <paramref name="additionalConfigure"/> is null.
        /// </exception>
        public static IApplicationBuilder UseCIDRFilteringMiddleware(this IApplicationBuilder app, Action<CIDRFilteringMiddlewareOptions> additionalConfigure)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }
            if (additionalConfigure == null)
            {
                throw new ArgumentNullException(nameof(additionalConfigure));
            }

            // Retrieve the DI-registered IOptionsMonitor for CIDRFilteringMiddlewareOptions.
            var innerOptionsMonitor = app.ApplicationServices.GetRequiredService<IOptionsMonitor<CIDRFilteringMiddlewareOptions>>();

            // Decorate the options monitor so that additional configuration is applied each time the options are accessed.
            var decoratedOptionsMonitor = new ConfiguredOptionsMonitor<CIDRFilteringMiddlewareOptions>(innerOptionsMonitor, additionalConfigure);

            // If RemoteIPFilteringMiddleware hasn't been added yet, add it.
            // This ensures IP filtering is properly configured before applying CIDR filtering.
            if (!app.GetProperty<RemoteIPFilteringMiddleware, bool>())
            {
                app.SetProperty<RemoteIPFilteringMiddleware, bool>(true);
                app.UseMiddleware<RemoteIPFilteringMiddleware>();
            }

            // CIDRFilteringMiddleware's constructor expects a RequestDelegate and an IOptionsMonitor of CIDRFilteringMiddlewareOptions.
            // The RequestDelegate is auto-resolved, and we supply our decorated options monitor with custom settings.
            return app.UseMiddleware<CIDRFilteringMiddleware>(decoratedOptionsMonitor);
        }
    }
}
