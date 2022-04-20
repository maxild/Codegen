using Microsoft.Data.SqlClient;

namespace Codegen.Database.CLI;

// TODO: Use external config or command CLI interface
internal static class SqlConfig
{
    public static string GetDefaultConnectionString()
    {
        var builder = new SqlConnectionStringBuilder
        {
            ["Server"] = "SQL-PDataware-PR",
            ["Initial Catalog"] = "Info",
            ["Integrated Security"] = true,
            // TODO: Undersoeg om SQL Server forces encryption
            // See https://github.com/dotnet/SqlClient/issues/609#issuecomment-645406981
            // Server Certificate validation when TLS encryption is enforced by the target Server
            // (i.e enforce use of SSL for the database connection, in order to bypass walking the
            // certificate chain to validate trust, because this will properly result in a runtime error:
            //    Microsoft.Data.SqlClient.SqlException (0x80131904): A connection was successfully established
            //    with the server, but then an error occurred during the login process:
            //       A connection was successfully established with the server, but then an error occurred during the pre-login handshake.
            //          Certifikatkæden blev udstedt af et nøglecenter, der ikke er tillid til.)
            ["TrustServerCertificate"] = true
        };

        return builder.ConnectionString;
    }
}
