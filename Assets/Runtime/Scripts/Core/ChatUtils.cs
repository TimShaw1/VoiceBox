
using Microsoft.Extensions.AI;
using NAudio.CoreAudioApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UnityEngine;


namespace TimShaw.VoiceBox.Core
{
    /// <summary>
    /// Provides various static utilities for chat services
    /// </summary>
    public static class ChatUtils
    {
        /// <summary>
        /// Represents the various different roles a <see cref="VoiceBoxChatMessage"/> can have.
        /// </summary>
        public readonly struct VoiceBoxChatRole
        {
            /// <summary>Gets the role that instructs or sets the behavior of the system.</summary>
            public static VoiceBoxChatRole System { get; } = new VoiceBoxChatRole("system");

            /// <summary>Gets the role that provides responses to system-instructed, user-prompted input.</summary>
            public static VoiceBoxChatRole Assistant { get; } = new VoiceBoxChatRole("assistant");

            /// <summary>Gets the role that provides user input for chat interactions.</summary>
            public static VoiceBoxChatRole User { get; } = new VoiceBoxChatRole("user");

            /// <summary>Gets the role that provides additional information and references in response to tool use requests.</summary>
            public static VoiceBoxChatRole Tool { get; } = new VoiceBoxChatRole("tool");

            /// <summary>
            /// Gets the string value of the chat role
            /// </summary>
            public string Value { get; }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="value"></param>
            public VoiceBoxChatRole(string value)
            {
                Value = value;
            }
        }

        #region
        /// <summary>
        /// Represents a chat message in a conversation.
        /// </summary>
        public class VoiceBoxChatMessage : ChatMessage
        {
            /// <summary>Initializes a new instance of the <see cref="VoiceBoxChatMessage"/> class.</summary>
            /// <param name="role">The role of the author of the message.</param>
            /// <param name="content">The text content of the message.</param>
            public VoiceBoxChatMessage(VoiceBoxChatRole role, string content)
            {
                Role = new ChatRole(role.Value);
                Contents = content is null ? new List<AIContent>() : new List<AIContent>() { new TextContent(content) };
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="VoiceBoxChatMessage"/> class from a <see cref="ChatMessage"/>
            /// </summary>
            /// <param name="chatMessage"></param>
            public VoiceBoxChatMessage(ChatMessage chatMessage)
            {
                Role = chatMessage.Role;
                Contents = chatMessage.Contents;
                AdditionalProperties = chatMessage.AdditionalProperties;
                CreatedAt = chatMessage.CreatedAt;
                RawRepresentation = chatMessage.RawRepresentation;
                MessageId = chatMessage.MessageId;
                AuthorName = chatMessage.AuthorName;
            }
        }
        #endregion

        #region

        /// <summary>
        /// Implements a list with an add callback. Used to implement tool calling.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class VoiceBoxList<T> : IList<T>
        {
            /// <summary>
            /// 
            /// </summary>
            public VoiceBoxList() { }
            /// <summary>
            /// Initializes a <see cref="VoiceBoxList{T}"/> with a callback that is called when an item is added.
            /// </summary>
            /// <param name="addCallback"></param>
            public VoiceBoxList(Action<T> addCallback)
            {
                AddCallback = addCallback;
            }

            /// <summary>
            /// An action that is invoked when an item is added to the list.
            /// </summary>
            public Action<T> AddCallback;
            private readonly List<T> _internalList = new List<T>();

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
            public T this[int index]
            {
                get => _internalList[index];
                set => _internalList[index] = value;
            }

            public int Count => _internalList.Count;


            public bool IsReadOnly => false;

            public void Add(T item)
            {
                _internalList.Add(item);
                AddCallback?.Invoke(item);
            }

            public void Clear()
            {
                _internalList.Clear();
            }

            public bool Contains(T item)
            {
                return _internalList.Contains(item);
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                _internalList.CopyTo(array, arrayIndex);
            }

            public IEnumerator<T> GetEnumerator()
            {
                return _internalList.GetEnumerator();
            }

            public int IndexOf(T item)
            {
                return _internalList.IndexOf(item);
            }

            public void Insert(int index, T item)
            {
                _internalList.Insert(index, item);
            }

            public bool Remove(T item)
            {
                return _internalList.Remove(item);
            }

            public void RemoveAt(int index)
            {
                _internalList.RemoveAt(index);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        }
        #endregion

        #region
        /// <summary>
        /// A wrapper for <see cref="ChatOptions"/> that enables support for <see cref="VoiceBoxChatTool"/> tools to be used.
        /// </summary>
        public class VoiceBoxChatCompletionOptions : ChatOptions
        {
            /// <summary>
            /// A list of chat tools that the <see cref="IChatService"/> can use
            /// </summary>
            public VoiceBoxList<VoiceBoxChatTool> VoiceBoxTools = new VoiceBoxList<VoiceBoxChatTool>();

            /// <summary>
            /// Initializes a new instance of the <see cref="VoiceBoxChatCompletionOptions"/> class.
            /// </summary>
            public VoiceBoxChatCompletionOptions()
            {
                if (Tools == null)
                    Tools = new List<AITool>();
                VoiceBoxTools.AddCallback = (tool) => Tools.Add(tool.InternalChatTool);
            }
        }

        #endregion

        #region
        /// <summary>
        /// A wrapper for an <see cref="AIFunction"/> that enables specifying a calling object and a method to call (via <see cref="MethodInfo"/>). 
        /// Also enables serialization of <see cref="UnityEngine.Vector2"/>, <see cref="UnityEngine.Vector3"/>, <see cref="UnityEngine.Vector4"/>, and <see cref="UnityEngine.Quaternion"/>
        /// </summary>
        public class VoiceBoxChatTool
        {
            /// <summary>
            /// Internal tracking of the <see cref="AIFunction"/> provided by <see cref="Microsoft.Extensions.AI"/>. 
            /// This approach enables cleaner user tool definition without needing to reference <see cref="Microsoft.Extensions.AI"/>.
            /// </summary>
            public AIFunction InternalChatTool;

            /// <summary>
            /// The object that will invoke <see cref="Method"/>
            /// </summary>
            public object Caller;

            /// <summary>
            /// The method the tool should call
            /// </summary>
            public MethodInfo Method;

            /// <summary>
            /// 
            /// </summary>
            /// <param name="caller">What object should call <paramref name="functionName"/> (usually <see langword="this"/> or <see langword="null"/> if the function is static)</param>
            /// <param name="functionName">The name of the function to call. Should be passed as <c><see langword="nameof"/>(MyFunc)</c></param>
            /// <param name="functionDescription">A description of the function to be provided to the LLM</param>
            /// <param name="customConverters">A list of custom <see cref="System.Text.Json.Serialization.JsonConverter"/> objects. Enables serialization of custom types</param>
            public VoiceBoxChatTool(object caller, string functionName, string functionDescription, IList<System.Text.Json.Serialization.JsonConverter> customConverters = default)
            {
                Caller = caller;
                Method = caller.GetType().GetMethod(functionName);

                var options = new JsonSerializerOptions()
                {
                    Converters = { new Vector2JsonConverter(), new Vector3JsonConverter(), new Vector4JsonConverter(), new QuaternionJsonConverter() },
                    TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
                };

                if (customConverters != null)
                    foreach (var converter in customConverters)
                        options.Converters.Add(converter);  

                InternalChatTool = AIFunctionFactory.Create(Method, Caller, functionName, functionDescription, options);

            }

            private BinaryData GetBinaryDataFromParameters(ParameterInfo[] parameters)
            {
                var properties = new JObject();
                var required = new JArray();

                foreach (var parameter in parameters)
                {
                    properties.Add(parameter.Name, GetJsonSchemaForType(parameter.ParameterType));
                    if (!parameter.IsOptional)
                    {
                        required.Add(parameter.Name);
                    }
                }

                var schema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["required"] = required
                };

                // Note: This requires the Newtonsoft.Json package (Json.NET)
                // which is a very common dependency in .NET projects.
                return BinaryData.FromString(schema.ToString());
            }

            private JObject GetJsonSchemaForType(Type type)
            {
                // --- Primitive and Simple Type Mapping ---
                if (type == typeof(string))
                {
                    return new JObject { ["type"] = "string" };
                }
                if (type == typeof(int) || type == typeof(long) || type == typeof(short))
                {
                    return new JObject { ["type"] = "integer" };
                }
                if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
                {
                    return new JObject { ["type"] = "number" };
                }
                if (type == typeof(bool))
                {
                    return new JObject { ["type"] = "boolean" };
                }
                if (type == typeof(Guid))
                {
                    return new JObject { ["type"] = "string", ["format"] = "uuid" };
                }

                // --- Array/List Type Mapping ---
                if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(List<>) || type.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
                {
                    var itemType = type.GetGenericArguments()[0];
                    return new JObject
                    {
                        ["type"] = "array",
                        ["items"] = GetJsonSchemaForType(itemType)
                    };
                }

                // --- Complex Class (Object) Type Mapping ---
                if (type.IsClass)
                {
                    var properties = new JObject();
                    var required = new JArray();
                    foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        // Recursively get the schema for the property's type
                        properties.Add(prop.Name, GetJsonSchemaForType(prop.PropertyType));
                        // For simplicity, let's assume all properties of a custom class are required.
                        // You could use custom attributes to mark optional properties.
                        required.Add(prop.Name);
                    }

                    return new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = properties,
                        ["required"] = required
                    };
                }

                // Fallback for unknown types
                return new JObject { ["type"] = "string" };
            }
        }
        #endregion

        #region
        /// <summary>
        /// Enables serialization of <see cref="UnityEngine.Vector2"/> for tool calling
        /// </summary>
        public class Vector2JsonConverter : System.Text.Json.Serialization.JsonConverter<UnityEngine.Vector2>
        {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
            public override bool CanConvert(Type typeToConvert)
            {
                return typeToConvert == typeof(UnityEngine.Vector2);
            }
            public override UnityEngine.Vector2 Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
            {
                if (reader.TokenType == System.Text.Json.JsonTokenType.String)
                {
                    string value = reader.GetString();
                    value = value.Trim('(', ')', ' ');
                    string[] components = value.Split(',');

                    if (components.Length == 2 &&
                        float.TryParse(components[0], out float x) &&
                        float.TryParse(components[1], out float y))
                    {
                        return new UnityEngine.Vector2(x, y);
                    }
                }
                else if (reader.TokenType == System.Text.Json.JsonTokenType.StartObject)
                {
                    float x = 0, y = 0;
                    while (reader.Read())
                    {
                        if (reader.TokenType == System.Text.Json.JsonTokenType.EndObject)
                        {
                            return new UnityEngine.Vector2(x, y);
                        }

                        if (reader.TokenType == System.Text.Json.JsonTokenType.PropertyName)
                        {
                            string propertyName = reader.GetString();
                            reader.Read();
                            switch (propertyName.ToLower())
                            {
                                case "x":
                                    x = reader.GetSingle();
                                    break;
                                case "y":
                                    y = reader.GetSingle();
                                    break;
                            }
                        }
                    }
                }
                throw new System.Text.Json.JsonException($"Unexpected token or format when parsing Vector2. Value: {reader.GetString()}");
            }

            public override void Write(System.Text.Json.Utf8JsonWriter writer, UnityEngine.Vector2 value, System.Text.Json.JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteNumber("x", value.x);
                writer.WriteNumber("y", value.y);
                writer.WriteEndObject();
            }
        }
        #endregion

        #region
        /// <summary>
        /// Enables serialization of <see cref="UnityEngine.Vector3"/> for tool calling
        /// </summary>
        public class Vector3JsonConverter : System.Text.Json.Serialization.JsonConverter<UnityEngine.Vector3>
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeToConvert == typeof(UnityEngine.Vector3);
            }
            public override UnityEngine.Vector3 Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
            {
                if (reader.TokenType == System.Text.Json.JsonTokenType.String)
                {
                    string value = reader.GetString();
                    value = value.Trim('(', ')', ' ');
                    string[] components = value.Split(',');

                    if (components.Length == 3 &&
                        float.TryParse(components[0], out float x) &&
                        float.TryParse(components[1], out float y) &&
                        float.TryParse(components[2], out float z))
                    {
                        return new UnityEngine.Vector3(x, y, z);
                    }
                }
                else if (reader.TokenType == System.Text.Json.JsonTokenType.StartObject)
                {
                    float x = 0, y = 0, z = 0;
                    while (reader.Read())
                    {
                        if (reader.TokenType == System.Text.Json.JsonTokenType.EndObject)
                        {
                            return new UnityEngine.Vector3(x, y, z);
                        }

                        if (reader.TokenType == System.Text.Json.JsonTokenType.PropertyName)
                        {
                            string propertyName = reader.GetString();
                            reader.Read();
                            switch (propertyName.ToLower())
                            {
                                case "x":
                                    x = reader.GetSingle();
                                    break;
                                case "y":
                                    y = reader.GetSingle();
                                    break;
                                case "z":
                                    z = reader.GetSingle();
                                    break;
                            }
                        }
                    }
                }
                throw new System.Text.Json.JsonException($"Unexpected token or format when parsing Vector3. Value: {reader.GetString()}");
            }

            public override void Write(System.Text.Json.Utf8JsonWriter writer, UnityEngine.Vector3 value, System.Text.Json.JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteNumber("x", value.x);
                writer.WriteNumber("y", value.y);
                writer.WriteNumber("z", value.z);
                writer.WriteEndObject();
            }
        }
        #endregion

        #region
        /// <summary>
        /// Enables serialization of <see cref="UnityEngine.Vector4"/> for tool calling
        /// </summary>
        public class Vector4JsonConverter : System.Text.Json.Serialization.JsonConverter<UnityEngine.Vector4>
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeToConvert == typeof(UnityEngine.Vector4);
            }
            public override UnityEngine.Vector4 Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
            {
                if (reader.TokenType == System.Text.Json.JsonTokenType.String)
                {
                    string value = reader.GetString();
                    value = value.Trim('(', ')', ' ');
                    string[] components = value.Split(',');

                    if (components.Length == 4 &&
                        float.TryParse(components[0], out float x) &&
                        float.TryParse(components[1], out float y) &&
                        float.TryParse(components[2], out float z) &&
                        float.TryParse(components[3], out float w))
                    {
                        return new UnityEngine.Vector4(x, y, z, w);
                    }
                }
                else if (reader.TokenType == System.Text.Json.JsonTokenType.StartObject)
                {
                    float x = 0, y = 0, z = 0, w = 0;
                    while (reader.Read())
                    {
                        if (reader.TokenType == System.Text.Json.JsonTokenType.EndObject)
                        {
                            return new UnityEngine.Vector4(x, y, z, w);
                        }

                        if (reader.TokenType == System.Text.Json.JsonTokenType.PropertyName)
                        {
                            string propertyName = reader.GetString();
                            reader.Read();
                            switch (propertyName.ToLower())
                            {
                                case "x":
                                    x = reader.GetSingle();
                                    break;
                                case "y":
                                    y = reader.GetSingle();
                                    break;
                                case "z":
                                    z = reader.GetSingle();
                                    break;
                                case "w":
                                    w = reader.GetSingle();
                                    break;
                            }
                        }
                    }
                }
                throw new System.Text.Json.JsonException($"Unexpected token or format when parsing Vector4. Value: {reader.GetString()}");
            }

            public override void Write(System.Text.Json.Utf8JsonWriter writer, UnityEngine.Vector4 value, System.Text.Json.JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteNumber("x", value.x);
                writer.WriteNumber("y", value.y);
                writer.WriteNumber("z", value.z);
                writer.WriteNumber("w", value.w);
                writer.WriteEndObject();
            }
        }
        #endregion

        #region
        /// <summary>
        /// Enables serialization of <see cref="UnityEngine.Quaternion"/> for tool calling
        /// </summary>
        public class QuaternionJsonConverter : System.Text.Json.Serialization.JsonConverter<UnityEngine.Quaternion>
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeToConvert == typeof(UnityEngine.Quaternion);
            }
            public override UnityEngine.Quaternion Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
            {
                if (reader.TokenType == System.Text.Json.JsonTokenType.String)
                {
                    string value = reader.GetString();
                    value = value.Trim('(', ')', ' ');
                    string[] components = value.Split(',');

                    if (components.Length == 4 &&
                        float.TryParse(components[0], out float x) &&
                        float.TryParse(components[1], out float y) &&
                        float.TryParse(components[2], out float z) &&
                        float.TryParse(components[3], out float w))
                    {
                        return new UnityEngine.Quaternion(x, y, z, w);
                    }
                }
                else if (reader.TokenType == System.Text.Json.JsonTokenType.StartObject)
                {
                    float x = 0, y = 0, z = 0, w = 0;
                    while (reader.Read())
                    {
                        if (reader.TokenType == System.Text.Json.JsonTokenType.EndObject)
                        {
                            return new UnityEngine.Quaternion(x, y, z, w);
                        }

                        if (reader.TokenType == System.Text.Json.JsonTokenType.PropertyName)
                        {
                            string propertyName = reader.GetString();
                            reader.Read();
                            switch (propertyName.ToLower())
                            {
                                case "x":
                                    x = reader.GetSingle();
                                    break;
                                case "y":
                                    y = reader.GetSingle();
                                    break;
                                case "z":
                                    z = reader.GetSingle();
                                    break;
                                case "w":
                                    w = reader.GetSingle();
                                    break;
                            }
                        }
                    }
                }
                throw new System.Text.Json.JsonException($"Unexpected token or format when parsing Quaternion. Value: {reader.GetString()}");
            }

            public override void Write(System.Text.Json.Utf8JsonWriter writer, UnityEngine.Quaternion value, System.Text.Json.JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteNumber("x", value.x);
                writer.WriteNumber("y", value.y);
                writer.WriteNumber("z", value.z);
                writer.WriteNumber("w", value.w);
                writer.WriteEndObject();
            }
        }
        #endregion

        /// <summary>
        /// A custom JsonConverter for serializing and deserializing the Unity Color struct.
        /// </summary>
        public class ColorJsonConverter : System.Text.Json.Serialization.JsonConverter<Color>
        {
            /// <summary>
            /// Specifies which types this converter can convert
            /// </summary>
            public override bool CanConvert(Type typeToConvert)
            {
                return typeToConvert == typeof(Color);
            }

            /// <summary>
            /// Reads and converts the JSON to a Color object.
            /// Supports both object format {"r":1,"g":0,"b":0,"a":1} and string format "(1,0,0,1)".
            /// </summary>
            public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                // Handle string format e.g., "(1.0, 0.5, 0.0, 1.0)"
                if (reader.TokenType == JsonTokenType.String)
                {
                    string value = reader.GetString();
                    if (string.IsNullOrEmpty(value))
                    {
                        return default;
                    }

                    value = value.Trim('(', ')', ' ');
                    string[] components = value.Split(',');

                    if (components.Length == 4 &&
                        float.TryParse(components[0], out float r) &&
                        float.TryParse(components[1], out float g) &&
                        float.TryParse(components[2], out float b) &&
                        float.TryParse(components[3], out float a))
                    {
                        return new Color(r, g, b, a);
                    }
                }
                // Handle object format e.g., { "r": 1.0, "g": 0.5, "b": 0.0, "a": 1.0 }
                else if (reader.TokenType == JsonTokenType.StartObject)
                {
                    float r = 0, g = 0, b = 0, a = 1; // Default alpha to 1 (opaque)
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndObject)
                        {
                            return new Color(r, g, b, a);
                        }

                        if (reader.TokenType == JsonTokenType.PropertyName)
                        {
                            string propertyName = reader.GetString();
                            reader.Read();
                            switch (propertyName.ToLower())
                            {
                                case "r":
                                    r = reader.GetSingle();
                                    break;
                                case "g":
                                    g = reader.GetSingle();
                                    break;
                                case "b":
                                    b = reader.GetSingle();
                                    break;
                                case "a":
                                    a = reader.GetSingle();
                                    break;
                            }
                        }
                    }
                }

                throw new System.Text.Json.JsonException($"Unexpected token or format when parsing Color. Token: {reader.TokenType}");
            }

            /// <summary>
            /// Writes a Color object to JSON in object format.
            /// </summary>
            public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteNumber("r", value.r);
                writer.WriteNumber("g", value.g);
                writer.WriteNumber("b", value.b);
                writer.WriteNumber("a", value.a);
                writer.WriteEndObject();
            }
        }

    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}