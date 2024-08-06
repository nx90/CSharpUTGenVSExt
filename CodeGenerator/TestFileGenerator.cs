using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VSIXHelloWorldProject.Entity;

namespace VSIXHelloWorldProject.CodeGenerator
{
    public class TestFileGenerator : CodeGeneratorBase
    {
        private List<string> systemUsingStatements = new List<string> {
            "using System",
            "using System.Collections.Generic",
            "using System.IO",
            "using System.Linq",
            "using System.Reflection",
            "using System.Text",
            "using System.Threading.Tasks"
        };
        private List<string> usingStatements = new List<string> {
            "using FluentAssertions",
            "using Newtonsoft.Json",
        };

        private TestFramework testFramework;
        private MockFramework mockFramework;
        private string className;
        private List<MethodGenerator> methodGenerators;

        public TestFileGenerator(
            TestFramework testFramework,
            MockFramework mockFramework,
            string className,
            List<MethodGenerator> methodGenerators) : base(0, "")
        {
            this.testFramework = testFramework;
            this.mockFramework = mockFramework;
            this.className = className;
            this.methodGenerators = methodGenerators;

            if (testFramework.UsingNamespace != null)
            {
                this.usingStatements.Add(testFramework.UsingNamespace);
            }
            if (mockFramework.UsingNamespaces != null)
            {
                this.usingStatements.AddRange(mockFramework.UsingNamespaces);
            }
            foreach (var methodGenerator in methodGenerators)
            {
                this.usingStatements.AddRange(methodGenerator.GetUsedNamespaces());
            }
            var systemNamespaces = this.usingStatements.Where(statement => statement.StartsWith("using System")).Distinct().OrderBy(s => s);
            var otherNamespaces = this.usingStatements.Where(statement => !statement.StartsWith("using System")).Distinct().OrderBy(s => s);
            this.usingStatements = systemUsingStatements.Concat(systemNamespaces).Concat(otherNamespaces).ToList();
        }

        public override string GetOutputCodeBlock()
        {
            AppendLineIndented($"// <copyright file=\"{this.className}.test.cs\" company=\"Microsoft\">");
            AppendLineIndented("//     Copyright (c) Microsoft Corporation.  All rights reserved.");
            AppendLineIndented("// </copyright>");
            AppendLineIndented();

            // Using statements  
            foreach (var usingStatement in this.usingStatements)
            {
                AppendLineIndented(usingStatement.EndsWith(";") ? usingStatement : usingStatement + ";");
            }
            AppendLineIndented();

            // Namespace  
            AppendLineIndented($"namespace UnitTestDemo");
            AppendLineIndented("{");
            IndentedLevelUp();

            // Test class documentation  
            AppendLineIndented("/// <summary>");
            AppendLineIndented($"/// {this.className}Tests");
            AppendLineIndented("/// <summary>");

            // Test class attribute  
            if (!string.IsNullOrEmpty(this.testFramework.TestClassAttribute))
            {
                AppendLineIndented($"[{this.testFramework.TestClassAttribute}]");
            }

            // Test class declaration  
            AppendLineIndented($"public class {this.className}Tests");
            if (mockFramework.HasTestCleanup() && (testFramework.TestCleanupStyle == TestCleanupStyle.Disposable))
            {
                outputCode += " : IDisposable";
            }
            AppendLineIndented("{");
            IndentedLevelUp();

            for (int index = 0; index < this.methodGenerators.Count; index++)
            {
                var methodGenerator = this.methodGenerators[index];
                methodGenerator.SetIndentLevel(this.indentLevel);
                if (!string.IsNullOrEmpty(this.testFramework.TestMethodAttribute))
                {
                    methodGenerator.SetAttributes(new List<string> { this.testFramework.TestMethodAttribute });
                }
                outputCode += methodGenerator.GetOutputCodeBlock();
                if (index != this.methodGenerators.Count - 1)
                {
                    AppendLineIndented();
                }
            }

            IndentedLevelDown();
            AppendLineIndented("}");
            IndentedLevelDown();
            AppendLineIndented("}");

            return outputCode.ToString();
        }
    }
}
