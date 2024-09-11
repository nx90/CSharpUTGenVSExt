using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpUnitTestGeneratorExt.Entity
{
    public class FunctionInfo
    {
        public int ObjHashCode { get; set; }
        public string FunctionName { get; set; }
        public string BelongedClassName { get; set; }
        public List<ObjectInfoWithName> InputParams { get; set; }
        public ObjectInfo Output { get; set; }
        public List<string> UsedNamespaces { get; set; }

        public FunctionInfo ShallowCopy()
        {
            List<ObjectInfoWithName> newInputParams = InputParams.Select(para => para.ShallowCopy()).ToList();
            var newOutput = Output.ShallowCopy();
            var result = (FunctionInfo)this.MemberwiseClone();
            result.Output = newOutput;
            result.InputParams = newInputParams;
            return result;
        }
    }
}
