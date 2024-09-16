using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpUnitTestGeneratorExt.Utils
{
    public static class ExtConstant
    {
        public static readonly string DebuggerHelperFileContent =
            @"#pragma warning disable 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace CSharpUnitTestGeneratorExtHelper
{
    public static class DebuggerHelpers
    {
        public static string SeriWithPrivate<T>(T obj)
        {
            var settings = new JsonSerializerSettings();
            settings.ContractResolver = new IncludePrivateStateContractResolver();
            settings.Formatting = Formatting.Indented;
            var json = JsonConvert.SerializeObject(obj, settings);
            return json;
        }

        public class IncludePrivateStateContractResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                .Select(p => base.CreateProperty(p, memberSerialization))
                                .Union(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                            .Select(f => base.CreateProperty(f, memberSerialization)))
                                .ToList();
                props.ForEach(p => { p.Writable = true; p.Readable = true; });
                return props;
            }
        }
    }
}
#pragma warning restore
";
    }
}
