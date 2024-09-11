using System;
using System.Collections.Generic;
using System.Linq;
using CSharpUnitTestGeneratorExt.LLM;

namespace CSharpUnitTestGeneratorExt.CodeGenerator
{
    public class LLMBoundaryCaseTestMethodGenerator : MethodGenerator
    {
        protected string testCaseName;
        protected string testCaseDescription;
        protected string normalCaseTestCode;
        protected AzureOpenAIClient azureOpenAIClient;

        public LLMBoundaryCaseTestMethodGenerator(int indentLevel,
            string outputCode,
            string testCaseDescription,
            string testCaseName,
            AzureOpenAIClient azureOpenAIClient
            ) : base(indentLevel, outputCode)
        {
            this.testCaseDescription = testCaseDescription;
            this.testCaseName = testCaseName;
            this.azureOpenAIClient = azureOpenAIClient;
        }

        public override List<string> GetUsedNamespaces()
        {
            return new List<string> ();
        }

        public void SetNormedTestCode(string normedTestCode)
        {
            this.normalCaseTestCode = normedTestCode;
        }

        public override string GetOutputCodeBlock()
        {
            var input = $@"You need to give another test method for a boundary case based on the test memothd of normal case.
**Only code of the boundary case test method is needed.**
**You need to add {indentLevel * 4} spaces to each line of your code to keep indent format consistent with the file.**
**Your respond must be code only, DO NOT start or end with markdown mark like ``` **
**Just use ""Exception"" type when setup or assert a exception.**
**Test case description format: [flag1] [flag2] [flag3] 1. case description.**
**Test case description is formatted as upper, flag1 is a flag about if there should be an exception, flag2 is a flag about if the exception is related with any mocked function, flag3 is TestMethodName suffix you need to use**

Test case description: 
{testCaseDescription}

Completed and valid test method for normal case:
{normalCaseTestCode}

";
            string result = string.Empty;
            try
            {
                result = azureOpenAIClient.GetSimpleChatCompletions(input).FirstOrDefault().TrimStart().TrimEnd();
            }
            catch (Exception)
            {
                
            }


            if (string.IsNullOrEmpty(result))
            {
                return string.Empty;
            }

            string toRemove = "```";
            if (result.StartsWith(toRemove))
            {
                result = result.Substring(toRemove.Length);
            }

            if (result.EndsWith(toRemove))
            {
                result = result.Substring(0, result.Length - toRemove.Length);
            }
            return result;
        }
    }
}
