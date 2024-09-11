using System.Collections.Generic;

namespace CSharpUnitTestGeneratorExt.Entity
{
    public class TestFramework
    {
        public readonly string Name;
        public readonly List<string> DetectionReferenceMatches;
        public readonly int DetectionRank;
        public readonly string UsingNamespace;
        public readonly string TestClassAttribute;
        public readonly string TestMethodAttribute;
        public readonly TestInitializeStyle TestInitializeStyle;
        public readonly string TestInitializeAttribute;
        public readonly TestCleanupStyle TestCleanupStyle;
        public readonly string TestCleanupAttribute;
        public readonly string AssertFailStatement;

        public TestFramework(
            string name,
            List<string> detectionReferenceMatches,
            int detectionRank,
            string usingNamespace,
            string testClassAttribute,
            string testMethodAttribute,
            TestInitializeStyle testInitializeStyle,
            string testInitializeAttribute,
            TestCleanupStyle testCleanupStyle,
            string testCleanupAttribute,
            string assertFailStatement
        ) 
        {
            this.Name = name;
            this.DetectionReferenceMatches = detectionReferenceMatches;
            this.DetectionRank = detectionRank;
            this.UsingNamespace = usingNamespace;
            this.TestClassAttribute = testClassAttribute;
            this.TestMethodAttribute = testMethodAttribute;
            this.TestInitializeStyle = testInitializeStyle;
            this.TestInitializeAttribute = testInitializeAttribute;
            this.TestCleanupStyle = testCleanupStyle;
            this.TestCleanupAttribute = testCleanupAttribute;
            this.AssertFailStatement = assertFailStatement;
        }

        public override string ToString() {
            return Name;
        }
    }
}
