using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace scratch_link
{
    [JsonConverter(typeof(JsonRpcExceptionConverter))]
    internal class JsonRpcException : Exception
    {
        public readonly int Code;
        public readonly object JsonRpcData; // JSON-RPC error field "data"

        [JsonConstructor]
        public JsonRpcException(int code, string message, string data = null)
            : base(message)
        {
            Code = code;
            JsonRpcData = data;
        }

        public static JsonRpcException ParseError(string data = null)
        {
            return new JsonRpcException(-32700, "Parse Error", data);
        }

        public static JsonRpcException InvalidRequest(string data = null)
        {
            return new JsonRpcException(-32600, "Invalid Request", data);
        }

        public static JsonRpcException MethodNotFound(string data = null)
        {
            return new JsonRpcException(-32601, "Method Not Found", data);
        }

        public static JsonRpcException InvalidParams(string data = null)
        {
            return new JsonRpcException(-32602, "Invalid Params", data);
        }

        public static JsonRpcException InternalError(string data = null)
        {
            return new JsonRpcException(-32603, "Internal Error", data);
        }

        public static JsonRpcException ServerError(int code, string data = null)
        {
            return new JsonRpcException(code, "Server Error", data);
        }

        public static JsonRpcException ApplicationError(string data = null)
        {
            return new JsonRpcException(-32500, "Application Error", data);
        }
    }

    internal class JsonRpcExceptionConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(JsonRpcException);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var exception = (JsonRpcException)value;

            writer.WriteStartObject();
            writer.WritePropertyName("code");
            writer.WriteValue(exception.Code);
            writer.WritePropertyName("message");
            writer.WriteValue(exception.Message);
            if (exception.JsonRpcData != null)
            {
                writer.WritePropertyName("data");
                writer.WriteValue(exception.JsonRpcData);
            }

            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);
            var code = jsonObject["code"].ToObject<int>();
            var message = jsonObject["message"].ToString();
            return new JsonRpcException(code, message,
                jsonObject.TryGetValue("data", out var data) ? data.ToString() : null);
        }
    }
}
