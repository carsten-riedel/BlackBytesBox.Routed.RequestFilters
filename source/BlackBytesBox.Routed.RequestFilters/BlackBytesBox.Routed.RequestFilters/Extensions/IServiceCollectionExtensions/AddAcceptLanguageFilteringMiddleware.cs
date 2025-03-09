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
    /// <see cref="AcceptLanguageFilteringMiddlewareOptions"/>.
    /// </summary>
    public static partial class IServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="AcceptLanguageFilteringMiddlewareOptions"/> from the application configuration
        /// and optionally applies additional code-based configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">
        /// The application configuration containing a section named "<c>AcceptLanguageFilteringMiddlewareOptions</c>".
        /// </param>
        /// <param name="manualConfigure">
        /// An optional delegate to modify or augment the bound configuration.
        /// This delegate is applied after binding from the configuration.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// <para>
        /// Ensure that the configuration includes a section with the name 
        /// "<c>AcceptLanguageFilteringMiddlewareOptions</c>". If the section is missing, default values will be used,
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
        ///   "AcceptLanguageFilteringMiddlewareOptions": {
        ///     "Whitelist": ["en-US", "en-GB", "de-DE", "fr-FR"],
        ///     "Blacklist": ["zh-CN", "ko-KR"],
        ///     "DisallowedStatusCode": 403,
        ///     "DisallowedFailureRating": 5,
        ///     "ContinueOnDisallowed": false
        ///   }
        /// }
        /// </code>
        /// 
        /// Usage in Startup.cs or Program.cs:
        /// <code>
        /// builder.Services.AddAcceptLanguageFilteringMiddleware(
        ///     builder.Configuration,
        ///     options => 
        ///     {
        ///         // Override or add to configuration values
        ///         options.DisallowedStatusCode = 400;
        ///         options.Whitelist = options.Whitelist.Concat(new[] { "es-ES" }).ToArray();
        ///     });
        /// </code>
        /// </example>
        public static IServiceCollection AddAcceptLanguageFilteringMiddleware(this IServiceCollection services, IConfiguration configuration, Action<AcceptLanguageFilteringMiddlewareOptions>? manualConfigure = null)
        {
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Bind configuration from appsettings.json (reloadOnChange is enabled by default).
            services.Configure<AcceptLanguageFilteringMiddlewareOptions>(configuration.GetSection(nameof(AcceptLanguageFilteringMiddlewareOptions)));

            // Optionally apply additional code configuration.
            if (manualConfigure != null)
            {
                services.PostConfigure<AcceptLanguageFilteringMiddlewareOptions>(manualConfigure);
            }

            return services;
        }

        /// <summary>
        /// Registers <see cref="AcceptLanguageFilteringMiddlewareOptions"/> using a direct manual configuration delegate.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="manualConfigure">
        /// A delegate for configuring <see cref="AcceptLanguageFilteringMiddlewareOptions"/> directly.
        /// </param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload bypasses binding from the application configuration.
        /// It is useful in scenarios where configuration is provided entirely in code.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage in Startup.cs or Program.cs:
        /// builder.Services.AddAcceptLanguageFilteringMiddleware(options =>
        /// {
        ///     // Allow specific languages
        ///     options.Whitelist = new[] { "en-US", "en-GB", "de-DE", "fr-FR", "es-ES" };
        ///     
        ///     // Block specific languages
        ///     options.Blacklist = new[] { 
        ///         "*zh-CN*",    // Block Simplified Chinese
        ///         "*zh-*",      // Block all Chinese variants
        ///         "*-CN*",      // Block all Chinese regional variants
        ///         ""            // Block empty Accept-Language header
        ///     };
        ///     
        ///     // Return 403 Forbidden for disallowed languages
        ///     options.DisallowedStatusCode = 403;
        ///     
        ///     // Set failure rating for monitoring and analytics
        ///     options.DisallowedFailureRating = 5;
        ///     
        ///     // Stop processing pipeline on disallowed languages
        ///     options.ContinueOnDisallowed = false;
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddAcceptLanguageFilteringMiddleware(this IServiceCollection services, Action<AcceptLanguageFilteringMiddlewareOptions> manualConfigure)
        {
            // Ensure the MiddlewareFailurePointService is registered.
            services.TryAddSingleton<MiddlewareFailurePointService>();

            // Directly configure the options using the provided delegate.
            services.Configure(manualConfigure);

            return services;
        }

        /// <summary>
        /// Registers <see cref="AcceptLanguageFilteringMiddlewareOptions"/> with a default configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated service collection.</returns>
        /// <remarks>
        /// This overload applies a default configuration to <see cref="AcceptLanguageFilteringMiddlewareOptions"/>.
        /// The default settings include values for Whitelist, Blacklist, DisallowedStatusCode, DisallowedFailureRating,
        /// and ContinueOnDisallowed. Ensure these defaults match your application's requirements.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage in Startup.cs or Program.cs:
        /// var builder = WebApplication.CreateBuilder(args);
        /// 
        /// // Add services to the container
        /// builder.Services.AddAcceptLanguageFilteringMiddleware(); // Uses default configuration
        /// 
        /// var app = builder.Build();
        /// 
        /// // Configure the HTTP request pipeline
        /// app.UseAcceptLanguageFilteringMiddleware();
        /// </code>
        /// </example>
        public static IServiceCollection AddAcceptLanguageFilteringMiddleware(this IServiceCollection services)
        {
            return services.AddAcceptLanguageFilteringMiddleware(configuration =>
            {
                configuration.Whitelist = null;
                configuration.Blacklist = new[] { "*zh-CN*", "*zh-*", "*-CN*", "" };
                configuration.DisallowedStatusCode = 400;
                configuration.DisallowedFailureRating = 10;
                configuration.ContinueOnDisallowed = true;
            });
        }
    }
}
