using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpUnitTestGeneratorExt.Entity
{
    public class ObjectInfoWithName : ObjectInfo
    {
        public string Name { get; set; }

        public new ObjectInfoWithName ShallowCopy()
        {
            return (ObjectInfoWithName)this.MemberwiseClone();
        }
    }
}
