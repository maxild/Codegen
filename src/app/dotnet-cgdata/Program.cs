﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Codegen.Library;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Codegen.Database.CLI;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var optionName = new Option<string>(
            name: "--name",
            description:
            "The name of the cssql file to load (i.e. <name>.cssql), and the name of the data file to save (i.e. <name>.json).")
        {
            IsRequired = true
        };

        var optionSqlDir = new Option<DirectoryInfo>(
                name: "--sqlDir",
                description:
                $"Path to an existing directory containing the <{optionName.Name}>.cssql file.")
        {
            IsRequired = true
        }.ExistingOnly();

        var optionOutDir = new Option<DirectoryInfo>(
            name: "--outDir",
            description:
            $"Path to an output directory where the <{optionName.Name}>.json output file will be saved. If the directory does not exist, it will be created.")
        {
            IsRequired = true
        }.LegalFilePathsOnly();

        var optionDbServer = new Option<string>(
            name: "--db-server",
            description: "The name or network address of the SQL Server instance to connect to.")
        {
            IsRequired = true
        };

        var optionDbName = new Option<string>(
            name: "--db-name",
            description: "The name of the database.")
        {
            IsRequired = true
        };

        var optionVerbose = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Verbose logging to terminal.");

        // TODO: No value
        // var optionInfo = new Option<bool>(name: "--info", description: "Display tool information.");

        // TODO: Version
        //_ = app.VersionOption("--version", () => Git.CurrentVersion.Version);

        var rootCommand =
            new RootCommand(
                "This program will execute a SQL script (defined in a custom cssql file) and save the data to a JSON file.")
            {
                optionName,
                optionSqlDir,
                optionOutDir,
                optionDbServer,
                optionDbName,
                optionVerbose
            };

        rootCommand.SetHandler(async (name, sqlDir, outDir, dbServer, dbName, verbose, context) =>
            {
                await DoMain(name, sqlDir, outDir, dbServer, dbName, verbose, context);
            },
            optionName, optionSqlDir, optionOutDir, optionDbServer, optionDbName, optionVerbose,
            Bind.FromServiceProvider<InvocationContext>());

        var parser = new CommandLineBuilder(rootCommand)
            .UseDefaults()
            // .UseVersionOption() // This one is part of UseDefaults, and it adds middleware to short-circuit the root command handler
            .Build();

        return await parser.InvokeAsync(args);
    }

    private static async Task DoMain(string name, DirectoryInfo sqlDir, DirectoryInfo outDir, string dbServer,
        string dbName, bool verbose, InvocationContext context)
    {
        CancellationToken cancellationToken = context.GetCancellationToken();

        // TODO: Make --info work (tests...)
        // if (optionInfo.HasValue())
        // {
        //     Console.Write(Git.CurrentVersion.ToInfoString("Codegen Data Tool (cgdata):"));
        //     return 0;
        // }

        void WriteLineVerbose(string msg)
        {
            if (verbose)
                Console.WriteLine(msg);
        }

        var cssqlPath = Path.Combine(sqlDir.FullName, name + ".cssql");
        WriteLineVerbose($"Reading file {cssqlPath} ...");
        var cssqlText = await File.ReadAllTextAsync(cssqlPath, cancellationToken);
        WriteLineVerbose("Reading file completed.");

        const string SECTION_SEP = "###";

        var segments = cssqlText.Split(SECTION_SEP);

        if (segments.Length != 3)
        {
            // TODO: This is not the correct way to report an error
            Console.WriteLine(
                $"The {cssqlPath} file does not a contain 3 regions separated by 2 ### tokens");
            return;
        }

        //
        // Segment 0: cg directives
        //

        // CG directives
        const string CG_NAMESPACE_DIRECTIVE = "@cg-Namespace";
        const string CG_TYPENAME_DIRECTIVE = "@cg-TypeName";
        const string CG_XMLDOC_DIRECTIVE = "@cg-XmlDoc";
        const string CG_ID_PREFIX = "@cg-IdentifierPrefix";
        const string CG_DATABASE_ID_PREFIXES = "@cg-DatabaseIdentifierPrefixes";
        const string CG_TEMPLATE_DIRECTIVE = "@cg-Template";

        string? cgNamespace = null,
            cgTypeName = null,
            cgXmlDoc = null,
            cgIdPrefix = null,
            cgDatabaseIdPrefix = null,
            cgTemplate = null;
        IReadOnlyDictionary<string, int>? cgDatabaseIdPrefixes = null;

        using (var sr = new StringReader(segments[0]))
        {
            while (await sr.ReadLineAsync() is { } line)
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

                if (line.StartsWith(CG_DATABASE_ID_PREFIXES))
                {
                    cgDatabaseIdPrefixes = DatabaseIdentifierPrefixesUtils.Parse(line[CG_DATABASE_ID_PREFIXES.Length..]);
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
            Console.WriteLine($"{CG_DATABASE_ID_PREFIXES} {cgDatabaseIdPrefix}");
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

        // NOTE: cgdata does not know about concrete types of records...just a list of objects (anything)
        List<object> records = SqlHelper.ExecuteProcedureReturnList(
            SqlConfig.GetConnectionString(dbServer, dbName),
            sqlText,
            factoryFunc);

        string? recordType = records.Count > 0 ? records[0].GetType().AssemblyQualifiedName : null;

        WriteLineVerbose("Performing query completed.");

        //
        // Save the result
        //

        string outDirFullPath = outDir.FullName;
        _ = Directory.CreateDirectory(outDirFullPath); // Ensure directories are created
        WriteLineVerbose($"Writing {name} model/data to dir '{outDirFullPath}'.");
        var metadata = MetadataModel.Create(
            queryName: name,
            templateName: cgTemplate ??
                          throw new InvalidOperationException($"The {CG_TEMPLATE_DIRECTIVE} directive is missing."),
            @namespace: cgNamespace ??
                        throw new InvalidOperationException($"The {CG_NAMESPACE_DIRECTIVE} directive is missing."),
            typeName: cgTypeName ??
                      throw new InvalidOperationException($"The {CG_TYPENAME_DIRECTIVE} directive is missing."),
            xmlDoc: cgXmlDoc ?? throw new InvalidOperationException($"The {CG_XMLDOC_DIRECTIVE} directive is missing."),
            identifierPrefix: cgIdPrefix ?? string.Empty,
            databaseIdentifierPrefixes: cgDatabaseIdPrefixes ?? ImmutableDictionary<string, int>.Empty,
            sqlText: sqlText,
            recordType ?? throw new InvalidOperationException(
                "The (runtime) recordType could not be resolved, because en empty recordset was received."),
            records);
        MetadataModelUtils.WriteFile(outDirFullPath, name, metadata);
        WriteLineVerbose($"Writing '{MetadataModelUtils.ResolvePath(outDirFullPath, name)}' completed.");
    }

    // TODO: Hvad med .deps.json file
    // TODO: Hvad med #r directive
    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        yield return
            MetadataReference.CreateFromFile(typeof(SqlDataReader).Assembly.Location); // Microsoft.Data.SqlClient.dll

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

// Beta 4 removed the service provider fallback and therefore led to more ceremony for accessing injected objects.
// This utility class (Bind) is a solution to this problem.
//
// In the beta2 version of SetHandler, parameters of commonly-used types such as ParseResult, CancellationToken,
// and IConsole would be implicitly bound from the service provider. The beta 4 version requires that the
// IValueDescriptor<T> parameter count match the generic type parameter count
// (e.g. public static void SetHandler<T1, T2>(this Command command, Action<T1, T2> handle, IValueDescriptor<T1> symbol1, IValueDescriptor<T2> symbol2).
// This makes it unclear what to pass to get instances of these other types. This can be done by implementing
// a custom BinderBase<T> but this also requires too much ceremony.
//
// See also https://github.com/dotnet/command-line-api/issues/1570#issuecomment-1170100340
//
// testCommand.SetHandler((intOption, boolOption, fileOption, console) => // 👈 parameter types are now inferred
// {
//     // ...
// }, optionInt, optionBool, optionFile, Bind.FromServiceProvider<IConsole>()); // 👈 call service provider binder here
//
// See https://github.com/dotnet/command-line-api/issues/1750#issuecomment-1152707726

internal static class Bind
{
    // No need to implement a custom BinderBase<T>
    public static ServiceProviderBinder<T> FromServiceProvider<T>() => ServiceProviderBinder<T>.Instance;

    internal class ServiceProviderBinder<T> : BinderBase<T>
    {
        public static ServiceProviderBinder<T> Instance { get; } = new();

        protected override T GetBoundValue(BindingContext bindingContext) => (T)bindingContext.GetRequiredService(typeof(T));
    }
}
