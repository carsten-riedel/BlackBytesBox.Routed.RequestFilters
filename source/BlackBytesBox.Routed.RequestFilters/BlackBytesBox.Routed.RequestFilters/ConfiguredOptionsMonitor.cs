
using System;
using System.Reflection;

using Microsoft.Extensions.Options;


namespace BlackBytesBox.Routed.RequestFilters
{
    public class ConfiguredOptionsMonitor<TOptions> : IOptionsMonitor<TOptions> where TOptions : class, new()
    {
        private readonly IOptionsMonitor<TOptions> _inner;
        private readonly Action<TOptions> _configure;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfiguredOptionsMonitor{TOptions}"/> class.
        /// </summary>
        /// <param name="inner">The inner options monitor (from DI).</param>
        /// <param name="configure">An additional configuration delegate to apply.</param>
        public ConfiguredOptionsMonitor(IOptionsMonitor<TOptions> inner, Action<TOptions> configure)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _configure = configure ?? throw new ArgumentNullException(nameof(configure));
        }

        /// <summary>
        /// Gets the current options value, applying the additional configuration.
        /// </summary>
        public TOptions CurrentValue
        {
            get
            {
                var options = Clone(_inner.CurrentValue);
                _configure(options);
                return options;
            }
        }

        /// <summary>
        /// Gets the options for a specified named instance, applying the additional configuration.
        /// </summary>
        /// <param name="name">The name of the options instance.</param>
        /// <returns>The configured options.</returns>
        public TOptions Get(string name)
        {
            var options = Clone(_inner.Get(name));
            _configure(options);
            return options;
        }

        /// <summary>
        /// Registers a change listener.
        /// </summary>
        public IDisposable OnChange(Action<TOptions, string> listener) => _inner.OnChange(listener);

        /// <summary>
        /// Creates a shallow clone of the options instance.
        /// </summary>
        private TOptions Clone(TOptions options)
        {
            // Create a new instance of TOptions and copy writable properties.
            var clone = new TOptions();
            foreach (PropertyInfo prop in typeof(TOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.CanRead && prop.CanWrite)
                {
                    var value = prop.GetValue(options);
                    prop.SetValue(clone, value);
                }
            }
            return clone;
        }
    }

}
