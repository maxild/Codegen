using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Codegen.Library;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Data.SqlClient;

namespace Codegen.Database.CLI
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandLineApplication();

            _ = app.HelpOption();

            var optionName =
                app.Option("--name <NAME>", "Required. The name of the model/data to load.",
                    CommandOptionType.SingleValue);
            //.IsRequired();

            var optionSqlDir =
                app.Option("--sqlDir <SQLDIR>",
                    "Required. Path to an existing directory containing a file '<NAME>.sql' containing the SQL query text.",
                    CommandOptionType.SingleValue)
                        //.IsRequired()
                        .Accepts(v => v.ExistingDirectory());

            var optionOutDir =
                app.Option("--outDir <OUTDIR>",
                    "Required. Path to a directory where the model/data will be saved. If the directory does not exist, it will be created.",
                    CommandOptionType.SingleValue)
                        //.IsRequired()
                        .Accepts(v => v.LegalFilePath());

            var optionVerbose =
                app.Option("-v|--verbose", "Verbose", CommandOptionType.NoValue);

            var optionInfo = app.Option("--info", "Display tool information.", CommandOptionType.NoValue);

            _ = app.VersionOption("--version", () => Git.CurrentVersion.Version);

            app.ExtendedHelpText =
                "This program will execute the SQL script referenced by the <SQL_PATH> and save the data locally to a file referenced by the <OUT> path.";

            app.OnExecuteAsync(async cancellationToken =>
            {
                // TODO: Had to uncomment IsRequired inorder for --info to work
                if (optionInfo.HasValue())
                {
                    Console.Write(Git.CurrentVersion.ToInfoString("Codegen Data Tool (cgdata):"));
                    return 0;
                }

                bool verbose = optionVerbose.HasValue();
                void WriteLineVerbose(string msg)
                {
                    if (verbose)
                        Console.WriteLine(msg);
                }

                string name = optionName.Value() ?? throw new InvalidOperationException($"The required {optionName.LongName} is missing");

                // TODO: Is "." a correct default, or should raise error
                var cssqlPath = Path.Combine(optionSqlDir.Value() ?? ".", name + ".cssql");
                WriteLineVerbose($"Reading file {cssqlPath} ...");
                var cssqlText = await File.ReadAllTextAsync(cssqlPath, cancellationToken);
                WriteLineVerbose("Reading file completed.");

                // ------------------------------------------
                // TODO: Create type for parsing cssql files
                // ------------------------------------------

                const string SECTION_SEP = "###";

                var segments = cssqlText.Split(SECTION_SEP);

                if (segments.Length != 3)
                {
                    Console.WriteLine(
                        $"The {cssqlPath} file does not a contain 3 regions separated by 2 ### tokens");
                    return 1;
                }

                //
                // Segment 0: cg directives
                //

                // CG directives
                const string CG_NAMESPACE_DIRECTIVE = "@cg-Namespace";
                const string CG_TYPENAME_DIRECTIVE = "@cg-TypeName";
                const string CG_XMLDOC_DIRECTIVE = "@cg-XmlDoc";
                const string CG_ID_PREFIX = "@cg-IdentifierPrefix";
                const string CG_TEMPLATE_DIRECTIVE = "@cg-Template";

                string? cgNamespace = null, cgTypeName = null, cgXmlDoc = null, cgIdPrefix = null, cgTemplate = null;
                using (var sr = new StringReader(segments[0]))
                {
                    string? line;
                    while ((line = await sr.ReadLineAsync()) is not null)
                    {
                        if (line.StartsWith(CG_NAMESPACE_DIRECTIVE))
                        {
                            cgNamespace = line[CG_NAMESPACE_DIRECTIVE.Length..].Trim();
                        }
                        if (line.StartsWith(CG_TYPENAME_DIRECTIVE))
                        {
                            cgTypeName = line[CG_TYPENAME_DIRECTIVE.Length..].Trim();
                        }
                        if (line.StartsWith(CG_XMLDOC_DIRECTIVE))
                        {
                            cgXmlDoc = line[CG_XMLDOC_DIRECTIVE.Length..].Trim();
                        }
                        if (line.StartsWith(CG_ID_PREFIX))
                        {
                            cgIdPrefix = line[CG_ID_PREFIX.Length..].Trim();
                        }
                        if (line.StartsWith(CG_TEMPLATE_DIRECTIVE))
                        {
                            cgTemplate = line[CG_TEMPLATE_DIRECTIVE.Length..].Trim();
                        }
                    }
                }

                //
                // Segment 1: SQL expression
                //

                var sqlText = segments[1].Trim();

                //
                // Segment 2: CSharp expression
                //

                // CSharp directives
                //const string REFERENCE_DIRECTIVE = "@reference";
                //const string USING_DIRECTIVE = "@using";

                //var references = new List<string>();
                string lambda = segments[2].Trim();

                if (verbose)
                {
                    Console.WriteLine("The parsed cssql file:");
                    Console.WriteLine("");
                    Console.WriteLine($"{CG_NAMESPACE_DIRECTIVE} {cgNamespace}");
                    Console.WriteLine($"{CG_TYPENAME_DIRECTIVE} {cgTypeName}");
                    Console.WriteLine($"{CG_XMLDOC_DIRECTIVE} {cgXmlDoc}");
                    Console.WriteLine($"{CG_ID_PREFIX} {cgIdPrefix}");
                    Console.WriteLine($"{CG_TEMPLATE_DIRECTIVE} {cgTemplate}");
                    Console.WriteLine(SECTION_SEP);
                    Console.WriteLine(sqlText);
                    Console.WriteLine(SECTION_SEP);
                    Console.WriteLine(lambda);
                    Console.WriteLine("");
                }

                //
                // Compile the C# expression
                //

                // Note: Namespaces must be added to C# expression section using plain vanilla C# ('using System;')
                //       We could add default namespace imports...
                var options = ScriptOptions.Default
                    .WithReferences(GetMetadataReferences()) // totally overkill (need #r/@reference support)
                    .WithMetadataResolver(ScriptMetadataResolver.Default);

                WriteLineVerbose("Evaluating C# expression");

                // NOTE: recordType is not known to cgdata program
                Func<IDataReader, object> factoryFunc =
                    await CSharpScript.EvaluateAsync<Func<IDataReader, object>>(
                        lambda,
                        options,
                        cancellationToken: cancellationToken);

                WriteLineVerbose("Evaluating C# expression completed.");

                //
                // Execute the SQL expression (and use the C# expression)
                //

                WriteLineVerbose("Performing query");

                // NOTE: cgdata does not no about concrete types of records...just a list of objects (anything)
                List<object> records = SqlHelper.ExecuteProcedureReturnList(
                    SqlConfig.GetDefaultConnectionString(), // TODO: Configurable using --db-server,--db-name
                    sqlText,
                    factoryFunc);

                string? recordType = records.Count > 0 ? records[0].GetType().AssemblyQualifiedName : null;

                WriteLineVerbose("Performing query completed.");

                //
                // Save the result
                //

                string dir = optionOutDir.Value() ?? throw new InvalidOperationException($"The required {optionOutDir.LongName} is missing.");
                _ = Directory.CreateDirectory(dir); // Ensure directories are created
                WriteLineVerbose($"Writing {name} model/data to dir '{dir}'.");
                var metadata = MetadataModel.Create(
                    toolVersion: Git.CurrentVersion.Version,
                    queryName: name,
                    templateName: cgTemplate ?? throw new InvalidOperationException($"The {CG_TEMPLATE_DIRECTIVE} directive is missing."),
                    @namespace: cgNamespace ?? throw new InvalidOperationException($"The {CG_NAMESPACE_DIRECTIVE} directive is missing."),
                    typeName: cgTypeName ?? throw new InvalidOperationException($"The {CG_TYPENAME_DIRECTIVE} directive is missing."),
                    xmlDoc: cgXmlDoc ?? throw new InvalidOperationException($"The {CG_XMLDOC_DIRECTIVE} directive is missing."),
                    identifierPrefix: cgIdPrefix ?? string.Empty,
                    queriedAt: DateTimeOffset.Now, // TODO: Not pure
                    sqlText: sqlText,
                    recordType ?? throw new InvalidOperationException("The (runtime) recordType could not be resolved, because en empty recordset was received."),
                    records);
                MetadataModelUtils.WriteFile(dir, name, metadata);
                WriteLineVerbose($"Writing '{MetadataModelUtils.ResolvePath(dir, name)}' completed.");

                return 0;
            });

            return app.Execute(args);
        }

        // TODO: Hvad med .deps.json file
        // TODO: Hvad med #r directive
        private static IEnumerable<MetadataReference> GetMetadataReferences()
        {
            yield return MetadataReference.CreateFromFile(typeof(SqlDataReader).Assembly.Location);   // System.Data.SqlClient.dll

            //yield return MetadataReference.CreateFromFile(typeof(Action).Assembly.Location); // mscorlib or System.Private.Core
            //yield return MetadataReference.CreateFromFile(typeof(IQueryable).Assembly.Location); // System.Core or System.Linq.Expressions
            //yield return MetadataReference.CreateFromFile(typeof(Uri).Assembly.Location); // System
            //yield return MetadataReference.CreateFromFile(typeof(System.Xml.XmlReader).Assembly.Location); // System.Xml
            //yield return MetadataReference.CreateFromFile(typeof(System.Xml.Linq.XDocument).Assembly.Location); // System.Xml.Linq
            //yield return MetadataReference.CreateFromFile(typeof(System.Data.DataTable).Assembly.Location); // System.Data

            //var entryAssembly = Assembly.GetEntryAssembly();
            //yield return MetadataReference.CreateFromFile(entryAssembly.Location);

            //foreach (var reference in entryAssembly.GetReferencedAssemblies())
            //{
            //    yield return MetadataReference.CreateFromFile(Assembly.Load(reference).Location);
            //}

            //foreach (var reference in _references)
            //{
            //    yield return MetadataReference.CreateFromFile(reference.Location);
            //}
        }
    }
}
