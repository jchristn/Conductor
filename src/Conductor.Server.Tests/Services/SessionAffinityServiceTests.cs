namespace Conductor.Server.Tests.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Server.Services;
    using FluentAssertions;
    using SyslogLogging;
    using Xunit;

    /// <summary>
    /// Unit tests for SessionAffinityService.
    /// </summary>
    public class SessionAffinityServiceTests : IDisposable
    {
        private readonly SessionAffinityService _Service;
        private readonly LoggingModule _Logging;
        private bool _Disposed = false;

        /// <summary>
        /// Instantiate the test class.
        /// </summary>
        public SessionAffinityServiceTests()
        {
            _Logging = new LoggingModule();
            _Service = new SessionAffinityService(_Logging);
        }

        #region Constructor-Tests

        [Fact]
        public void Constructor_WithNullLogging_ThrowsArgumentNullException()
        {
            Action act = () => new SessionAffinityService(null);
            act.Should().Throw<ArgumentNullException>().WithParameterName("logging");
        }

        #endregion

        #region TryGetPinnedEndpoint-Tests

        [Fact]
        public void TryGetPinnedEndpoint_WhenNoPinExists_ReturnsFalse()
        {
            bool result = _Service.TryGetPinnedEndpoint("vmr_1", "client_1", out string endpointId);

            result.Should().BeFalse();
            endpointId.Should().BeNull();
        }

        [Fact]
        public void TryGetPinnedEndpoint_WhenPinExists_ReturnsTrueAndEndpointId()
        {
            _Service.SetPinnedEndpoint("vmr_1", "client_1", "mre_1", 600000, 10000);

            bool result = _Service.TryGetPinnedEndpoint("vmr_1", "client_1", out string endpointId);

            result.Should().BeTrue();
            endpointId.Should().Be("mre_1");
        }

        [Fact]
        public void TryGetPinnedEndpoint_WhenPinExpired_ReturnsFalse()
        {
            // Set a pin with a very short timeout (1ms)
            _Service.SetPinnedEndpoint("vmr_1", "client_1", "mre_1", 1, 10000);

            // Wait for expiration
            Thread.Sleep(50);

            bool result = _Service.TryGetPinnedEndpoint("vmr_1", "client_1", out string endpointId);

            result.Should().BeFalse();
            endpointId.Should().BeNull();
        }

        [Fact]
        public void TryGetPinnedEndpoint_RefreshesLastAccessUtc()
        {
            // Set a pin with a short timeout
            _Service.SetPinnedEndpoint("vmr_1", "client_1", "mre_1", 200, 10000);

            // Access multiple times within the timeout window
            for (int i = 0; i < 5; i++)
            {
                Thread.Sleep(50);
                bool result = _Service.TryGetPinnedEndpoint("vmr_1", "client_1", out string endpointId);
                result.Should().BeTrue();
                endpointId.Should().Be("mre_1");
            }

            // After 5 * 50ms = 250ms total, entry should still be alive because each access refreshed TTL
        }

        [Fact]
        public void TryGetPinnedEndpoint_NullVmrId_ReturnsFalse()
        {
            bool result = _Service.TryGetPinnedEndpoint(null, "client_1", out string endpointId);

            result.Should().BeFalse();
            endpointId.Should().BeNull();
        }

        [Fact]
        public void TryGetPinnedEndpoint_EmptyVmrId_ReturnsFalse()
        {
            bool result = _Service.TryGetPinnedEndpoint("", "client_1", out string endpointId);

            result.Should().BeFalse();
            endpointId.Should().BeNull();
        }

        [Fact]
        public void TryGetPinnedEndpoint_NullClientKey_ReturnsFalse()
        {
            bool result = _Service.TryGetPinnedEndpoint("vmr_1", null, out string endpointId);

            result.Should().BeFalse();
            endpointId.Should().BeNull();
        }

        [Fact]
        public void TryGetPinnedEndpoint_EmptyClientKey_ReturnsFalse()
        {
            bool result = _Service.TryGetPinnedEndpoint("vmr_1", "", out string endpointId);

            result.Should().BeFalse();
            endpointId.Should().BeNull();
        }

        #endregion

        #region SetPinnedEndpoint-Tests

        [Fact]
        public void SetPinnedEndpoint_CreatesNewEntry()
        {
            _Service.SetPinnedEndpoint("vmr_1", "client_1", "mre_1", 600000, 10000);

            bool result = _Service.TryGetPinnedEndpoint("vmr_1", "client_1", out string endpointId);

            result.Should().BeTrue();
            endpointId.Should().Be("mre_1");
        }

        [Fact]
        public void SetPinnedEndpoint_UpdatesExistingEntry()
        {
            _Service.SetPinnedEndpoint("vmr_1", "client_1", "mre_1", 600000, 10000);
            _Service.SetPinnedEndpoint("vmr_1", "client_1", "mre_2", 600000, 10000);

            bool result = _Service.TryGetPinnedEndpoint("vmr_1", "client_1", out string endpointId);

            result.Should().BeTrue();
            endpointId.Should().Be("mre_2");
        }

        [Fact]
        public void SetPinnedEndpoint_EvictsOldestWhenMaxEntriesExceeded()
        {
            // Add 5 entries
            for (int i = 0; i < 5; i++)
            {
                _Service.SetPinnedEndpoint("vmr_1", $"client_{i}", $"mre_{i}", 600000, 5);
                Thread.Sleep(10); // Ensure different LastAccessUtc
            }

            // Add a 6th entry which should trigger eviction
            _Service.SetPinnedEndpoint("vmr_1", "client_new", "mre_new", 600000, 5);

            // The newest entry should exist
            bool newResult = _Service.TryGetPinnedEndpoint("vmr_1", "client_new", out string newEndpoint);
            newResult.Should().BeTrue();
            newEndpoint.Should().Be("mre_new");

            // The oldest entry (client_0) should have been evicted
            bool oldResult = _Service.TryGetPinnedEndpoint("vmr_1", "client_0", out _);
            oldResult.Should().BeFalse();
        }

        [Fact]
        public void SetPinnedEndpoint_NullVmrId_DoesNotThrow()
        {
            Action act = () => _Service.SetPinnedEndpoint(null, "client_1", "mre_1", 600000, 10000);
            act.Should().NotThrow();
        }

        [Fact]
        public void SetPinnedEndpoint_NullClientKey_DoesNotThrow()
        {
            Action act = () => _Service.SetPinnedEndpoint("vmr_1", null, "mre_1", 600000, 10000);
            act.Should().NotThrow();
        }

        [Fact]
        public void SetPinnedEndpoint_NullEndpointId_DoesNotThrow()
        {
            Action act = () => _Service.SetPinnedEndpoint("vmr_1", "client_1", null, 600000, 10000);
            act.Should().NotThrow();
        }

        #endregion

        #region RemovePinnedEndpoint-Tests

        [Fact]
        public void RemovePinnedEndpoint_RemovesEntry()
        {
            _Service.SetPinnedEndpoint("vmr_1", "client_1", "mre_1", 600000, 10000);
            _Service.RemovePinnedEndpoint("vmr_1", "client_1");

            bool result = _Service.TryGetPinnedEndpoint("vmr_1", "client_1", out _);

            result.Should().BeFalse();
        }

        [Fact]
        public void RemovePinnedEndpoint_WhenNotExists_DoesNotThrow()
        {
            Action act = () => _Service.RemovePinnedEndpoint("vmr_1", "nonexistent");
            act.Should().NotThrow();
        }

        [Fact]
        public void RemovePinnedEndpoint_NullVmrId_DoesNotThrow()
        {
            Action act = () => _Service.RemovePinnedEndpoint(null, "client_1");
            act.Should().NotThrow();
        }

        [Fact]
        public void RemovePinnedEndpoint_NullClientKey_DoesNotThrow()
        {
            Action act = () => _Service.RemovePinnedEndpoint("vmr_1", null);
            act.Should().NotThrow();
        }

        #endregion

        #region RemoveAllForVmr-Tests

        [Fact]
        public void RemoveAllForVmr_RemovesAllEntriesForVmr()
        {
            _Service.SetPinnedEndpoint("vmr_1", "client_1", "mre_1", 600000, 10000);
            _Service.SetPinnedEndpoint("vmr_1", "client_2", "mre_2", 600000, 10000);
            _Service.SetPinnedEndpoint("vmr_1", "client_3", "mre_3", 600000, 10000);

            _Service.RemoveAllForVmr("vmr_1");

            _Service.TryGetPinnedEndpoint("vmr_1", "client_1", out _).Should().BeFalse();
            _Service.TryGetPinnedEndpoint("vmr_1", "client_2", out _).Should().BeFalse();
            _Service.TryGetPinnedEndpoint("vmr_1", "client_3", out _).Should().BeFalse();
        }

        [Fact]
        public void RemoveAllForVmr_DoesNotAffectOtherVmrs()
        {
            _Service.SetPinnedEndpoint("vmr_1", "client_1", "mre_1", 600000, 10000);
            _Service.SetPinnedEndpoint("vmr_2", "client_1", "mre_2", 600000, 10000);

            _Service.RemoveAllForVmr("vmr_1");

            _Service.TryGetPinnedEndpoint("vmr_1", "client_1", out _).Should().BeFalse();
            _Service.TryGetPinnedEndpoint("vmr_2", "client_1", out string endpointId).Should().BeTrue();
            endpointId.Should().Be("mre_2");
        }

        [Fact]
        public void RemoveAllForVmr_NullVmrId_DoesNotThrow()
        {
            Action act = () => _Service.RemoveAllForVmr(null);
            act.Should().NotThrow();
        }

        #endregion

        #region RemoveAllForEndpoint-Tests

        [Fact]
        public void RemoveAllForEndpoint_RemovesAcrossVmrs()
        {
            _Service.SetPinnedEndpoint("vmr_1", "client_1", "mre_shared", 600000, 10000);
            _Service.SetPinnedEndpoint("vmr_2", "client_2", "mre_shared", 600000, 10000);
            _Service.SetPinnedEndpoint("vmr_1", "client_3", "mre_other", 600000, 10000);

            _Service.RemoveAllForEndpoint("mre_shared");

            _Service.TryGetPinnedEndpoint("vmr_1", "client_1", out _).Should().BeFalse();
            _Service.TryGetPinnedEndpoint("vmr_2", "client_2", out _).Should().BeFalse();
            _Service.TryGetPinnedEndpoint("vmr_1", "client_3", out string remaining).Should().BeTrue();
            remaining.Should().Be("mre_other");
        }

        [Fact]
        public void RemoveAllForEndpoint_NullEndpointId_DoesNotThrow()
        {
            Action act = () => _Service.RemoveAllForEndpoint(null);
            act.Should().NotThrow();
        }

        #endregion

        #region GetSessionCount-Tests

        [Fact]
        public void GetSessionCount_ReturnsCorrectCount()
        {
            _Service.SetPinnedEndpoint("vmr_1", "client_1", "mre_1", 600000, 10000);
            _Service.SetPinnedEndpoint("vmr_1", "client_2", "mre_2", 600000, 10000);
            _Service.SetPinnedEndpoint("vmr_1", "client_3", "mre_3", 600000, 10000);

            int count = _Service.GetSessionCount("vmr_1");

            count.Should().Be(3);
        }

        [Fact]
        public void GetSessionCount_ExcludesExpiredEntries()
        {
            // Add entry with very short timeout
            _Service.SetPinnedEndpoint("vmr_1", "client_expired", "mre_1", 1, 10000);
            // Add entry with long timeout
            _Service.SetPinnedEndpoint("vmr_1", "client_active", "mre_2", 600000, 10000);

            // Wait for the short-timeout entry to expire
            Thread.Sleep(50);

            int count = _Service.GetSessionCount("vmr_1");

            count.Should().Be(1);
        }

        [Fact]
        public void GetSessionCount_ReturnsZeroForUnknownVmr()
        {
            int count = _Service.GetSessionCount("vmr_unknown");

            count.Should().Be(0);
        }

        [Fact]
        public void GetSessionCount_NullVmrId_ReturnsZero()
        {
            int count = _Service.GetSessionCount(null);

            count.Should().Be(0);
        }

        [Fact]
        public void GetSessionCount_EmptyVmrId_ReturnsZero()
        {
            int count = _Service.GetSessionCount("");

            count.Should().Be(0);
        }

        #endregion

        #region Cleanup-Tests

        [Fact]
        public void Cleanup_RemovesExpiredEntries()
        {
            _Service.SetPinnedEndpoint("vmr_1", "client_expired", "mre_1", 1, 10000);

            Thread.Sleep(50);

            _Service.Cleanup();

            bool result = _Service.TryGetPinnedEndpoint("vmr_1", "client_expired", out _);
            result.Should().BeFalse();
        }

        [Fact]
        public void Cleanup_PreservesActiveEntries()
        {
            _Service.SetPinnedEndpoint("vmr_1", "client_active", "mre_1", 600000, 10000);

            _Service.Cleanup();

            bool result = _Service.TryGetPinnedEndpoint("vmr_1", "client_active", out string endpointId);
            result.Should().BeTrue();
            endpointId.Should().Be("mre_1");
        }

        #endregion

        #region ConcurrentAccess-Tests

        [Fact]
        public void ConcurrentAccess_NoExceptionsThrown()
        {
            List<Task> tasks = new List<Task>();
            int taskCount = 20;

            for (int i = 0; i < taskCount; i++)
            {
                int index = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 100; j++)
                    {
                        string vmrId = $"vmr_{index % 3}";
                        string clientKey = $"client_{index}_{j}";
                        string endpointId = $"mre_{j % 5}";

                        _Service.SetPinnedEndpoint(vmrId, clientKey, endpointId, 600000, 10000);
                        _Service.TryGetPinnedEndpoint(vmrId, clientKey, out _);
                        _Service.GetSessionCount(vmrId);

                        if (j % 10 == 0)
                        {
                            _Service.RemovePinnedEndpoint(vmrId, clientKey);
                        }

                        if (j % 50 == 0)
                        {
                            _Service.Cleanup();
                        }
                    }
                }));
            }

            Action act = () => Task.WaitAll(tasks.ToArray());
            act.Should().NotThrow();
        }

        #endregion

        #region Dispose-Tests

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            SessionAffinityService service = new SessionAffinityService(new LoggingModule());

            Action act = () =>
            {
                service.Dispose();
                service.Dispose();
            };

            act.Should().NotThrow();
        }

        #endregion

        /// <summary>
        /// Dispose of test resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of test resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing)
            {
                _Service?.Dispose();
                _Logging?.Dispose();
            }

            _Disposed = true;
        }
    }
}
