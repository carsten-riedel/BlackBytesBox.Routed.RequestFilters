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
    /// <see cref="HttpProtocolFilteringMiddlewareOptions"/>.
    /// </summary>
    public static partial class IServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="HttpProtocolFilteringMiddlewareOptions"/> from the application configuration
        /// and optionally applies additional code-based configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">
        /// The application configuration containing a section named "<c>HttpProtocolFilteringMiddlewareOptions</c>".
        /// </param>
        /// <param name="manualConfigure">
        /// An optional delegate to modify or augment the bound configuration.
        /// This delegate is applied after binding from the configuration.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// <para>
        /// Ensure that the configuration includes a section with the name 
        /// "<c>HttpProtocolFilteringMiddlewareOptions</c>". If the section is missing, default values will be used,
        /// which may not be appropriate for your environment.
        /// </para>
        /// <para>
        /// This method also registers a singleton instance of <see cref="MiddlewareFailurePointService"/>
        /// using <c>TryAddSingleton</c> to avoid duplicate registrations.
        /// </para>
        /// </remarks>
        /// <example>
        /// Configuration in appsettings.json:
        /// <code>
        /// {
        ///   "HttpProtocolFilteringMiddlewareOptions": {
        ///     "Whitelist": [
        ///       "HTTP/2",
        ///       "HTTP/2.0",
        ///       "HTTP/3",
        ///       "HTTP/3.0"
        ///     ],
        ///     "Blacklist": [
        ///       "HTTP/1.0",
        ///       "HTTP/1.?",
        ///       ""
        ///     ],
        ///     "DisallowedStatusCode": 426,
        ///     "DisallowedFailureRating": 5,
        ///     "ContinueOnDisallowed": false
        ///   }
        /// }
        /// </code>
        /// 
        /// Usage in Startup.cs or Program.cs:
        /// <code>
        /// builder.Services.AddHttpProtocolFilteringMiddleware(
        ///     builder.Configuration,
        ///     options => 
        ///     {
        ///         // Allow HTTP/1.1 in addition to HTTP/2 and HTTP/3
        ///         options.Whitelist = options.Whitelist
        ///             .Concat(new[] { "HTTP/1.1" })
        ///             .ToArray();
        ///             
        ///         // Use Upgrade Required status code
        ///         options.DisallowedStatusCode = StatusCodes.Status426UpgradeRequired;
        ///     });
        /// </code>
        /// </example>
        public static IServiceCollection AddHttpProtocolFilteringMiddleware(this IServiceCollection services, IConfiguration configuration, Action<HttpProtocolFilteringMiddlewareOptions>? manualConfigure = null)
        {
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Bind configuration from appsettings.json (ensure reloadOnChange is enabled, which is the default).
            services.Configure<HttpProtocolFilteringMiddlewareOptions>(configuration.GetSection(nameof(HttpProtocolFilteringMiddlewareOptions)));

            // Optionally apply additional code configuration.
            if (manualConfigure != null)
            {
                services.PostConfigure<HttpProtocolFilteringMiddlewareOptions>(manualConfigure);
            }

            return services;
        }

        /// <summary>
        /// Registers <see cref="HttpProtocolFilteringMiddlewareOptions"/> using a direct manual configuration delegate.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="manualConfigure">
        /// A delegate for configuring <see cref="HttpProtocolFilteringMiddlewareOptions"/> directly.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload bypasses binding from the application configuration.
        /// It is useful in scenarios where configuration is provided entirely in code.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage in Startup.cs or Program.cs:
        /// builder.Services.AddHttpProtocolFilteringMiddleware(options =>
        /// {
        ///     // Allow modern HTTP protocols
        ///     options.Whitelist = new[] { 
        ///         "HTTP/2",     // HTTP/2 protocol
        ///         "HTTP/2.0",   // Alternative HTTP/2 notation
        ///         "HTTP/3",     // HTTP/3 protocol
        ///         "HTTP/3.0"    // Alternative HTTP/3 notation
        ///     };
        ///     
        ///     // Block legacy and invalid protocols
        ///     options.Blacklist = new[] { 
        ///         "",           // Empty protocol string
        ///         "HTTP/1.0",   // Legacy HTTP/1.0
        ///         "HTTP/1.?"    // Any HTTP/1.x variant
        ///     };
        ///     
        ///     // Return 426 Upgrade Required for disallowed protocols
        ///     options.DisallowedStatusCode = StatusCodes.Status426UpgradeRequired;
        ///     
        ///     // Set failure rating for monitoring
        ///     options.DisallowedFailureRating = 5;
        ///     
        ///     // Stop processing pipeline for disallowed protocols
        ///     options.ContinueOnDisallowed = false;
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddHttpProtocolFilteringMiddleware(this IServiceCollection services, Action<HttpProtocolFilteringMiddlewareOptions> manualConfigure)
        {
            // Ensure the MiddlewareFailurePointService is registered.
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Directly configure the options using the provided delegate.
            services.Configure(manualConfigure);

            return services;
        }

        /// <summary>
        /// Registers <see cref="HttpProtocolFilteringMiddlewareOptions"/> with a default configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload applies a default configuration to <see cref="HttpProtocolFilteringMiddlewareOptions"/>.
        /// The default settings include values for Whitelist (HTTP/1.1, HTTP/2, HTTP/3), Blacklist (HTTP/1.0),
        /// DisallowedStatusCode, DisallowedFailureRating, and ContinueOnDisallowed. Ensure these defaults
        /// match your application's requirements.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage in Startup.cs or Program.cs:
        /// var builder = WebApplication.CreateBuilder(args);
        /// 
        /// // Add services to the container
        /// builder.Services.AddHttpProtocolFilteringMiddleware(); // Uses default configuration
        /// 
        /// var app = builder.Build();
        /// 
        /// // Configure the HTTP request pipeline
        /// app.UseHttpsRedirection();
        /// app.UseHttpProtocolFilteringMiddleware(); // Add protocol filtering before other middleware
        /// app.MapControllers();
        /// 
        /// app.Run();
        /// </code>
        /// </example>
        public static IServiceCollection AddHttpProtocolFilteringMiddleware(this IServiceCollection services)
        {
            return services.AddHttpProtocolFilteringMiddleware(configuration =>
            {
                configuration.Whitelist = new[] { "HTTP/1.1", "HTTP/2", "HTTP/2.0", "HTTP/3", "HTTP/3.0" };
                configuration.Blacklist = new[] { "", "HTTP/1.0", "HTTP/1.?" };
                configuration.DisallowedStatusCode = 400;
                configuration.DisallowedFailureRating = 10;
                configuration.ContinueOnDisallowed = true;
            });
        }
    }
}