using System;

using BlackBytesBox.Routed.RequestFilters.Middleware;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.IApplicationBuilderExtensions
{
    /// <summary>
    /// Provides extension methods for registering <see cref="FailurePointsFilteringMiddleware"/> in the application's request pipeline.
    /// </summary>
    public static partial class IApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds <see cref="FailurePointsFilteringMiddleware"/> to the application's request pipeline.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The updated application builder.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="app"/> is null.</exception>
        public static IApplicationBuilder UseFailurePointsFilteringMiddleware(this IApplicationBuilder app)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            return app.UseMiddleware<FailurePointsFilteringMiddleware>();
        }

        /// <summary>
        /// Adds <see cref="FailurePointsFilteringMiddleware"/> to the request pipeline while applying additional configuration.
        /// The extra configuration is applied on top of the DI‑registered options (which are auto‑refreshed if appsettings change).
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="additionalConfigure">
        /// A delegate to apply extra configuration to <see cref="FailurePointsFilteringMiddlewareOptions"/>.
        /// </param>
        /// <returns>The updated application builder.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="app"/> or <paramref name="additionalConfigure"/> is null.
        /// </exception>
        public static IApplicationBuilder UseFailurePointsFilteringMiddleware(this IApplicationBuilder app, Action<FailurePointsFilteringMiddlewareOptions> additionalConfigure)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            if (additionalConfigure == null)
                throw new ArgumentNullException(nameof(additionalConfigure));

            // Retrieve the DI-registered IOptionsMonitor<FailurePointsFilteringMiddlewareOptions>.
            var innerOptionsMonitor = app.ApplicationServices.GetRequiredService<IOptionsMonitor<FailurePointsFilteringMiddlewareOptions>>();

            // Wrap it with our decorator so that additionalConfigure is applied on each access.
            var decoratedOptionsMonitor = new ConfiguredOptionsMonitor<FailurePointsFilteringMiddlewareOptions>(innerOptionsMonitor, additionalConfigure);

            // The middleware's constructor accepts (RequestDelegate, IOptionsMonitor<FailurePointsFilteringMiddlewareOptions>).
            // The RequestDelegate is auto-resolved, and we supply our decoratedOptionsMonitor.
            return app.UseMiddleware<FailurePointsFilteringMiddleware>(decoratedOptionsMonitor);
        }
    }
}
