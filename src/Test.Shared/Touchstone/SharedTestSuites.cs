namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using global::Touchstone.Core;
    using global::Test.Shared.Touchstone;

    public static class SharedTestSuites
    {
        private static readonly Lazy<IReadOnlyList<TestSuiteDescriptor>> _All = new Lazy<IReadOnlyList<TestSuiteDescriptor>>(LoadSuites);

        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return _All.Value; }
        }

        private static IReadOnlyList<TestSuiteDescriptor> LoadSuites()
        {
            return ReflectionTestCaseDiscovery.Discover(typeof(SharedTestSuites).Assembly);
        }
    }
}
