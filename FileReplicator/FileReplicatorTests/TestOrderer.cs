using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace FileReplicator.Tests.Integration
{
    /// <summary>
    /// Упорядочивает тесты по имени: Start сначала, Stop потом, остальные посередине
    /// </summary>
    public class StartStopTestOrderer : ITestCaseOrderer
    {
        public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases) where TTestCase : ITestCase
        {
            return testCases.OrderBy(tc =>
            {
                var testName = tc.TestMethod.Method.Name;

                if (testName.Contains("Start"))
                    return 0; // Start выполняется первым
                if (testName.Contains("Stop"))
                    return 2; // Stop выполняется последним
                return 1; // Остальные посередине
            });
        }
    }
}
