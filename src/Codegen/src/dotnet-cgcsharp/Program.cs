using System;
using System.IO;
using System.Text;
using Codegen.Library;
using CSharpRazor;
using McMaster.Extensions.CommandLineUtils;

namespace Codegen.CSharp.CLI
{
    static class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandLineApplication();

            app.HelpOption();

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
                app.Option("--template <PATH>", "Required. The path to the Razor template file.",
                    CommandOptionType.SingleValue)
                        //.IsRequired()
                        .Accepts(v => v.ExistingFile());

            var optionDiagDir =
                app.Option("--diagDir <DIAGDIR>",
                    "Optional. The path to the directory, where the *.g.cshtml.cs file will be written.",
                    CommandOptionType.SingleValue);

            var optionOutDir =
                app.Option("--outDir <OUTDIR>", "Output filename", CommandOptionType.SingleValue);

            var optionVerbose = app.Option("-v|--verbose", "Verbose", CommandOptionType.NoValue);

            var optionInfo = app.Option("--info", "Display tool information.", CommandOptionType.NoValue);

            app.VersionOption("--version", () => Git.CurrentVersion.Version);

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

                WriteLineVerbose($"Reading {name} model/data from dir '{optionDataDir.Value()}'.");
                // NOTE: cgcsharp does not know about concrete types of records...just a list of objects (anything)
                MetadataModel metadata = MetadataModelUtils.ReadFile(optionDataDir.Value() ?? throw new InvalidOperationException($"The required {optionDataDir.LongName} is missing."), name)
                    .WithToolVersion(Git.CurrentVersion.Version);
                WriteLineVerbose($"Reading {name} model/data from dir '{optionDataDir.Value()}' completed.");

                string templatePath = optionTemplate.Value() ?? throw new InvalidOperationException($"The required {optionTemplate.LongName} is missing.");
                string templateFilename = Path.GetFileName(templatePath);
                string? templateDir = Path.GetDirectoryName(templatePath);

                WriteLineVerbose($"Initializing Razor engine with root directory : '{templateDir}");

                var engine = new RazorEngineBuilder()
                    .SetRootDirectory(templateDir)
                    .Build();

                WriteLineVerbose($"Rendering '{templateFilename}' ...");

                // TODO: Investigate CR vs CRLF line-endings of SourceCSharpCode (*.g.cshtml.cs) and Content (*.generated.cs)
                var renderResult = await engine.RenderTemplateAsync(templateFilename, model: metadata);

                WriteLineVerbose("Rendering complete");

                // Save <templateName>.g.cshtml.cs
                if (optionDiagDir.HasValue())
                {
                    string diagDir = optionDiagDir.Value() ?? throw new InvalidOperationException($"The required {optionDataDir.LongName} is missing.");
                    string diagFilename = Path.GetFileNameWithoutExtension(templateFilename) + ".g.cshtml.cs";
                    string diagPath = Path.Combine(diagDir, diagFilename);
                    Directory.CreateDirectory(diagDir);
                    await File.WriteAllTextAsync(diagPath, renderResult.SourceCSharpCode, Encoding.UTF8, cancellationToken);
                }

                // Save <name>.generated.cs
                string csharpFilename = $"{name}.generated.cs";
                string csharpPath = Path.Combine(optionOutDir.Value() ?? throw new InvalidOperationException($"The required {optionOutDir.LongName} is missing."), csharpFilename);
                await File.WriteAllTextAsync(csharpPath, renderResult.Content, Encoding.UTF8, cancellationToken);

                return 0;
            });

            return app.Execute(args);
        }
    }
}
