namespace Test.Shared.Touchstone
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using global::Touchstone.Core;

    internal static class ReflectionTestCaseDiscovery
    {
        private static readonly HashSet<string> _LifecycleMethods = new HashSet<string>(StringComparer.Ordinal)
        {
            "Initialize",
            "InitializeAsync",
            "Dispose",
            "DisposeAsync"
        };

        internal static IReadOnlyList<TestSuiteDescriptor> Discover(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            List<TestSuiteDescriptor> suites = new List<TestSuiteDescriptor>();

            foreach (Type type in assembly
                .GetTypes()
                .Where(IsTestClass)
                .OrderBy(t => t.Namespace, StringComparer.Ordinal)
                .ThenBy(t => t.Name, StringComparer.Ordinal))
            {
                List<TestCaseDescriptor> cases = type
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(IsTestMethod)
                    .OrderBy(m => m.MetadataToken)
                    .Select(m => CreateCaseDescriptor(type, m))
                    .ToList();

                if (cases.Count < 1)
                    continue;

                string suiteId = type.FullName ?? type.Name;
                suites.Add(new TestSuiteDescriptor(
                    suiteId: suiteId,
                    displayName: type.Name,
                    cases: cases));
            }

            return suites;
        }

        private static TestCaseDescriptor CreateCaseDescriptor(Type testType, MethodInfo method)
        {
            string suiteId = testType.FullName ?? testType.Name;
            string displayName = testType.Name + "." + method.Name;

            return new TestCaseDescriptor(
                suiteId: suiteId,
                caseId: method.Name,
                displayName: displayName,
                executeAsync: cancellationToken => ReflectionTestInvoker.ExecuteAsync(testType, method, cancellationToken),
                tags: BuildTags(testType));
        }

        private static IReadOnlyList<string> BuildTags(Type testType)
        {
            List<string> tags = new List<string>();

            if (!String.IsNullOrWhiteSpace(testType.Namespace))
            {
                string[] segments = testType.Namespace.Split('.');
                for (int i = 2; i < segments.Length; i++)
                {
                    tags.Add(segments[i]);
                }
            }

            return tags;
        }

        private static bool IsTestClass(Type type)
        {
            if (type == null || !type.IsClass || type.IsAbstract)
                return false;

            if (String.IsNullOrWhiteSpace(type.Namespace) || !type.Namespace.StartsWith("Test.Shared.", StringComparison.Ordinal))
                return false;

            return type.Name.EndsWith("Tests", StringComparison.Ordinal);
        }

        private static bool IsTestMethod(MethodInfo method)
        {
            if (method == null || method.IsSpecialName)
                return false;

            if (_LifecycleMethods.Contains(method.Name))
                return false;

            if (method.GetParameters().Length > 0)
                return false;

            return method.ReturnType == typeof(void)
                || method.ReturnType == typeof(System.Threading.Tasks.Task)
                || method.ReturnType == typeof(System.Threading.Tasks.ValueTask);
        }
    }
}
