using BlackBytesBox.Routed.RequestFilters.Middleware;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using System;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.IApplicationBuilderExtensions
{
    /// <summary>
    /// Provides extension methods for registering <see cref="HeaderPresentsFilteringMiddleware"/> in the application's request pipeline.
    /// </summary>
    public static partial class IApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds <see cref="HeaderPresentsFilteringMiddleware"/> to the application's request pipeline.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/> used to configure the request pipeline.</param>
        /// <returns>The updated <see cref="IApplicationBuilder"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="app"/> parameter is null.</exception>
        public static IApplicationBuilder UseHeaderPresentsFilteringMiddleware(this IApplicationBuilder app)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            return app.UseMiddleware<HeaderPresentsFilteringMiddleware>();
        }

        /// <summary>
        /// Adds <see cref="HeaderPresentsFilteringMiddleware"/> to the request pipeline with additional configuration.
        /// The extra configuration is applied on top of the dependency-injected options, which are auto-refreshed if appsettings change.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/> used to configure the request pipeline.</param>
        /// <param name="additionalConfigure">A delegate to apply extra configuration to the <see cref="HeaderPresentsFilteringMiddlewareOptions"/>.</param>
        /// <returns>The updated <see cref="IApplicationBuilder"/> instance.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the <paramref name="app"/> or <paramref name="additionalConfigure"/> parameter is null.
        /// </exception>
        public static IApplicationBuilder UseHeaderPresentsFilteringMiddleware(this IApplicationBuilder app, Action<HeaderPresentsFilteringMiddlewareOptions> additionalConfigure)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            if (additionalConfigure == null)
                throw new ArgumentNullException(nameof(additionalConfigure));

            // Retrieve the DI-registered IOptionsMonitor for HeaderPresentsFilteringMiddlewareOptions.
            var innerOptionsMonitor = app.ApplicationServices.GetRequiredService<IOptionsMonitor<HeaderPresentsFilteringMiddlewareOptions>>();

            // Decorate the options monitor so that additional configuration is applied on each access.
            var decoratedOptionsMonitor = new ConfiguredOptionsMonitor<HeaderPresentsFilteringMiddlewareOptions>(innerOptionsMonitor, additionalConfigure);

            // The middleware's constructor expects a RequestDelegate and an IOptionsMonitor of HeaderPresentsFilteringMiddlewareOptions.
            // The RequestDelegate is auto-resolved, and we supply the decorated options monitor.
            return app.UseMiddleware<HeaderPresentsFilteringMiddleware>(decoratedOptionsMonitor);
        }
    }
}
