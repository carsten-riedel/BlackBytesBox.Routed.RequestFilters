using System;

using BlackBytesBox.Routed.RequestFilters.Middleware;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.IApplicationBuilderExtensions
{
    /// <summary>
    /// Provides extension methods for registering <see cref="DnsHostNameFilteringMiddleware"/> in the application's request pipeline.
    /// </summary>
    public static partial class IApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds <see cref="DnsHostNameFilteringMiddleware"/> to the application's request pipeline.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The updated application builder.</returns>
        public static IApplicationBuilder UseAcceptLanguageFilteringMiddleware(this IApplicationBuilder app)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            return app.UseMiddleware<AcceptLanguageFilteringMiddleware>();
        }

        /// <summary>
        /// Adds <see cref="DnsHostNameFilteringMiddleware"/> to the request pipeline while applying an additional configuration.
        /// The extra configuration is applied on top of the DI‑registered options (which are auto‑refreshed if appsettings change).
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="additionalConfigure">A delegate to apply extra configuration to <see cref="DnsHostNameFilteringMiddlewareOptions"/>.</param>
        /// <returns>The updated application builder.</returns>
        public static IApplicationBuilder UseAcceptLanguageFilteringMiddleware(this IApplicationBuilder app, Action<AcceptLanguageFilteringMiddlewareOptions> additionalConfigure)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            if (additionalConfigure == null)
                throw new ArgumentNullException(nameof(additionalConfigure));

            // Retrieve the DI-registered IOptionsMonitor<DnsHostNameFilteringMiddlewareOptions>.
            var innerOptionsMonitor = app.ApplicationServices.GetRequiredService<IOptionsMonitor<AcceptLanguageFilteringMiddlewareOptions>>();

            // Wrap it with our decorator so that additionalConfigure is applied on each access.
            var decoratedOptionsMonitor = new ConfiguredOptionsMonitor<AcceptLanguageFilteringMiddlewareOptions>(innerOptionsMonitor, additionalConfigure);

            // The middleware's constructor: (RequestDelegate, IOptionsMonitor<DnsHostNameFilteringMiddlewareOptions>)
            // The RequestDelegate is auto-resolved, and we supply our decoratedOptionsMonitor.
            return app.UseMiddleware<AcceptLanguageFilteringMiddleware>(decoratedOptionsMonitor);
        }
    }
}