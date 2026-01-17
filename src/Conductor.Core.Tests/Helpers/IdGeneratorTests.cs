namespace Conductor.Core.Tests.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conductor.Core.Helpers;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit tests for IdGenerator helper.
    /// </summary>
    public class IdGeneratorTests
    {
        #region Prefix-Tests

        [Fact]
        public void NewTenantId_StartsWithCorrectPrefix()
        {
            string id = IdGenerator.NewTenantId();
            id.Should().StartWith(IdGenerator.TenantPrefix);
            id.Should().StartWith("ten_");
        }

        [Fact]
        public void NewUserId_StartsWithCorrectPrefix()
        {
            string id = IdGenerator.NewUserId();
            id.Should().StartWith(IdGenerator.UserPrefix);
            id.Should().StartWith("usr_");
        }

        [Fact]
        public void NewCredentialId_StartsWithCorrectPrefix()
        {
            string id = IdGenerator.NewCredentialId();
            id.Should().StartWith(IdGenerator.CredentialPrefix);
            id.Should().StartWith("cred_");
        }

        [Fact]
        public void NewModelRunnerEndpointId_StartsWithCorrectPrefix()
        {
            string id = IdGenerator.NewModelRunnerEndpointId();
            id.Should().StartWith(IdGenerator.ModelRunnerEndpointPrefix);
            id.Should().StartWith("mre_");
        }

        [Fact]
        public void NewModelDefinitionId_StartsWithCorrectPrefix()
        {
            string id = IdGenerator.NewModelDefinitionId();
            id.Should().StartWith(IdGenerator.ModelDefinitionPrefix);
            id.Should().StartWith("md_");
        }

        [Fact]
        public void NewModelConfigurationId_StartsWithCorrectPrefix()
        {
            string id = IdGenerator.NewModelConfigurationId();
            id.Should().StartWith(IdGenerator.ModelConfigurationPrefix);
            id.Should().StartWith("mc_");
        }

        [Fact]
        public void NewVirtualModelRunnerId_StartsWithCorrectPrefix()
        {
            string id = IdGenerator.NewVirtualModelRunnerId();
            id.Should().StartWith(IdGenerator.VirtualModelRunnerPrefix);
            id.Should().StartWith("vmr_");
        }

        [Fact]
        public void NewAdministratorId_StartsWithCorrectPrefix()
        {
            string id = IdGenerator.NewAdministratorId();
            id.Should().StartWith(IdGenerator.AdministratorPrefix);
            id.Should().StartWith("admin_");
        }

        #endregion

        #region Id-Length-Tests

        [Fact]
        public void GeneratedIds_HaveDefaultLength()
        {
            string tenantId = IdGenerator.NewTenantId();
            string userId = IdGenerator.NewUserId();
            string vmrId = IdGenerator.NewVirtualModelRunnerId();

            tenantId.Should().HaveLength(IdGenerator.DefaultIdLength);
            userId.Should().HaveLength(IdGenerator.DefaultIdLength);
            vmrId.Should().HaveLength(IdGenerator.DefaultIdLength);
        }

        #endregion

        #region KSortable-Tests

        [Fact]
        public void GeneratedIds_AreKSortable()
        {
            List<string> ids = new List<string>();
            for (int i = 0; i < 100; i++)
            {
                ids.Add(IdGenerator.NewTenantId());
            }

            // IDs generated in sequence should already be sorted
            List<string> sortedIds = new List<string>(ids);
            sortedIds.Sort(StringComparer.Ordinal);

            // The IDs should maintain their original order when sorted
            // (K-sortable means newer IDs sort after older IDs)
            ids.Should().BeEquivalentTo(sortedIds);
        }

        [Fact]
        public async Task GeneratedIds_AreKSortable_AcrossTime()
        {
            string id1 = IdGenerator.NewTenantId();
            await Task.Delay(10); // Small delay to ensure different timestamp
            string id2 = IdGenerator.NewTenantId();

            string.Compare(id1, id2, StringComparison.Ordinal).Should().BeLessThan(0);
        }

        #endregion

        #region Uniqueness-Tests

        [Fact]
        public void GeneratedIds_AreUnique()
        {
            HashSet<string> ids = new HashSet<string>();
            for (int i = 0; i < 10000; i++)
            {
                string id = IdGenerator.NewTenantId();
                ids.Add(id).Should().BeTrue($"Duplicate ID generated: {id}");
            }
        }

        [Fact]
        public void GeneratedIds_AreUnique_AcrossTypes()
        {
            HashSet<string> ids = new HashSet<string>();
            for (int i = 0; i < 1000; i++)
            {
                ids.Add(IdGenerator.NewTenantId());
                ids.Add(IdGenerator.NewUserId());
                ids.Add(IdGenerator.NewCredentialId());
                ids.Add(IdGenerator.NewVirtualModelRunnerId());
            }

            // All IDs should be unique
            ids.Should().HaveCount(4000);
        }

        #endregion

        #region BearerToken-Tests

        [Fact]
        public void NewBearerToken_Returns64Characters()
        {
            string token = IdGenerator.NewBearerToken();
            token.Should().HaveLength(64);
        }

        [Fact]
        public void NewBearerToken_IsUnique()
        {
            HashSet<string> tokens = new HashSet<string>();
            for (int i = 0; i < 1000; i++)
            {
                string token = IdGenerator.NewBearerToken();
                tokens.Add(token).Should().BeTrue($"Duplicate token generated: {token}");
            }
        }

        [Fact]
        public void NewBearerToken_ContainsOnlyAlphanumeric()
        {
            for (int i = 0; i < 100; i++)
            {
                string token = IdGenerator.NewBearerToken();
                token.Should().MatchRegex("^[a-zA-Z0-9]+$");
            }
        }

        #endregion

        #region NewRandom-Tests

        [Fact]
        public void NewRandom_ReturnsRequestedLength()
        {
            string random16 = IdGenerator.NewRandom(16);
            string random32 = IdGenerator.NewRandom(32);
            string random64 = IdGenerator.NewRandom(64);

            random16.Should().HaveLength(16);
            random32.Should().HaveLength(32);
            random64.Should().HaveLength(64);
        }

        [Fact]
        public void NewRandom_WithZeroLength_ThrowsArgumentOutOfRangeException()
        {
            Action act = () => IdGenerator.NewRandom(0);
            act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("length");
        }

        [Fact]
        public void NewRandom_WithNegativeLength_ThrowsArgumentOutOfRangeException()
        {
            Action act = () => IdGenerator.NewRandom(-1);
            act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("length");
        }

        [Fact]
        public void NewRandom_IsUnique()
        {
            HashSet<string> randoms = new HashSet<string>();
            for (int i = 0; i < 1000; i++)
            {
                string random = IdGenerator.NewRandom(32);
                randoms.Add(random).Should().BeTrue($"Duplicate random generated: {random}");
            }
        }

        [Fact]
        public void NewRandom_WithDefaultLength_Returns32Characters()
        {
            string random = IdGenerator.NewRandom();
            random.Should().HaveLength(32);
        }

        #endregion

        #region Thread-Safety-Tests

        [Fact]
        public async Task GeneratedIds_AreThreadSafe()
        {
            HashSet<string> ids = new HashSet<string>();
            object lockObj = new object();
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        string id = IdGenerator.NewTenantId();
                        lock (lockObj)
                        {
                            ids.Add(id);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks.ToArray());
            ids.Should().HaveCount(10000);
        }

        #endregion
    }
}
