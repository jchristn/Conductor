namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using global::Touchstone.Core;
    using global::Touchstone.XunitAdapter;
    using global::Xunit;

    public sealed class TouchstoneTheoryTests
    {
        public static TheoryData<TestCaseDescriptor> TestCases()
        {
            return new TouchstoneTheoryData(Shared.SharedTestSuites.All);
        }

        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
