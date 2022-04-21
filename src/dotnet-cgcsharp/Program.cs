using System;
using System.CommandLine;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codegen.Library;
using CSharpRazor;

namespace Codegen.CSharp.CLI;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var optionName = new Option<string>(
            name: "--name",
            description: "The name of the data file to load (i.e. <name>.json).")
        {
            IsRequired = true
        };

        var optionDataDir = new Option<DirectoryInfo>(
                name: "--dataDir",
                description: "Path to the directory containing the data file.")
        {
            IsRequired = true
        }.ExistingOnly();

        var optionOutDir = new Option<DirectoryInfo>(
                name: "--outDir",
                description: "The output directory, where the C# file (<name>.generated.cs) is written.")
        {
            IsRequired = true
        }.ExistingOnly();

        var optionTemplate = new Option<FileSystemInfo>(
            name: "--template",
            description:
            "Either a directory, where the (@cg-Template referenced) Razor template file can be found, or a path to a Razor template file, if the @cg-Template value is to be ignored.")
        {
            IsRequired = true
        }.ExistingOnly();

        var optionDiagDir = new Option<DirectoryInfo?>(
            name: "--diagDir",
            description: "An optional path to the directory, where the diagnostic (<name>.g.cshtml.cs) file will be written. If the directory does not exist, it is created.");

        var optionVerbose = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Verbose logging to terminal.");

        // TODO: No value
        // var optionInfo = new Option<bool>(name: "--info", description: "Display tool information.");

        // TODO: Version
        //_ = app.VersionOption("--version", () => Git.CurrentVersion.Version);

        var rootCommand =
            new RootCommand(
                "Compile and Render a (Razor based) template that will convert the data in the file (<name>.json) to a C# source file (<name>.generated.cs).")
            {
                optionName,
                optionDataDir,
                optionOutDir,
                optionTemplate,
                optionDiagDir,
                optionVerbose
            };

        rootCommand.SetHandler(async (string name, DirectoryInfo dataDir, DirectoryInfo outDir, FileSystemInfo template,
                DirectoryInfo? diagDir, bool verbose, CancellationToken cancellationToken) =>
            {
                await DoMain(name, dataDir, outDir, template, diagDir, verbose, cancellationToken);
            },
            optionName, optionDataDir, optionOutDir, optionTemplate, optionDiagDir, optionVerbose);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task DoMain(
        string name,
        DirectoryInfo dataDir,
        DirectoryInfo outDir,
        FileSystemInfo template,
        DirectoryInfo? diagDir,
        bool verbose,
        CancellationToken cancellationToken)
    {
        // TODO: Make --info work (tests...)
        // if (optionInfo.HasValue())
        // {
        //     Console.Write(Git.CurrentVersion.ToInfoString("Codegen CSharp Tool (cgcsharp):"));
        //     return 0;
        // }

        void WriteLineVerbose(string msg)
        {
            if (verbose)
                Console.WriteLine(msg);
        }

        WriteLineVerbose($"Reading '{name}.json' data from dir '{dataDir.FullName}'.");
        // NOTE: cgcsharp does not know about concrete types of records...just a list of objects (anything)
        MetadataModel metadata = MetadataModelUtils
            .ReadFile(dataDir.FullName, name)
            .WithToolVersion(Git.CurrentVersion.Version);
        WriteLineVerbose($"Reading '{name}.json' data from dir '{dataDir.FullName}' completed.");

        // Resolve template directory and filename
        string rootDir, templateFilename;
        if (template is DirectoryInfo templateDir)
        {
            // The --template value is a path to the template directory
            rootDir = templateDir.FullName;
            // use the name of the cshtml file
            templateFilename = Path.HasExtension(metadata.TemplateName)
                ? metadata.TemplateName
                : $"{metadata.TemplateName}.cshtml";
        }
        else if (template is FileInfo templateFile)
        {
            // The --template value is a path to a template file
            templateFilename = templateFile.Name;
            rootDir = templateFile.DirectoryName ??
                      throw new InvalidOperationException(
                          "The template directory cannot be null, empty, or a system root.");
        }
        else
        {
            // TODO: How do we report back error
            throw new InvalidOperationException($"The --template={template} value does not exist.");
        }

        WriteLineVerbose($"Initializing Razor engine with root directory : '{rootDir}");

        var engine = new RazorEngineBuilder()
            .SetRootDirectory(rootDir)
            .Build();

        WriteLineVerbose($"Rendering '{templateFilename}'.");

        string? diagPath = null;
        if (diagDir is not null)
        {
            string diagDirFullPath = diagDir.FullName;
            string diagFilename = Path.GetFileNameWithoutExtension(templateFilename) + ".g.cshtml.cs";
            diagPath = Path.Combine(diagDirFullPath, diagFilename);
            _ = Directory.CreateDirectory(diagDirFullPath);
        }

        // TODO: Investigate CR vs CRLF line-endings of SourceCSharpCode (*.g.cshtml.cs) and Content (*.generated.cs)
        string? razorSource = null;
        RenderResult? renderResult;
        try
        {
            renderResult = await engine.RenderTemplateAsync(templateFilename, model: metadata,
                onRazorCompilerOutput: source => razorSource = source);
            WriteLineVerbose($"Rendering '{templateFilename}' completed.");
        }
        finally
        {
            // Save <templateName>.g.cshtml.cs
            if (!string.IsNullOrEmpty(diagPath) && !string.IsNullOrEmpty(razorSource))
            {
                await File.WriteAllTextAsync(diagPath, razorSource, Encoding.UTF8, cancellationToken);
            }
        }

        // Save the generated C# file
        string csharpFilename = $"{name}.generated.cs";
        string csharpPath = Path.Combine(outDir.FullName, csharpFilename);
        WriteLineVerbose($"Writing '{csharpFilename}' to '{outDir.FullName}'.");
        await File.WriteAllTextAsync(csharpPath, renderResult.Content, Encoding.UTF8, cancellationToken);
        WriteLineVerbose($"Writing '{csharpFilename}' to '{outDir.FullName}' completed.");
    }
}
