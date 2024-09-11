using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text.RegularExpressions;
using CSharpUnitTestGeneratorExt.Entity;
using Task = System.Threading.Tasks.Task;
using EnvDTE80;
using Newtonsoft.Json.Linq;
using System.IO;
using Newtonsoft.Json;
using EnvDTE90a;
using CSharpUnitTestGeneratorExt.Utils;

namespace CSharpUnitTestGeneratorExt
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class RecordModeStartCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 256;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("ed44d629-7911-4aa2-9c27-d37520eed74e");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly VSIXHelloWorldProjectPackage package;

        private EnvDTE100.Debugger5 debugger;
        private string solutionDirectory;
        private IVsOutputWindowPane logPane = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="RecordModeStartCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param Name="package">Owner package, not null.</param>
        /// <param Name="commandService">Command service to add command to, not null.</param>
        private RecordModeStartCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.package = package as VSIXHelloWorldProjectPackage ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(this.Execute, menuCommandID);
            // menuItem.BeforeQueryStatus += new EventHandler(OnBeforeQueryStatus);
            commandService.AddCommand(menuItem);

            var outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            Guid paneGuid = Guid.NewGuid();  // 这应该是一个固定的值，而不是每次都新生成  
            outputWindow.CreatePane(ref paneGuid, "UT Gen Extension Log Pane", 1, 1);
            outputWindow.GetPane(ref paneGuid, out logPane);
            logPane.Activate();
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static RecordModeStartCommand Instance
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
            // Switch to the main thread - the call to AddCommand in RecordModeStartCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new RecordModeStartCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param Name="sender">Event sender.</param>
        /// <param Name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = package.dte;

            // Add debugger helper class into project before build.
            Document activeDocument = dte.ActiveDocument;
            string activeDocumentPath = activeDocument.Path;
            string debuggerHelperFileNamePrefix = Guid.NewGuid().ToString().Replace("-", "_");
            string debuggerHelperFilePath = $"{activeDocumentPath}\\{debuggerHelperFileNamePrefix}_DebuggerHelper.cs";
            AddFileToProject(debuggerHelperFilePath, ExtConstant.DebuggerHelperFileContent);

            debugger = (EnvDTE100.Debugger5)dte.Debugger;
            try
            {
                debugger.Go(true);
            }
            catch (Exception ex)
            {
                logPane.OutputStringThreadSafe($"Recording failed. Exception:\n {ex.Message}");
            }
            
            EnvDTE.StackFrame sf = debugger.CurrentStackFrame;

            // FunctionCallNode currentNode = new FunctionCallNode { CreationTime = DateTime.Now };
            string uniquePrefix = GetQualifiedPrefix(debugger);
            debugger.ExecuteStatement($"var {uniquePrefix}_assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyName(new System.Reflection.AssemblyName(\"Newtonsoft.Json\"));", -1, false);
            debugger.ExecuteStatement($"var {uniquePrefix}_type = {uniquePrefix}_assembly.GetType(\"Newtonsoft.Json.JsonConvert\");", -1, false);
            debugger.ExecuteStatement($"var {uniquePrefix}_serializeMethod = {uniquePrefix}_type.GetMethod(\"SerializeObject\", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public, new Type[] {{ typeof(object) }});", -1, false);
            debugger.ExecuteStatement($"var {uniquePrefix}_frame = new System.Diagnostics.StackFrame();", -1, false);
            // Before change, this is MethodBase
            debugger.ExecuteStatement($"System.Reflection.MethodInfo {uniquePrefix}_methodInfo;", -1, false);
            debugger.ExecuteStatement($"System.Object {uniquePrefix}_output;", -1, false);
            debugger.ExecuteStatement($"System.Object {uniquePrefix}_serializedInputOrThis;", -1, false);
            debugger.ExecuteStatement($"int {uniquePrefix}_objHashCode;", -1, false);
            // FunctionCallNode currentNode = new FunctionCallNode();
            FunctionCallNode currentNode = BuildCurrenctNode(uniquePrefix, null);

            solutionDirectory = Path.GetDirectoryName(dte.Solution.FullName);
            string copilotPlaygroundPath = Path.Combine(solutionDirectory, ".CSharpUTGen");
            WriteDownFuncIORecJson(currentNode, copilotPlaygroundPath, "funcIORec.json");
            debugger.TerminateAll();

            // Delete debugger helper class into project before build.
            DeleteFile(debuggerHelperFilePath);
        }

        private FunctionCallNode BuildCurrenctNode(string uniquePrefix, FunctionCallNode parentNode)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            StackFrame2 sf = debugger.CurrentStackFrame as StackFrame2;
            
            string funcName = sf.FunctionName;
            var userCode = sf.UserCode;

            if (!CheckIfNeedRecord(uniquePrefix, parentNode))
            {
                return null;
            }
            FunctionCallNode currentNode = new FunctionCallNode { CreationTime = DateTime.Now };
            // 被Mock的函数只记录一次输入输出，在UT文件里也只Mock一次行为，输出重算
            try
            {
                GetSerializedMethodInfo(currentNode, uniquePrefix);
                GetMethodCodePosition(currentNode);
                GetSerializedFields(currentNode);
                GetSerializedInputAndThis(currentNode, uniquePrefix);
                GetConstructorParams(currentNode, uniquePrefix);
                currentNode.NamespaceName = currentNode.NamespaceName.Distinct().ToList();
                StackFrame2 stackFrame = package.dte.Debugger.CurrentStackFrame as StackFrame2;
                currentNode.CodeFunctionNameFromDTE = stackFrame.FunctionName;
            }
            catch
            {
                return null;
            }
            while (true)
            {
                debugger.StepInto(true);
                var node = BuildCurrenctNode(uniquePrefix, currentNode);
                if (node != null)
                {
                    currentNode.Children.Add(node);
                }
                StackFrame2 stackFrameBeforeStepOut = debugger.CurrentStackFrame as StackFrame2;
                if (stackFrameBeforeStepOut.FunctionName != currentNode.CodeFunctionNameFromDTE
                    || stackFrameBeforeStepOut.LineNumber > currentNode.CodeEndLine
                    || stackFrameBeforeStepOut.LineNumber < currentNode.CodeStartLine)
                {
                    // this means the function is not in current function and need step out
                    debugger.StepOut(true);
                }

                StackFrame2 stackFrame = debugger.CurrentStackFrame as StackFrame2;
                if (stackFrame == null || stackFrame.LineNumber == currentNode.CodeEndLine)
                {
                    break;
                }
            }
            debugger.StepOut(true);
            GetSerializedOutput(currentNode, uniquePrefix);
            return currentNode;
        }

        private bool CheckIfNeedRecord(string uniquePrefix, FunctionCallNode parentNode)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (parentNode == null)
            {
                return true;
            }
            var exp = package.dte.Debugger.GetExpression("this");
            if (exp == null)
            {
                // this means the function is a lambda function or static function.
                return false;
            }
            string thisType = exp.Type.TrimStart('"').TrimEnd('"');
            return parentNode.InterfaceTypeFieldsRuntimeTypesMap.Values.Contains(thisType) || parentNode.InterfaceTypeInputsRuntimeTypesMap.Values.Contains(thisType);
        }
        
        private void GetSerializedOutput(FunctionCallNode currentNode, string uniquePrefix)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            debugger.ExecuteStatement($"{uniquePrefix}_output = {uniquePrefix}_serializeMethod.Invoke(null, new object[] {{ $returnvalue }});", -1, false);
            var jsonValueExp = debugger.GetExpression($"{uniquePrefix}_output");
            currentNode.Output = Regex.Unescape(jsonValueExp.Value).TrimStart('"').TrimEnd('"');

            var outputexp = debugger.GetExpression($"$returnvalue");
            if (outputexp.Type.Contains(' '))
            {
                var types = outputexp.Type.TrimStart('"').TrimEnd('"').Split(' ');
                currentNode.OutputType = types[1].TrimStart('{').TrimEnd('}');
            }
            else
            {
                currentNode.OutputType = outputexp.Type;
            }
        }

        private void GetMethodCodePosition(FunctionCallNode currentNode)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var projectItem = package.dte.Solution.FindProjectItem(currentNode.CodeFileName);
            if (projectItem == null) throw new Exception($"Can not find project item with file name: {currentNode.CodeFileName}");

            var codeModel = projectItem.FileCodeModel;
            if (codeModel == null) throw new Exception($"Can not find code model with file name: {currentNode.CodeFileName}");

            var startAndEndRow = FindFunctionBodyElementWithStartLineAndType(codeModel.CodeElements, currentNode.CodeStartLine);

            currentNode.CodeStartLine = startAndEndRow?.Item1 ?? 0;
            currentNode.CodeEndLine = startAndEndRow?.Item2 ?? 0;
        }

        private void GetSerializedFields(FunctionCallNode currentNode)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            int fieldCount = 0;
            string x = debugger.GetExpression($"this.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Length").Value.TrimStart('"').TrimEnd('"');
            int.TryParse( x, out fieldCount);

            HashSet<string> InterfaceTypeFields = new HashSet<string>();
            HashSet<string> ClassTypeFields = new HashSet<string>();
            for (int i = 0; i < fieldCount; i++)
            {
                if (bool.TryParse(debugger.GetExpression($"this.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)[{i}].FieldType.IsInterface").Value.TrimStart('"').TrimEnd('"'), out var isInterface))
                {
                    string fieldName = debugger.GetExpression($"this.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)[{i}].Name").Value.TrimStart('"').TrimEnd('"');
                    if (isInterface)
                    {
                        InterfaceTypeFields.Add(fieldName);
                    }
                    else
                    {
                        ClassTypeFields.Add(fieldName);
                    }
                }
            }
            currentNode.InterfaceTypeFields = InterfaceTypeFields;
            currentNode.ClassTypeFields = ClassTypeFields;
        }

        private void GetConstructorParams(FunctionCallNode currentNode, string uniquePrefix)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string constructorString = debugger.GetExpression($"(this.GetType().GetConstructors()[0] as System.Reflection.RuntimeConstructorInfo).ToString()").Value.TrimStart('"').TrimEnd('"');
            var match = Regex.Match(constructorString, @"(?<=\().*(?=\))");

            if (match.Success && !string.IsNullOrEmpty(match.Value))
            {
                currentNode.ConstructorParameters = match.Value.Split(',').ToList();
            }


            int ConstructorParamsCount = int.Parse(debugger.GetExpression($"(this.GetType().GetConstructors()[0] as System.Reflection.RuntimeConstructorInfo).ArgumentTypes.Length").Value.TrimStart('"').TrimEnd('"'));
            for (int index = 0; index < ConstructorParamsCount; index++)
            {
                bool isVauleType = bool.Parse(debugger.GetExpression($"(this.GetType().GetConstructors()[0] as System.Reflection.RuntimeConstructorInfo).ArgumentTypes[{index}].IsValueType").Value.TrimStart('"').TrimEnd('"'));
                if (isVauleType)
                {
                    // Use default here because no Type should named as default
                    currentNode.ConstructorParameters[index] = "default";
                }
            }
        }

        private void GetSerializedMethodInfo(FunctionCallNode currentNode, string uniquePrefix)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            debugger.ExecuteStatement($"{uniquePrefix}_frame = new System.Diagnostics.StackTrace(true).GetFrame(0);", -1, false);
            debugger.ExecuteStatement($"{uniquePrefix}_methodInfo = {uniquePrefix}_frame.GetMethod() as System.Reflection.MethodInfo;", -1, false);
            debugger.ExecuteStatement($"{uniquePrefix}_objHashCode = this.GetHashCode();", -1, false);
            currentNode.ModuleName = debugger.GetExpression($"{uniquePrefix}_methodInfo.DeclaringType.Module.Name").Value.TrimStart('"').TrimEnd('"');
            currentNode.NamespaceName.Add(debugger.GetExpression($"{uniquePrefix}_methodInfo.DeclaringType.Namespace").Value.TrimStart('"').TrimEnd('"'));
            currentNode.NamespaceName.Add(debugger.GetExpression($"{uniquePrefix}_methodInfo.ReturnType.Namespace").Value.TrimStart('"').TrimEnd('"'));

            currentNode.ClassName = debugger.GetExpression($"{uniquePrefix}_methodInfo.DeclaringType.Name").Value.TrimStart('"').TrimEnd('"');
            currentNode.MethodName = debugger.GetExpression($"{uniquePrefix}_methodInfo.Name").Value.TrimStart('"').TrimEnd('"');
            currentNode.CodeFileName = Regex.Unescape(debugger.GetExpression($"{uniquePrefix}_frame.GetFileName()").Value).TrimStart('"').TrimEnd('"');
            int.TryParse(debugger.GetExpression($"{uniquePrefix}_objHashCode").Value.TrimStart('"').TrimEnd('"'), out var objHashCode);
            int.TryParse(debugger.GetExpression($"{uniquePrefix}_frame.GetFileLineNumber()").Value.TrimStart('"').TrimEnd('"'), out var codeStartLine);
            int.TryParse(debugger.GetExpression($"{uniquePrefix}_frame.GetFileColumnNumber()").Value.TrimStart('"').TrimEnd('"'), out var codeStartCharacter);

            currentNode.ObjHashCode = objHashCode;
            // this codeStartLine is not real codeStartLine, it is the startLine of function body
            currentNode.CodeStartLine = codeStartLine;
            currentNode.CodeStartCharacter = codeStartCharacter;
        }

        private void GetSerializedInputAndThis(FunctionCallNode currentNode, string uniquePrefix)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Dictionary<string, string> inputJsonValues = new Dictionary<string, string>();
            Dictionary<string, string> inputTypes = new Dictionary<string, string>();
            Dictionary<int, string> interfaceInputHashCodes = new Dictionary<int, string>();
            var InterfaceTypeInputsRuntimeTypesMap = new Dictionary<Tuple<string, string>, string>();

            Dictionary<string, string> fieldJsonValues = new Dictionary<string, string>();
            Dictionary<string, string> fieldTypes = new Dictionary<string, string>();
            Dictionary<int, string> interfaceFieldHashCodes = new Dictionary<int, string>();
            var InterfaceTypeFieldsRuntimeTypesMap = new Dictionary<Tuple<string, string>, string>();
            const string thisObjectName = "this";
            EnvDTE.StackFrame sf = debugger.CurrentStackFrame;
            var argNames = sf.Arguments.Cast<Expression>().Select(ex => ex.Name).ToList();

            // GetSerializedInput
            Expression jsonValueExp;
            string jsonValueObjName = $"{uniquePrefix}_serializedInputOrThis";
            string jsonValue;
            HashSet<string> interfaceTypeInputs = new HashSet<string>();
            HashSet<string> classTypeInputs = new HashSet<string>();

            foreach (Expression exp in sf.Arguments)
            {
                if (exp.Name.StartsWith(uniquePrefix))
                {
                    continue;
                }
                string name = exp.Name;
                debugger.ExecuteStatement($"{jsonValueObjName} = {uniquePrefix}_serializeMethod.Invoke(null, new object[] {{ {exp.Name} }});", -1, false);
                jsonValueExp = debugger.GetExpression(jsonValueObjName);
                
                jsonValue = Regex.Unescape(jsonValueExp.Value).TrimStart('"').TrimEnd('"');
                inputJsonValues[exp.Name] = jsonValue;
                if (exp.Type.Contains(' '))
                {
                    // this input parameter is in interface type
                    var types = exp.Type.Split(' ');
                    string interfaceType = types[0];
                    string instanceType = types[1].TrimStart('{').TrimEnd('}');
                    inputTypes[exp.Name] = interfaceType;
                    InterfaceTypeInputsRuntimeTypesMap[new Tuple<string, string>(interfaceType, exp.Name)] = instanceType;

                    debugger.ExecuteStatement($"{uniquePrefix}_objHashCode = {exp.Name}.GetHashCode();", -1, false);
                    string argHashCodeString = debugger.GetExpression($"{uniquePrefix}_objHashCode").Value.TrimStart('"').TrimEnd('"');
                    int.TryParse(argHashCodeString, out int argHashCode);
                    interfaceInputHashCodes[argHashCode] = exp.Name;
                    interfaceTypeInputs.Add(exp.Name);
                }
                else
                {
                    inputTypes[exp.Name] = exp.Type;
                    classTypeInputs.Add(exp.Name);
                }
                currentNode.NamespaceName.Add(debugger.GetExpression($"{name}.GetType().Namespace").Value.TrimStart('"').TrimEnd('"'));
            }
            currentNode.Input = inputJsonValues;
            currentNode.InputTypes = inputTypes;
            currentNode.InterfaceInputHashCodes = interfaceInputHashCodes;
            currentNode.InterfaceTypeInputs = interfaceTypeInputs;
            currentNode.ClassTypeInputs = classTypeInputs;
            currentNode.InterfaceTypeInputsRuntimeTypesMap = InterfaceTypeInputsRuntimeTypesMap;

            // GetSerializedThis
            debugger.ExecuteStatement($"{jsonValueObjName} = CSharpUnitTestGeneratorExtHelper.DebuggerHelpers.SeriWithPrivate({thisObjectName});", -1, false);
            var thisExp = debugger.GetExpression(thisObjectName);
            jsonValueExp = debugger.GetExpression(jsonValueObjName);
            jsonValue = Regex.Unescape(jsonValueExp.Value).TrimStart('"').TrimEnd('"');
            currentNode.thisJsonValue = jsonValue;
            foreach (string fieldName in currentNode.InterfaceTypeFields)
            {
                Expression fieldExp = thisExp.DataMembers.Cast<Expression>().Where(member =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return member.Name == fieldName;
                }).FirstOrDefault();
                string fieldType = fieldExp.Type;
                var x = fieldType.Split(' ').ToList();
                string interfaceType = x[0];
                string instanceTypeRaw = x[1];
                string instanceType = instanceTypeRaw.TrimStart('{').TrimEnd('}');
                
                debugger.ExecuteStatement($"{uniquePrefix}_objHashCode = {fieldName}.GetHashCode();", -1, false);
                string fieldHashCodeString = debugger.GetExpression($"{uniquePrefix}_objHashCode").Value.TrimStart('"').TrimEnd('"');
                int.TryParse(fieldHashCodeString, out int fieldHashCode);
                interfaceFieldHashCodes[fieldHashCode] = fieldName;
                fieldTypes[fieldName] = interfaceType;
                InterfaceTypeFieldsRuntimeTypesMap[new Tuple<string, string>(interfaceType, fieldName)] = instanceType;
                currentNode.NamespaceName.Add(debugger.GetExpression($"{fieldName}.GetType().Namespace").Value.TrimStart('"').TrimEnd('"'));
            }
            foreach (string fieldName in currentNode.ClassTypeFields)
            {
                JObject thisJObject = JObject.Parse(jsonValue);
                fieldJsonValues[fieldName] = thisJObject[fieldName]?.ToString() ?? string.Empty;
                Expression fieldExp = thisExp.DataMembers.Cast<Expression>().Where(member =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return member.Name == fieldName;
                }).FirstOrDefault();
                string fieldType = fieldExp.Type;
                fieldTypes[fieldName] = fieldExp.Type;
                currentNode.NamespaceName.Add(debugger.GetExpression($"{fieldName}.GetType().Namespace").Value.TrimStart('"').TrimEnd('"'));
            }

            currentNode.Fields = fieldJsonValues;
            currentNode.FieldsTypes = fieldTypes;
            currentNode.InterfaceFieldHashCodes = interfaceFieldHashCodes;
            currentNode.InterfaceTypeFieldsRuntimeTypesMap = InterfaceTypeFieldsRuntimeTypesMap;
        }

        // use SQLLite instead of json file
        private void WriteDownFuncIORecJson(FunctionCallNode currentNode, string folderPath, string fileName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Directory.CreateDirectory(folderPath);
            var filePath = Path.Combine(folderPath, fileName);
            string content = string.Empty;
            List<FunctionCallNode> nodes = new List<FunctionCallNode> ();
            if (File.Exists(filePath))
            {
                content = File.ReadAllText(filePath);
                var settings = new JsonSerializerSettings();
                settings.Converters.Add(new JsonTupleConverter());
                nodes = JsonConvert.DeserializeObject<List<FunctionCallNode>>(content, settings);
                nodes.RemoveAll(node => node == null ||( node.CodeFileName == currentNode.CodeFileName && node.CodeStartLine == currentNode.CodeStartLine && node.CodeStartCharacter == currentNode.CodeStartCharacter));
            }
            if (currentNode != null)
            {
                nodes.Add(currentNode);
            }
            content = JsonConvert.SerializeObject(nodes, Formatting.Indented);
            File.WriteAllText(filePath, content);
        }

        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            if (sender is OleMenuCommand menuCommand)
            {
                menuCommand.Text = package.InRecordMode ? "UTGen Record Mode End" : "UTGen Record Mode Start";
            }
        }

        private string GetQualifiedPrefix(EnvDTE100.Debugger5 debugger)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string uniquePrefix = string.Empty;
            // string uniquePrefix = $"_{Guid.NewGuid().ToString().Replace("-", "_")}";
            EnvDTE.StackFrame sf = debugger.CurrentStackFrame;

            while (true)
            {
                uniquePrefix = $"_{Guid.NewGuid().ToString().Replace("-", "_")}";
                bool qualifiedPrefix = true;
                foreach (Expression exp in sf.Locals)
                {
                    if (exp.Name.StartsWith(uniquePrefix))
                    {
                        qualifiedPrefix = false;
                    }
                }
                if (qualifiedPrefix)
                {
                    break;
                }
            }
            return uniquePrefix;
        }

        private Tuple<int, int> FindFunctionBodyElementWithStartLineAndType(CodeElements roots, int line)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (roots == null) return null;

            foreach (CodeElement element in roots)
            {
                if (element.Kind == vsCMElement.vsCMElementFunction)
                {
                    var function = element as CodeFunction;
                    if (function != null)
                    {
                        var startPoint = function.GetStartPoint(vsCMPart.vsCMPartHeader);
                        var endPoint = function.GetEndPoint();

                        if (startPoint.Line <= line && endPoint.Line >= line)
                        {
                            return new Tuple<int, int> (startPoint.Line, endPoint.Line);
                        }
                    }
                }
                else if (element.Kind == vsCMElement.vsCMElementClass 
                        || element.Kind == vsCMElement.vsCMElementStruct
                        || element.Kind == vsCMElement.vsCMElementNamespace)
                {
                    var ele = FindFunctionBodyElementWithStartLineAndType(element.Children, line);
                    if (ele != null)
                    {
                        return ele;
                    }
                }
            }
            return null;
        }

        private void AddFileToProject(string filePath, string content)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.Write(content);
            }
        }

        private void DeleteFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
