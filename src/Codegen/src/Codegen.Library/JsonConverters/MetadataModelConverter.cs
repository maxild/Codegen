using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Codegen.Library.JsonConverters
{
    public class MetadataModelConverter : JsonConverter<MetadataModel>
    {
        // Because this converter can convert both MetadataModel and MetadataModel<TModel> we need to override CanConvert
        public override bool CanConvert(Type objectType)
        {
            if (objectType.IsGenericType)
            {
                var genType = objectType.GetGenericTypeDefinition();
                return typeof(MetadataModel<>).IsAssignableFrom(genType);
            }

            return typeof(MetadataModel).IsAssignableFrom(objectType);
        }

        public override void Write(Utf8JsonWriter writer, MetadataModel value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(nameof(MetadataModel.ToolVersion), value.ToolVersion);

            writer.WriteString(nameof(MetadataModel.QueryName), value.QueryName);

            writer.WriteString(nameof(MetadataModel.TemplateName), value.TemplateName);

            writer.WriteString(nameof(MetadataModel.Namespace), value.Namespace);

            writer.WriteString(nameof(MetadataModel.TypeName), value.TypeName);

            writer.WriteString(nameof(MetadataModel.XmlDoc), value.XmlDoc);

            if (!string.IsNullOrEmpty(value.IdentifierPrefix))
                writer.WriteString(nameof(MetadataModel.IdentifierPrefix), value.IdentifierPrefix);

            if (!string.IsNullOrEmpty(value.DomusIdentifierPrefix))
                writer.WriteString(nameof(MetadataModel.DomusIdentifierPrefix), value.DomusIdentifierPrefix);

            writer.WriteString(nameof(MetadataModel.SqlText), value.SqlText);

            writer.WriteString(nameof(MetadataModel.RecordTypeName), value.RecordTypeName);

            // Records
            writer.WritePropertyName(nameof(MetadataModel.Records));
            writer.WriteStartArray();
            foreach (object record in value.Records)
                JsonSerializer.Serialize(writer, record, options);
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        public override MetadataModel Read(ref Utf8JsonReader reader, Type objectType, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new InvalidOperationException("Expected StartObject token type.");

            string? toolVersion = null,
                queryName = null,
                templateName = null,
                @namespace = null,
                typeName = null,
                xmlDoc = null,
                identifierPrefix = null,
                domusIdentifierPrefix = null,
                sqlText = null,
                recordTypeName = null;
            IReadOnlyList<object>? records = null;

            while (true)
            {
                // Read PropertyName or EndObject.
                reader.Read();

                JsonTokenType tokenType = reader.TokenType;

                if (tokenType == JsonTokenType.EndObject)
                    break;

                // Read method would have thrown if otherwise.
                Debug.Assert(tokenType == JsonTokenType.PropertyName);

                string propertyName = reader.GetString()!;

                // Read the property value
                reader.Read();

                switch (propertyName)
                {
                    case nameof(MetadataModel.ToolVersion):
                        toolVersion = reader.GetString();
                        continue;
                    case nameof(MetadataModel.QueryName):
                        queryName = reader.GetString();
                        continue;
                    case nameof(MetadataModel.TemplateName):
                        templateName = reader.GetString();
                        continue;
                    case nameof(MetadataModel.Namespace):
                        @namespace = reader.GetString();
                        continue;
                    case nameof(MetadataModel.TypeName):
                        typeName = reader.GetString();
                        continue;
                    case nameof(MetadataModel.XmlDoc):
                        xmlDoc = reader.GetString();
                        continue;
                    case nameof(MetadataModel.IdentifierPrefix):
                        identifierPrefix = reader.GetString();
                        continue;
                    case nameof(MetadataModel.DomusIdentifierPrefix):
                        domusIdentifierPrefix = reader.GetString();
                        continue;
                    case nameof(MetadataModel.SqlText):
                        sqlText = reader.GetString();
                        continue;
                    case nameof(MetadataModel.RecordTypeName):
                        recordTypeName = reader.GetString();
                        continue;
                    case nameof(MetadataModel.Records):
                        if (recordTypeName is null)
                            throw new InvalidOperationException($"{nameof(MetadataModel.RecordTypeName)} is missing.");
                        Type? recordType = Type.GetType(recordTypeName);
                        if (recordType is null)
                            throw new InvalidOperationException($"The record type '{recordTypeName}' in the metadata cannot be found.");
                        Type listOfRecordsType = typeof(List<>).MakeGenericType(recordType);
                        object listOfRecords = JsonSerializer.Deserialize(ref reader, listOfRecordsType, options)!;
                        records = ((IEnumerable)listOfRecords).Cast<object>().ToList();
                        continue;
                    default:
                        throw new InvalidOperationException(
                            $"The '{propertyName}' property could not be deserialized.");
                }
            }

            return MetadataModel.Create(
                toolVersion: toolVersion ?? throw new InvalidOperationException($"Missing {nameof(MetadataModel.ToolVersion)} property."),
                queryName: queryName ?? throw new InvalidOperationException($"Missing {nameof(MetadataModel.QueryName)} property."),
                templateName: templateName ?? throw new InvalidOperationException($"Missing {nameof(MetadataModel.TemplateName)} property."),
                @namespace: @namespace ?? throw new InvalidOperationException($"Mi ssing {nameof(MetadataModel.Namespace)} property."),
                typeName: typeName ?? throw new InvalidOperationException($"Missing {nameof(MetadataModel.TypeName)} property."),
                xmlDoc: xmlDoc ?? throw new InvalidOperationException($"Missing {nameof(MetadataModel.XmlDoc)} property."),
                identifierPrefix: identifierPrefix ?? string.Empty,
                domusIdentifierPrefix: domusIdentifierPrefix ?? string.Empty,
                sqlText: sqlText ?? throw new InvalidOperationException($"Missing {nameof(MetadataModel.SqlText)} property."),
                recordTypeName: recordTypeName ?? throw new InvalidOperationException($"Missing {nameof(MetadataModel.RecordTypeName)} property."),
                records: records ?? throw new InvalidOperationException($"Missing {nameof(MetadataModel.Records)} property."));
        }
    }
}
