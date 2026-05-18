namespace Test.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    [TestFixture]
    public sealed class TouchstoneNunitTests
    {
        private static IEnumerable TestCases()
        {
            return new TouchstoneTestCaseSource(Shared.SharedTestSuites.All);
        }

        [Test]
        [TestCaseSource(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
