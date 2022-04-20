using System;
using System.IO;
using System.Text;
using Codegen.Library;
using CSharpRazor;
using McMaster.Extensions.CommandLineUtils;

#if !GIT_VERSION_INFO_EXISTS
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Codegen
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public static class Git
    {
        private static readonly Lazy<GitVersion> s_version = new(()
            => new GitVersion(
                "0.0.0-missing.commandline.build",
                "0.0.0-missing.commandline.build",
                "local",
                "0000000000000000000000000000000000000000",
                "1/1/0000 00:00:00 PM +00:00",
                "unknown-branch"));
        public static GitVersion CurrentVersion => s_version.Value;
    }
}
#endif

namespace Codegen.CSharp.CLI
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandLineApplication();

            _ = app.HelpOption();

            var optionName =
                app.Option("--name <NAME>", "Required. The name of the model/data to load and turn into csharp.",
                    CommandOptionType.SingleValue);
            //.IsRequired();

            var optionDataDir =
                app.Option("--dataDir <DATADIR>", "Required. The path to the directory containing data files.",
                    CommandOptionType.SingleValue)
                        //.IsRequired()
                        .Accepts(v => v.ExistingDirectory());

            var optionTemplate =
                app.Option("--template <PATH>", "Required. The path to the Razor template directory, where the @cg-Template filename is to be found. If the path is to a file, then that file is used, and the @cg-Template value is ignored.",
                    CommandOptionType.SingleValue)
                        //.IsRequired()
                        .Accepts(v => v.ExistingFileOrDirectory());

            var optionDiagDir =
                app.Option("--diagDir <DIAGDIR>",
                    "Optional. The path to the directory, where the *.g.cshtml.cs file will be written.",
                    CommandOptionType.SingleValue);

            var optionOutDir =
                app.Option("--outDir <OUTDIR>", "Output filename", CommandOptionType.SingleValue);

            var optionVerbose = app.Option("-v|--verbose", "Verbose", CommandOptionType.NoValue);

            var optionInfo = app.Option("--info", "Display tool information.", CommandOptionType.NoValue);

            _ = app.VersionOption("--version", () => Git.CurrentVersion.Version);

            app.OnExecuteAsync(async cancellationToken =>
            {
                // TODO: Had to uncomment IsRequired inorder for --info to work
                if (optionInfo.HasValue())
                {
                    Console.Write(Git.CurrentVersion.ToInfoString("Codegen CSharp Tool (cgcsharp):"));
                    return 0;
                }

                bool verbose = optionVerbose.HasValue();
                void WriteLineVerbose(string msg)
                {
                    if (verbose)
                        Console.WriteLine(msg);
                }

                string name = optionName.Value() ?? throw new InvalidOperationException($"The required {optionName.LongName} is missing.");

                WriteLineVerbose($"Reading '{name}.json' data from dir '{optionDataDir.Value()}'.");
                // NOTE: cgcsharp does not know about concrete types of records...just a list of objects (anything)
                MetadataModel metadata = MetadataModelUtils
                    .ReadFile(
                        optionDataDir.Value() ??
                            throw new InvalidOperationException($"The required {optionDataDir.LongName} is missing."),
                        name)
                    .WithToolVersion(Git.CurrentVersion.Version);
                WriteLineVerbose($"Reading '{name}.json' data from dir '{optionDataDir.Value()}' completed.");

                // Resolve template directory and filename
                string templatePath = optionTemplate.Value() ?? throw new InvalidOperationException($"The required {optionTemplate.LongName} is missing.");
                string rootDir, templateFilename;
                if (Directory.Exists(templatePath))
                {
                    // use filename from @cg-Template directive
                    rootDir = templatePath;
                    templateFilename = Path.HasExtension(metadata.TemplateName)
                        ? metadata.TemplateName
                        : $"{metadata.TemplateName}.cshtml";

                }
                else if (File.Exists(templatePath))
                {
                    // Override the @cg-Template directive with the commandline --template provided filename
                    // The templateFilename is null if templatePath is null (The API have been annotated with [return: NotNullIfNotNull("path")]).
                    templateFilename = Path.GetFileName(templatePath);
                    // templateDir is null if templatePath is null, empty, or a root (such as "\", "C:", or "\\server\share").
                    string? templateDir = Path.GetDirectoryName(templatePath);
                    rootDir = templateDir ?? throw new InvalidOperationException($"The template directory cannot be null, empty, or a system root.");
                }
                else
                {
                    throw new InvalidOperationException($"The {optionTemplate.LongName} value does not exist.");
                }

                WriteLineVerbose($"Initializing Razor engine with root directory : '{rootDir}");

                var engine = new RazorEngineBuilder()
                    .SetRootDirectory(rootDir)
                    .Build();

                WriteLineVerbose($"Rendering '{templateFilename}'.");

                string? diagPath = null;
                if (optionDiagDir.HasValue())
                {
                    string diagDir = optionDiagDir.Value() ?? throw new InvalidOperationException($"The required {optionDataDir.LongName} is missing.");
                    string diagFilename = Path.GetFileNameWithoutExtension(templateFilename) + ".g.cshtml.cs";
                    diagPath = Path.Combine(diagDir, diagFilename);
                    _ = Directory.CreateDirectory(diagDir);
                }

                // TODO: Investigate CR vs CRLF line-endings of SourceCSharpCode (*.g.cshtml.cs) and Content (*.generated.cs)
                string? razorSource = null;
                RenderResult? renderResult;
                try
                {
                    renderResult = await engine.RenderTemplateAsync(templateFilename, model: metadata, onRazorCompilerOutput: source => razorSource = source);
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
                string csharpPath =
                    Path.Combine(
                        optionOutDir.Value() ??
                        throw new InvalidOperationException($"The required {optionOutDir.LongName} is missing."),
                        csharpFilename);
                WriteLineVerbose($"Writing '{csharpFilename}' to '{optionOutDir.Value()}'.");
                await File.WriteAllTextAsync(csharpPath, renderResult.Content, Encoding.UTF8, cancellationToken);
                WriteLineVerbose($"Writing '{csharpFilename}' to '{optionOutDir.Value()}' completed.");

                return 0;
            });

            return app.Execute(args);
        }
    }
}
