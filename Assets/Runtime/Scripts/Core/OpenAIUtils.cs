
using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Unity.VisualScripting;


namespace TimShaw.VoiceBox.Core
{
    public static class OpenAIUtils
    {
        #region

        public class VoiceBoxList<T> : IList<T>
        {
            public VoiceBoxList() { }
            public VoiceBoxList(Action<T> addCallback)
            {
                AddCallback = addCallback;
            }
            public Action<T> AddCallback;
            private readonly List<T> _internalList = new List<T>();

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
        }
        #endregion

        #region
        /// <summary>
        /// A wrapper for <see cref="ChatOptions"/> that enables support for <see cref="VoiceBoxChatTool"/> tools to be used.
        /// </summary>
        public class VoiceBoxChatCompletionOptions : ChatOptions
        {
            public VoiceBoxList<VoiceBoxChatTool> VoiceBoxTools = new VoiceBoxList<VoiceBoxChatTool>();
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
        /// A wrapper for a <see cref="ChatTool"/> that enables specifying a calling object and a method to call (via <see cref="MethodInfo"/>)
        /// </summary>
        public class VoiceBoxChatTool
        {
            public AITool InternalChatTool;

            public object Caller;

            public MethodInfo Method;

            /// <summary>
            /// 
            /// </summary>
            /// <param name="caller">What object should call <paramref name="functionName"/> (usually <see langword="this"/> or <see langword="null"/> if the function is static)</param>
            /// <param name="functionName">The name of the function to call. Should be passed as <c><see langword="nameof"/>(MyFunc)</c></param>
            /// <param name="functionDescription">A description of the function to be provided to the LLM</param>
            public VoiceBoxChatTool(object caller, string functionName, string functionDescription)
            {
                Caller = caller;
                Method = caller.GetType().GetMethod(functionName);

                var options = new JsonSerializerOptions();
                options.Converters.Add(new Vector2JsonConverter());
                options.Converters.Add(new Vector3JsonConverter());
                options.Converters.Add(new Vector4JsonConverter());
                options.Converters.Add(new QuaternionJsonConverter());

                InternalChatTool = AIFunctionFactory.Create(Method, Caller, functionName, functionDescription);

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
        public class SequenceBuilder<T>
        {
            Segment _first;
            Segment _last;

            public void Append(ReadOnlyMemory<T> data)
            {
                if (_first == null)
                {
                    Debug.Assert(_last == null);
                    _first = new Segment(data);
                    _last = _first;
                }
                else
                {
                    _last = _last!.Append(data);
                }
            }

            public ReadOnlySequence<T> Build()
            {
                if (_first == null)
                {
                    Debug.Assert(_last == null);
                    return ReadOnlySequence<T>.Empty;
                }

                if (_first == _last)
                {
                    Debug.Assert(_first.Next == null);
                    return new ReadOnlySequence<T>(_first.Memory);
                }

                return new ReadOnlySequence<T>(_first, 0, _last!, _last!.Memory.Length);
            }

            private sealed class Segment : ReadOnlySequenceSegment<T>
            {
                public Segment(ReadOnlyMemory<T> items) : this(items, 0)
                {
                }

                private Segment(ReadOnlyMemory<T> items, long runningIndex)
                {
                    Debug.Assert(runningIndex >= 0);
                    Memory = items;
                    RunningIndex = runningIndex;
                }

                public Segment Append(ReadOnlyMemory<T> items)
                {
                    long runningIndex;
                    checked { runningIndex = RunningIndex + Memory.Length; }
                    Segment segment = new(items, runningIndex);
                    Next = segment;
                    return segment;
                }
            }
        }
        #endregion

        #region

        public class Vector2JsonConverter : System.Text.Json.Serialization.JsonConverter<UnityEngine.Vector2>
        {
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
        public class Vector3JsonConverter : System.Text.Json.Serialization.JsonConverter<UnityEngine.Vector3>
        {
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
        public class Vector4JsonConverter : System.Text.Json.Serialization.JsonConverter<UnityEngine.Vector4>
        {
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
        public class QuaternionJsonConverter : System.Text.Json.Serialization.JsonConverter<UnityEngine.Quaternion>
        {
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
    }
}