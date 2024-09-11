using System.Collections.Generic;

namespace CSharpUnitTestGeneratorExt.CodeGenerator
{
    public abstract class MethodGenerator : CodeGeneratorBase
    {
        protected List<string> attributes = new List<string>();

        public MethodGenerator(int indentLevel, string outputCode) : base(indentLevel, outputCode)
        {
        }

        public void SetIndentLevel(int level) {
            this.indentLevel = level;
        }

        public void SetAttributes(List<string> attributes)
        {
            this.attributes = attributes;
        }

        public abstract List<string> GetUsedNamespaces();
    }
}
