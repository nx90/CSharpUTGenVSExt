using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSIXHelloWorldProject
{
    public class JsonTupleConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Dictionary<Tuple<string, string>, string>);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            var dictionary = new Dictionary<Tuple<string, string>, string>();
            foreach (var item in token.Children<JProperty>())
            {
                var keyParts = item.Name.Trim('(', ')').Split(',');
                if (keyParts.Length == 2)
                {
                    var tuple = new Tuple<string, string>(keyParts[0].Trim(), keyParts[1].Trim());
                    dictionary[tuple] = item.Value.ToString();
                }
            }
            return dictionary;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var dictionary = (Dictionary<Tuple<string, string>, string>)value;
            writer.WriteStartObject();
            foreach (var kvp in dictionary)
            {
                writer.WritePropertyName($"({kvp.Key.Item1}, {kvp.Key.Item2})");
                serializer.Serialize(writer, kvp.Value);
            }
            writer.WriteEndObject();
        }
    }
}
