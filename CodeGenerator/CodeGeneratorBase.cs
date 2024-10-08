﻿using System;

namespace CSharpUnitTestGeneratorExt.CodeGenerator
{
    public abstract class CodeGeneratorBase
    {
        protected int indentLevel = 0;
        protected string outputCode;

        public CodeGeneratorBase(int indentLevel, string outputCode)
        {
            this.indentLevel = indentLevel;
            this.outputCode = outputCode;
        }

        public abstract string GetOutputCodeBlock();

        protected void AppendIndent()
        {
            this.outputCode += "\n";
            for (int i = 0; i < this.indentLevel; i++)
            {
                this.outputCode += "    ";
            }
        }

        protected void AppendLineIndented(string line = " ")
        {
            this.AppendIndent();
            this.outputCode += line;
        }

        protected void AppendLineInFileStarted(string line = "")
        {
            this.outputCode += line;
        }

        protected void IndentedLevelUp()
        {
            this.indentLevel++;
        }

        protected void IndentedLevelDown()
        {
            this.indentLevel--;
        }
    }
}
