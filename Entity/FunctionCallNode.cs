using Microsoft.VisualStudio.OLE.Interop;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSIXHelloWorldProject.Entity
{
    public class FunctionCallNode
    {
        public DateTime CreationTime { get; set; }
        public string ModuleName { get; set; }
        public List<string> NamespaceName { get; set; } = new List<string>();
        public string ClassName { get; set; }
        // This is used to find the exactly interface obj who called this function.
        public int ObjHashCode { get; set; }
        public List<string> ConstructorParameters { get; set; } = new List<string> ();
        public Dictionary<string, string> Fields { get; set; }
        public Dictionary<string, string> FieldsTypes { get; set; }
        public Dictionary<int, string> InterfaceFieldHashCodes { get; set; }
        public HashSet<string> InterfaceTypeFields { get; set; }
        public HashSet<string> ClassTypeFields { get; set; }
        //Key: interfaceType, fieldName Value: instanceType
        public Dictionary<Tuple<string, string>, string> InterfaceTypeFieldsRuntimeTypesMap { get; set; }
        public string MethodName { get; set; }
        public ConcurrentBag<FunctionCallNode> Children { get { return children; } set { this.children = value; } }
        private ConcurrentBag<FunctionCallNode> children = new ConcurrentBag<FunctionCallNode>();
        public Dictionary<string, string> Input { get; set; }
        public Dictionary<string, string> InputTypes { get; set; }
        public Dictionary<int, string> InterfaceInputHashCodes { get; set; }
        public HashSet<string> InterfaceTypeInputs { get; set; }
        public HashSet<string> ClassTypeInputs { get; set; }
        //Key: interfaceType, fieldName Value: instanceType
        public Dictionary<Tuple<string, string>, string> InterfaceTypeInputsRuntimeTypesMap { get; set; }
        public string Output { get; set; }
        public string OutputType { get; set; }
        public string thisJsonValue { get; set; }
        public string CodeFunctionNameFromDTE { get; set; }
        public string CodeFileName { get; set; } = "";
        public int CodeStartLine { get; set; } = 0;
        public int CodeStartCharacter { get; set; } = 0;
        public int CodeEndLine { get; set; } = 0;
        public int CodeEndCharacter { get; set; } = 0;
    }
}
