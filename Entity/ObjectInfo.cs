using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSIXHelloWorldProject.Entity
{
    public class ObjectInfo
    {
        public string Type { get; set; }
        public string Value { get; set; }

        public ObjectInfo ShallowCopy()
        {
            return (ObjectInfo)this.MemberwiseClone();
        }
    }
}
