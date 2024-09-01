using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Threading;
using System.Diagnostics;
using VSIXHelloWorldProject.Entity;
using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.VisualStudio.TextTemplating;
using Microsoft.CodeAnalysis;
using System.Xml;
using VSIXHelloWorldProject.CodeGenerator;
using static System.Net.Mime.MediaTypeNames;
using VSIXHelloWorldProject.LLM;
using System.Text.RegularExpressions;
using System.Text;
using Microsoft.VisualStudio;
using System.Collections.Concurrent;

namespace VSIXHelloWorldProject
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class UTGenCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("c5ec4415-d494-46fd-b323-adca902d27e8");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly VSIXHelloWorldProjectPackage package;

        private string solutionDirectory;
        private string apiEndpoint;
        private string apiKey;
        private string deploymentName;
        private string copilotPlaygroundPath;
        private string outputCalcProjPath;
        private string outputCalcProjCsFilePath;
        private string outputCalcProjCsprojFilePath;
        private string unitTestProjPath;
        private string unitTestProjCsFilePath;
        private string unitTestProjCsprojFilePath;
        private string funcIORecFile;
        private AzureOpenAIClient azureOpenAIClient;
        private IVsOutputWindowPane generalPane = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="UTGenCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param Name="package">Owner package, not null.</param>
        /// <param Name="commandService">Command service to add command to, not null.</param>
        private UTGenCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.package = package as VSIXHelloWorldProjectPackage ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);

            var outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;

            Guid paneGuid = Guid.NewGuid();  // 这应该是一个固定的值，而不是每次都新生成  
            outputWindow.CreatePane(ref paneGuid, "UT Gen Extension Log Pane", 1, 1);
            outputWindow.GetPane(ref paneGuid, out generalPane);
            generalPane.Activate();
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static UTGenCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param Name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in UTGenCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new UTGenCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param Name="sender">Event sender.</param>
        /// <param Name="e">Event args.</param>
        private async void Execute(object sender, EventArgs e)
        {
            // 
            await ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                // GetAllBoundaryCasesInputList的prompt 是有问题的，input是一个list, 但是example的输出却是一个List而不是List<List>
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                solutionDirectory = Path.GetDirectoryName(package.dte.Solution.FullName);
                GetLLMServiceConfig();
                GetFolderAndFuncIORecFilePath();
                var methodCallRecord = GetTheNode();
                string funcImplString = GetTextInScope(methodCallRecord.CodeStartLine, methodCallRecord.CodeEndLine);
                FunctionInfo testedFunction = GetTestedFunc(methodCallRecord);
                List<FunctionInfo> mockedFunctions = GetMockedFunctions(methodCallRecord);
                List<string> projectsInSln = GetProjCsprojFilesPath();
                XmlDocument csprojFileContent = GetSourceProjCsprojContent();

                RuledTestMethodGenerator normalCaseTestMethodGenerator = new RuledTestMethodGenerator(
                    0, string.Empty, testedFunction, "NormalCase", mockedFunctions, methodCallRecord
                );
                var testFramework = TestFrameworks.Get(TestFrameworks.VisualStudioName);
                var mockFramework = MockFrameworks.Get(MockFrameworks.MoqName);

                unitTestProjPath = Path.Combine(copilotPlaygroundPath, "UTProj");
                unitTestProjCsFilePath = Path.Combine(unitTestProjPath, $"{methodCallRecord.ClassName}.test.cs");
                unitTestProjCsprojFilePath = Path.Combine(unitTestProjPath, "unitTestDemo.csproj");

                generalPane.OutputStringThreadSafe("Basic Preparation for code genertion done.");

                List<int> outputNeedCalcCaseIndexs = new List<int>();
                List<string> boundaryCasesList = new List<string>();
                List<List<ObjectInfoWithName>> boundaryCasesInput = new List<List<ObjectInfoWithName>>();

                try
                {
                    await Task.Factory.StartNew(() =>
                    {
                        boundaryCasesList = GetBoundaryCasesList(funcImplString, mockedFunctions);
                    }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                }
                catch (Exception ex)
                {
                    generalPane.OutputStringThreadSafe($"\nBoundary cases list generation failed. \nError Msg: \n {ex.Message}");
                    return;
                }
                generalPane.OutputStringThreadSafe($"\nBoundaryCasesList generated: \n     {JsonConvert.SerializeObject(boundaryCasesList, Newtonsoft.Json.Formatting.Indented)}");
                generalPane.OutputStringThreadSafe("\nStart to generate Inputs.");

                try
                {
                    await Task.Factory.StartNew(() =>
                    {
                        List<ObjectInfoWithName> basicInputList = new List<ObjectInfoWithName>();

                        // 这里要改，应该用Json格式
                        foreach (var inputName in methodCallRecord.Input.Keys)
                        {
                            basicInputList.Add(new ObjectInfoWithName { Name = inputName, Type = methodCallRecord.InputTypes[inputName], Value = methodCallRecord.Input[inputName] });
                        }
                        // string basicInput = JsonConvert.SerializeObject(basicInputList);
                        boundaryCasesInput = GetAllBoundaryCasesInputList(funcImplString, basicInputList, boundaryCasesList);
                    }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                }
                catch (Exception ex)
                {
                    generalPane.OutputStringThreadSafe($"\nBoundary cases input generation failed. \nError Msg: \n {ex.Message}");
                    return;
                }
                generalPane.OutputStringThreadSafe($"\nBoundaryCasesInput generated");

                try
                {
                    await Task.Factory.StartNew(() =>
                    {
                        GenerateOutputCalcProjWithCase(funcImplString, mockedFunctions, testedFunction,
                        methodCallRecord, projectsInSln, csprojFileContent, testFramework,
                        mockFramework, boundaryCasesList, boundaryCasesInput, out outputNeedCalcCaseIndexs);
                    }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                }
                catch (Exception ex)
                {
                    generalPane.OutputStringThreadSafe($"\nBoundary cases output generation failed. \nError Msg: \n {ex.Message}");
                    return;
                }
                generalPane.OutputStringThreadSafe($"\nBoundaryCasesOutput generated");

                Dictionary<int, string> caseOutputDict = new Dictionary<int, string>();
                try
                {
                    await Task.Factory.StartNew(() =>
                    {
                        caseOutputDict = GetTestingFuncOutput(outputCalcProjPath, outputNeedCalcCaseIndexs);
                    }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                }
                catch (Exception ex)
                {
                    generalPane.OutputStringThreadSafe($"\nGet Boundary cases output failed. \nError Msg: \n {ex.Message}");
                    return;
                }
                generalPane.OutputStringThreadSafe($"\nBoundaryCasesOutput got in memory");

                try
                {
                    await Task.Factory.StartNew(() =>
                    {
                        GenerateUnitTestProj(boundaryCasesList, boundaryCasesInput, testedFunction,
                            caseOutputDict, mockedFunctions, methodCallRecord, projectsInSln, csprojFileContent,
                            testFramework, mockFramework, normalCaseTestMethodGenerator);
                    }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                }
                catch (Exception ex)
                {
                    generalPane.OutputStringThreadSafe($"\nUnit test project generation failed. \nError Msg: \n {ex.Message}");
                    return;
                }
                generalPane.OutputStringThreadSafe($"\nAll Done!");
            });
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param Name="sender">Event sender.</param>
        /// <param Name="e">Event args.</param>
        private void Execute_old_llm(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            solutionDirectory = Path.GetDirectoryName(package.dte.Solution.FullName);
            GetLLMServiceConfig();
            GetFolderAndFuncIORecFilePath();
            var methodCallRecord = GetTheNode();
            string funcImplString = GetTextInScope(methodCallRecord.CodeStartLine, methodCallRecord.CodeEndLine);
            FunctionInfo testedFunction = GetTestedFunc(methodCallRecord);
            List<FunctionInfo> mockedFunctions = GetMockedFunctions(methodCallRecord);
            List<string> projectsInSln = GetProjCsprojFilesPath();
            XmlDocument csprojFileContent = GetSourceProjCsprojContent();

            RuledTestMethodGenerator normalCaseTestMethodGenerator = new RuledTestMethodGenerator(
                0, string.Empty, testedFunction, "NormalCase", mockedFunctions, methodCallRecord
            );
            var testFramework = TestFrameworks.Get(TestFrameworks.VisualStudioName);
            var mockFramework = MockFrameworks.Get(MockFrameworks.MoqName);

            unitTestProjPath = Path.Combine(copilotPlaygroundPath, "UTProj");
            unitTestProjCsFilePath = Path.Combine(unitTestProjPath, $"{methodCallRecord.ClassName}.test.cs");
            unitTestProjCsprojFilePath = Path.Combine(unitTestProjPath, "unitTestDemo.csproj");

            generalPane.OutputStringThreadSafe("Basic Preparation for code genertion done.");

            var boundaryCasesList = GetBoundaryCasesList(funcImplString, mockedFunctions);
            generalPane.OutputStringThreadSafe($"boundaryCasesList generated: \n {JsonConvert.SerializeObject(boundaryCasesList)}");
            generalPane.OutputStringThreadSafe("Start to generate code.");

            GenerateUnitTestProjWithLLM(boundaryCasesList, methodCallRecord, projectsInSln, csprojFileContent,
                testFramework, mockFramework, normalCaseTestMethodGenerator);

            generalPane.OutputStringThreadSafe("All done.");
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param Name="sender">Event sender.</param>
        /// <param Name="e">Event args.</param>
        private async void Execute_new_llm(object sender, EventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                //var x = new AzureOpenAIClient("https://gpt4testyingchaolv.openai.azure.com/", "6a443a2cd3404d5a8c71e6839bb77dd3", "gpt-4");
                //var y = x.GetSimpleChatCompletions("Hi");

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                solutionDirectory = Path.GetDirectoryName(package.dte.Solution.FullName);
                GetLLMServiceConfig();
                GetFolderAndFuncIORecFilePath();
                var methodCallRecord = GetTheNode();
                string funcImplString = GetTextInScope(methodCallRecord.CodeStartLine, methodCallRecord.CodeEndLine);
                FunctionInfo testedFunction = GetTestedFunc(methodCallRecord);
                List<FunctionInfo> mockedFunctions = GetMockedFunctions(methodCallRecord);
                List<string> projectsInSln = GetProjCsprojFilesPath();
                XmlDocument csprojFileContent = GetSourceProjCsprojContent();

                RuledTestMethodGenerator normalCaseTestMethodGenerator = new RuledTestMethodGenerator(
                    0, string.Empty, testedFunction, "NormalCase", mockedFunctions, methodCallRecord
                );
                var testFramework = TestFrameworks.Get(TestFrameworks.VisualStudioName);
                var mockFramework = MockFrameworks.Get(MockFrameworks.MoqName);

                unitTestProjPath = Path.Combine(copilotPlaygroundPath, "UTProj");
                unitTestProjCsFilePath = Path.Combine(unitTestProjPath, $"{methodCallRecord.ClassName}.test.cs");
                unitTestProjCsprojFilePath = Path.Combine(unitTestProjPath, "unitTestDemo.csproj");

                generalPane.OutputStringThreadSafe("Basic Preparation for code genertion done.");

                List<string> boundaryCasesList = new List<string>();
                await Task.Factory.StartNew(() =>
                {
                    boundaryCasesList = GetBoundaryCasesList(funcImplString, mockedFunctions);
                }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);

                generalPane.OutputStringThreadSafe($"\nBoundaryCasesList generated: \n     {JsonConvert.SerializeObject(boundaryCasesList)}");
                generalPane.OutputStringThreadSafe("\nStart to generate code.");

                await Task.Factory.StartNew(() =>
                {
                    GenerateUnitTestProjWithLLM(boundaryCasesList, methodCallRecord, projectsInSln, csprojFileContent,
                        testFramework, mockFramework, normalCaseTestMethodGenerator);
                }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);

                generalPane.OutputStringThreadSafe("\nAll done.");
            });
        }

        private void GenerateUnitTestProjWithLLM(List<string> boundaryCasesList, FunctionCallNode methodCallRecord,
            List<string> projectsInSln, XmlDocument sourceCsprojFile,
            TestFramework testFramework, MockFramework mockFramework,
            RuledTestMethodGenerator normalCaseTestMethodGenerator)
        {
            var boundaryCaseTestMethodGenerators = new List<LLMBoundaryCaseTestMethodGenerator>();
            var length = boundaryCasesList.Count;
            for (var index = 0; index < length; index++)
            {
                var testCaseName = index.ToString();
                var caseDescription = boundaryCasesList[index];
                var boundaryCaseTestMethodGenerator = new LLMBoundaryCaseTestMethodGenerator(0, "",
                    caseDescription, testCaseName, azureOpenAIClient);
                boundaryCaseTestMethodGenerators.Add(boundaryCaseTestMethodGenerator);
            }

            var testFileGenerator = new LLMTestFileGenerator(testFramework, mockFramework,
                methodCallRecord.ClassName, normalCaseTestMethodGenerator, boundaryCaseTestMethodGenerators);
            var code = testFileGenerator.GetOutputCodeBlock();

            var csprojFileGenerator = new CsprojFileGenerator(unitTestProjCsprojFilePath, sourceCsprojFile, projectsInSln);
            var csprojFileContent = csprojFileGenerator.GetOutputCodeBlock();



            // Create the new test class file  
            if (!Directory.Exists(unitTestProjPath))
            {
                Directory.CreateDirectory(unitTestProjPath);
            }

            File.WriteAllText(unitTestProjCsFilePath, code, Encoding.UTF8);

            // Create the csproj file  
            File.WriteAllText(unitTestProjCsprojFilePath, csprojFileContent, Encoding.UTF8);
            Utils.Utils.ExecuteDotnetTestCommand(unitTestProjCsprojFilePath, unitTestProjPath);
        }

        private void GenerateUnitTestProj(List<string> boundaryCasesList, List<List<ObjectInfoWithName>> boundaryCasesInput,
            FunctionInfo testedFunction, Dictionary<int, string> caseOutputDict,
            List<FunctionInfo> mockedFunctions, FunctionCallNode methodCallRecord,
            List<string> projectsInSln, XmlDocument sourceCsprojFile,
            TestFramework testFramework, MockFramework mockFramework,
            RuledTestMethodGenerator normalCaseTestMethodGenerator)
        {
            // Clear project generated last time
            if (Directory.Exists(unitTestProjPath))
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(unitTestProjPath);
                directoryInfo.Delete(true);
            }

            var boundaryCaseTestMethodGenerators = new List<BoundaryCaseTestMethodGenerator>();
            var length = Math.Min(boundaryCasesList.Count, boundaryCasesInput.Count);
            for (var index = 0; index < length; index++)
            {
                var testCaseName = index.ToString();
                var functionInput = boundaryCasesInput[index];
                var caseDescription = boundaryCasesList[index];
                var testedFunctionCopy = testedFunction.ShallowCopy();
                var inputParamsCount = testedFunctionCopy.InputParams.Count;
                for (var inputIndex = 0; inputIndex < inputParamsCount; inputIndex++ )
                {
                    testedFunctionCopy.InputParams[inputIndex].Value = functionInput[inputIndex].Value;
                }

                if (caseOutputDict.TryGetValue(index, out var newOutput))
                {
                    testedFunctionCopy.Output.Value = newOutput;

                }
                else
                {
                    caseDescription = Regex.Replace(caseDescription, @"^// \[noException\]", "// [exception]");
                }
                var boundaryCaseTestMethodGenerator = new BoundaryCaseTestMethodGenerator(0, "",
                    testedFunctionCopy, testCaseName, mockedFunctions, methodCallRecord, caseDescription);
                boundaryCaseTestMethodGenerators.Add(boundaryCaseTestMethodGenerator);
            }
            var allTestMethodGenerators = new List<MethodGenerator> { normalCaseTestMethodGenerator };
            allTestMethodGenerators = allTestMethodGenerators.Concat(boundaryCaseTestMethodGenerators).ToList();

            var testFileGenerator = new TestFileGenerator(testFramework, mockFramework,
                methodCallRecord.ClassName, allTestMethodGenerators);
            var code = testFileGenerator.GetOutputCodeBlock();

            var csprojFileGenerator = new CsprojFileGenerator(unitTestProjCsprojFilePath, sourceCsprojFile, projectsInSln);
            var csprojFileContent = csprojFileGenerator.GetOutputCodeBlock();

            // Create the new test class file  
            if (!Directory.Exists(unitTestProjPath))
            {
                Directory.CreateDirectory(unitTestProjPath);
            }

            File.WriteAllText(unitTestProjCsFilePath, code, Encoding.UTF8);

            // Create the csproj file  
            File.WriteAllText(unitTestProjCsprojFilePath, csprojFileContent, Encoding.UTF8);
            Utils.Utils.ExecuteDotnetTestCommand(unitTestProjCsprojFilePath, unitTestProjPath);
        }

        private List<List<ObjectInfoWithName>> GetAllBoundaryCasesInputList(string funcImplString, List<ObjectInfoWithName> basicInputList, List<string> boundaryCases)
        {
            string basicInput = JsonConvert.SerializeObject(basicInputList);

            ConcurrentDictionary<int, List<ObjectInfoWithName>> resultDict = new ConcurrentDictionary<int, List<ObjectInfoWithName>>();

            Parallel.ForEach(boundaryCases.Select((value, index) => (value, index)), pair =>
            {
                Console.WriteLine($"Index: {pair.index}, Value: {pair.value}");
                string boundaryCasesListQuestion = $@"
Give me input parameters of the testing function for the boundary case shows below based on an example input which haved tested for normal case testing.
Note that your responce will be treat as Json content directly, so do not contains any comments or text other than the json file content.
Do not start with markdown grammer like '```json', consider your responce as raw json file content.

tested function body:
\`\`\`
{funcImplString}
\`\`\`

normal case input parameters of the function(all parameters are valid):
\`\`\`
{basicInput}
\`\`\`

You need to generate input parameters of the function to test boundary case below, it is started with three tags quoted by square brackets like [noException], [mockFuncUnRelated] [SomethingLikeATestMethodName], please ingnore all of them, they are just for marking:
\`\`\`
{pair.value}
\`\`\`

you can just use normal case input as function input if the boundary case has [mockFuncThrowException] tag,
output should be JSON formatted(NO COMMENTS SHOULD EXIST IN JSON FILE), 
correct output format example below, this example contains 3 parameters:
\`\`\`Json
[
    {{
        ""Name"": ""arg1"",
        ""Type"": ""int"",
        ""Value"": ""1""
    }},
    {{
        ""Name"": ""arg2"",
        ""Type"": ""string"",
        ""Value"": ""\""xx\""""
    }},
    {{
        ""Name"": ""arg3"",
        ""Type"": ""Class1"",
        ""Value"": ""{{
                \""field1\"": \""yy\""
            }}""
    }},
]
\`\`\`

**Clear all comments in the json file before submitting it.**
";
                var boundaryCasesInputsChoices = azureOpenAIClient.GetSimpleChatCompletions(boundaryCasesListQuestion);
                var casesInput = basicInputList;

                foreach (var choice in boundaryCasesInputsChoices)
                {
                    try
                    {
                        casesInput = JsonConvert.DeserializeObject<List<ObjectInfoWithName>>(choice);
                        break;
                    }
                    catch
                    {
                        continue;
                    }
                }

                resultDict.TryAdd(pair.index, casesInput);

            });

            List<List<ObjectInfoWithName>> result = Enumerable.Repeat(basicInputList, boundaryCases.Count).ToList();

            foreach (var item in resultDict)
            {
                result[item.Key] = item.Value;
            }

            return result;
        }

        private List<ObjectInfoWithName> GetBoundaryCasesInputList(string funcImplString, string basicInput, string boundaryCases)
        {
            string boundaryCasesListQuestion = $@"
Give me a list of inputs of testing function signature for each boundary cases shows below based on an example input which haved tested for normal case testing and testing function's signature.
Note that your responce will be treat as Json content directly, so do not contains any comments or text other than the json file content.
Do not start with markdown grammer like '```json', consider your responce as raw json file content.

testing function implementation:
\`\`\`
{funcImplString}
\`\`\`

normal case input of the function(all parameters are valid):
\`\`\`
{basicInput}
\`\`\`

testing boundary cases list below, there are three tags quoted by square brackets like [noException], [mockFuncUnRelated] [SomethingLikeATestMethodName] in each testing boundary case, please ingnore all of them, they are just for marking:
\`\`\`
[{string.Join(",\n ", boundaryCases)}]
\`\`\`

you can just use normal case input as function input in boundary case which has [mockFuncThrowException] tag,
output should be JSON formatted(NO COMMENTS SHOULD EXIST IN JSON FILE), 
correct output format example below, this example contains 3 groups of boundary case inputs, each group contains 2 parameters, and each group MUST be a list of values:
\`\`\`Json
[
    {{
        ""Name"": ""arg1"",
        ""Type"": ""int"",
        ""Value"": ""1""
    }},
    {{
        ""Name"": ""arg2"",
        ""Type"": ""string"",
        ""Value"": ""\""xx\""""
    }},
    {{
        ""Name"": ""arg3"",
        ""Type"": ""Class1"",
        ""Value"": ""{{
                \""field1\"": \""yy\""
            }}""
    }},
]
\`\`\`

**Never set a group of boundary case inputs as null or empty list.**
**Clear all comments in the json file before submitting it.**
";
            var boundaryCasesInputsChoices = azureOpenAIClient.GetSimpleChatCompletions(boundaryCasesListQuestion);
            var casesInputsList = new List<ObjectInfoWithName> ();

            foreach(var choice in boundaryCasesInputsChoices)
            {
                try
                {
                    casesInputsList = JsonConvert.DeserializeObject<List<ObjectInfoWithName>>(choice);
                    break;
                }
                catch
                {
                    continue;
                }
            }
            return casesInputsList;
        }

        private List<string> GetBoundaryCasesList(string funcImplString, List<FunctionInfo> mockedFunctions)
        {
            string boundaryCasesListQuestion = $@"List minimum boundary test cases(not code just cases) for the following function implementation below to achieve the goal:
			Goal:
			```
			1. Generate boundary test cases as LESS as better to cover code lines in given function implementation.
            2. Your answer should not contains markdown grammar like '```' 
            3. Your answer should strictly formatted as example shows and never give additional notes.
            4. No need to contains normal case in your answer, I don't need the normal case or basic case.
            5. Generate empty ouput or only 1 or 2 boundary test cases as your answer when function implementation is short or simple or less than 5 lines.
            6. Never generate more than 5 boundary test cases, your answer should contains boundary test cases as LESS as better.
			```

			Some rules about your answer:
			```
			1. Mark the case as '[exception]' at the start of case if there should be an exception thrown in the case, mark as '[noException]' if not.
			2. Then mark [mockFuncThrowException] if the case is about how mocked function throw exception, mark it as [mockFuncUnRelated] if the case is not related with any mocked function, drop the case if the case is related with a mocked function and the mock function don't throw any exception.
			3. Mark test method name for the case like [FooHigherThanBar], test method name should be understandable for human to know what case you test in it. each test method name must be unique, one case should have different Short test function name with the other cases.
            4. Finally give us the description of the test case.
			5. Do NOT check null value for any parameter in the case, just check the value is valid or not.
			```

			According the rules, your answer format should be like this:
			```
			[noException] [mockFuncUnRelated] [ShortTestFunctionNameForCase1] 1. boundary test case1 description.
			[noException] [mockFuncUnRelated] [ShortTestFunctionNameForCase2] 2. boundary test case2 description.
			[noException] [mockFuncUnRelated] [ShortTestFunctionNameForCase3] 3. boundary test case3 description.
			[exception] [mockFuncThrowException] [ShortTestFunctionNameForCase4] 4. boundary test case4 description.
			...
			```

			Your answer example:
			```
            [noException] [mockFuncUnRelated] [FooHigherThanBar] 1. this case hit xx code branch.
			[noException] [mockFuncUnRelated] [SecondParamIsValue1] 2. second parameter is value1.
			[exception] [mockFuncUnRelated] [FirstParamInvalid] 3. first parameter is invalid.
			[exception] [mockFuncThrowException] [MockFunction1Exception] 4. mocked function function1 throw exception.
			...
			```

			mocked function names:
			```
			{JsonConvert.SerializeObject(mockedFunctions.Select(func => func.FunctionName).ToList())}
			```

			Function Implementation:
			```
			{funcImplString}
			```
";

            var boundaryCasesChoices = azureOpenAIClient.GetSimpleChatCompletions(boundaryCasesListQuestion);
            List<string> boundaryCasesList = new List<string>();

            foreach (var choice in boundaryCasesChoices)
            {
                try
                {
                    boundaryCasesList = choice.Split('\n').ToList();
                    foreach (var boundaryCase in boundaryCasesList)
                    {
                        var flagArray = Regex.Matches(boundaryCase, @"(?<=\[)(\S+)(?=\])");
                        if (flagArray.Count < 3)
                        {
                            throw new Exception("Invalid boundary case flag array");
                        }
                    }
                    break;
                }
                catch
                {
                    continue;
                }
            }
            return boundaryCasesList;
        }

        private void GenerateOutputCalcProjWithCase(string funcImplString, List<FunctionInfo> mockedFunctions,
            FunctionInfo testedFunction, FunctionCallNode methodCallRecord, List<string> projectsInSln,
            XmlDocument sourceCsprojFile, TestFramework testFramework, MockFramework mockFramework,
            List<string> boundaryCasesList, List<List<ObjectInfoWithName>> boundaryCasesInput, out List<int> outputNeedCalcCaseIndexs)
        {
            // Clear project generated last time
            if (Directory.Exists(outputCalcProjPath))
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(outputCalcProjPath);
                directoryInfo.Delete(true);
            }

            outputNeedCalcCaseIndexs = new List<int>();
            var boundaryCasesCount = boundaryCasesList.Count;
            for (int index = 0; index < boundaryCasesCount; index++)
            {
                var boundaryCase = boundaryCasesList[index];
                var flagArray = Regex.Matches(boundaryCase, @"(?<=\[)(\S+)(?=\])");
                if (flagArray.Count < 3)
                {
                    throw new Exception("Invalid boundary case flag array");
                }
                if (flagArray[0].Value == "noException")
                {
                    outputNeedCalcCaseIndexs.Add(index);
                }
            }
            var testingFuncOutputCalcMethodGenerators = new List<MethodGenerator>();
            try
            {
                foreach (var index in outputNeedCalcCaseIndexs)
                {
                    var outputJsonFilePath = Path.Combine(outputCalcProjPath, "outputJsons", $"{index}.json");
                    var functionInput = boundaryCasesInput[index];
                    var testCaseName = index.ToString();
                    var testedFunctionCopy = testedFunction.ShallowCopy();
                    var inputParamsCount = testedFunctionCopy.InputParams.Count;
                    for (var inputIndex = 0; inputIndex < inputParamsCount; inputIndex++)
                    {
                        testedFunctionCopy.InputParams[inputIndex].Value = functionInput[inputIndex].Value;
                    }

                    var testingFuncOutputCalcMethodGenerator = new TestingFuncOutputCalcMethodGenerator(
                        0, "", testedFunctionCopy, testCaseName, mockedFunctions, methodCallRecord, outputJsonFilePath
                    );
                    testingFuncOutputCalcMethodGenerators.Add(testingFuncOutputCalcMethodGenerator);
                }

                var testFileGenerator = new TestFileGenerator(testFramework, mockFramework,
                    methodCallRecord.ClassName,
                    testingFuncOutputCalcMethodGenerators);

                // 现在调试到这里了
                var code = testFileGenerator.GetOutputCodeBlock();
                var csprojFileGenerator = new CsprojFileGenerator(outputCalcProjCsprojFilePath, sourceCsprojFile, projectsInSln);
                var csprojFileContent = csprojFileGenerator.GetOutputCodeBlock();
                // Create the new test class file  
                if (!Directory.Exists(outputCalcProjPath))
                {
                    Directory.CreateDirectory(outputCalcProjPath);
                }

                File.WriteAllText(outputCalcProjCsFilePath, code, Encoding.UTF8);

                // Create the csproj file  
                File.WriteAllText(outputCalcProjCsprojFilePath, csprojFileContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine("GenerateOutputCalcProj failed", ex.ToString());
            }

            Utils.Utils.ExecuteDotnetTestCommand(outputCalcProjCsprojFilePath, solutionDirectory);
        }

        private void GenerateOutputCalcProj(string funcImplString, List<FunctionInfo> mockedFunctions,
            FunctionInfo testedFunction, FunctionCallNode methodCallRecord, List<string> projectsInSln,
            XmlDocument sourceCsprojFile, TestFramework testFramework, MockFramework mockFramework,
            out List<int> outputNeedCalcCaseIndexs, out List<string> boundaryCasesList,
            out List<List<ObjectInfoWithName>> boundaryCasesInput)
        {
            List<ObjectInfoWithName> basicInputList = new List<ObjectInfoWithName>();

            // 这里要改，应该用Json格式
            foreach (var inputName in methodCallRecord.Input.Keys)
            {
                basicInputList.Add(new ObjectInfoWithName { Name=inputName, Type= methodCallRecord.InputTypes[inputName], Value= methodCallRecord.Input[inputName] });
            }
            // string basicInput = JsonConvert.SerializeObject(basicInputList);
            outputNeedCalcCaseIndexs = new List<int> ();
            boundaryCasesList = GetBoundaryCasesList(funcImplString, mockedFunctions);
            boundaryCasesInput = GetAllBoundaryCasesInputList(funcImplString, basicInputList, boundaryCasesList);
            var boundaryCasesCount = boundaryCasesList.Count;
            for (int index = 0; index < boundaryCasesCount; index++)
            {
                var boundaryCase = boundaryCasesList[index];
                var flagArray = Regex.Matches(boundaryCase, @"(?<=\[)(\S+)(?=\])");
                if (flagArray.Count < 3)
                {
                    throw new Exception("Invalid boundary case flag array");
                }
                if (flagArray[0].Value == "noException")
                {
                    outputNeedCalcCaseIndexs.Add(index);
                }
            }
            var testingFuncOutputCalcMethodGenerators = new List<MethodGenerator>();
            try
            {
                foreach (var index in outputNeedCalcCaseIndexs)
                {
                    var outputJsonFilePath = Path.Combine(outputCalcProjPath, "outputJsons", $"{index}.json");
                    var functionInput = boundaryCasesInput[index];
                    var testCaseName = index.ToString();
                    var testedFunctionCopy = testedFunction.ShallowCopy();
                    var inputParamsCount = testedFunctionCopy.InputParams.Count;
                    for (var inputIndex = 0; inputIndex < inputParamsCount; inputIndex++)
                    {
                        testedFunctionCopy.InputParams[inputIndex].Value = functionInput[inputIndex].Value;
                    }

                    var testingFuncOutputCalcMethodGenerator = new TestingFuncOutputCalcMethodGenerator(
                        0, "", testedFunction, testCaseName, mockedFunctions, methodCallRecord, outputJsonFilePath
                    );
                    testingFuncOutputCalcMethodGenerators.Add(testingFuncOutputCalcMethodGenerator);
                }

                var testFileGenerator = new TestFileGenerator(testFramework, mockFramework,
                    methodCallRecord.ClassName,
                    testingFuncOutputCalcMethodGenerators);

                // 现在调试到这里了
                var code = testFileGenerator.GetOutputCodeBlock();
                var csprojFileGenerator = new CsprojFileGenerator(outputCalcProjCsprojFilePath, sourceCsprojFile, projectsInSln);
                var csprojFileContent = csprojFileGenerator.GetOutputCodeBlock();
                // Create the new test class file  
                if (!Directory.Exists(outputCalcProjPath))
                {
                    Directory.CreateDirectory(outputCalcProjPath);
                }

                File.WriteAllText(outputCalcProjCsFilePath, code, Encoding.UTF8);

                // Create the csproj file  
                File.WriteAllText(outputCalcProjCsprojFilePath, csprojFileContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine("GenerateOutputCalcProj failed", ex.ToString());
            }

            Utils.Utils.ExecuteDotnetTestCommand(outputCalcProjCsprojFilePath, solutionDirectory);
        }

        private XmlDocument GetSourceProjCsprojContent()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            EnvDTE.Document activeDocument = package.dte.ActiveDocument;
            if (activeDocument == null)
            {
                throw new InvalidOperationException("No active document detected");
            }

            ProjectItem projectItem = activeDocument.ProjectItem;
            if (projectItem == null)
            {
                throw new InvalidOperationException("No active project item detected");
            }

            EnvDTE.Project project = projectItem.ContainingProject;
            if (project == null)
            {
                throw new InvalidOperationException("No active project detected");
            }

            string projectFilePath = project.FullName;

            if (!string.IsNullOrEmpty(projectFilePath) && File.Exists(projectFilePath) && Path.GetExtension(projectFilePath).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                // 读取.csproj文件  
                XmlDocument csprojDocument = new XmlDocument();
                csprojDocument.Load(projectFilePath);
                return csprojDocument;
            }
            else
            {
                throw new InvalidOperationException("Can not load csproj file as xml.");
            }
        }

        private List<string> GetProjCsprojFilesPath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            EnvDTE.Solution solution = package.dte.Solution;
            if (solution == null || !solution.IsOpen)
                throw new InvalidOperationException("No active solution detected");
            var result = solution.Projects.Cast<EnvDTE.Project>().Select(project => project.FileName).ToList();

            return result;
        }

        private List<FunctionInfo> GetMockedFunctions(FunctionCallNode methodCallRecord)
        {
            var result = new List<FunctionInfo>();
            foreach(var child in methodCallRecord.Children)
            {
                List<ObjectInfoWithName> functionInputParams = new List<ObjectInfoWithName>();
                foreach (var name in child.Input.Keys)
                {
                    functionInputParams.Add(new ObjectInfoWithName { Name = name, Type = child.InputTypes[name], Value = child.Input[name] });
                }
                ObjectInfo output = new ObjectInfo { Type = child.OutputType, Value = child.Output };
                result.Add(new FunctionInfo
                {
                    ObjHashCode = child.ObjHashCode,
                    FunctionName = child.MethodName,
                    BelongedClassName = child.ClassName,
                    InputParams = functionInputParams,
                    Output = output,
                    UsedNamespaces = child.NamespaceName
                });
            }
            return result;
        }

        private FunctionInfo GetTestedFunc(FunctionCallNode methodCallRecord)
        {
            List<ObjectInfoWithName> functionInputParams = new List<ObjectInfoWithName>();
            foreach (var name in methodCallRecord.Input.Keys)
            {
                functionInputParams.Add(new ObjectInfoWithName { Name = name, Type = methodCallRecord.InputTypes[name], Value = methodCallRecord.Input[name] });
            }
            ObjectInfo output = new ObjectInfo { Type = methodCallRecord.OutputType, Value = methodCallRecord.Output };
            return new FunctionInfo
            {
                FunctionName = methodCallRecord.MethodName,
                BelongedClassName = methodCallRecord.ClassName,
                InputParams = functionInputParams,
                Output = output,
                UsedNamespaces = methodCallRecord.NamespaceName
            };
        }

        private void GetLLMServiceConfig()
        {
            apiEndpoint = package.page.ApiEndpoint;
            apiKey = package.page.ApiKey;
            deploymentName = package.page.DeploymentName;
            if (String.IsNullOrEmpty(apiEndpoint) || String.IsNullOrEmpty(apiKey) || String.IsNullOrEmpty(deploymentName))
            {
                // Show a message box to prove we were here
                VsShellUtilities.ShowMessageBox(
                    this.package,
                    "apiEndpoint or apiKey or deploymentName is null or empty please check it in Tools->Options->UTGenSettings",
                    "UTGen Error!",
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                throw new Exception("Invalid configuration.");
            }
            azureOpenAIClient = new AzureOpenAIClient(apiEndpoint, apiKey, deploymentName);
        }

        private void GetFolderAndFuncIORecFilePath()
        {
            copilotPlaygroundPath = Path.Combine(solutionDirectory, ".CSharpUTGen");
            outputCalcProjPath = Path.Combine(copilotPlaygroundPath, ".CSharpUTGenOutputCalcProj");
            outputCalcProjCsFilePath = Path.Combine(outputCalcProjPath, "outputCalc.cs");
            outputCalcProjCsprojFilePath = Path.Combine(outputCalcProjPath, "outputCalcProj.csproj");

            var funcIORecFiles = Directory.GetFiles(copilotPlaygroundPath, "funcIORec.json", SearchOption.AllDirectories).ToArray();

            if (funcIORecFiles.Length == 0)
            {
                VsShellUtilities.ShowMessageBox(
                    package.dte as System.IServiceProvider,
                    "Cannot find the funcIORec.json file in the Visual Studio solution, please make sure you have recorded the C# function call using our C# utils.",
                    "File Not Found",
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
            else if (funcIORecFiles.Length > 1)
            {
                VsShellUtilities.ShowMessageBox(
                    package.dte as System.IServiceProvider,
                    "Found more than one funcIORec.json file in the Visual Studio solution, please navigate to the C# project which needs to generate UT code this time or just delete other funcIORec.json files.",
                    "Multiple Files Found",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
            funcIORecFile = funcIORecFiles[0];
        }

        private FunctionCallNode GetTheNode()
        {
            //await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = package.dte;
            EnvDTE.Document activeDocument = dte.ActiveDocument;
            TextSelection textSelection = (TextSelection)dte.ActiveDocument.Selection;
            int currentLine = textSelection.ActivePoint.Line;
            string fileName = activeDocument.FullName;

            string content = File.ReadAllText(funcIORecFile);
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new JsonTupleConverter());
            List<FunctionCallNode> nodes = JsonConvert.DeserializeObject<List<FunctionCallNode>>(content, settings);
            FunctionCallNode node = FunctionCallTreeDFSAndFindFirstMatch(nodes, currentLine, fileName);
            if (node == null)
            {
                VsShellUtilities.ShowMessageBox(
                    package.dte as System.IServiceProvider,
                    "Cannot find the funcIORec.json file in the Visual Studio solution, please make sure you have recorded the C# function call using our C# utils.",
                    "File Not Found",
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                throw new Exception("Cannot find the funcIORec.json file in the Visual Studio solution, please make sure you have recorded the C# function call using our C# utils.");
            }
            return node;
        }

        public Dictionary<int, string> GetTestingFuncOutput(string outputCalcProjPath, IEnumerable<int> outputNeedCalcCaseIndexes)
        {
            var caseOutputDict = new Dictionary<int, string>();

            foreach (var index in outputNeedCalcCaseIndexes)
            {
                string outputJsonFilePath = Path.Combine(outputCalcProjPath, "outputJsons", $"{index}.json");

                try
                {
                    string outputJson = File.ReadAllText(outputJsonFilePath, Encoding.UTF8);

                    if (outputJson.StartsWith("Exception"))
                    {
                        caseOutputDict[index] = null;
                    }
                    else if (outputJson.StartsWith("NoException"))
                    {
                        caseOutputDict[index] = string.Empty;
                    }
                    else
                    {
                        caseOutputDict[index] = outputJson;
                    }
                }
                catch (Exception ex)
                {
                    // Handle any exceptions that occur during file read or JSON deserialization
                    // For example, log the error and set the dictionary value to null
                    caseOutputDict[index] = null;
                    // Log the exception (ex) here
                }
            }

            return caseOutputDict;
        }

        private FunctionCallNode FunctionCallTreeDFSAndFindFirstMatch(List<FunctionCallNode> nodes, int currentLine, string fileName)
        {
            foreach(var node in nodes)
            {
                if (node.CodeFileName == fileName && node.CodeStartLine <= currentLine && node.CodeEndLine >= currentLine)
                {
                    return node;
                }
                else
                {
                    var result = FunctionCallTreeDFSAndFindFirstMatch(node.Children.ToList(), currentLine, fileName);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }
            return null;
        }

        private string GetTextInScope(int startLine,int endLine)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            // 获取当前活动的文档  
            EnvDTE.Document activeDocument = package.dte.ActiveDocument;
            if (activeDocument == null)
                return null;

            // 获取TextDocument对象  
            EnvDTE.TextDocument textDoc = (EnvDTE.TextDocument)activeDocument.Object("TextDocument");
            if (textDoc == null)
                return null;

            // 获取TextSelection对象  
            TextSelection selection = textDoc.Selection;

            // 设置选区为指定的行范围  
            selection.StartOfDocument(false);
            selection.MoveToLineAndOffset(startLine, 1, false);
            selection.MoveToLineAndOffset(endLine + 1, 1, true);

            // 获取选区内的文本  
            string textBetweenLines = selection.Text;

            // 可能需要重置选区  
            selection.StartOfDocument(false);

            return textBetweenLines;
        }

        private async Task<IWpfTextView> GetActiveTextViewAsync()
        {
            var textManager = await package.GetServiceAsync(typeof(SVsTextManager)) as IVsTextManager;
            textManager.GetActiveView(1, null, out IVsTextView vTextView);

            IVsEditorAdaptersFactoryService editorAdapter =
                await package.GetServiceAsync(typeof(IVsEditorAdaptersFactoryService)) as IVsEditorAdaptersFactoryService;

            return editorAdapter.GetWpfTextView(vTextView);
        }

        private async Task<SnapshotSpan?> GetMethodCodePositionRangeAsync()
        {
            var textView = await GetActiveTextViewAsync();

            var caretPosition = textView.Caret.Position.BufferPosition;

            /*
            // Assuming you have a way to get your Roslyn Document object  
            var document = GetRoslynDocument();

            // Now you can use Roslyn to analyze the document and find the method at the caret position  
            // This is a simplified example, and actual implementation will depend on your specific needs  
            var syntaxRoot = await document.GetSyntaxRootAsync();
            var methodNode = syntaxRoot.FindToken(caretPosition.Position).Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();

            if (methodNode != null)
            {
                var span = methodNode.Span;
                return new SnapshotSpan(viewHost.TextView.TextSnapshot, new Span(span.Start, span.Length));
            }
            */

            return null;
        }
    }
}
