namespace Conductor.Server.Tests.Controllers
{
    using System;
    using System.Threading.Tasks;
    using Conductor.Core.Models;
    using Conductor.Server.Controllers;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit tests for CredentialController.
    /// </summary>
    public class CredentialControllerTests : ControllerTestBase, IAsyncLifetime
    {
        private CredentialController _Controller;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _Controller = new CredentialController(Database, AuthService, Serializer, Logging);
        }

        /// <summary>
        /// Cleanup after test.
        /// </summary>
        /// <returns>Task.</returns>
        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        #region Create-Tests

        [Fact]
        public async Task Create_WithNullBody_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Create(TestTenantId, TestUserId, null);

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Create_WithValidInput_ReturnsCreatedCredential()
        {
            Credential credential = new Credential();

            Credential result = await _Controller.Create(TestTenantId, TestUserId, credential);

            result.Should().NotBeNull();
            result.Id.Should().StartWith("cred_");
            result.TenantId.Should().Be(TestTenantId);
            result.UserId.Should().Be(TestUserId);
        }

        [Fact]
        public async Task Create_GeneratesUniqueBearerToken()
        {
            Credential credential = new Credential();

            Credential result = await _Controller.Create(TestTenantId, TestUserId, credential);

            result.BearerToken.Should().NotBeNullOrEmpty();
            result.BearerToken.Should().HaveLength(64);
        }

        [Fact]
        public async Task Create_WithProvidedBearerToken_UsesProvidedValue()
        {
            string customToken = "custom-bearer-token-12345678901234567890123456789012345678901234";
            Credential credential = new Credential
            {
                BearerToken = customToken
            };

            Credential result = await _Controller.Create(TestTenantId, TestUserId, credential);

            result.BearerToken.Should().Be(customToken);
        }

        [Fact]
        public async Task Create_WithDuplicateBearerToken_ThrowsException()
        {
            string duplicateToken = "duplicate-token-12345678901234567890123456789012345678901234";

            // Create first credential
            await _Controller.Create(TestTenantId, TestUserId, new Credential { BearerToken = duplicateToken });

            // Try to create second with same token
            Func<Task> act = async () => await _Controller.Create(TestTenantId, TestUserId, new Credential { BearerToken = duplicateToken });

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Create_SetsDefaultValues()
        {
            Credential credential = new Credential();

            Credential result = await _Controller.Create(TestTenantId, TestUserId, credential);

            result.Active.Should().BeTrue();
        }

        [Fact]
        public async Task Create_OverridesTenantIdAndUserId()
        {
            Credential credential = new Credential
            {
                TenantId = "wrong_tenant",
                UserId = "wrong_user"
            };

            Credential result = await _Controller.Create(TestTenantId, TestUserId, credential);

            result.TenantId.Should().Be(TestTenantId);
            result.UserId.Should().Be(TestUserId);
        }

        [Fact]
        public async Task Create_GeneratesNewId()
        {
            Credential credential = new Credential
            {
                Id = "existing_id"
            };

            Credential result = await _Controller.Create(TestTenantId, TestUserId, credential);

            result.Id.Should().NotBe("existing_id");
            result.Id.Should().StartWith("cred_");
        }

        [Fact]
        public async Task Create_MultipleCredentials_HaveDifferentTokens()
        {
            Credential cred1 = await _Controller.Create(TestTenantId, TestUserId, new Credential());
            Credential cred2 = await _Controller.Create(TestTenantId, TestUserId, new Credential());
            Credential cred3 = await _Controller.Create(TestTenantId, TestUserId, new Credential());

            cred1.BearerToken.Should().NotBe(cred2.BearerToken);
            cred2.BearerToken.Should().NotBe(cred3.BearerToken);
            cred1.BearerToken.Should().NotBe(cred3.BearerToken);
        }

        #endregion

        #region Read-Tests

        [Fact]
        public async Task Read_WithNullId_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Read(TestTenantId, null);

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Read_WithEmptyId_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Read(TestTenantId, "");

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Read_WithNonExistentId_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Read(TestTenantId, "cred_nonexistent");

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Read_WithValidId_ReturnsCredential()
        {
            Credential created = await _Controller.Create(TestTenantId, TestUserId, new Credential { Name = "Test Credential" });

            Credential result = await _Controller.Read(TestTenantId, created.Id);

            result.Should().NotBeNull();
            result.Id.Should().Be(created.Id);
            result.Name.Should().Be("Test Credential");
        }

        [Fact]
        public async Task Read_WrongTenant_ThrowsException()
        {
            Credential created = await _Controller.Create(TestTenantId, TestUserId, new Credential());

            Func<Task> act = async () => await _Controller.Read("wrong_tenant", created.Id);

            await act.Should().ThrowAsync<Exception>();
        }

        #endregion

        #region Update-Tests

        [Fact]
        public async Task Update_WithNullId_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Update(TestTenantId, null, new Credential());

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Update_WithEmptyId_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Update(TestTenantId, "", new Credential());

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Update_WithNonExistentId_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Update(TestTenantId, "cred_nonexistent", new Credential());

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Update_WithNullBody_ThrowsException()
        {
            Credential created = await _Controller.Create(TestTenantId, TestUserId, new Credential());

            Func<Task> act = async () => await _Controller.Update(TestTenantId, created.Id, null);

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Update_PreservesCreatedUtc()
        {
            Credential created = await _Controller.Create(TestTenantId, TestUserId, new Credential());

            Credential updateRequest = new Credential
            {
                Name = "Updated",
                CreatedUtc = DateTime.UtcNow.AddDays(-10)
            };

            Credential result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.CreatedUtc.Should().BeCloseTo(created.CreatedUtc, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task Update_PreservesUserId()
        {
            Credential created = await _Controller.Create(TestTenantId, TestUserId, new Credential());

            Credential updateRequest = new Credential
            {
                UserId = "different_user"
            };

            Credential result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.UserId.Should().Be(TestUserId);
        }

        [Fact]
        public async Task Update_CanKeepSameBearerToken()
        {
            // Note: Credential model auto-generates BearerToken in constructor
            // The controller preserves token when explicitly set to same value or when empty/null is passed
            // Since Credential() generates a new token by default, we test by passing the existing token
            Credential created = await _Controller.Create(TestTenantId, TestUserId, new Credential());
            string originalToken = created.BearerToken;

            Credential updateRequest = new Credential
            {
                Name = "Updated Name",
                BearerToken = originalToken // Explicitly pass same token
            };

            Credential result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.BearerToken.Should().Be(originalToken);
        }

        [Fact]
        public async Task Update_CanChangeBearerToken()
        {
            Credential created = await _Controller.Create(TestTenantId, TestUserId, new Credential());
            string newToken = "new-unique-token-12345678901234567890123456789012345678901234";

            Credential updateRequest = new Credential
            {
                BearerToken = newToken
            };

            Credential result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.BearerToken.Should().Be(newToken);
        }

        [Fact]
        public async Task Update_WithDuplicateBearerToken_ThrowsException()
        {
            // Create two credentials
            Credential cred1 = await _Controller.Create(TestTenantId, TestUserId, new Credential { BearerToken = "token-one-12345678901234567890123456789012345678901234" });
            Credential cred2 = await _Controller.Create(TestTenantId, TestUserId, new Credential { BearerToken = "token-two-12345678901234567890123456789012345678901234" });

            // Try to update cred2 with cred1's token
            Func<Task> act = async () => await _Controller.Update(TestTenantId, cred2.Id, new Credential { BearerToken = "token-one-12345678901234567890123456789012345678901234" });

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Update_PreservesId()
        {
            Credential created = await _Controller.Create(TestTenantId, TestUserId, new Credential());

            Credential updateRequest = new Credential
            {
                Id = "different_id"
            };

            Credential result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.Id.Should().Be(created.Id);
        }

        [Fact]
        public async Task Update_UpdatesFields()
        {
            Credential created = await _Controller.Create(TestTenantId, TestUserId, new Credential
            {
                Name = "Original Name",
                Active = true
            });

            Credential updateRequest = new Credential
            {
                Name = "Updated Name",
                Active = false
            };

            Credential result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.Name.Should().Be("Updated Name");
            result.Active.Should().BeFalse();
        }

        #endregion

        #region Delete-Tests

        [Fact]
        public async Task Delete_WithNullId_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Delete(TestTenantId, null);

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Delete_WithEmptyId_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Delete(TestTenantId, "");

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Delete_WithNonExistentId_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Delete(TestTenantId, "cred_nonexistent");

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Delete_WithValidId_DeletesSuccessfully()
        {
            Credential created = await _Controller.Create(TestTenantId, TestUserId, new Credential());

            await _Controller.Delete(TestTenantId, created.Id);

            Func<Task> act = async () => await _Controller.Read(TestTenantId, created.Id);
            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Delete_WrongTenant_ThrowsException()
        {
            Credential created = await _Controller.Create(TestTenantId, TestUserId, new Credential());

            Func<Task> act = async () => await _Controller.Delete("wrong_tenant", created.Id);

            await act.Should().ThrowAsync<Exception>();
        }

        #endregion

        #region Enumerate-Tests

        [Fact]
        public async Task Enumerate_WithNoData_ReturnsEmptyResult()
        {
            EnumerationResult<Credential> result = await _Controller.Enumerate(TestTenantId);

            result.Should().NotBeNull();
            result.Data.Should().NotBeNull();
            result.Data.Should().BeEmpty();
        }

        [Fact]
        public async Task Enumerate_ReturnsAllCredentials()
        {
            await _Controller.Create(TestTenantId, TestUserId, new Credential { Name = "Cred 1" });
            await _Controller.Create(TestTenantId, TestUserId, new Credential { Name = "Cred 2" });
            await _Controller.Create(TestTenantId, TestUserId, new Credential { Name = "Cred 3" });

            EnumerationResult<Credential> result = await _Controller.Enumerate(TestTenantId);

            result.Data.Should().HaveCount(3);
        }

        [Fact]
        public async Task Enumerate_WithMaxResults_LimitsResults()
        {
            await _Controller.Create(TestTenantId, TestUserId, new Credential { Name = "Cred 1" });
            await _Controller.Create(TestTenantId, TestUserId, new Credential { Name = "Cred 2" });
            await _Controller.Create(TestTenantId, TestUserId, new Credential { Name = "Cred 3" });

            EnumerationResult<Credential> result = await _Controller.Enumerate(TestTenantId, maxResults: 2);

            result.Data.Should().HaveCount(2);
        }

        [Fact]
        public async Task Enumerate_WithActiveFilter_FiltersResults()
        {
            await _Controller.Create(TestTenantId, TestUserId, new Credential { Name = "Active", Active = true });

            Credential credToDeactivate = await _Controller.Create(TestTenantId, TestUserId, new Credential { Name = "Inactive" });
            await _Controller.Update(TestTenantId, credToDeactivate.Id, new Credential { Name = "Inactive", Active = false });

            EnumerationResult<Credential> result = await _Controller.Enumerate(TestTenantId, activeFilter: true);

            result.Data.Should().HaveCount(1);
            result.Data.Should().OnlyContain(c => c.Active);
        }

        [Fact]
        public async Task Enumerate_WrongTenant_ReturnsEmpty()
        {
            await _Controller.Create(TestTenantId, TestUserId, new Credential { Name = "Cred 1" });

            EnumerationResult<Credential> result = await _Controller.Enumerate("wrong_tenant");

            result.Data.Should().BeEmpty();
        }

        #endregion
    }
}
