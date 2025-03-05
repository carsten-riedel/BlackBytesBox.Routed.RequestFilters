using BlackBytesBox.Routed.RequestFilters.Middleware;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using System;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.IApplicationBuilderExtensions
{
    /// <summary>
    /// Provides extension methods for registering the <see cref="UserAgentFilteringMiddleware"/> in the application's request pipeline.
    /// </summary>
    public static partial class IApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds the <see cref="UserAgentFilteringMiddleware"/> to the application's request pipeline.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The updated application builder.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="app"/> is null.</exception>
        public static IApplicationBuilder UseUserAgentFilteringMiddleware(this IApplicationBuilder app)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            return app.UseMiddleware<UserAgentFilteringMiddleware>();
        }

        /// <summary>
        /// Adds the <see cref="UserAgentFilteringMiddleware"/> to the request pipeline while applying additional configuration.
        /// The additional configuration is applied on top of the DI‑registered options, which are auto‑refreshed if appsettings change.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="additionalConfigure">A delegate to apply extra configuration to the <see cref="UserAgentFilteringMiddlewareOptions"/>.</param>
        /// <returns>The updated application builder.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="app"/> or <paramref name="additionalConfigure"/> is null.
        /// </exception>
        public static IApplicationBuilder UseUserAgentFilteringMiddleware(this IApplicationBuilder app, Action<UserAgentFilteringMiddlewareOptions> additionalConfigure)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            if (additionalConfigure == null)
                throw new ArgumentNullException(nameof(additionalConfigure));

            // Retrieve the DI-registered IOptionsMonitor<UserAgentFilteringMiddlewareOptions>.
            var innerOptionsMonitor = app.ApplicationServices.GetRequiredService<IOptionsMonitor<UserAgentFilteringMiddlewareOptions>>();

            // Wrap it with our decorator so that additionalConfigure is applied on each access.
            var decoratedOptionsMonitor = new ConfiguredOptionsMonitor<UserAgentFilteringMiddlewareOptions>(innerOptionsMonitor, additionalConfigure);

            // Supply the decorated options monitor to the middleware.
            return app.UseMiddleware<UserAgentFilteringMiddleware>(decoratedOptionsMonitor);
        }
    }
}
