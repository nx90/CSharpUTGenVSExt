using System;
using System.Collections.Generic;
using System.Linq;
using CSharpUnitTestGeneratorExt.Entity;

namespace CSharpUnitTestGeneratorExt.CodeGenerator
{
    public class TestingFuncOutputCalcMethodGenerator : RuledTestMethodGenerator
    {
        private string resultFilePath;

        public TestingFuncOutputCalcMethodGenerator(int indentLevel,
            string outputCode,
            FunctionInfo testedFunctionInfo,
            string testCaseName,
            List<FunctionInfo> mockedFunctions,
            FunctionCallNode node,
            string resultFilePath) : base(indentLevel, outputCode, testedFunctionInfo, testCaseName, mockedFunctions, node)
        {
            this.resultFilePath = resultFilePath;
        }

        public override string GetOutputCodeBlock()
        {
            // method documention
            AppendLineIndented("/// <summary>");
            AppendLineIndented($"/// {testedFunctionInfo.FunctionName}_{testCaseName}");
            AppendLineIndented("/// <summary>");

            // method attributes
            foreach (var attribute in attributes)
            {
                AppendLineIndented($"[{attribute}]");
            }

            // create function signature part
            AppendLineIndented($"public void Test{testedFunctionInfo.FunctionName}{testCaseName}()");
            AppendLineIndented("{");
            IndentedLevelUp();

            // Arrange in test method
            AppendLineIndented("// Arrange");
            var mockObjNameAndType = new Dictionary<string, string>();

            // 暂时是这里有问题，mockObjNameAndType好像是空的
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
                        mockObjNameAndType[$"{(isField ? "field" : "arg")}_{name}"] = interfaceType;
                        /*
                        if (toObjType.TryGetValue(new Tuple<string, string>(interfaceType, name), out string objType))
                        {
                            mockObjNameAndType[$"{(isField ? "field" : "arg")}_{name}"] = objType;
                        }
                        */
                    }
                }
            }
            foreach (var item in mockObjNameAndType)
            {
                AppendLineIndented($"var {item.Key} = new Mock<{item.Value}> ();");
            }

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
                    AppendLineIndented($".Returns({this.ObjectToCSharpCode(func.Output.Value, func.Output.Type)});");
                }
            }

            // Create tested class instance by reflection
            // AppendLineIndented($"{testedFunctionInfo.BelongedClassName} instance = ({testedFunctionInfo.BelongedClassName})Activator.CreateInstance(testedType, true);");

            var constructorParams = node.ConstructorParameters.Select(item => item == "default" ? item : $"new Mock<{item}> ().Object");

            AppendLineIndented($"{testedFunctionInfo.BelongedClassName} instance = new {testedFunctionInfo.BelongedClassName}({string.Join(", ", constructorParams)});");
            AppendLineIndented($"Type testedType = typeof({testedFunctionInfo.BelongedClassName});");
            foreach (var fieldName in node.FieldsTypes.Keys)
            {
                AppendLineIndented($"FieldInfo fieldInfo_{fieldName} = testedType.GetField(\"{fieldName}\", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);");
                if (node.InterfaceTypeFields.Contains(fieldName))
                {
                    AppendLineIndented($"fieldInfo_{fieldName}.SetValue(instance, field_{fieldName}.Object);");
                }
                else
                {
                    AppendLineIndented($"fieldInfo_{fieldName}.SetValue(instance, {ObjectToCSharpCode(node.Fields[fieldName], node.FieldsTypes[fieldName])});");
                }
            }
            AppendLineIndented($"");
            // outputCode += ");";
            // IndentedLevelDown();
            AppendLineIndented();

            // Act in test method
            AppendLineIndented("// Act");
            AppendLineIndented("var jsonContent = String.Empty;");
            AppendLineIndented("try");
            AppendLineIndented("{");
            IndentedLevelUp();

            AppendLineIndented($"var actual = instance.{testedFunctionInfo.FunctionName}(");
            IndentedLevelUp();
            var funcInputParamsLen = testedFunctionInfo.InputParams.Count;
            for (var index = 0; index < funcInputParamsLen; index++)
            {
                var param = testedFunctionInfo.InputParams[index];

                if (node.InterfaceTypeInputs.Contains(param.Name))
                {
                    AppendLineIndented($"arg_{param.Name}.Object{(index != funcInputParamsLen - 1 ? "," : string.Empty)}");
                }
                else
                {
                    AppendLineIndented($"{this.ObjectToCSharpCode(param.Value, param.Type)}{(index != funcInputParamsLen - 1 ? "," : string.Empty)}");
                }
            }
            outputCode += ");";
            IndentedLevelDown();
            AppendLineIndented();

            if (testedFunctionInfo.Output.Type == "void")
            {
                GenerateFunctionBodyWithVoid();
            }
            else
            {
                GenerateFunctionBodyWithoutVoid();
            }

            // Save result to file
            GenerateOutputSavingCode();

            IndentedLevelDown();
            AppendLineIndented("}");

            return outputCode;
        }

        private void GenerateFunctionBodyWithVoid()
        {
            AppendLineIndented("jsonContent = \"NoException\";");
            IndentedLevelDown();
            AppendLineIndented("}");
            AppendLineIndented("catch");
            AppendLineIndented("{");
            IndentedLevelUp();
            AppendLineIndented("jsonContent = \"Exception\";");
            IndentedLevelDown();
            AppendLineIndented("}");
        }

        private void GenerateFunctionBodyWithoutVoid()
        {
            AppendLineIndented("jsonContent = JsonConvert.SerializeObject(actual);");
            IndentedLevelDown();
            AppendLineIndented("}");
            AppendLineIndented("catch");
            AppendLineIndented("{");
            IndentedLevelUp();
            AppendLineIndented("jsonContent = \"Exception\";");
            IndentedLevelDown();
            AppendLineIndented("}");
        }

        protected void GenerateMockedFuncParasAnyBlock(List<ObjectInfoWithName> paras)
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

        private void GenerateOutputSavingCode()
        {
            AppendLineIndented($"FileInfo fileInfo = new FileInfo(@\"{resultFilePath}\");");
            AppendLineIndented("if (!fileInfo.Directory.Exists)");
            AppendLineIndented("{");
            IndentedLevelUp();
            AppendLineIndented("fileInfo.Directory.Create();");
            IndentedLevelDown();
            AppendLineIndented("}");
            AppendLineIndented($"using (StreamWriter file = File.CreateText(@\"{resultFilePath}\"))");
            AppendLineIndented("{");
            IndentedLevelUp();
            AppendLineIndented("file.Write(jsonContent);");
            IndentedLevelDown();
            AppendLineIndented("}");
        }
    }
}
