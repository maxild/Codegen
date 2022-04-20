using Microsoft.Data.SqlClient;

namespace Codegen.Database.CLI;

internal static class SqlConfig
{
    public static string GetConnectionString(string server, string database)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            IntegratedSecurity = true,
            TrustServerCertificate = true
        };

        return builder.ConnectionString;
    }
}
