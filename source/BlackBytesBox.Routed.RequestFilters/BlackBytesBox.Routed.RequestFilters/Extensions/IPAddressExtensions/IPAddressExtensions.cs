using System.Net;
using System.Net.Sockets;

namespace BlackBytesBox.Routed.RequestFilters.Extensions.IPAddressExtensions
{

    public static class IPAddressExtensions
    {
        /// <summary>
        /// Represents the parsed information of an IP address, including its version and plain string form.
        /// </summary>
        /// <remarks>
        /// Populated by <see cref="IPAddressExtensions.ToIpInfo(IPAddress)"/> to unify IPv4, IPv6,
        /// and IPv4‐mapped IPv6 addresses into a simple model.
        /// </remarks>
        public class IpInfo
        {
            /// <summary>
            /// Gets or sets the IP protocol version determined for the address.
            /// </summary>
            /// <remarks>
            /// Will be <see cref="IpVersion.IPv4"/> for IPv4 (or IPv4‐mapped IPv6),
            /// <see cref="IpVersion.IPv6"/> for native IPv6, or <see cref="IpVersion.Unknown"/> if undetermined.
            /// </remarks>
            public IpVersion Version { get; set; }

            /// <summary>
            /// Gets or sets the normalized IP string without any scope identifier.
            /// </summary>
            /// <remarks>
            /// For IPv6, any “%scope” suffix (e.g. “%eth0”) is removed. For null or unsupported families, this may be “Unknown”.
            /// </remarks>
            public string? RemoteIp { get; set; }
        }

        /// <summary>
        /// Enumerates the possible IP protocol versions that <see cref="IpInfo"/> can represent.
        /// </summary>
        public enum IpVersion
        {
            /// <summary>
            /// The IP version is not known or unsupported.
            /// </summary>
            Unknown,

            /// <summary>
            /// The address is IPv4 (including IPv4‐mapped IPv6).
            /// </summary>
            IPv4,

            /// <summary>
            /// The address is native IPv6.
            /// </summary>
            IPv6
        }


        /// <summary>
        /// Determines the IP version and extracts the remote IP string for the specified <paramref name="address"/>.
        /// </summary>
        /// <remarks>
        /// Handles IPv4, IPv6, and IPv4‐mapped IPv6 addresses, and strips any IPv6 scope identifier (e.g. “%eth0”).
        /// </remarks>
        /// <param name="address">The <see cref="IPAddress"/> to convert into an <see cref="IpInfo"/>.</param>
        /// <returns>
        /// An <see cref="IpInfo"/> instance whose <see cref="IpInfo.Version"/> indicates IPv4, IPv6, or Unknown,
        /// and whose <see cref="IpInfo.RemoteIp"/> is the plain IP string.
        /// </returns>
        /// <example>
        /// <code>
        /// IPAddress ip1 = IPAddress.Parse("::ffff:203.0.113.5");
        /// IpInfo info1 = ip1.ToIpInfo();
        /// // info1.Version == IpVersion.IPv4
        /// // info1.RemoteIp == "203.0.113.5"
        ///
        /// IPAddress ip2 = IPAddress.Parse("203.0.113.5");
        /// IpInfo info2 = ip2.ToIpInfo();
        /// // info2.Version == IpVersion.IPv4
        /// // info2.RemoteIp == "203.0.113.5"
        ///
        /// IPAddress ip3 = IPAddress.Parse("2001:db8::1%eth0");
        /// IpInfo info3 = ip3.ToIpInfo();
        /// // info3.Version == IpVersion.IPv6
        /// // info3.RemoteIp == "2001:db8::1"
        ///
        /// IPAddress ip4 = IPAddress.Parse("2a02:8108:9304:f300:e5ad:a411:36b4:298f");
        /// IpInfo info4 = ip4.ToIpInfo();
        /// // info4.Version == IpVersion.IPv6
        /// // info4.RemoteIp == "2a02:8108:9304:f300:e5ad:a411:36b4:298f"
        /// </code>
        /// </example>
        public static IpInfo ToIpInfo(this IPAddress? address)
        {
            // Initialize output
            var info = new IpInfo();

            // 1) Null check
            if (address is null)
            {
                info.Version = IpVersion.Unknown;
                return info;
            }

            // 2) Determine version and map if needed
            if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
            {
                // IPv4-mapped IPv6 -> treat as IPv4
                address = address.MapToIPv4();
                info.Version = IpVersion.IPv4;
            }
            else if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                info.Version = IpVersion.IPv4;
            }
            else if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                info.Version = IpVersion.IPv6;
            }
            else
            {
                info.Version = IpVersion.Unknown;
            }

            // 3) Convert to string
            string raw = address.ToString();
            if (info.Version == IpVersion.IPv6 && raw.Contains('%'))
            {
                // Strip scope identifier (e.g. %eth0)
                raw = raw.Split('%')[0];
            }
            info.RemoteIp = raw;

            return info;
        }

    }
}
