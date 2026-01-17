namespace Conductor.Core.Tests.Enums
{
    using System;
    using Conductor.Core.Enums;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit tests for enums.
    /// </summary>
    public class EnumTests
    {
        #region LoadBalancingModeEnum-Tests

        [Fact]
        public void LoadBalancingModeEnum_RoundRobin_HasValue0()
        {
            ((int)LoadBalancingModeEnum.RoundRobin).Should().Be(0);
        }

        [Fact]
        public void LoadBalancingModeEnum_Random_HasValue1()
        {
            ((int)LoadBalancingModeEnum.Random).Should().Be(1);
        }

        [Fact]
        public void LoadBalancingModeEnum_FirstAvailable_HasValue2()
        {
            ((int)LoadBalancingModeEnum.FirstAvailable).Should().Be(2);
        }

        [Fact]
        public void LoadBalancingModeEnum_CanParse()
        {
            Enum.TryParse<LoadBalancingModeEnum>("RoundRobin", out LoadBalancingModeEnum result).Should().BeTrue();
            result.Should().Be(LoadBalancingModeEnum.RoundRobin);
        }

        #endregion

        #region EnumerationOrderEnum-Tests

        [Fact]
        public void EnumerationOrderEnum_CreatedAscending_HasValue0()
        {
            ((int)EnumerationOrderEnum.CreatedAscending).Should().Be(0);
        }

        [Fact]
        public void EnumerationOrderEnum_CreatedDescending_HasValue1()
        {
            ((int)EnumerationOrderEnum.CreatedDescending).Should().Be(1);
        }

        [Fact]
        public void EnumerationOrderEnum_LastUpdateAscending_HasValue2()
        {
            ((int)EnumerationOrderEnum.LastUpdateAscending).Should().Be(2);
        }

        [Fact]
        public void EnumerationOrderEnum_LastUpdateDescending_HasValue3()
        {
            ((int)EnumerationOrderEnum.LastUpdateDescending).Should().Be(3);
        }

        [Fact]
        public void EnumerationOrderEnum_NameAscending_HasValue4()
        {
            ((int)EnumerationOrderEnum.NameAscending).Should().Be(4);
        }

        [Fact]
        public void EnumerationOrderEnum_NameDescending_HasValue5()
        {
            ((int)EnumerationOrderEnum.NameDescending).Should().Be(5);
        }

        [Fact]
        public void EnumerationOrderEnum_CanParse()
        {
            Enum.TryParse<EnumerationOrderEnum>("CreatedDescending", out EnumerationOrderEnum result).Should().BeTrue();
            result.Should().Be(EnumerationOrderEnum.CreatedDescending);
        }

        #endregion

        #region DatabaseTypeEnum-Tests

        [Fact]
        public void DatabaseTypeEnum_Sqlite_HasValue0()
        {
            ((int)DatabaseTypeEnum.Sqlite).Should().Be(0);
        }

        [Fact]
        public void DatabaseTypeEnum_PostgreSql_HasValue1()
        {
            ((int)DatabaseTypeEnum.PostgreSql).Should().Be(1);
        }

        [Fact]
        public void DatabaseTypeEnum_SqlServer_HasValue2()
        {
            ((int)DatabaseTypeEnum.SqlServer).Should().Be(2);
        }

        [Fact]
        public void DatabaseTypeEnum_MySql_HasValue3()
        {
            ((int)DatabaseTypeEnum.MySql).Should().Be(3);
        }

        [Fact]
        public void DatabaseTypeEnum_CanParse()
        {
            Enum.TryParse<DatabaseTypeEnum>("PostgreSql", out DatabaseTypeEnum result).Should().BeTrue();
            result.Should().Be(DatabaseTypeEnum.PostgreSql);
        }

        #endregion

        #region ApiTypeEnum-Tests

        [Fact]
        public void ApiTypeEnum_Ollama_HasValue0()
        {
            ((int)ApiTypeEnum.Ollama).Should().Be(0);
        }

        [Fact]
        public void ApiTypeEnum_OpenAI_HasValue1()
        {
            ((int)ApiTypeEnum.OpenAI).Should().Be(1);
        }

        [Fact]
        public void ApiTypeEnum_CanParse()
        {
            Enum.TryParse<ApiTypeEnum>("OpenAI", out ApiTypeEnum result).Should().BeTrue();
            result.Should().Be(ApiTypeEnum.OpenAI);
        }

        #endregion

        #region HealthCheckMethodEnum-Tests

        [Fact]
        public void HealthCheckMethodEnum_GET_HasValue0()
        {
            ((int)HealthCheckMethodEnum.GET).Should().Be(0);
        }

        [Fact]
        public void HealthCheckMethodEnum_HEAD_HasValue1()
        {
            ((int)HealthCheckMethodEnum.HEAD).Should().Be(1);
        }

        [Fact]
        public void HealthCheckMethodEnum_CanParse()
        {
            Enum.TryParse<HealthCheckMethodEnum>("HEAD", out HealthCheckMethodEnum result).Should().BeTrue();
            result.Should().Be(HealthCheckMethodEnum.HEAD);
        }

        #endregion

        #region RequestTypeEnum-Tests

        [Fact]
        public void RequestTypeEnum_Unknown_HasValue0()
        {
            ((int)RequestTypeEnum.Unknown).Should().Be(0);
        }

        [Fact]
        public void RequestTypeEnum_HasOpenAIOperations()
        {
            Enum.IsDefined(typeof(RequestTypeEnum), "OpenAIChatCompletions").Should().BeTrue();
            Enum.IsDefined(typeof(RequestTypeEnum), "OpenAICompletions").Should().BeTrue();
            Enum.IsDefined(typeof(RequestTypeEnum), "OpenAIListModels").Should().BeTrue();
            Enum.IsDefined(typeof(RequestTypeEnum), "OpenAIEmbeddings").Should().BeTrue();
        }

        [Fact]
        public void RequestTypeEnum_HasOllamaOperations()
        {
            Enum.IsDefined(typeof(RequestTypeEnum), "OllamaGenerate").Should().BeTrue();
            Enum.IsDefined(typeof(RequestTypeEnum), "OllamaChat").Should().BeTrue();
            Enum.IsDefined(typeof(RequestTypeEnum), "OllamaListTags").Should().BeTrue();
            Enum.IsDefined(typeof(RequestTypeEnum), "OllamaEmbeddings").Should().BeTrue();
        }

        [Fact]
        public void RequestTypeEnum_HasCRUDOperations()
        {
            Enum.IsDefined(typeof(RequestTypeEnum), "CreateUser").Should().BeTrue();
            Enum.IsDefined(typeof(RequestTypeEnum), "ReadUser").Should().BeTrue();
            Enum.IsDefined(typeof(RequestTypeEnum), "UpdateUser").Should().BeTrue();
            Enum.IsDefined(typeof(RequestTypeEnum), "DeleteUser").Should().BeTrue();
        }

        [Fact]
        public void RequestTypeEnum_CanParse()
        {
            Enum.TryParse<RequestTypeEnum>("OpenAIChatCompletions", out RequestTypeEnum result).Should().BeTrue();
            result.Should().Be(RequestTypeEnum.OpenAIChatCompletions);
        }

        #endregion
    }
}
