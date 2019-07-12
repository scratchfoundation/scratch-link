using Newtonsoft.Json.Linq;
using System;
using System.Text;

namespace scratch_link
{
    internal static class EncodingHelpers
    {
        /// <summary>
        /// Decode the "message" property of `jsonBuffer` into bytes.
        /// If the buffer has an `encoding` property, use that method. Otherwise, assume the message is Unicode text.
        /// </summary>
        /// <param name="jsonBuffer">
        /// A JSON object containing a "message" property and optionally an "encoding" property.
        /// </param>
        /// <returns>An array of bytes containing the decoded data</returns>
        public static byte[] DecodeBuffer(JObject jsonBuffer)
        {
            var message = jsonBuffer["message"].ToObject<string>();
            var encoding = jsonBuffer["encoding"]?.ToObject<string>();

            switch (encoding)
            {
                case "base64": // "message" is encoded with Base64
                    return Convert.FromBase64String(message);

                case null: // "message" is a Unicode string with no additional encoding
                    return Encoding.UTF8.GetBytes(message);

                default:
                    throw JsonRpcException.InvalidParams($"unsupported encoding: {encoding}");
            }
        }


        /// <summary>
        /// Encode `data` using `encoding`, either into `destination` or a new JSON object.
        /// </summary>
        /// <param name="data">The data to encode</param>
        /// <param name="encoding">The type of encoding to use, or null to "encode" as a Unicode string</param>
        /// <param name="destination">
        /// The optional object to encode into.
        /// If not null, the "message" and "encoding" properties will be adjusted as necessary.
        /// If null, a new object will be created with "message" and (possibly) "encoding" properties.
        /// </param>
        /// <returns>The object to which the encoded message was written, regardless of source</returns>
        public static JObject EncodeBuffer(byte[] data, string encoding, JObject destination = null)
        {
            if (destination == null)
            {
                destination = new JObject();
            }

            switch (encoding)
            {
                case "base64":
                    destination["encoding"] = encoding;
                    destination["message"] = Convert.ToBase64String(data);
                    break;

                case null:
                    destination.Remove("encoding");
                    destination["message"] = Encoding.UTF8.GetString(data);
                    break;

                default:
                    throw JsonRpcException.InvalidParams($"unsupported encoding: {encoding}");
            }

            return destination;
        }
    }
}
