namespace Test.Shared.Server.Services
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Server.Services;
    using FluentAssertions;
    using Test.Shared.Server.Fakes;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// UT-016, UT-023 and UT-030 regression tests for webserver startup: a port that is already in use is detected before
    /// startup and makes the start fail fast, request-only headers are removed from Watson's default response headers, and
    /// background services start only after the listener is verified.
    /// </summary>
    public class WebserverStartupTests
    {
        /// <summary>
        /// IsPortAvailable reports an occupied loopback port as unavailable and a free one as available.
        /// </summary>
        public void IsPortAvailable_DetectsPortInUse()
        {
            TcpListener occupier = new TcpListener(IPAddress.Loopback, 0);
            occupier.Start();
            try
            {
                int port = ((IPEndPoint)occupier.LocalEndpoint).Port;
                WebserverStartup.IsPortAvailable("127.0.0.1", port).Should().BeFalse();
            }
            finally
            {
                occupier.Stop();
            }

            WebserverStartup.IsPortAvailable("127.0.0.1", FakeUpstreamServer.GetFreePort()).Should().BeTrue();
        }

        /// <summary>
        /// Starting a second webserver on a port another listener already holds throws instead of running silently.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task StartAndVerifyAsync_FailsFastWhenPortIsInUse()
        {
            int port = FakeUpstreamServer.GetFreePort();
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            using (Webserver first = new Webserver(new WebserverSettings("127.0.0.1", port, false), ctx => ctx.Response.Send("first")))
            using (Webserver second = new Webserver(new WebserverSettings("127.0.0.1", port, false), ctx => ctx.Response.Send("second")))
            {
                await WebserverStartup.StartAndVerifyAsync(first, TimeSpan.FromSeconds(5), tokenSource.Token).ConfigureAwait(false);
                first.IsListening.Should().BeTrue();

                Func<Task> startSecond = async () => await WebserverStartup.StartAndVerifyAsync(second, TimeSpan.FromSeconds(3), tokenSource.Token).ConfigureAwait(false);
                await startSecond.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(false);

                tokenSource.Cancel();
                try { first.Stop(); } catch (Exception) { }
            }
        }

        /// <summary>
        /// UT-030: the background-start callback (health checks, keep-warm, cleanup) runs only after the listener is verified.
        /// When the listener cannot start, the callback never runs; when it starts, the callback sees a listening server.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task StartAndVerifyAsync_StartsBackgroundServicesOnlyAfterListenerIsVerified()
        {
            int port = FakeUpstreamServer.GetFreePort();
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            using (Webserver first = new Webserver(new WebserverSettings("127.0.0.1", port, false), ctx => ctx.Response.Send("first")))
            using (Webserver second = new Webserver(new WebserverSettings("127.0.0.1", port, false), ctx => ctx.Response.Send("second")))
            {
                bool listeningWhenStarted = false;
                int firstCallbackCount = 0;
                await WebserverStartup.StartAndVerifyAsync(first, TimeSpan.FromSeconds(5), token =>
                {
                    Interlocked.Increment(ref firstCallbackCount);
                    listeningWhenStarted = first.IsListening;
                    return Task.CompletedTask;
                }, tokenSource.Token).ConfigureAwait(false);
                firstCallbackCount.Should().Be(1);
                listeningWhenStarted.Should().BeTrue();

                int secondCallbackCount = 0;
                Func<Task> startSecond = async () => await WebserverStartup.StartAndVerifyAsync(second, TimeSpan.FromSeconds(3), token =>
                {
                    Interlocked.Increment(ref secondCallbackCount);
                    return Task.CompletedTask;
                }, tokenSource.Token).ConfigureAwait(false);
                await startSecond.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(false);
                secondCallbackCount.Should().Be(0, "background services must not start when the listener fails");

                tokenSource.Cancel();
                try { first.Stop(); } catch (Exception) { }
            }
        }

        /// <summary>
        /// Request-only headers are removed from the default response headers; CORS defaults remain.
        /// </summary>
        public void RemoveRequestOnlyDefaultHeaders_RemovesHostAndAcceptHeaders()
        {
            WebserverSettings settings = new WebserverSettings("127.0.0.1", 9, false);
            settings.Headers.DefaultHeaders["Host"] = "127.0.0.1:9";
            settings.Headers.DefaultHeaders["Accept"] = "*/*";

            WebserverStartup.RemoveRequestOnlyDefaultHeaders(settings);

            foreach (string name in WebserverStartup.RequestOnlyHeaderNames)
            {
                settings.Headers.DefaultHeaders.Keys.Should().NotContain(key => String.Equals(key, name, StringComparison.OrdinalIgnoreCase), name);
            }
        }
    }
}
