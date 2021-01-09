using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Newtonsoft.Json;

namespace Codegen.Library.JsonConverters
{
    public class MetadataModelConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            if (objectType.IsGenericType)
            {
                var genType = objectType.GetGenericTypeDefinition();
                return typeof(MetadataModel<>).IsAssignableFrom(genType);
            }
            return typeof(MetadataModel).IsAssignableFrom(objectType);
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
            }
            else if (value is MetadataModel model)
            {
                writer.WriteStartObject();

                writer.WritePropertyName(nameof(MetadataModel.ToolVersion));
                writer.WriteValue(model.ToolVersion);

                writer.WritePropertyName(nameof(MetadataModel.QueryName));
                writer.WriteValue(model.QueryName);

                writer.WritePropertyName(nameof(MetadataModel.TemplateName));
                writer.WriteValue(model.TemplateName);

                writer.WritePropertyName(nameof(MetadataModel.Namespace));
                writer.WriteValue(model.Namespace);

                writer.WritePropertyName(nameof(MetadataModel.TypeName));
                writer.WriteValue(model.TypeName);

                writer.WritePropertyName(nameof(MetadataModel.XmlDoc));
                writer.WriteValue(model.XmlDoc);

                writer.WritePropertyName(nameof(MetadataModel.QueriedAt));
                writer.WriteValue(model.QueriedAt);

                writer.WritePropertyName(nameof(MetadataModel.SqlText));
                writer.WriteValue(model.SqlText);

                writer.WritePropertyName(nameof(MetadataModel.RecordTypeName));
                writer.WriteValue(model.RecordTypeName);

                // Records
                writer.WritePropertyName(nameof(MetadataModel.Records));
                writer.WriteStartArray();
                foreach (object record in model.Records)
                {
                    // TODO: Full control of ordering of record fields/props
                    serializer.Serialize(writer, record); // TODO: recordType here and above
                }
                writer.WriteEndArray();

                writer.WriteEndObject();
            }
            else
            {
                throw new JsonSerializationException($"Expected {typeof(MetadataModel).FullName} object value.");
            }
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            string? toolVersion = null,
                queryName = null,
                templateName = null,
                @namespace = null,
                typeName = null,
                xmlDoc = null,
                sqlText = null,
                recordType = null;
            DateTimeOffset? queriedAt = null;
            List<object>? records = null;

            _ = reader.ReadStartObject();

            while (reader.ReadPropertyName(out string? propertyName))
            {
                switch (propertyName)
                {
                    case nameof(MetadataModel.ToolVersion):
                        toolVersion = reader.ReadPropertyValue<string>(serializer);
                        continue;
                    case nameof(MetadataModel.QueryName):
                        queryName = reader.ReadPropertyValue<string>(serializer);
                        continue;
                    case nameof(MetadataModel.TemplateName):
                        templateName = reader.ReadPropertyValue<string>(serializer);
                        continue;
                    case nameof(MetadataModel.Namespace):
                        @namespace = reader.ReadPropertyValue<string>(serializer);
                        continue;
                    case nameof(MetadataModel.TypeName):
                        typeName = reader.ReadPropertyValue<string>(serializer);
                        continue;
                    case nameof(MetadataModel.XmlDoc):
                        xmlDoc = reader.ReadPropertyValue<string>(serializer);
                        continue;
                    case nameof(MetadataModel.QueriedAt):
                        queriedAt = reader.ReadPropertyValue<DateTimeOffset>(serializer);
                        continue;
                    case nameof(MetadataModel.SqlText):
                        sqlText = reader.ReadPropertyValue<string>(serializer);
                        continue;
                    case nameof(MetadataModel.RecordTypeName):
                        recordType = reader.ReadPropertyValue<string>(serializer);
                        continue;
                    case nameof(MetadataModel.Records):
                        if (recordType is null)
                        {
                            throw new InvalidOperationException($"{nameof(MetadataModel.RecordTypeName)} is missing");
                        }
                        records = reader.ReadListOfRecords(serializer, Type.GetType(recordType) ?? throw new InvalidOperationException($"The {recordType} type cannot be found."));
                        continue;
                    default:
                        throw new InvalidOperationException($"The '{propertyName}' property could not be deserialized.");
                }
            }

            _ = reader.ReadEndObject();

            return MetadataModel.Create(
                toolVersion: toolVersion ?? throw new InvalidOperationException($"Missing {nameof(MetadataModel.ToolVersion)} property."),
                queryName: queryName ?? throw new InvalidOperationException($"Missing {nameof(MetadataModel.QueryName)} property."),
                templateName: templateName ?? throw new InvalidOperationException($"Missing {nameof(MetadataModel.TemplateName)} property."),
                @namespace: @namespace ?? throw new InvalidOperationException($"Missing {nameof(MetadataModel.Namespace)} property."),
                typeName: typeName ?? throw new InvalidOperationException($"Missing {nameof(MetadataModel.TypeName)} property."),
                xmlDoc: xmlDoc ?? throw new InvalidOperationException($"Missing {nameof(MetadataModel.XmlDoc)} property."),
                queriedAt: queriedAt ?? throw new InvalidOperationException($"Missing {nameof(MetadataModel.QueriedAt)} property."),
                sqlText: sqlText ?? throw new InvalidOperationException($"Missing {nameof(MetadataModel.SqlText)} property."),
                recordTypeName: recordType ?? throw new InvalidOperationException($"Missing {nameof(MetadataModel.RecordTypeName)} property."),
                records: records ?? throw new InvalidOperationException($"Missing {nameof(MetadataModel.Records)} property."));
        }
    }

    internal static class JsonReaderExtensions
    {
        public static bool ReadStartObject(this JsonReader reader)
        {
            return reader.TokenType == JsonToken.StartObject && reader.Read();
        }

        public static bool ReadEndObject(this JsonReader reader)
        {
            return reader.TokenType == JsonToken.EndObject && reader.Read();
        }

        public static bool ReadPropertyName(this JsonReader reader, [NotNullWhen(true)] out string? property)
        {
            if (reader.TokenType == JsonToken.PropertyName)
            {
                property = (string)reader.Value;
                return reader.Read();
            }
            property = null;
            return false;
        }

        public static T ReadPropertyValue<T>(this JsonReader reader, JsonSerializer serializer)
        {
            T value = reader.Value is T variable ? variable : serializer.Deserialize<T>(reader);
            _ = reader.Read();
            return value;
        }

        public static List<object> ReadListOfRecords(this JsonReader reader, JsonSerializer serializer, Type recordType)
        {
            object listOfRecords = serializer.Deserialize(reader, typeof(IList<>).MakeGenericType(recordType));
            _ = reader.Read();
            var records = ((IEnumerable)listOfRecords).Cast<object>().ToList();
            return records;
        }
    }
}
