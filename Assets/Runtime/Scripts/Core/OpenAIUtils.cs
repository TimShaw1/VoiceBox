

using NAudio.CoreAudioApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Unity.VisualScripting;


namespace TimShaw.VoiceBox.Core
{
    public static class OpenAIUtils
    {
        #region
        /// <summary>
        /// Invokes a given <paramref name="method"/> via the provided <paramref name="instance"/> (ignored if <paramref name="method"/> is static) with arguments <paramref name="argumentsData"/>
        /// </summary>
        /// <param name="method">The <see cref="MethodInfo"/> of the method to call</param>
        /// <param name="instance">The object that should call <paramref name="method"/></param>
        /// <param name="argumentsData">The arguments to be provided to <paramref name="method"/></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static object InvokeMethodWithJsonArguments(MethodInfo method, object instance, BinaryData argumentsData)
        {
            // 1. Parse the JSON string into a JObject
            JObject parsedArguments = JObject.Parse(argumentsData.ToString());
            // 2. Get the parameters directly from the method. This is the only source of truth needed.
            ParameterInfo[] methodParameters = method.GetParameters();
            object[] invokeArgs = new object[methodParameters.Length];

            var serializerSettings = new JsonSerializerSettings();
            serializerSettings.Converters.Add(new UnityVectorJSONConverter());
            serializerSettings.Converters.Add(new UnityQuaternionJSONConverter());

            // 3. Match JSON properties to method parameters and build the argument array
            for (int i = 0; i < methodParameters.Length; i++)
            {
                ParameterInfo parameter = methodParameters[i];

                // Find the corresponding argument in the JObject using the parameter's name.
                if (parsedArguments.TryGetValue(parameter.Name, StringComparison.OrdinalIgnoreCase, out JToken argToken))
                {
                    // 4. THIS IS THE KEY:
                    // Deserialize the JToken directly to the type of the current parameter.
                    // The `parameter` object already has the `.ParameterType` we need.
                    invokeArgs[i] = argToken.ToObject(parameter.ParameterType, JsonSerializer.Create(serializerSettings));
                }
                else if (parameter.HasDefaultValue)
                {
                    // If the AI didn't provide a value but the method has a default, use it.
                    invokeArgs[i] = parameter.DefaultValue;
                }
                else
                {
                    // If a required parameter is missing, we can't proceed.
                    throw new ArgumentException($"Missing argument for required parameter '{parameter.Name}' in AI response.");
                }
            }

            // 5. Invoke the method with the prepared arguments
            return method.Invoke(instance, invokeArgs);
        }
        #endregion
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
        public class VoiceBoxChatCompletionOptions : ChatCompletionOptions
        {
            public VoiceBoxList<VoiceBoxChatTool> VoiceBoxTools = new VoiceBoxList<VoiceBoxChatTool>();
            public VoiceBoxChatCompletionOptions()
            {
                VoiceBoxTools.AddCallback = (tool) => Tools.Add(tool.OpenAIChatTool);
            }
        }

        #endregion

        #region
        /// <summary>
        /// A wrapper for a <see cref="ChatTool"/> that enables specifying a calling object and a method to call (via <see cref="MethodInfo"/>)
        /// </summary>
        public class VoiceBoxChatTool
        {
            public ChatTool OpenAIChatTool;

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

                OpenAIChatTool = ChatTool.CreateFunctionTool(
                    functionName: Method.Name,
                    functionDescription: functionDescription,
                    functionParameters: GetBinaryDataFromParameters(Method.GetParameters())
                );

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
        public class StreamingChatToolCallsBuilder
        {
            private readonly Dictionary<int, string> _indexToToolCallId = new();
            private readonly Dictionary<int, string> _indexToFunctionName = new();
            private readonly Dictionary<int, SequenceBuilder<byte>> _indexToFunctionArguments = new();

            public void Append(StreamingChatToolCallUpdate toolCallUpdate)
            {
                // Keep track of which tool call ID belongs to this update index.
                if (toolCallUpdate.ToolCallId != null)
                {
                    _indexToToolCallId[toolCallUpdate.Index] = toolCallUpdate.ToolCallId;
                }

                // Keep track of which function name belongs to this update index.
                if (toolCallUpdate.FunctionName != null)
                {
                    _indexToFunctionName[toolCallUpdate.Index] = toolCallUpdate.FunctionName;
                }

                // Keep track of which function arguments belong to this update index,
                // and accumulate the arguments as new updates arrive.
                if (toolCallUpdate.FunctionArgumentsUpdate != null && !toolCallUpdate.FunctionArgumentsUpdate.ToMemory().IsEmpty)
                {
                    if (!_indexToFunctionArguments.TryGetValue(toolCallUpdate.Index, out SequenceBuilder<byte> argumentsBuilder))
                    {
                        argumentsBuilder = new SequenceBuilder<byte>();
                        _indexToFunctionArguments[toolCallUpdate.Index] = argumentsBuilder;
                    }

                    argumentsBuilder.Append(toolCallUpdate.FunctionArgumentsUpdate);
                }
            }

            public IReadOnlyList<ChatToolCall> Build()
            {
                List<ChatToolCall> toolCalls = new();

                foreach ((int index, string toolCallId) in _indexToToolCallId)
                {
                    ReadOnlySequence<byte> sequence = _indexToFunctionArguments[index].Build();
                    string readableSequence = BinaryData.FromBytes(sequence.ToArray()).ToString();
                    List<string> realToolCalls = ParseConcatenatedJson(readableSequence);

                    foreach (string realToolCall in realToolCalls)
                    {
                        ChatToolCall toolCall = ChatToolCall.CreateFunctionToolCall(
                            id: toolCallId,
                            functionName: _indexToFunctionName[index],
                            functionArguments: BinaryData.FromString(realToolCall));

                        toolCalls.Add(toolCall);
                    }
                }

                return toolCalls;
            }

            private static List<string> ParseConcatenatedJson(string concatenatedJson)
            {
                var jsonObjects = new List<string>();
                int braceCount = 0;
                int startIndex = 0;

                for (int i = 0; i < concatenatedJson.Length; i++)
                {
                    char currentChar = concatenatedJson[i];

                    if (currentChar == '{')
                    {
                        if (braceCount == 0)
                        {
                            startIndex = i;
                        }
                        braceCount++;
                    }
                    else if (currentChar == '}')
                    {
                        braceCount--;
                        if (braceCount == 0 && startIndex != -1)
                        {
                            jsonObjects.Add(concatenatedJson.Substring(startIndex, i - startIndex + 1));
                            startIndex = -1; // Reset startIndex
                        }
                    }
                }

                return jsonObjects;
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

        public class UnityVectorJSONConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                // This converter can be used for UnityEngine.Vector types.
                return (objectType == typeof(UnityEngine.Vector2) ||  objectType == typeof(UnityEngine.Vector3) || objectType == typeof(UnityEngine.Vector4));
            }

            // This method handles converting JSON to a UnityEngine.Vector
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                // We expect the value to be a string like "(1, 2, 3)"
                if (reader.TokenType == JsonToken.String)
                {
                    string value = reader.Value.ToString();

                    // 1. Clean the string: remove parentheses and spaces
                    value = value.Trim('(', ')', ' ');

                    // 2. Split the string into components by the comma
                    string[] components = value.Split(',');

                        switch (components.Length)
                        {
                            case 2:
                                if (
                                    float.TryParse(components[0], out float x) &&
                                    float.TryParse(components[1], out float y) 
                                )
                                {
                                    if (objectType != typeof(UnityEngine.Vector2))
                                        UnityEngine.Debug.LogWarning($"Desired type is {objectType.Name} but parsed type is Vector2.");
                                    return new UnityEngine.Vector2(x, y);
                                }
                                break;
                            case 3:
                                if (
                                    float.TryParse(components[0], out float x1) &&
                                    float.TryParse(components[1], out float y1) &&
                                    float.TryParse(components[2], out float z1)
                                )
                                {
                                    if (objectType != typeof(UnityEngine.Vector3))
                                        UnityEngine.Debug.LogWarning($"Desired type is {objectType.Name} but parsed type is Vector3.");
                                    return new UnityEngine.Vector3(x1, y1, z1);
                                }
                                break;
                            case 4:
                                if (
                                    float.TryParse(components[0], out float x2) &&
                                    float.TryParse(components[1], out float y2) &&
                                    float.TryParse(components[2], out float z2) &&
                                    float.TryParse(components[3], out float w2)
                                )
                                {
                                    if (objectType != typeof(UnityEngine.Vector4))
                                        UnityEngine.Debug.LogWarning($"Desired type is {objectType.Name} but parsed type is Vector4.");
                                    return new UnityEngine.Vector4(x2, y2, z2, w2);
                                }
                                break;
                            default:
                                return null;
                        }

                
                }
                // If the JSON is already a proper object { "x": ... }, let the default logic handle it
                else if (reader.TokenType == JsonToken.StartObject)
                {
                    JObject item = JObject.Load(reader);
                    float x = item["x"]?.Value<float>() ?? 0;
                    float y = item["y"]?.Value<float>() ?? 0;
                    float z = item["z"]?.Value<float>() ?? 0;
                    return new UnityEngine.Vector3(x, y, z);
                }

                // If the format is unexpected, return a default UnityEngine.Vector3 or throw an exception
                throw new JsonSerializationException($"Unexpected token or format when parsing UnityEngine.Vector3. Value: {reader.Value}");
            }

            // This method handles converting a UnityEngine.Vector3 to JSON (optional but good practice)
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                // We'll write it out in the standard, structured format
                var vec = (UnityEngine.Vector3)value;
                writer.WriteStartObject();
                writer.WritePropertyName("x");
                writer.WriteValue(vec.x);
                writer.WritePropertyName("y");
                writer.WriteValue(vec.y);
                writer.WritePropertyName("z");
                writer.WriteValue(vec.z);
                writer.WriteEndObject();
            }
        }
        #endregion

        #region
        public class UnityQuaternionJSONConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                // This converter can be used for UnityEngine.Quaternion type.
                return objectType == typeof(UnityEngine.Quaternion);
            }

            // This method handles converting JSON to a UnityEngine.Quaternion
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                // We expect the value to be a string like "(0.1, 0.2, 0.3, 1.0)"
                if (reader.TokenType == JsonToken.String)
                {
                    string value = reader.Value.ToString();

                    // 1. Clean the string: remove parentheses and spaces
                    value = value.Trim('(', ')', ' ');

                    // 2. Split the string into components by the comma
                    string[] components = value.Split(',');

                    if (components.Length == 4)
                    {
                        if (
                            float.TryParse(components[0], out float x) &&
                            float.TryParse(components[1], out float y) &&
                            float.TryParse(components[2], out float z) &&
                            float.TryParse(components[3], out float w)
                        )
                        {
                            return new UnityEngine.Quaternion(x, y, z, w);
                        }
                    }
                }
                // If the JSON is already a proper object { "x": ..., "y": ..., "z": ..., "w": ... }, let the default logic handle it
                else if (reader.TokenType == JsonToken.StartObject)
                {
                    JObject item = JObject.Load(reader);
                    float x = item["x"]?.Value<float>() ?? 0;
                    float y = item["y"]?.Value<float>() ?? 0;
                    float z = item["z"]?.Value<float>() ?? 0;
                    float w = item["w"]?.Value<float>() ?? 1.0f; // Default w to 1.0 for a valid rotation
                    return new UnityEngine.Quaternion(x, y, z, w);
                }

                // If the format is unexpected, return a default UnityEngine.Quaternion or throw an exception
                throw new JsonSerializationException($"Unexpected token or format when parsing UnityEngine.Quaternion. Value: {reader.Value}");
            }

            // This method handles converting a UnityEngine.Quaternion to JSON
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                // We'll write it out in the standard, structured format
                var quat = (UnityEngine.Quaternion)value;
                writer.WriteStartObject();
                writer.WritePropertyName("x");
                writer.WriteValue(quat.x);
                writer.WritePropertyName("y");
                writer.WriteValue(quat.y);
                writer.WritePropertyName("z");
                writer.WriteValue(quat.z);
                writer.WritePropertyName("w");
                writer.WriteValue(quat.w);
                writer.WriteEndObject();
            }
        }
        #endregion
    }
}