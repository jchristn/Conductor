namespace Test.Shared.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Conductor.Core.Models;
    using FluentAssertions;
    
    /// <summary>
    /// Unit tests for UserMaster model.
    /// </summary>
    public class UserMasterTests
    {
        #region TenantId-Tests
        public void TenantId_WhenNull_ThrowsArgumentNullException()
        {
            UserMaster user = new UserMaster();
            Action act = () => user.TenantId = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("TenantId");
        }
        public void TenantId_WhenEmpty_ThrowsArgumentNullException()
        {
            UserMaster user = new UserMaster();
            Action act = () => user.TenantId = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("TenantId");
        }

        #endregion

        #region FirstName-Tests
        public void FirstName_WhenNull_ThrowsArgumentNullException()
        {
            UserMaster user = new UserMaster();
            Action act = () => user.FirstName = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("FirstName");
        }
        public void FirstName_WhenEmpty_ThrowsArgumentNullException()
        {
            UserMaster user = new UserMaster();
            Action act = () => user.FirstName = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("FirstName");
        }

        #endregion

        #region LastName-Tests
        public void LastName_WhenNull_ThrowsArgumentNullException()
        {
            UserMaster user = new UserMaster();
            Action act = () => user.LastName = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("LastName");
        }
        public void LastName_WhenEmpty_ThrowsArgumentNullException()
        {
            UserMaster user = new UserMaster();
            Action act = () => user.LastName = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("LastName");
        }

        #endregion

        #region Email-Tests
        public void Email_WhenNull_ThrowsArgumentNullException()
        {
            UserMaster user = new UserMaster();
            Action act = () => user.Email = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("Email");
        }
        public void Email_WhenEmpty_ThrowsArgumentNullException()
        {
            UserMaster user = new UserMaster();
            Action act = () => user.Email = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("Email");
        }

        #endregion

        #region Password-Tests
        public void Password_WhenNull_ThrowsArgumentNullException()
        {
            UserMaster user = new UserMaster();
            Action act = () => user.Password = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("Password");
        }
        public void Password_WhenEmpty_ThrowsArgumentNullException()
        {
            UserMaster user = new UserMaster();
            Action act = () => user.Password = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("Password");
        }

        #endregion

        #region Default-Value-Tests
        public void IsAdmin_DefaultsToFalse()
        {
            UserMaster user = new UserMaster();
            user.IsAdmin.Should().BeFalse();
        }
        public void IsTenantAdmin_DefaultsToFalse()
        {
            UserMaster user = new UserMaster();
            user.IsTenantAdmin.Should().BeFalse();
        }
        public void Active_DefaultsToTrue()
        {
            UserMaster user = new UserMaster();
            user.Active.Should().BeTrue();
        }
        public void Labels_InitializesAsEmptyList()
        {
            UserMaster user = new UserMaster();
            user.Labels.Should().NotBeNull();
            user.Labels.Should().BeEmpty();
        }
        public void Tags_InitializesAsEmptyDictionary()
        {
            UserMaster user = new UserMaster();
            user.Tags.Should().NotBeNull();
            user.Tags.Should().BeEmpty();
        }
        public void FirstName_HasDefaultValue()
        {
            UserMaster user = new UserMaster();
            user.TenantId = "ten_test";
            user.FirstName.Should().Be("First");
        }
        public void LastName_HasDefaultValue()
        {
            UserMaster user = new UserMaster();
            user.TenantId = "ten_test";
            user.LastName.Should().Be("Last");
        }
        public void Email_HasDefaultValue()
        {
            UserMaster user = new UserMaster();
            user.TenantId = "ten_test";
            user.Email.Should().Be("user@example.com");
        }
        public void Password_HasDefaultValue()
        {
            UserMaster user = new UserMaster();
            user.TenantId = "ten_test";
            user.Password.Should().Be("password");
        }

        #endregion

        #region Redact-Tests
        public void Redact_WithNullUser_ReturnsNull()
        {
            UserMaster result = UserMaster.Redact(null);
            result.Should().BeNull();
        }
        public void Redact_MasksPassword_KeepingLast4Chars()
        {
            UserMaster user = new UserMaster();
            user.TenantId = "ten_test";
            user.Password = "mySecurePassword123";

            UserMaster redacted = UserMaster.Redact(user);

            redacted.Password.Should().EndWith("d123");
            redacted.Password.Should().StartWith("***");
        }
        public void Redact_ShortPassword_ShowsAsterisks()
        {
            UserMaster user = new UserMaster();
            user.TenantId = "ten_test";
            user.Password = "abc";

            UserMaster redacted = UserMaster.Redact(user);

            redacted.Password.Should().Be("****");
        }
        public void Redact_DoesNotModifyOriginal()
        {
            UserMaster user = new UserMaster();
            user.TenantId = "ten_test";
            user.Password = "originalpassword";

            UserMaster redacted = UserMaster.Redact(user);

            user.Password.Should().Be("originalpassword");
            redacted.Password.Should().NotBe("originalpassword");
        }

        #endregion

        #region FromDataRow-Tests
        public void FromDataRow_WithNullRow_ReturnsNull()
        {
            UserMaster result = UserMaster.FromDataRow(null);
            result.Should().BeNull();
        }
        public void FromDataRow_WithValidData_CreatesInstance()
        {
            DataTable table = CreateTestDataTable();
            DataRow row = table.NewRow();
            row["id"] = "usr_test123";
            row["tenantid"] = "ten_test";
            row["firstname"] = "John";
            row["lastname"] = "Doe";
            row["email"] = "john.doe@example.com";
            row["password"] = "hashedpassword";
            row["active"] = true;
            row["isadmin"] = false;
            row["istenantadmin"] = true;
            row["createdutc"] = DateTime.UtcNow;
            row["lastupdateutc"] = DateTime.UtcNow;
            table.Rows.Add(row);

            UserMaster user = UserMaster.FromDataRow(row);

            user.Should().NotBeNull();
            user.Id.Should().Be("usr_test123");
            user.TenantId.Should().Be("ten_test");
            user.FirstName.Should().Be("John");
            user.LastName.Should().Be("Doe");
            user.Email.Should().Be("john.doe@example.com");
            user.IsTenantAdmin.Should().BeTrue();
        }

        #endregion

        #region FromDataTable-Tests
        public void FromDataTable_WithNullTable_ReturnsNull()
        {
            List<UserMaster> result = UserMaster.FromDataTable(null);
            result.Should().BeNull();
        }
        public void FromDataTable_WithEmptyTable_ReturnsEmptyList()
        {
            DataTable table = CreateTestDataTable();
            List<UserMaster> result = UserMaster.FromDataTable(table);
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }
        public void FromDataTable_WithMultipleRows_ReturnsCollection()
        {
            DataTable table = CreateTestDataTable();
            for (int i = 0; i < 3; i++)
            {
                DataRow row = table.NewRow();
                row["id"] = $"usr_test{i}";
                row["tenantid"] = "ten_test";
                row["firstname"] = $"User{i}";
                row["lastname"] = "Test";
                row["email"] = $"user{i}@example.com";
                row["password"] = "password";
                row["active"] = true;
                table.Rows.Add(row);
            }

            List<UserMaster> result = UserMaster.FromDataTable(table);
            result.Should().HaveCount(3);
        }

        #endregion

        #region Id-Tests
        public void Id_StartsWithCorrectPrefix()
        {
            UserMaster user = new UserMaster();
            user.Id.Should().StartWith("usr_");
        }

        #endregion

        #region Helper-Methods

        private DataTable CreateTestDataTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("id", typeof(string));
            table.Columns.Add("tenantid", typeof(string));
            table.Columns.Add("firstname", typeof(string));
            table.Columns.Add("lastname", typeof(string));
            table.Columns.Add("email", typeof(string));
            table.Columns.Add("password", typeof(string));
            table.Columns.Add("active", typeof(bool));
            table.Columns.Add("isadmin", typeof(bool));
            table.Columns.Add("istenantadmin", typeof(bool));
            table.Columns.Add("createdutc", typeof(DateTime));
            table.Columns.Add("lastupdateutc", typeof(DateTime));
            table.Columns.Add("labels", typeof(string));
            table.Columns.Add("tags", typeof(string));
            table.Columns.Add("metadata", typeof(string));
            return table;
        }

        #endregion
    }
}
