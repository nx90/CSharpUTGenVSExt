using System;
using System.Collections.Generic;
using System.Linq;
using CSharpUnitTestGeneratorExt.Entity;

namespace CSharpUnitTestGeneratorExt.CodeGenerator
{
    public class RuledTestMethodGenerator : MethodGenerator
    {
        protected FunctionInfo testedFunctionInfo;
        protected string testCaseName;
        protected List<FunctionInfo> mockedFunctions;
        protected readonly FunctionCallNode node;

        public RuledTestMethodGenerator(int indentLevel, 
            string outputCode, 
            FunctionInfo testedFunctionInfo,
            string testCaseName,
            List<FunctionInfo> mockedFunctions,
            FunctionCallNode node) : base(indentLevel, outputCode)
        {
            this.testedFunctionInfo = testedFunctionInfo;
            this.testCaseName = testCaseName;
            this.mockedFunctions = mockedFunctions;
            this.node = node;
        }

        public override List<string> GetUsedNamespaces()
        {
            HashSet<string> namespaces = new HashSet<string>();
            foreach (var namespaceName in testedFunctionInfo.UsedNamespaces)
            {
                namespaces.Add($"using {namespaceName};");
            }
            foreach (var functionInfo in mockedFunctions)
            {
                foreach (var namespaceName in functionInfo.UsedNamespaces)
                {
                    namespaces.Add($"using {namespaceName};");
                }
            }
            return namespaces.ToList();
        }

        public override string GetOutputCodeBlock()
        {
            // method documention
            AppendLineIndented("/// <summary>");
            AppendLineIndented($"/// {this.testedFunctionInfo.FunctionName}_{this.testCaseName}");
            AppendLineIndented("/// <summary>");

            // method attributes
            foreach (var attribute in attributes)
            {
                AppendLineIndented($"[{attribute}]");
            }

            // create function signature part
            AppendLineIndented($"public void Test{this.testedFunctionInfo.FunctionName}In{this.testCaseName}()");
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
                        mockObjNameAndType[$"{(isField ? "field" : "arg")}_{name}"] = interfaceType;
                    }
                }
            }
            foreach (var item in mockObjNameAndType)
            {
                AppendLineIndented($"var {item.Key} = new Mock<{item.Value}> ();");
            }

            if (testedFunctionInfo.Output.Type != "void")
            {
                AppendLineIndented($"var expected = {this.ObjectToCSharpCode(this.testedFunctionInfo.Output.Value, this.testedFunctionInfo.Output.Type)};");
            }

            // Set up mocked functions
            foreach (var func in mockedFunctions)
            {
                bool isField = node.InterfaceFieldHashCodes.Keys.Contains(func.ObjHashCode);
                var hashcodeToName = isField ? node.InterfaceFieldHashCodes : node.InterfaceInputHashCodes;
                var nameToInterfaceType = isField ? node.FieldsTypes : node.InputTypes;
                var toObjType = isField ? node.InterfaceTypeFieldsRuntimeTypesMap : node.InterfaceTypeInputsRuntimeTypesMap;
                if (hashcodeToName.TryGetValue(func.ObjHashCode, out string name))
                {
                    AppendLineIndented($"{(isField ? "field" : "arg")}_{name}.Setup(x => x.{func.FunctionName}(");
                    GenerateMockedFuncParasBlock(func.InputParams);
                    outputCode += "))";
                    AppendLineIndented($".Returns({this.ObjectToCSharpCode(func.Output.Value, func.Output.Type)});");
                }
            }

            // Create tested class instance by reflection
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
            AppendLineIndented();

            // Act in test method
            AppendLineIndented("// Act");
            if (testedFunctionInfo.Output.Type == "void")
            {
                AppendLineIndented($"Action act = () => instance.{this.testedFunctionInfo.FunctionName}(");
            }
            else
            {
                AppendLineIndented($"var actual = instance.{this.testedFunctionInfo.FunctionName}(");
            }
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

            // Assert in test method
            AppendLineIndented("// Assert");
            switch (testedFunctionInfo.Output.Type)
            {
                case "void":
                    AppendLineIndented("act.Should().NotThrow();");
                    break;
                case "int":
                case "bool":
                case "Guid":
                    AppendLineIndented("actual.Should().Be(expected);");
                    break;
                default:
                    AppendLineIndented("actual.Should().BeEquivalentTo(expected);");
                    break;
            }
            IndentedLevelDown();
            AppendLineIndented("}");

            return outputCode;
        }

        protected void GenerateMockedFuncParasBlock(List<ObjectInfoWithName> paras)
        {
            IndentedLevelUp();
            int length = paras.Count;
            for (int index = 0; index < length; index++)
            {
                ObjectInfo param = paras[index];
                AppendLineIndented($"{ObjectToCSharpCode(param.Value, param.Type)}{(index != length - 1 ? "," : string.Empty)}");
            }
            IndentedLevelDown();
        }

        public string ObjectToCSharpCode(string obj, string objType)
        {
            if (obj == null)
            {
                return $"default({objType})";
            }
            else if (objType == "string")
            {
                // may be wrong here
                string objString = obj.Replace("\"", "\\\"");
                return $"JsonConvert.DeserializeObject<{objType}>(@\"[\"\"{objString}\"\"]\")[0]";
            }
            else
            {
                obj = obj.Replace("\"", "\"\"");
                return $"JsonConvert.DeserializeObject<{objType}>(@\"{obj}\")";
            }
        }
    }
}
