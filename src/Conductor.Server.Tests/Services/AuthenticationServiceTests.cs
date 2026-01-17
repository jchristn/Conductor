namespace Conductor.Server.Tests.Services
{
    using System;
    using Conductor.Core.Models;
    using Conductor.Server.Services;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit tests for AuthenticationService result classes.
    /// Note: Full authentication flow tests require integration tests due to non-mockable DatabaseDriverBase.
    /// </summary>
    public class AuthenticationServiceTests
    {
        #region AuthenticationResult-Default-Values-Tests

        [Fact]
        public void AuthenticationResult_IsAuthenticated_DefaultsToFalse()
        {
            AuthenticationResult result = new AuthenticationResult();
            result.IsAuthenticated.Should().BeFalse();
        }

        [Fact]
        public void AuthenticationResult_Tenant_DefaultsToNull()
        {
            AuthenticationResult result = new AuthenticationResult();
            result.Tenant.Should().BeNull();
        }

        [Fact]
        public void AuthenticationResult_User_DefaultsToNull()
        {
            AuthenticationResult result = new AuthenticationResult();
            result.User.Should().BeNull();
        }

        [Fact]
        public void AuthenticationResult_Credential_DefaultsToNull()
        {
            AuthenticationResult result = new AuthenticationResult();
            result.Credential.Should().BeNull();
        }

        [Fact]
        public void AuthenticationResult_AuthMethod_DefaultsToNull()
        {
            AuthenticationResult result = new AuthenticationResult();
            result.AuthMethod.Should().BeNull();
        }

        [Fact]
        public void AuthenticationResult_ErrorMessage_DefaultsToNull()
        {
            AuthenticationResult result = new AuthenticationResult();
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public void AuthenticationResult_RequestType_DefaultsToUnknown()
        {
            AuthenticationResult result = new AuthenticationResult();
            result.RequestType.Should().Be(Conductor.Core.Enums.RequestTypeEnum.Unknown);
        }

        #endregion

        #region AuthenticationResult-IsAdmin-Tests

        [Fact]
        public void AuthenticationResult_IsAdmin_ReturnsFalseWhenUserIsNull()
        {
            AuthenticationResult result = new AuthenticationResult();
            result.IsAdmin.Should().BeFalse();
        }

        [Fact]
        public void AuthenticationResult_IsAdmin_ReturnsFalseWhenUserIsNotAdmin()
        {
            AuthenticationResult result = new AuthenticationResult
            {
                User = new UserMaster { IsAdmin = false }
            };
            result.IsAdmin.Should().BeFalse();
        }

        [Fact]
        public void AuthenticationResult_IsAdmin_ReturnsTrueWhenUserIsAdmin()
        {
            AuthenticationResult result = new AuthenticationResult
            {
                User = new UserMaster { IsAdmin = true }
            };
            result.IsAdmin.Should().BeTrue();
        }

        #endregion

        #region AuthenticationResult-IsTenantAdmin-Tests

        [Fact]
        public void AuthenticationResult_IsTenantAdmin_ReturnsFalseWhenUserIsNull()
        {
            AuthenticationResult result = new AuthenticationResult();
            result.IsTenantAdmin.Should().BeFalse();
        }

        [Fact]
        public void AuthenticationResult_IsTenantAdmin_ReturnsFalseWhenUserIsNotTenantAdmin()
        {
            AuthenticationResult result = new AuthenticationResult
            {
                User = new UserMaster { IsTenantAdmin = false }
            };
            result.IsTenantAdmin.Should().BeFalse();
        }

        [Fact]
        public void AuthenticationResult_IsTenantAdmin_ReturnsTrueWhenUserIsTenantAdmin()
        {
            AuthenticationResult result = new AuthenticationResult
            {
                User = new UserMaster { IsTenantAdmin = true }
            };
            result.IsTenantAdmin.Should().BeTrue();
        }

        #endregion

        #region AuthenticationResult-HasCrossTenantAccess-Tests

        [Fact]
        public void AuthenticationResult_HasCrossTenantAccess_ReturnsFalseWhenUserIsNull()
        {
            AuthenticationResult result = new AuthenticationResult();
            result.HasCrossTenantAccess.Should().BeFalse();
        }

        [Fact]
        public void AuthenticationResult_HasCrossTenantAccess_ReturnsFalseWhenUserIsNotAdmin()
        {
            AuthenticationResult result = new AuthenticationResult
            {
                User = new UserMaster { IsAdmin = false }
            };
            result.HasCrossTenantAccess.Should().BeFalse();
        }

        [Fact]
        public void AuthenticationResult_HasCrossTenantAccess_ReturnsTrueWhenUserIsAdmin()
        {
            AuthenticationResult result = new AuthenticationResult
            {
                User = new UserMaster { IsAdmin = true }
            };
            result.HasCrossTenantAccess.Should().BeTrue();
        }

        [Fact]
        public void AuthenticationResult_HasCrossTenantAccess_ReturnsFalseForTenantAdmin()
        {
            AuthenticationResult result = new AuthenticationResult
            {
                User = new UserMaster { IsAdmin = false, IsTenantAdmin = true }
            };
            result.HasCrossTenantAccess.Should().BeFalse();
        }

        #endregion

        #region AuthenticationResult-CanManageUsers-Tests

        [Fact]
        public void AuthenticationResult_CanManageUsers_ReturnsFalseWhenUserIsNull()
        {
            AuthenticationResult result = new AuthenticationResult();
            result.CanManageUsers.Should().BeFalse();
        }

        [Fact]
        public void AuthenticationResult_CanManageUsers_ReturnsFalseForStandardUser()
        {
            AuthenticationResult result = new AuthenticationResult
            {
                User = new UserMaster { IsAdmin = false, IsTenantAdmin = false }
            };
            result.CanManageUsers.Should().BeFalse();
        }

        [Fact]
        public void AuthenticationResult_CanManageUsers_ReturnsTrueForAdmin()
        {
            AuthenticationResult result = new AuthenticationResult
            {
                User = new UserMaster { IsAdmin = true, IsTenantAdmin = false }
            };
            result.CanManageUsers.Should().BeTrue();
        }

        [Fact]
        public void AuthenticationResult_CanManageUsers_ReturnsTrueForTenantAdmin()
        {
            AuthenticationResult result = new AuthenticationResult
            {
                User = new UserMaster { IsAdmin = false, IsTenantAdmin = true }
            };
            result.CanManageUsers.Should().BeTrue();
        }

        [Fact]
        public void AuthenticationResult_CanManageUsers_ReturnsTrueForBothAdminAndTenantAdmin()
        {
            AuthenticationResult result = new AuthenticationResult
            {
                User = new UserMaster { IsAdmin = true, IsTenantAdmin = true }
            };
            result.CanManageUsers.Should().BeTrue();
        }

        #endregion

        #region AuthenticationResult-Property-Setting-Tests

        [Fact]
        public void AuthenticationResult_CanSetIsAuthenticated()
        {
            AuthenticationResult result = new AuthenticationResult { IsAuthenticated = true };
            result.IsAuthenticated.Should().BeTrue();
        }

        [Fact]
        public void AuthenticationResult_CanSetTenant()
        {
            TenantMetadata tenant = new TenantMetadata { Name = "Test" };
            AuthenticationResult result = new AuthenticationResult { Tenant = tenant };
            result.Tenant.Should().BeSameAs(tenant);
        }

        [Fact]
        public void AuthenticationResult_CanSetUser()
        {
            UserMaster user = new UserMaster();
            AuthenticationResult result = new AuthenticationResult { User = user };
            result.User.Should().BeSameAs(user);
        }

        [Fact]
        public void AuthenticationResult_CanSetCredential()
        {
            Credential credential = new Credential();
            AuthenticationResult result = new AuthenticationResult { Credential = credential };
            result.Credential.Should().BeSameAs(credential);
        }

        [Fact]
        public void AuthenticationResult_CanSetAuthMethod()
        {
            AuthenticationResult result = new AuthenticationResult { AuthMethod = "Bearer" };
            result.AuthMethod.Should().Be("Bearer");
        }

        [Fact]
        public void AuthenticationResult_CanSetErrorMessage()
        {
            AuthenticationResult result = new AuthenticationResult { ErrorMessage = "Test error" };
            result.ErrorMessage.Should().Be("Test error");
        }

        [Fact]
        public void AuthenticationResult_CanSetRequestType()
        {
            AuthenticationResult result = new AuthenticationResult
            {
                RequestType = Conductor.Core.Enums.RequestTypeEnum.OpenAIChatCompletions
            };
            result.RequestType.Should().Be(Conductor.Core.Enums.RequestTypeEnum.OpenAIChatCompletions);
        }

        #endregion

        #region AdminAuthenticationResult-Default-Values-Tests

        [Fact]
        public void AdminAuthenticationResult_IsAuthenticated_DefaultsToFalse()
        {
            AdminAuthenticationResult result = new AdminAuthenticationResult();
            result.IsAuthenticated.Should().BeFalse();
        }

        [Fact]
        public void AdminAuthenticationResult_Administrator_DefaultsToNull()
        {
            AdminAuthenticationResult result = new AdminAuthenticationResult();
            result.Administrator.Should().BeNull();
        }

        [Fact]
        public void AdminAuthenticationResult_IsApiKeyAuth_DefaultsToFalse()
        {
            AdminAuthenticationResult result = new AdminAuthenticationResult();
            result.IsApiKeyAuth.Should().BeFalse();
        }

        [Fact]
        public void AdminAuthenticationResult_ApiKey_DefaultsToNull()
        {
            AdminAuthenticationResult result = new AdminAuthenticationResult();
            result.ApiKey.Should().BeNull();
        }

        [Fact]
        public void AdminAuthenticationResult_ErrorMessage_DefaultsToNull()
        {
            AdminAuthenticationResult result = new AdminAuthenticationResult();
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public void AdminAuthenticationResult_RequestType_DefaultsToUnknown()
        {
            AdminAuthenticationResult result = new AdminAuthenticationResult();
            result.RequestType.Should().Be(Conductor.Core.Enums.RequestTypeEnum.Unknown);
        }

        #endregion

        #region AdminAuthenticationResult-Property-Setting-Tests

        [Fact]
        public void AdminAuthenticationResult_CanSetIsAuthenticated()
        {
            AdminAuthenticationResult result = new AdminAuthenticationResult { IsAuthenticated = true };
            result.IsAuthenticated.Should().BeTrue();
        }

        [Fact]
        public void AdminAuthenticationResult_CanSetAdministrator()
        {
            Administrator admin = new Administrator();
            AdminAuthenticationResult result = new AdminAuthenticationResult { Administrator = admin };
            result.Administrator.Should().BeSameAs(admin);
        }

        [Fact]
        public void AdminAuthenticationResult_CanSetIsApiKeyAuth()
        {
            AdminAuthenticationResult result = new AdminAuthenticationResult { IsApiKeyAuth = true };
            result.IsApiKeyAuth.Should().BeTrue();
        }

        [Fact]
        public void AdminAuthenticationResult_CanSetApiKey()
        {
            AdminAuthenticationResult result = new AdminAuthenticationResult { ApiKey = "test-key" };
            result.ApiKey.Should().Be("test-key");
        }

        [Fact]
        public void AdminAuthenticationResult_CanSetErrorMessage()
        {
            AdminAuthenticationResult result = new AdminAuthenticationResult { ErrorMessage = "Test error" };
            result.ErrorMessage.Should().Be("Test error");
        }

        [Fact]
        public void AdminAuthenticationResult_CanSetRequestType()
        {
            AdminAuthenticationResult result = new AdminAuthenticationResult
            {
                RequestType = Conductor.Core.Enums.RequestTypeEnum.OpenAIEmbeddings
            };
            result.RequestType.Should().Be(Conductor.Core.Enums.RequestTypeEnum.OpenAIEmbeddings);
        }

        #endregion
    }
}
