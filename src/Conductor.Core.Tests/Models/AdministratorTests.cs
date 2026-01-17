namespace Conductor.Core.Tests.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Conductor.Core.Models;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit tests for Administrator model.
    /// </summary>
    public class AdministratorTests
    {
        #region Email-Tests

        [Fact]
        public void Email_WhenNull_ThrowsArgumentNullException()
        {
            Administrator admin = new Administrator();
            Action act = () => admin.Email = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("Email");
        }

        [Fact]
        public void Email_WhenEmpty_ThrowsArgumentNullException()
        {
            Administrator admin = new Administrator();
            Action act = () => admin.Email = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("Email");
        }

        [Fact]
        public void Email_ConvertsToLowercase()
        {
            Administrator admin = new Administrator();
            admin.Email = "Admin@Example.COM";
            admin.PasswordSha256 = Administrator.ComputePasswordHash("password");
            admin.Email.Should().Be("admin@example.com");
        }

        #endregion

        #region PasswordSha256-Tests

        [Fact]
        public void PasswordSha256_WhenNull_ThrowsArgumentNullException()
        {
            Administrator admin = new Administrator();
            Action act = () => admin.PasswordSha256 = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("PasswordSha256");
        }

        [Fact]
        public void PasswordSha256_WhenEmpty_ThrowsArgumentNullException()
        {
            Administrator admin = new Administrator();
            Action act = () => admin.PasswordSha256 = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("PasswordSha256");
        }

        #endregion

        #region ComputePasswordHash-Tests

        [Fact]
        public void ComputePasswordHash_WithNullPassword_ThrowsArgumentNullException()
        {
            Action act = () => Administrator.ComputePasswordHash(null);
            act.Should().Throw<ArgumentNullException>().WithParameterName("password");
        }

        [Fact]
        public void ComputePasswordHash_WithEmptyPassword_ThrowsArgumentNullException()
        {
            Action act = () => Administrator.ComputePasswordHash("");
            act.Should().Throw<ArgumentNullException>().WithParameterName("password");
        }

        [Fact]
        public void ComputePasswordHash_ReturnsSha256Hash()
        {
            string hash = Administrator.ComputePasswordHash("password");
            hash.Should().NotBeNullOrEmpty();
            hash.Should().HaveLength(64); // SHA256 produces 64 hex characters
        }

        [Fact]
        public void ComputePasswordHash_IsDeterministic()
        {
            string hash1 = Administrator.ComputePasswordHash("testpassword");
            string hash2 = Administrator.ComputePasswordHash("testpassword");
            hash1.Should().Be(hash2);
        }

        [Fact]
        public void ComputePasswordHash_DifferentInputs_DifferentHashes()
        {
            string hash1 = Administrator.ComputePasswordHash("password1");
            string hash2 = Administrator.ComputePasswordHash("password2");
            hash1.Should().NotBe(hash2);
        }

        [Fact]
        public void ComputePasswordHash_ReturnsLowercaseHex()
        {
            string hash = Administrator.ComputePasswordHash("test");
            hash.Should().MatchRegex("^[a-f0-9]+$");
        }

        #endregion

        #region VerifyPassword-Tests

        [Fact]
        public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
        {
            Administrator admin = new Administrator();
            admin.Email = "test@example.com";
            admin.PasswordSha256 = Administrator.ComputePasswordHash("correctpassword");

            bool result = admin.VerifyPassword("correctpassword");
            result.Should().BeTrue();
        }

        [Fact]
        public void VerifyPassword_WithIncorrectPassword_ReturnsFalse()
        {
            Administrator admin = new Administrator();
            admin.Email = "test@example.com";
            admin.PasswordSha256 = Administrator.ComputePasswordHash("correctpassword");

            bool result = admin.VerifyPassword("wrongpassword");
            result.Should().BeFalse();
        }

        [Fact]
        public void VerifyPassword_WithNullPassword_ReturnsFalse()
        {
            Administrator admin = new Administrator();
            admin.Email = "test@example.com";
            admin.PasswordSha256 = Administrator.ComputePasswordHash("password");

            bool result = admin.VerifyPassword(null);
            result.Should().BeFalse();
        }

        [Fact]
        public void VerifyPassword_WithEmptyPassword_ReturnsFalse()
        {
            Administrator admin = new Administrator();
            admin.Email = "test@example.com";
            admin.PasswordSha256 = Administrator.ComputePasswordHash("password");

            bool result = admin.VerifyPassword("");
            result.Should().BeFalse();
        }

        [Fact]
        public void VerifyPassword_IsCaseSensitive()
        {
            Administrator admin = new Administrator();
            admin.Email = "test@example.com";
            admin.PasswordSha256 = Administrator.ComputePasswordHash("Password");

            admin.VerifyPassword("PASSWORD").Should().BeFalse();
            admin.VerifyPassword("password").Should().BeFalse();
            admin.VerifyPassword("Password").Should().BeTrue();
        }

        #endregion

        #region Default-Value-Tests

        [Fact]
        public void Active_DefaultsToTrue()
        {
            Administrator admin = new Administrator();
            admin.Active.Should().BeTrue();
        }

        [Fact]
        public void FirstName_DefaultsToNull()
        {
            Administrator admin = new Administrator();
            admin.FirstName.Should().BeNull();
        }

        [Fact]
        public void LastName_DefaultsToNull()
        {
            Administrator admin = new Administrator();
            admin.LastName.Should().BeNull();
        }

        #endregion

        #region FromDataRow-Tests

        [Fact]
        public void FromDataRow_WithNullRow_ReturnsNull()
        {
            Administrator result = Administrator.FromDataRow(null);
            result.Should().BeNull();
        }

        [Fact]
        public void FromDataRow_WithValidData_CreatesInstance()
        {
            DataTable table = CreateTestDataTable();
            DataRow row = table.NewRow();
            row["id"] = "admin_test123";
            row["email"] = "admin@test.com";
            row["passwordsha256"] = Administrator.ComputePasswordHash("password");
            row["firstname"] = "Admin";
            row["lastname"] = "User";
            row["active"] = true;
            row["createdutc"] = DateTime.UtcNow;
            row["lastupdateutc"] = DateTime.UtcNow;
            table.Rows.Add(row);

            Administrator admin = Administrator.FromDataRow(row);

            admin.Should().NotBeNull();
            admin.Id.Should().Be("admin_test123");
            admin.Email.Should().Be("admin@test.com");
            admin.FirstName.Should().Be("Admin");
            admin.LastName.Should().Be("User");
        }

        #endregion

        #region FromDataTable-Tests

        [Fact]
        public void FromDataTable_WithNullTable_ReturnsNull()
        {
            List<Administrator> result = Administrator.FromDataTable(null);
            result.Should().BeNull();
        }

        [Fact]
        public void FromDataTable_WithEmptyTable_ReturnsEmptyList()
        {
            DataTable table = CreateTestDataTable();
            List<Administrator> result = Administrator.FromDataTable(table);
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        #endregion

        #region Id-Tests

        [Fact]
        public void Id_StartsWithCorrectPrefix()
        {
            Administrator admin = new Administrator();
            admin.Id.Should().StartWith("admin_");
        }

        #endregion

        #region Helper-Methods

        private DataTable CreateTestDataTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("id", typeof(string));
            table.Columns.Add("email", typeof(string));
            table.Columns.Add("passwordsha256", typeof(string));
            table.Columns.Add("firstname", typeof(string));
            table.Columns.Add("lastname", typeof(string));
            table.Columns.Add("active", typeof(bool));
            table.Columns.Add("createdutc", typeof(DateTime));
            table.Columns.Add("lastupdateutc", typeof(DateTime));
            return table;
        }

        #endregion
    }
}
