using System;

using BlackBytesBox.Routed.RequestFilters.Middleware;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.IApplicationBuilderExtensions
{
    /// <summary>
    /// Provides extension methods for registering <see cref="HttpProtocolFilteringMiddleware"/> in the application's request pipeline.
    /// </summary>
    public static partial class IApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds MyMiddleware to the application's request pipeline.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The updated application builder.</returns>
        public static IApplicationBuilder UseHttpProtocolFilteringMiddleware(this IApplicationBuilder app)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));

            // Check if the RemoteIPFilteringMiddleware has already been added.
            if (!app.GetProperty<RemoteIPFilteringMiddleware, bool>())
            {
                app.SetProperty<RemoteIPFilteringMiddleware, bool>(true);
                app.UseMiddleware<RemoteIPFilteringMiddleware>();
            }

            return app.UseMiddleware<HttpProtocolFilteringMiddleware>();
        }

        /// <summary>
        /// Adds MyMiddleware to the request pipeline while applying an additional configuration.
        /// The extra configuration is applied on top of the DI‑registered options (which are auto‑refreshed if appsettings change).
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="additionalConfigure">A delegate to apply extra configuration.</param>
        /// <returns>The updated application builder.</returns>
        public static IApplicationBuilder UseHttpProtocolFilteringMiddleware(this IApplicationBuilder app, Action<HttpProtocolFilteringMiddlewareOptions> additionalConfigure)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            if (additionalConfigure == null)
                throw new ArgumentNullException(nameof(additionalConfigure));

            // Retrieve the DI-registered IOptionsMonitor<MyMiddlewareOptions>.
            var innerOptionsMonitor = app.ApplicationServices.GetRequiredService<IOptionsMonitor<HttpProtocolFilteringMiddlewareOptions>>();

            // Wrap it with our decorator so that additionalConfigure is applied on each access.
            var decoratedOptionsMonitor = new ConfiguredOptionsMonitor<HttpProtocolFilteringMiddlewareOptions>(innerOptionsMonitor, additionalConfigure);

            // Check if the RemoteIPFilteringMiddleware has already been added.
            if (!app.GetProperty<RemoteIPFilteringMiddleware, bool>())
            {
                app.SetProperty<RemoteIPFilteringMiddleware, bool>(true);
                app.UseMiddleware<RemoteIPFilteringMiddleware>();
            }

            // The middleware's constructor: (RequestDelegate, IOptionsMonitor<MyMiddlewareOptions>)
            // The RequestDelegate is auto-resolved, and we supply our decoratedOptionsMonitor.
            return app.UseMiddleware<HttpProtocolFilteringMiddleware>(decoratedOptionsMonitor);
        }
    }
}
