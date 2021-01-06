using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Codegen.Library
{
    // TODO: Move to dotnet-cgdata project
    public static class SqlHelper
    {
        public static List<object> ExecuteProcedureReturnList(
            string connString,
            string sqlText,
            Func<IDataReader, object> factoryFunc)
        {
            using (SqlConnection sqlConnection = new(connString))
            {
                sqlConnection.Open();

                using (SqlCommand command = sqlConnection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = sqlText;
                    var dataReader = command.ExecuteReader();

                    return ToList(dataReader, factoryFunc);
                }
            }
        }

        public static List<object> ToList(IDataReader dataReader, Func<IDataReader, object> factoryFunc)
        {
            var result = new List<object>();
            while (dataReader.Read())
            {
                object dataRow = factoryFunc(dataReader);
                result.Add(dataRow);
            }
            return result;
        }

        // TODO: Should be deleted
        //public static List<object> ExecuteProcedureReturnList<TRecord>(
        //    string connString,
        //    string sqlText,
        //    Func<SqlDataReader, TRecord> factoryFunc)
        //{
        //    var result = new List<object>();

        //    using (SqlConnection sqlConnection = new SqlConnection(connString))
        //    {
        //        sqlConnection.Open();

        //        using (SqlCommand command = sqlConnection.CreateCommand())
        //        {
        //            command.CommandType = System.Data.CommandType.Text;
        //            command.CommandText = sqlText;
        //            SqlDataReader dataReader = command.ExecuteReader();

        //            while (dataReader.Read())
        //            {
        //                object dataRow = factoryFunc(dataReader);
        //                result.Add(dataRow);
        //            }
        //        }
        //    }

        //    return result;
        //}
    }
}
