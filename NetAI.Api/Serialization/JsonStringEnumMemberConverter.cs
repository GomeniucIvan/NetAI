using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetAI.Api.Serialization;

//todo est

/// <summary>
/// A <see cref="JsonConverterFactory"/> that serializes enums using the values defined by <see cref="EnumMemberAttribute"/>.
/// </summary>
public sealed class JsonStringEnumMemberConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        Type converterType = typeof(EnumMemberConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class EnumMemberConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        private readonly Dictionary<string, TEnum> _fromString;
        private readonly Dictionary<TEnum, string> _toString;

        public EnumMemberConverter()
        {
            _fromString = new Dictionary<string, TEnum>(StringComparer.OrdinalIgnoreCase);
            _toString = new Dictionary<TEnum, string>();

            foreach (FieldInfo field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                TEnum value = (TEnum)field.GetValue(null)!;
                string serialized = field.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? field.Name;
                _fromString[serialized] = value;
                _toString[value] = serialized;
            }
        }

        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Expected string when parsing enum {typeof(TEnum).Name}.");
            }

            string raw = reader.GetString();
            if (raw is null)
            {
                throw new JsonException($"Unable to parse null value for enum {typeof(TEnum).Name}.");
            }

            if (_fromString.TryGetValue(raw, out TEnum value))
            {
                return value;
            }

            throw new JsonException($"Value '{raw}' is not valid for enum {typeof(TEnum).Name}.");
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            if (!_toString.TryGetValue(value, out string serialized))
            {
                serialized = value.ToString();
            }

            writer.WriteStringValue(serialized);
        }
    }
}
