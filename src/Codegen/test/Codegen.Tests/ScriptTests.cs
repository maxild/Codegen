using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using Codegen.Library;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Data.SqlClient;
using Shouldly;
using Xunit;

namespace Codegen.Tests
{
    public class ScriptTests
    {
        [Fact]
        public async void CompileLambda()
        {
            var options = ScriptOptions.Default
                .WithReferences(MetadataReference.CreateFromFile(typeof(SqlDataReader).Assembly.Location))
                .WithImports("System.Collections.Generic")
                .WithMetadataResolver(ScriptMetadataResolver.Default);

            Func<IDataReader, object> factoryFunc =
                await CSharpScript.EvaluateAsync<Func<IDataReader, object>>(
                    @"reader => new KeyValuePair<string, string>(reader[0].ToString(), reader[1].ToString())",
                    options);

            // Mocking or faking an IDataReader is pretty cumbersome. You are forced
            // to either mock every successive call to the reader's Read(), plus Get*()
            // and indexer data accessor methods.
            // Or we can set up a DataTable (complete with dummy rows) and call its
            // CreateDataReader() method. That is easier, and doesnt require us to depend
            // on Moq, Rhino or other Mocking library...
            IDataReader dataReader = CreateDataReaderStub(new []
            {
                new { Key = "key1", Value = "value1" },
                new { Key = "key2", Value = "value2" }
            });

            object records = SqlHelper.ToList(dataReader, factoryFunc);

            var recordsAsList = records.ShouldBeOfType<List<object>>();
            recordsAsList.Count.ShouldBe(2);
            recordsAsList[0].ShouldBe(new KeyValuePair<string,string>("key1", "value1"));
            recordsAsList[1].ShouldBe(new KeyValuePair<string,string>("key2", "value2"));
        }

        private static IDataReader CreateDataReaderStub<T>(IEnumerable<T> records)
            where T : notnull
        {
            var dataTable = new DataTable();
            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(typeof(T));

            foreach (PropertyDescriptor property in properties)
            {
                var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                dataTable.Columns.Add(property.Name, type);
            }

            object?[] propertyValues = new object?[properties.Count];
            foreach (T item in records)
            {
                for (int i = 0; i < propertyValues.Length; i++)
                {
                    propertyValues[i] = properties[i].GetValue(item);
                }
                dataTable.Rows.Add(propertyValues);
            }

            return dataTable.CreateDataReader();
        }
    }
}
