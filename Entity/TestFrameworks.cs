using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSIXHelloWorldProject.Entity
{
    public static class TestFrameworks
    {
        public static readonly string VisualStudioName = "VisualStudio";
        public static readonly string NUnitName = "NUnit";
        public static readonly string XUnitName = "xUnit";

        public static readonly Dictionary<string, TestFramework> testFrameworks = new Dictionary<string, TestFramework> {
            { VisualStudioName,
            new TestFramework(
                TestFrameworks.VisualStudioName,
                new List<string> { "Microsoft.VisualStudio.QualityTools.UnitTestFramework", "Microsoft.VisualStudio.TestPlatform.TestFramework" },
                1,
                "using Microsoft.VisualStudio.TestTools.UnitTesting",
                "TestClass",
                "TestMethod",
                TestInitializeStyle.AttributedMethod,
                "TestInitialize",
                TestCleanupStyle.AttributedMethod,
                "TestCleanup",
                "Assert.Fail();"
            ) },
            { NUnitName,
            new TestFramework(
                TestFrameworks.NUnitName,
                new List<string> { "NUnit", "NUnit.Framework" },
                0,
                "using NUnit.Framework",
                "TestFixture",
                "Test",
                TestInitializeStyle.AttributedMethod,
                "SetUp",
                TestCleanupStyle.AttributedMethod,
                "TearDown",
                "Assert.Fail();"
            ) },
            { XUnitName,
            new TestFramework(
                TestFrameworks.XUnitName,
                new List<string> { "xunit", "xunit.core" },
                0,
                "using Xunit",
                null,
                "Fact",
                TestInitializeStyle.Constructor,
                null,
                TestCleanupStyle.Disposable,
                null,
                "Assert.True(false);"
            ) }
        };

        public static readonly TestFramework Default = testFrameworks[VisualStudioName];

        public static TestFramework Get(string name)
        {
            if(testFrameworks.TryGetValue(name, out var testFramework))
            {
                return testFramework;
            }
            return Default;
        }
    }
}
