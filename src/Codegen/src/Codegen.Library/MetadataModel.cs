using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Codegen.Library.JsonConverters;
using Newtonsoft.Json;

namespace Codegen.Library
{
    /// <summary>
    /// Metadata wrapper used to represent a single code generated type
    /// when type of records are not known --- this is the case when reading
    /// and writing instances to disk --- only the dynamically invoked C#
    /// expressions and Razor templates will know the type of each record at runtime.
    /// </summary>
    public abstract class MetadataModel : IEquatable<MetadataModel>
    {
        public static string Serialize(MetadataModel metadataModel)
        {
            //return JsonConvert.SerializeObject(metadataModel, new JsonSerializerSettings
            //{
            //    Formatting = Formatting.Indented,
            //    Converters = {new MetadataModelConverter()}
            //});

            // Full control of indentation
            var sb = new StringBuilder();
            using (var sw = new StringWriter(sb))
            using (var jtw = new JsonTextWriter(sw)
            {
                Formatting = Formatting.Indented,
                Indentation = 2,
                IndentChar = ' ',
            })
            {
                new JsonSerializer { Converters = { new MetadataModelConverter() } }.Serialize(jtw, metadataModel);
            }
            return sb.ToString();
        }

        public static MetadataModel Deserialize(string json)
        {
            return JsonConvert.DeserializeObject<MetadataModel>(json, new JsonSerializerSettings
            {
                Converters = { new MetadataModelConverter() }
            });
        }

        public static MetadataModel<TRecord> Deserialize<TRecord>(string json)
        {
            return JsonConvert.DeserializeObject<MetadataModel<TRecord>>(json, new JsonSerializerSettings
            {
                Converters = { new MetadataModelConverter() }
            });
        }

        public static object Deserialize(string json, Type recordType)
        {
            Type modelType = typeof(MetadataModel<>).MakeGenericType(recordType);
            return JsonConvert.DeserializeObject(json, modelType, new JsonSerializerSettings
            {
                Converters = { new MetadataModelConverter() }
            });
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataModel"/> type.
        /// </summary>
        /// <param name="toolVersion"></param>
        /// <param name="queryName">The name of the cssql file</param>
        /// <param name="templateName">The name of the cshtml file</param>
        /// <param name="namespace">The namespace of the generated type</param>
        /// <param name="typeName">The type name of the generated type.</param>
        /// <param name="xmlDoc">The xml-doc of the generated type.</param>
        /// <param name="identifierPrefix">The prefix used when building identifiers.</param>
        /// <param name="queriedAt">The timestamp when the data was queried.</param>
        /// <param name="sqlText">The SQL expression that have generated the records.</param>
        /// <param name="recordTypeName">The assembly-qualified name of the record type.</param>
        /// <param name="records">The list of records.</param>
        public static MetadataModel Create(
            string toolVersion,
            string queryName,
            string templateName,
            string @namespace,
            string typeName,
            string xmlDoc,
            string identifierPrefix,
            DateTimeOffset queriedAt,
            string sqlText,
            string recordTypeName,
            IList<object> records)
        {
            Type? rt = Type.GetType(recordTypeName);
            if (rt is null)
            {
                throw new ArgumentException($"The {recordTypeName} type cannot be found.", nameof(recordTypeName));
            }
            Type t = typeof(MetadataModel<>).MakeGenericType(rt);
            return (MetadataModel)Activator.CreateInstance(t,
                toolVersion,
                queryName,
                templateName,
                @namespace,
                typeName,
                xmlDoc,
                identifierPrefix,
                queriedAt,
                sqlText,
                recordTypeName,
                records)!;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataModel"/> type.
        /// </summary>
        /// <param name="toolVersion"></param>
        /// <param name="queryName">The name of the cssql file</param>
        /// <param name="templateName">The name of the cshtml file</param>
        /// <param name="namespace">The namespace of the generated type</param>
        /// <param name="typeName">The type name of the generated type.</param>
        /// <param name="xmlDoc">The xml-doc of the generated type.</param>
        /// <param name="identifierPrefix">The prefix to be used on identifiers.</param>
        /// <param name="queriedAt">The timestamp when the data was queried.</param>
        /// <param name="sqlText">The SQL expression that have generated the records.</param>
        /// <param name="recordTypeName">The assembly-qualified name of the record type.</param>
        /// <param name="records">The list of records.</param>
        public static MetadataModel<TRecord> Create<TRecord>(
            string toolVersion,
            string queryName,
            string templateName,
            string @namespace,
            string typeName,
            string xmlDoc,
            string identifierPrefix,
            DateTimeOffset queriedAt,
            string sqlText,
            string recordTypeName,
            IList<object> records)
        {
            return new(toolVersion, queryName, templateName, @namespace, typeName, xmlDoc, identifierPrefix, queriedAt,
                sqlText, recordTypeName, records);
        }

        protected MetadataModel(
            string toolVersion,
            string queryName,
            string templateName,
            string @namespace,
            string typeName,
            string xmlDoc,
            string identifierPrefix,
            DateTimeOffset queriedAt,
            string sqlText,
            string recordTypeName,
            IList<object> records)
        {
            ToolVersion = toolVersion ?? throw new ArgumentNullException(nameof(toolVersion));
            QueryName = queryName ?? throw new ArgumentNullException(nameof(queryName));
            TemplateName = templateName ?? throw new ArgumentNullException(nameof(templateName));
            Namespace = @namespace ?? throw new ArgumentNullException(nameof(@namespace));
            TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
            XmlDoc = xmlDoc ?? throw new ArgumentNullException(nameof(xmlDoc));
            IdentifierPrefix = identifierPrefix;
            QueriedAt = queriedAt;
            SqlText = sqlText ?? throw new ArgumentNullException(nameof(sqlText));
            RecordTypeName = recordTypeName ?? throw new ArgumentNullException(nameof(recordTypeName));
            Records = records ?? throw new ArgumentNullException(nameof(records)); // TODO: ?? Enumerable.Empty<object>();
        }

        public MetadataModel WithToolVersion(string version)
        {
            // TODO: Test that ToList create a deep copy??? should it?
            return Create(version, QueryName, TemplateName, Namespace, TypeName, XmlDoc, IdentifierPrefix, QueriedAt, SqlText,
                RecordTypeName, Records.ToList());
        }

        public string ToolVersion { get; }

        public string QueryName { get; }

        public string TemplateName { get; }

        public string Namespace { get; }

        public string TypeName { get; }

        public string XmlDoc { get; }

        public string IdentifierPrefix { get; }

        public DateTimeOffset QueriedAt { get; }

        public string QueriedAtText => QueriedAt.ToString("yyyy-MM-ddThh:mm:sszzz");

        public string SqlText { get; }

        /// <summary>
        /// The assembly-qualified name of the record type.
        /// </summary>
        public string RecordTypeName { get; }

        /// <summary>
        /// The <see cref="Type"/> of each record.
        /// </summary>
        public Type RecordType => Type.GetType(RecordTypeName) ?? throw new InvalidOperationException($"The {RecordTypeName} type cannot be found.");

        public IEnumerable<object> Records { get; }

        public bool Equals(MetadataModel? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (!SimpleEquals(other))
            {
                return false;
            }

            //for (int i = 0; i < Records.Count; i++)
            //{
            //    object thisValue = Records[i];
            //    object otherValue = other.Records[i];
            //    // We cannot use equality comparer and use Object.Equals, because of System.Object record type
            //    if (!Equals(thisValue, otherValue))
            //    {
            //        return false;
            //    }
            //}

            using (var thisIter = Records.GetEnumerator())
            using (var otherIter = other.Records.GetEnumerator())
            {
                while (true)
                {
                    bool thisCanRead = thisIter.MoveNext();
                    bool otherCanRead = otherIter.MoveNext();
                    if (thisCanRead != otherCanRead)
                    {
                        return false; // different count
                    }

                    if (!thisCanRead)
                    {
                        break; // both sequences are finished
                    }

                    object thisValue = thisIter.Current;
                    object otherValue = otherIter.Current;
                    // We cannot use equality comparer and use Object.Equals, because of System.Object record type
                    if (!Equals(thisValue, otherValue))
                    {
                        return false; // different values
                    }
                }
            }

            return true;
        }

        protected bool SimpleEquals(MetadataModel other)
        {
            // simple fields comparison (all fields are non-null)
            bool simple = ToolVersion.Equals(other.ToolVersion, StringComparison.OrdinalIgnoreCase) &&
                          QueryName.Equals(other.QueryName, StringComparison.OrdinalIgnoreCase) &&
                          TemplateName.Equals(other.TemplateName, StringComparison.OrdinalIgnoreCase) &&
                          Namespace.Equals(other.Namespace, StringComparison.OrdinalIgnoreCase) &&
                          TypeName.Equals(other.TypeName, StringComparison.OrdinalIgnoreCase) &&
                          XmlDoc.Equals(other.XmlDoc, StringComparison.OrdinalIgnoreCase) &&
                          IdentifierPrefix.Equals(other.IdentifierPrefix, StringComparison.OrdinalIgnoreCase) &&
                          QueriedAt.Equals(other.QueriedAt) &&
                          SqlText.Equals(other.SqlText, StringComparison.OrdinalIgnoreCase);

            return simple;
        }
    }

    /// <summary>
    /// Metadata wrapper used to represent a single code generated type
    /// when the type of records are known --- this is the case in Razor template
    /// files where the @inherits directive will reference this model type.
    /// </summary>
    public class MetadataModel<TRecord> : MetadataModel, IEquatable<MetadataModel<TRecord>>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataModel{TRecord}"/> type.
        /// </summary>
        /// <param name="toolVersion"></param>
        /// <param name="queryName">The name of the cssql file</param>
        /// <param name="templateName">The name of the cshtml file</param>
        /// <param name="namespace">The namespace of the generated type</param>
        /// <param name="typeName">The type name of the generated type.</param>
        /// <param name="xmlDoc">The xml-doc of the generated type.</param>
        /// <param name="identifierPrefix">The prefix to be used on the identifier.</param>
        /// <param name="queriedAt"></param>
        /// <param name="sqlText">The SQL expression that have generated the records.</param>
        /// <param name="recordTypeName">The assembly-qualified name of the record type.</param>
        /// <param name="records">The list of records.</param>
        public MetadataModel(
            string toolVersion,
            string queryName,
            string templateName,
            string @namespace,
            string typeName,
            string xmlDoc,
            string identifierPrefix,
            DateTimeOffset queriedAt,
            string sqlText,
            string recordTypeName,
            IList<object> records)
            : base(toolVersion, queryName, templateName, @namespace, typeName, xmlDoc, identifierPrefix, queriedAt, sqlText, recordTypeName, records)
        {
        }

        public new IEnumerable<TRecord> Records => base.Records.Cast<TRecord>(); // NOTE: det er det eneste vigtige!!!!!

        public bool Equals(MetadataModel<TRecord>? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (!SimpleEquals(other))
            {
                return false;
            }

            //for (int i = 0; i < Records.Count; i++)
            //{
            //    TRecord thisValue = Records[i];
            //    TRecord otherValue = other.Records[i];
            //    if (!EqualityComparer<TRecord>.Default.Equals(thisValue, otherValue))
            //    {
            //        return false;
            //    }
            //}

            using (var thisIter = Records.GetEnumerator())
            using (var otherIter = other.Records.GetEnumerator())
            {
                while (true)
                {
                    bool thisCanRead = thisIter.MoveNext();
                    bool otherCanRead = otherIter.MoveNext();
                    if (thisCanRead != otherCanRead)
                    {
                        return false; // different count
                    }

                    if (!thisCanRead)
                    {
                        break; // both sequences are finished
                    }

                    TRecord thisValue = thisIter.Current;
                    TRecord otherValue = otherIter.Current;
                    if (!EqualityComparer<TRecord>.Default.Equals(thisValue, otherValue))
                    {
                        return false; // different values
                    }
                }
            }

            return true;
        }
    }
}
