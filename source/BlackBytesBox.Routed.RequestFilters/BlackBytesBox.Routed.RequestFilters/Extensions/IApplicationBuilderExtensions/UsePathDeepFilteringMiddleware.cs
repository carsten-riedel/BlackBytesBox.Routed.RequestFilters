﻿using System;

using BlackBytesBox.Routed.RequestFilters.Middleware;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.IApplicationBuilderExtensions
{
    /// <summary>
    /// Provides extension methods for registering <see cref="PathDeepFilteringMiddleware"/> in the application's request pipeline.
    /// </summary>
    public static partial class IApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds <see cref="PathDeepFilteringMiddleware"/> to the application's request pipeline.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The updated application builder.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="app"/> is null.</exception>
        public static IApplicationBuilder UsePathDeepFilteringMiddleware(this IApplicationBuilder app)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));

            // Check if the RemoteIPFilteringMiddleware has already been added.
            if (!app.GetProperty<RemoteIPFilteringMiddleware, bool>())
            {
                app.SetProperty<RemoteIPFilteringMiddleware, bool>(true);
                app.UseMiddleware<RemoteIPFilteringMiddleware>();
            }

            return app.UseMiddleware<PathDeepFilteringMiddleware>();
        }

        /// <summary>
        /// Adds <see cref="PathDeepFilteringMiddleware"/> to the request pipeline while applying additional configuration.
        /// The extra configuration is applied on top of the DI‑registered options (which are auto‑refreshed if appsettings change).
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="additionalConfigure">
        /// A delegate to apply extra configuration to <see cref="PathDeepFilteringMiddlewareOptions"/>.
        /// </param>
        /// <returns>The updated application builder.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="app"/> or <paramref name="additionalConfigure"/> is null.
        /// </exception>
        public static IApplicationBuilder UsePathDeepFilteringMiddleware(this IApplicationBuilder app, Action<PathDeepFilteringMiddlewareOptions> additionalConfigure)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            if (additionalConfigure == null)
                throw new ArgumentNullException(nameof(additionalConfigure));

            // Retrieve the DI-registered IOptionsMonitor for PathDeepFilteringMiddlewareOptions.
            var innerOptionsMonitor = app.ApplicationServices.GetRequiredService<IOptionsMonitor<PathDeepFilteringMiddlewareOptions>>();

            // Decorate the options monitor so that additional configuration is applied on each access.
            var decoratedOptionsMonitor = new ConfiguredOptionsMonitor<PathDeepFilteringMiddlewareOptions>(innerOptionsMonitor, additionalConfigure);

            // Check if the RemoteIPFilteringMiddleware has already been added.
            if (!app.GetProperty<RemoteIPFilteringMiddleware, bool>())
            {
                app.SetProperty<RemoteIPFilteringMiddleware, bool>(true);
                app.UseMiddleware<RemoteIPFilteringMiddleware>();
            }

            // The middleware's constructor expects a RequestDelegate and an IOptionsMonitor of PathDeepFilteringMiddlewareOptions.
            // The RequestDelegate is auto-resolved, and we supply our decorated options monitor.
            return app.UseMiddleware<PathDeepFilteringMiddleware>(decoratedOptionsMonitor);
        }
    }
}
