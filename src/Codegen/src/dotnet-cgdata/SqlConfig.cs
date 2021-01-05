using Microsoft.Data.SqlClient;

namespace Codegen.Database.CLI
{
    internal static class SqlConfig
    {
        public static string GetDefaultConnectionString()
        {
            var builder = new SqlConnectionStringBuilder
            {
                ["Server"] = "SQL-PDataware-PR",
                ["Initial Catalog"] = "Info",
                ["Integrated Security"] = true
            };

            return builder.ConnectionString;
        }
    }
}
