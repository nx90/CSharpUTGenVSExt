using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpUnitTestGeneratorExt.Entity
{
    public class MockFramework
    {
        public readonly string Name;
        public readonly List<string> DetectionReferenceMatches;
        public readonly int DetectionRank;
        public readonly List<string> UsingNamespaces;
        public readonly bool SupportsGenerics;
        public readonly string ClassStartCode;
        public readonly bool HasMockFields;
        public readonly string InitializeStartCode;
        public readonly string MockFieldDeclarationCode;
        public readonly string MockFieldInitializationCode;
        public readonly string TestCleanupCode;
        public readonly string TestArrangeCode;
        public readonly TestedObjectCreationStyle TestedObjectCreationStyle;
        public readonly string TestedObjectCreationCode;
        public readonly string MockObjectReferenceCode;
        public readonly string AssertStatement;

        public MockFramework(
            string name,
            List<string> detectionReferenceMatches,
            int detectionRank,
            List<string> usingNamespace,
            bool supportsGenerics,
            string classStartCode,
            bool hasMockFields,
            string initializeStartCode,
            string mockFieldDeclarationCode,
            string mockFieldInitializationCode,
            string testCleanupCode,
            string testArrangeCode,
            TestedObjectCreationStyle testedObjectCreationStyle,
            string testedObjectCreationCode,
            string mockObjectReferenceCode,
            string assertStatement
        )
        {
            this.Name = name;
            this.DetectionReferenceMatches = detectionReferenceMatches;
            this.DetectionRank = detectionRank;
            this.UsingNamespaces = usingNamespace;
            this.SupportsGenerics = supportsGenerics;
            this.ClassStartCode = classStartCode;
            this.HasMockFields = hasMockFields;
            this.InitializeStartCode = initializeStartCode;
            this.MockFieldDeclarationCode = mockFieldDeclarationCode;
            this.MockFieldInitializationCode = mockFieldInitializationCode;
            this.TestCleanupCode = testCleanupCode;
            this.TestArrangeCode = testArrangeCode;
            this.TestedObjectCreationStyle = testedObjectCreationStyle;
            this.TestedObjectCreationCode = testedObjectCreationCode;
            this.MockObjectReferenceCode = mockObjectReferenceCode;
            this.AssertStatement = assertStatement;
        }

        public bool HasTestCleanup()
        {
            return !string.IsNullOrEmpty(TestCleanupCode);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
