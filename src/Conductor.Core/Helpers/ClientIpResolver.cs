namespace Conductor.Core.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;

    /// <summary>
    /// Resolves the originating client IP address of a request, honoring a forwarded-for header
    /// only when the connecting peer is a trusted reverse proxy.
    /// Instances are immutable after construction and are safe for concurrent use.
    /// </summary>
    public class ClientIpResolver
    {
        /// <summary>
        /// Name of the forwarded-for header consulted when the connecting peer is trusted.
        /// Never null or empty.
        /// </summary>
        public string ForwardedForHeader
        {
            get => _ForwardedForHeader;
        }

        /// <summary>
        /// True when at least one trusted proxy is configured.
        /// When false, Resolve always returns the connecting peer address.
        /// </summary>
        public bool HasTrustedProxies
        {
            get => _TrustedNetworks.Count > 0;
        }

        private readonly List<IPNetwork> _TrustedNetworks = new List<IPNetwork>();
        private readonly string _ForwardedForHeader = "X-Forwarded-For";

        /// <summary>
        /// Instantiate a resolver that trusts no proxies and always returns the connecting peer address.
        /// </summary>
        public ClientIpResolver()
        {
        }

        /// <summary>
        /// Instantiate a resolver.
        /// </summary>
        /// <param name="trustedProxies">IP addresses or CIDR ranges of trusted reverse proxies. Null is treated as empty; null or whitespace entries are ignored.</param>
        /// <param name="forwardedForHeader">Forwarded-for header name. Null or empty defaults to "X-Forwarded-For".</param>
        /// <exception cref="ArgumentException">Thrown when a trusted proxy entry is not a valid IP address or CIDR range.</exception>
        public ClientIpResolver(IEnumerable<string> trustedProxies, string forwardedForHeader = null)
        {
            if (!String.IsNullOrWhiteSpace(forwardedForHeader)) _ForwardedForHeader = forwardedForHeader;
            if (trustedProxies == null) return;

            foreach (string entry in trustedProxies)
            {
                if (String.IsNullOrWhiteSpace(entry)) continue;
                _TrustedNetworks.Add(ParseNetwork(entry.Trim()));
            }
        }

        /// <summary>
        /// Resolve the originating client IP address.
        /// If the peer is not trusted, or the forwarded-for header is absent, the peer address is returned.
        /// Otherwise the forwarded-for chain is walked from right to left while each hop is trusted,
        /// and the first untrusted (or leftmost) address is returned. Walking stops at a malformed entry,
        /// returning the last valid address reached.
        /// </summary>
        /// <param name="peerIpAddress">IP address of the connecting peer. May be null.</param>
        /// <param name="forwardedForValue">Value of the forwarded-for header, possibly comma-separated. May be null.</param>
        /// <returns>The resolved client IP address, or peerIpAddress unchanged when no forwarded address applies.</returns>
        public string Resolve(string peerIpAddress, string forwardedForValue)
        {
            if (_TrustedNetworks.Count < 1 || String.IsNullOrWhiteSpace(forwardedForValue)) return peerIpAddress;
            if (!TryParseAddress(peerIpAddress, out IPAddress current)) return peerIpAddress;
            if (!IsTrusted(current)) return peerIpAddress;

            string resolved = peerIpAddress;
            string[] hops = forwardedForValue.Split(',');

            for (int i = hops.Length - 1; i >= 0; i--)
            {
                if (!IsTrusted(current)) break;
                if (!TryParseAddress(hops[i], out IPAddress hop)) break;

                current = hop;
                resolved = hop.ToString();
            }

            return resolved;
        }

        private bool IsTrusted(IPAddress address)
        {
            foreach (IPNetwork network in _TrustedNetworks)
            {
                if (network.Contains(address)) return true;
            }

            return false;
        }

        private static IPNetwork ParseNetwork(string entry)
        {
            string addressPart = entry;
            int prefixLength = -1;

            int slash = entry.IndexOf('/');
            if (slash >= 0)
            {
                addressPart = entry.Substring(0, slash);
                if (!Int32.TryParse(entry.Substring(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out prefixLength)) prefixLength = Int32.MaxValue;
            }

            if (IPAddress.TryParse(addressPart, out IPAddress address))
            {
                address = Normalize(address);
                int maxPrefixLength = address.AddressFamily == AddressFamily.InterNetworkV6 ? 128 : 32;
                if (prefixLength < 0) prefixLength = maxPrefixLength;

                if (prefixLength <= maxPrefixLength)
                {
                    // Clear host bits so entries such as 172.17.0.1/16 are accepted as 172.17.0.0/16.
                    byte[] bytes = address.GetAddressBytes();
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        int bitsInByte = Math.Clamp(prefixLength - (i * 8), 0, 8);
                        bytes[i] &= (byte)(0xFF << (8 - bitsInByte));
                    }

                    return new IPNetwork(new IPAddress(bytes), prefixLength);
                }
            }

            throw new ArgumentException("Trusted proxy entry '" + entry + "' is not a valid IP address or CIDR range.", "trustedProxies");
        }

        private static bool TryParseAddress(string value, out IPAddress address)
        {
            address = null;
            if (String.IsNullOrWhiteSpace(value)) return false;

            string candidate = value.Trim();

            // Bracketed IPv6, optionally with a port: [2001:db8::1]:8080
            if (candidate.StartsWith("[", StringComparison.Ordinal))
            {
                int close = candidate.IndexOf(']');
                if (close < 0) return false;
                candidate = candidate.Substring(1, close - 1);
            }
            else if (candidate.Count(c => c == ':') == 1)
            {
                // IPv4 with a port: 203.0.113.7:51234
                string port = candidate.Substring(candidate.IndexOf(':') + 1);
                if (!Int32.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out _)) return false;
                candidate = candidate.Substring(0, candidate.IndexOf(':'));
            }

            if (!IPAddress.TryParse(candidate, out IPAddress parsed)) return false;

            address = Normalize(parsed);
            return true;
        }

        private static IPAddress Normalize(IPAddress address)
        {
            return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        }
    }
}
