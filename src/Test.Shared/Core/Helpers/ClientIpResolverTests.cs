namespace Test.Shared.Core.Helpers
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Helpers;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for ClientIpResolver helper.
    /// </summary>
    public class ClientIpResolverTests
    {
        #region No-Trusted-Proxies-Tests
        public void Resolve_NoTrustedProxies_ReturnsPeer()
        {
            ClientIpResolver resolver = new ClientIpResolver();
            resolver.Resolve("172.17.0.1", "203.0.113.7").Should().Be("172.17.0.1");
            resolver.HasTrustedProxies.Should().BeFalse();
        }
        public void Resolve_UntrustedPeer_IgnoresForwardedFor()
        {
            ClientIpResolver resolver = new ClientIpResolver(new List<string> { "10.0.0.5" });
            resolver.Resolve("198.51.100.9", "203.0.113.7").Should().Be("198.51.100.9");
        }
        public void Resolve_TrustedPeerWithoutHeader_ReturnsPeer()
        {
            ClientIpResolver resolver = new ClientIpResolver(new List<string> { "10.0.0.5" });
            resolver.Resolve("10.0.0.5", null).Should().Be("10.0.0.5");
            resolver.Resolve("10.0.0.5", "  ").Should().Be("10.0.0.5");
        }
        #endregion

        #region Trusted-Proxy-Tests
        public void Resolve_TrustedPeer_ReturnsForwardedClient()
        {
            ClientIpResolver resolver = new ClientIpResolver(new List<string> { "172.16.0.0/12" });
            resolver.Resolve("172.17.0.1", "203.0.113.7").Should().Be("203.0.113.7");
        }
        public void Resolve_SpoofedLeftmostEntry_ReturnsRightmostUntrusted()
        {
            ClientIpResolver resolver = new ClientIpResolver(new List<string> { "172.16.0.0/12" });
            resolver.Resolve("172.17.0.1", "1.1.1.1, 203.0.113.7").Should().Be("203.0.113.7");
        }
        public void Resolve_ChainedTrustedProxies_SkipsAllTrustedHops()
        {
            ClientIpResolver resolver = new ClientIpResolver(new List<string> { "172.17.0.1", "10.0.0.0/8" });
            resolver.Resolve("172.17.0.1", "203.0.113.7, 10.1.2.3").Should().Be("203.0.113.7");
        }
        public void Resolve_AllHopsTrusted_ReturnsLeftmost()
        {
            ClientIpResolver resolver = new ClientIpResolver(new List<string> { "10.0.0.0/8" });
            resolver.Resolve("10.0.0.1", "10.0.0.3, 10.0.0.2").Should().Be("10.0.0.3");
        }
        public void Resolve_MalformedEntry_StopsAtLastValidHop()
        {
            ClientIpResolver resolver = new ClientIpResolver(new List<string> { "10.0.0.0/8" });
            resolver.Resolve("10.0.0.1", "203.0.113.7, unknown, 10.0.0.2").Should().Be("10.0.0.2");
            resolver.Resolve("10.0.0.1", "garbage").Should().Be("10.0.0.1");
        }
        public void Resolve_EntriesWithPorts_StripsPorts()
        {
            ClientIpResolver resolver = new ClientIpResolver(new List<string> { "10.0.0.0/8" });
            resolver.Resolve("10.0.0.1", "203.0.113.7:51234").Should().Be("203.0.113.7");
            resolver.Resolve("10.0.0.1", "[2001:db8::7]:443").Should().Be("2001:db8::7");
        }
        public void Resolve_Ipv6Entries_Supported()
        {
            ClientIpResolver resolver = new ClientIpResolver(new List<string> { "fd00::/8" });
            resolver.Resolve("fd00::1", "2001:db8::7").Should().Be("2001:db8::7");
        }
        public void Resolve_Ipv4MappedPeer_MatchesIpv4Range()
        {
            ClientIpResolver resolver = new ClientIpResolver(new List<string> { "172.17.0.0/16" });
            resolver.Resolve("::ffff:172.17.0.1", "203.0.113.7").Should().Be("203.0.113.7");
        }
        public void Resolve_InvalidPeer_ReturnsPeer()
        {
            ClientIpResolver resolver = new ClientIpResolver(new List<string> { "10.0.0.0/8" });
            resolver.Resolve(null, "203.0.113.7").Should().BeNull();
            resolver.Resolve("not-an-ip", "203.0.113.7").Should().Be("not-an-ip");
        }
        #endregion

        #region Configuration-Tests
        public void Constructor_CidrWithHostBits_IsAccepted()
        {
            ClientIpResolver resolver = new ClientIpResolver(new List<string> { "172.17.0.1/16" });
            resolver.Resolve("172.17.5.5", "203.0.113.7").Should().Be("203.0.113.7");
        }
        public void Constructor_InvalidEntry_Throws()
        {
            Action act = () => new ClientIpResolver(new List<string> { "not-an-ip" });
            act.Should().Throw<ArgumentException>();

            Action badPrefix = () => new ClientIpResolver(new List<string> { "10.0.0.0/33" });
            badPrefix.Should().Throw<ArgumentException>();
        }
        public void Constructor_NullAndBlankEntries_Ignored()
        {
            new ClientIpResolver(null).HasTrustedProxies.Should().BeFalse();
            new ClientIpResolver(new List<string> { null, " " }).HasTrustedProxies.Should().BeFalse();
        }
        public void Constructor_ForwardedForHeader_DefaultsWhenEmpty()
        {
            new ClientIpResolver(null, null).ForwardedForHeader.Should().Be("X-Forwarded-For");
            new ClientIpResolver(null, "X-Real-IP").ForwardedForHeader.Should().Be("X-Real-IP");
        }
        #endregion
    }
}
