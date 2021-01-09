using System;
using System.Collections.Generic;
using System.Linq;
using Codegen.Library;
using Shouldly;
using Xunit;

namespace Codegen.Tests
{
    public class MetadataModelTests
    {
#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
        struct RecordTuple : IEquatable<RecordTuple>
#pragma warning restore CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Remove when .editorconfig have been updated.")]
            public string Key;
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Remove when .editorconfig have neem updated.")]
            public string Value;

#pragma warning disable 659
            public override bool Equals(object? obj)
#pragma warning restore 659
            {
                return obj is RecordTuple recordTuple && Equals(recordTuple);
            }

            public bool Equals(RecordTuple other)
            {
                return string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void RecordsOfObjectsAreDownCastedToItemsOfKeyValuePair()
        {
            // Problem: KeyValuePair values are boxed (bad perf!!!)
            var records = new List<object>
            {
                new KeyValuePair<string, string>("key1", "value1"),
                new KeyValuePair<string, string>("key2", "value2")
            };

            var datePart = new DateTime(2019, 3, 8);
            var timePart = new TimeSpan();

            var metadata = MetadataModel.Create<KeyValuePair<string, string>>(
                toolVersion: "0.1.0",
                queryName: "betalingstype",
                templateName: "dataenum",
                @namespace: "Brf.Domus.Models",
                typeName: "Betalingstype",
                xmlDoc: "Betalingstype er en type fra Domus",
                queriedAt: new DateTimeOffset(datePart, timePart),
                sqlText: "SELECT * FROM SOME_TABLE",
                recordTypeName: typeof(KeyValuePair<string, string>).AssemblyQualifiedName!,
                records);

            metadata.Records.ElementAt(0).ShouldBe(new KeyValuePair<string, string>("key1", "value1"));
            metadata.Records.ElementAt(1).ShouldBe(new KeyValuePair<string, string>("key2", "value2"));
        }

        [Fact]
        public void RecordsOfObjectsAreDownCastedToItemsOfRecordTuple()
        {
            // Problem: RecordTuple values are boxed (bad perf!!!)
            var records = new List<object>
            {
                new RecordTuple { Key = "key1", Value = "value1"},
                new RecordTuple { Key = "key2", Value = "value2"}
            };

            var datePart = new DateTime(2019, 3, 8);
            var timePart = new TimeSpan();

            var metadata = MetadataModel.Create<RecordTuple>(
                toolVersion: "0.1.0",
                queryName: "betalingstype",
                templateName: "dataenum",
                @namespace: "Brf.Domus.Models",
                typeName: "Betalingstype",
                xmlDoc: "Betalingstype er en type fra Domus",
                queriedAt: new DateTimeOffset(datePart, timePart),
                sqlText: "SELECT * FROM SOME_TABLE",
                recordTypeName: typeof(RecordTuple).AssemblyQualifiedName!,
                records);

            metadata.Records.ElementAt(0).ShouldBe(new RecordTuple { Key = "key1", Value = "value1" });
            metadata.Records.ElementAt(1).ShouldBe(new RecordTuple { Key = "key2", Value = "value2" });
        }

        [Fact]
        public void IfRecordTypesInSqlLambdaDoesntMatchRecordTypeInTemplateBase_Then_ThrowsInvalidCastException()
        {
            // Problem: RecordTuple values are boxed (bad perf!!!)
            var records = new List<object>
            {
                new RecordTuple { Key = "key1", Value = "value1"},
                new RecordTuple { Key = "key2", Value = "value2"}
            };

            var datePart = new DateTime(2019, 3, 8);
            var timePart = new TimeSpan();

            var metadata = MetadataModel.Create<KeyValuePair<string, string>>(
                toolVersion: "0.1.0",
                queryName: "betalingstype",
                templateName: "dataenum",
                @namespace: "Brf.Domus.Models",
                typeName: "Betalingstype",
                xmlDoc: "Betalingstype er en type fra Domus",
                queriedAt: new DateTimeOffset(datePart, timePart),
                sqlText: "SELECT * FROM SOME_TABLE",
                recordTypeName: typeof(RecordTuple).AssemblyQualifiedName!,
                records);

            Assert.Throws<InvalidCastException>(() =>
                metadata.Records.ElementAt(0).ShouldBe(new KeyValuePair<string, string>("key1", "value1")));

            // TODO: message is different on .NET Framework and .NET Core
            //.Message.ShouldBe("Unable to cast object of type 'RecordTuple' to type 'System.Collections.Generic.KeyValuePair`2[System.String,System.String]'.");
        }

        [Fact]
        public void Serialize()
        {
            // Problem: RecordTuple values are boxed (bad perf!!!)
            var records = new List<object>
                {
                    new RecordTuple {Key = "key1", Value = "value1"},
                    new RecordTuple {Key = "key2", Value = "value2"}
                };

            var datePart = new DateTime(2019, 3, 8, 12, 24, 36);
            var offset = new TimeSpan(1, 0, 0);

            MetadataModel model1 = MetadataModel.Create(
                toolVersion: "0.1.0",
                queryName: "betalingstype",
                templateName: "dataenum",
                @namespace: "Brf.Domus.Models",
                typeName: "Betalingstype",
                xmlDoc: "Betalingstype er en type fra Domus.",
                queriedAt: new DateTimeOffset(datePart, offset),
                sqlText: "SELECT * FROM SOME_TABLE",
                recordTypeName: typeof(RecordTuple).AssemblyQualifiedName!,
                records);

            MetadataModel<RecordTuple> model2 = MetadataModel.Create<RecordTuple>(
                toolVersion: "0.1.0",
                queryName: "betalingstype",
                templateName: "dataenum",
                @namespace: "Brf.Domus.Models",
                typeName: "Betalingstype",
                xmlDoc: "Betalingstype er en type fra Domus.",
                queriedAt: new DateTimeOffset(datePart, offset),
                sqlText: "SELECT * FROM SOME_TABLE",
                recordTypeName: typeof(RecordTuple).AssemblyQualifiedName!,
                records);

            var json1 = MetadataModel.Serialize(model1);
            var json2 = MetadataModel.Serialize(model2);

            string expectedJson = string.Join(Environment.NewLine, new[]
            {
                "{",
                @"  ""ToolVersion"": ""0.1.0"",",
                @"  ""QueryName"": ""betalingstype"",",
                @"  ""TemplateName"": ""dataenum"",",
                @"  ""Namespace"": ""Brf.Domus.Models"",",
                @"  ""TypeName"": ""Betalingstype"",",
                @"  ""XmlDoc"": ""Betalingstype er en type fra Domus."",",
                @"  ""QueriedAt"": ""2019-03-08T12:24:36+01:00"",",
                @"  ""SqlText"": ""SELECT * FROM SOME_TABLE"",",
                @"  ""RecordTypeName"": ""Codegen.Tests.MetadataModelTests+RecordTuple, Codegen.Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"",",
                @"  ""Records"": [",
                @"    {",
                @"      ""Key"": ""key1"",",
                @"      ""Value"": ""value1""",
                @"    },",
                @"    {",
                @"      ""Key"": ""key2"",",
                @"      ""Value"": ""value2""",
                @"    }",
                "  ]",
                "}"
            });
            json1.ShouldBe(expectedJson);
            json2.ShouldBe(expectedJson);
        }

        [Fact]
        public void Deserialize()
        {
            string json = @"{
  ""ToolVersion"": ""0.1.0"",
  ""QueryName"": ""betalingstype"",
  ""TemplateName"": ""dataenum"",
  ""Namespace"": ""Brf.Domus.Models"",
  ""TypeName"": ""Betalingstype"",
  ""XmlDoc"": ""Betalingstype er en type fra Domus."",
  ""QueriedAt"": ""2019-03-08T12:24:36+01:00"",
  ""SqlText"": ""SELECT * FROM SOME_TABLE"",
  ""RecordTypeName"": ""Codegen.Tests.MetadataModelTests+RecordTuple, Codegen.Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"",
  ""Records"": [
    {
      ""Key"": ""key1"",
      ""Value"": ""value1""
    },
    {
      ""Key"": ""key2"",
      ""Value"": ""value2""
    }
  ]
}";
            MetadataModel model1 = MetadataModel.Deserialize(json);
            MetadataModel<RecordTuple> model2 = MetadataModel.Deserialize<RecordTuple>(json);
            MetadataModel<RecordTuple> model3 = (MetadataModel<RecordTuple>)MetadataModel.Deserialize(json, typeof(RecordTuple));

            model3.ToolVersion.ShouldBe("0.1.0");
            model1.QueryName.ShouldBe("betalingstype");
            model1.TemplateName.ShouldBe("dataenum");
            model1.Namespace.ShouldBe("Brf.Domus.Models");
            model1.TypeName.ShouldBe("Betalingstype");
            model1.XmlDoc.ShouldBe("Betalingstype er en type fra Domus.");
            model1.QueriedAt.ShouldBe(new DateTimeOffset(2019, 3, 8, 12, 24, 36, new TimeSpan(1, 0, 0)));
            model1.SqlText.ShouldBe("SELECT * FROM SOME_TABLE");
            model1.RecordTypeName.ShouldBe("Codegen.Tests.MetadataModelTests+RecordTuple, Codegen.Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            model1.RecordType.ShouldBe(typeof(RecordTuple));
            model1.Records.ElementAt(0).ShouldBe(new RecordTuple { Key = "key1", Value = "value1" });
            model1.Records.ElementAt(1).ShouldBe(new RecordTuple { Key = "key2", Value = "value2" });
            model1.Records.Count().ShouldBe(2);

            model2.ShouldBe(model1);
            model3.ShouldBe(model1);
        }

        [Fact]
        public void SerializeAndDeserializeWithTypedRecords()
        {
            // Problem: RecordTuple values are boxed (bad perf!!!)
            var records = new List<object>
            {
                new RecordTuple { Key = "key1", Value = "value1"},
                new RecordTuple { Key = "key2", Value = "value2"}
            };

            var datePart = new DateTime(2019, 3, 8);
            var timePart = new TimeSpan();

            MetadataModel<RecordTuple> model = MetadataModel.Create<RecordTuple>(
                toolVersion: "0.1.0",
                queryName: "betalingstype",
                templateName: "dataenum",
                @namespace: "Brf.Domus.Models",
                typeName: "Betalingstype",
                xmlDoc: "Betalingstype er en type fra Domus",
                queriedAt: new DateTimeOffset(datePart, timePart),
                sqlText: "SELECT * FROM SOME_TABLE",
                recordTypeName: typeof(RecordTuple).AssemblyQualifiedName!,
                records);

            var json = MetadataModel.Serialize(model);

            MetadataModel<RecordTuple> copyOfModel = MetadataModel.Deserialize<RecordTuple>(json);

            copyOfModel.ShouldBe(model);
        }
    }
}
