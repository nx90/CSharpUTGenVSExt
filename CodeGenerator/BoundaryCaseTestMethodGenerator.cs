using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VSIXHelloWorldProject.Entity;

namespace VSIXHelloWorldProject.CodeGenerator
{
    public class BoundaryCaseTestMethodGenerator : RuledTestMethodGenerator
    {
        private bool isExceptionCase = false;
        private bool isMockFuncThrowException = false;

        public BoundaryCaseTestMethodGenerator(int indentLevel,
            string outputCode,
            FunctionInfo testedFunctionInfo,
            string testCaseName,
            List<FunctionInfo> mockedFunctions,
            FunctionCallNode node,
            string caseDescription) : base(indentLevel, outputCode, testedFunctionInfo, testCaseName, mockedFunctions, node)
        {
            var flagArray = Regex.Matches(caseDescription, @"(?<=\[)(\S+)(?=\])");
            if (flagArray.Count < 3)
            {
                throw new Exception("Invalid boundary case flag array");
            }
            isExceptionCase = flagArray[0].Value == "exception";
            isMockFuncThrowException = flagArray[1].Value == "mockFuncThrowException";
            this.testCaseName = flagArray[2].Value;
        }

        public override string GetOutputCodeBlock()
        {
            AppendLineIndented("/// <summary>");
            AppendLineIndented($"/// {testedFunctionInfo.FunctionName}_{testCaseName}");
            AppendLineIndented("/// <summary>");

            // method attributes
            foreach(var attribute in attributes)
            {
                AppendLineIndented($"[{ attribute}]");
            }

            // create function signature part
            AppendLineIndented($"public void Test{testedFunctionInfo.FunctionName}{testCaseName}()");
            AppendLineIndented("{");
            IndentedLevelUp();

            // Arrange in test method
            AppendLineIndented("// Arrange");
            var mockObjNameAndType = new Dictionary<string, string>();
            foreach (var func in mockedFunctions)
            {
                bool isField = node.InterfaceFieldHashCodes.Keys.Contains(func.ObjHashCode);
                var hashcodeToName = isField ? node.InterfaceFieldHashCodes : node.InterfaceInputHashCodes;
                var nameToInterfaceType = isField ? node.FieldsTypes : node.InputTypes;
                var toObjType = isField ? node.InterfaceTypeFieldsRuntimeTypesMap : node.InterfaceTypeInputsRuntimeTypesMap;
                if (hashcodeToName.TryGetValue(func.ObjHashCode, out string name))
                {
                    if (nameToInterfaceType.TryGetValue(name, out string interfaceType))
                    {
                        if (toObjType.TryGetValue(new Tuple<string, string>(name, interfaceType), out string objType))
                        {
                            mockObjNameAndType[$"{(isField ? "field" : "arg")}_{name}"] = objType;
                        }
                    }
                }
            }
            foreach (var item in mockObjNameAndType)
            {
                AppendLineIndented($"var {item.Key} = new Mock<{item.Value}> ();");
            }

            if (isExceptionCase)
            {
                GenerateFunctionBodyException();
            }
            else
            {
                if (testedFunctionInfo.Output.Type == "void")
                {
                    GenerateFunctionBodyNoExceptionWithVoid();
                }
                else
                {
                    GenerateFunctionBodyNoExceptionWithoutVoid();
                }
            }

            IndentedLevelDown();
            AppendLineIndented("}");
            return outputCode;
        }

        protected void GenerateMockedFuncParasAnyBlock(List<ObjectInfo> paras)
        {
            IndentedLevelUp();
            var length = paras.Count;
            for (var index = 0; index < length; index++)
            {
                var param = paras[index];
                AppendLineIndented($"It.IsAny<{param.Type}>(){((index != length - 1) ? "," : "")}");
            }
            IndentedLevelDown();
        }

        private void GenerateFunctionBodyException()
        {
            GenerateFuncBodyCommonPart();
            // Assert in test method
            AppendLineIndented("// Assert");
            AppendLineIndented("act.Should().Throw<Exception>();");
        }

        private void GenerateFunctionBodyNoExceptionWithVoid()
        {
            GenerateFuncBodyCommonPart();
            // Assert in test method
            AppendLineIndented("// Assert");
            AppendLineIndented($"act.Should().NotThrow();");
        }

        private void GenerateFunctionBodyNoExceptionWithoutVoid()
        {
            GenerateFuncBodyCommonPart();
            // Assert in test method
            AppendLineIndented("// Assert");
            switch (testedFunctionInfo.Output.Type)
            {
                case "int":
                case "bool":
                case "Guid":
                    AppendLineIndented("actual.Should().Be(expected);");
                    break;
                default:
                    AppendLineIndented("actual.Should().BeEquivalentTo(expected);");
                    break;
            }
        }

        private void GenerateFuncBodyCommonPart()
        {
            // Set up mocked functions
            foreach (var func in mockedFunctions)
            {
                bool isField = node.InterfaceFieldHashCodes.Keys.Contains(func.ObjHashCode);
                var hashcodeToName = isField ? node.InterfaceFieldHashCodes : node.InterfaceInputHashCodes;
                if (hashcodeToName.TryGetValue(func.ObjHashCode, out string name))
                {
                    AppendLineIndented($"{(isField ? "field" : "arg")}_{name}.Setup(x => x.{func.FunctionName}(");
                    GenerateMockedFuncParasAnyBlock(func.InputParams);
                    outputCode += "))";
                    if (this.isMockFuncThrowException)
                    {
                        AppendLineIndented($".Throws<Exception>();");
                    }
                    else
                    {
                        AppendLineIndented($".Returns({this.ObjectToCSharpCode(func.Output.Value, func.Output.Type)});");
                    }
                }
            }

            // Create tested class instance by reflection
            AppendLineIndented($"Type testedType = typeof({testedFunctionInfo.BelongedClassName});");
            AppendLineIndented($"{testedFunctionInfo.BelongedClassName} instance = ({testedFunctionInfo.BelongedClassName})Activator.CreateInstance(testedType, true);");
            foreach (var fieldName in node.FieldsTypes.Keys)
            {
                AppendLineIndented($"FieldInfo fieldInfo_{fieldName} = testedType.GetField(\"{fieldName}\");");
                AppendLineIndented($"fieldInfo_{fieldName}.SetValue(instance, {ObjectToCSharpCode(node.Fields[fieldName], node.FieldsTypes[fieldName])});");
            }
            AppendLineIndented($"");
            outputCode += ");";
            IndentedLevelDown();
            AppendLineIndented();

            // Act
            AppendLineIndented("// Act");
            AppendLineIndented($"Action act = () => instance.{testedFunctionInfo.FunctionName}(");
            IndentedLevelUp();
            var funcInputParamsLen = testedFunctionInfo.InputParams.Count;
            for (var index = 0; index < funcInputParamsLen; index++)
            {
                var param = testedFunctionInfo.InputParams[index];
                AppendLineIndented($"{this.ObjectToCSharpCode(param.Value, param.Type)}{(index != funcInputParamsLen - 1 ? "," : string.Empty)}");
            }
            outputCode += ");";
            IndentedLevelDown();
            AppendLineIndented();
        }
    }
}
