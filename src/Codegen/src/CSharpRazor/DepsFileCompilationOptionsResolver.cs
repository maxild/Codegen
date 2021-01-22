using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using DependencyContextCompilationOptions = Microsoft.Extensions.DependencyModel.CompilationOptions;

namespace CSharpRazor
{
    // Credit: https://github.com/aspnet/AspNetCore/blob/release/2.2/src/Mvc/src/Microsoft.AspNetCore.Mvc.Razor/Internal/CSharpCompiler.cs

    public class DepsFileCompilationOptionsResolver : PreservedCompilationContextLoader
    {
        private bool _optionsInitialized;
        private CSharpParseOptions? _parseOptions;
        private CSharpCompilationOptions? _compilationOptions;
        private EmitOptions? _emitOptions;
        private bool _emitPdb;

        public DepsFileCompilationOptionsResolver(Assembly assembly) : base(assembly)
        {
        }

        public CSharpParseOptions ParseOptions
        {
            get
            {
                EnsureOptions();
                return _parseOptions;
            }
        }

        public CSharpCompilationOptions CompilationOptions
        {
            get
            {
                EnsureOptions();
                return _compilationOptions;
            }
        }

        public bool EmitPdb
        {
            get
            {
                EnsureOptions();
                return _emitPdb;
            }
        }

        public EmitOptions EmitOptions
        {
            get
            {
                EnsureOptions();
                return _emitOptions;
            }
        }

        // Internal for unit testing.
        protected internal DependencyContextCompilationOptions GetDependencyContextCompilationOptions()
        {
            var dependencyContext = GetDependencyContext();
            if (dependencyContext is not null)
            {
                var dependencyContextCompilationOptions = dependencyContext.CompilationOptions;
                return dependencyContextCompilationOptions is null
                    ? throw new InvalidOperationException(
                        "Can't load compilation options from the entry assembly. " +
                        "Make sure PreserveCompilationContext is set to true in *.csproj file")
                    : dependencyContextCompilationOptions;
            }

            throw new InvalidOperationException(
                $"DependencyContextLoader could not resolve the DependencyContext of the entry point Assembly -- DependencyContext.Load(Assembly) returned null. Assembly.IsDynamic == {Assembly.IsDynamic}");
        }

        [MemberNotNull(nameof(_parseOptions), nameof(_compilationOptions), nameof(_emitOptions))]
        private void EnsureOptions()
        {
            if (!_optionsInitialized)
            {
                var dependencyContextOptions = GetDependencyContextCompilationOptions();
                _parseOptions = GetParseOptions(dependencyContextOptions);
                _compilationOptions = GetCompilationOptions(dependencyContextOptions);
                _emitOptions = GetEmitOptions(dependencyContextOptions);

                _optionsInitialized = true;
            }
            else
            {
                // Roslyn NRT flow analysis need thw following assertions
                Debug.Assert(_parseOptions is not null);
                Debug.Assert(_compilationOptions is not null);
                Debug.Assert(_emitOptions is not null);
            }
        }

        private EmitOptions GetEmitOptions(DependencyContextCompilationOptions dependencyContextOptions)
        {
            // Assume we're always producing pdbs unless DebugType = none
            _emitPdb = true;
            DebugInformationFormat debugInformationFormat;
            if (string.IsNullOrEmpty(dependencyContextOptions.DebugType))
            {
                debugInformationFormat = CurrentPlatformSupportsFullPdbGeneration() ?
                    DebugInformationFormat.Pdb :
                    DebugInformationFormat.PortablePdb;
            }
            else
            {
                // Based on https://github.com/dotnet/roslyn/blob/1d28ff9ba248b332de3c84d23194a1d7bde07e4d/src/Compilers/CSharp/Portable/CommandLine/CSharpCommandLineParser.cs#L624-L640
                switch (dependencyContextOptions.DebugType.ToLower())
                {
                    case "none":
                        // There isn't a way to represent none in DebugInformationFormat.
                        // We'll set EmitPdb to false and let callers handle it by setting a null pdb-stream.
                        _emitPdb = false;
                        return new EmitOptions();
                    case "portable":
                        debugInformationFormat = DebugInformationFormat.PortablePdb;
                        break;
                    case "embedded":
                        // Roslyn does not expose enough public APIs to produce a binary with embedded pdbs.
                        // We'll produce PortablePdb instead to continue providing a reasonable user experience.
                        debugInformationFormat = DebugInformationFormat.PortablePdb;
                        break;
                    case "full":
                    case "pdbonly":
                        debugInformationFormat = CurrentPlatformSupportsFullPdbGeneration() ?
                            DebugInformationFormat.Pdb :
                            DebugInformationFormat.PortablePdb;
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported debug type '{dependencyContextOptions.DebugType}'.");
                }
            }

            var emitOptions = new EmitOptions(debugInformationFormat: debugInformationFormat);
            return emitOptions;
        }

        // Common options as seen in the batch compiler (csc.exe) CLI
        //    * CheckOverflow, WithOverflowCheck: /checked+
        //    * Optimize: /optimize+ (debug, release)
        //    * ConcurrentBuild
        //    * Platform: /platform (x86, AnyCPU, ...)
        //    * Signing flags (strong name, delay signing etc..)
        // C# specific options:
        //    * AllowUnsafe: /unsafe+
        //    * Usings (concept of 'global usings'. great for scripting, codegen). does not exist in csc.exe
        private CSharpCompilationOptions GetCompilationOptions(DependencyContextCompilationOptions dependencyContextOptions)
        {
            var csharpCompilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            // Disable 1702 until roslyn turns this off by default
            csharpCompilationOptions = csharpCompilationOptions.WithSpecificDiagnosticOptions(
                new Dictionary<string, ReportDiagnostic>
                {
                    //
                    // Binding redirects and unnecessary using
                    //

                    // warning CS1701: Assuming assembly reference 'A,Version=...'
                    //                 matches 'A, Version=...', you may need to supply runtime policy
                    {"CS1701", ReportDiagnostic.Suppress},
                    // warning CS1702: Assuming assembly reference 'A, Version=...'
                    //                 matches 'A, Version=...', you may need to supply runtime policy
                    {"CS1702", ReportDiagnostic.Suppress},
                    // error CS1705: Assembly 'MyAssembly' uses 'A, Version=...' which has a
                    //               higher version than referenced assembly 'A, Version=...'
                    {"CS1705", ReportDiagnostic.Suppress},
                    // warning CS8019: Unnecessary using directive.
                    {"CS8019", ReportDiagnostic.Suppress}
                });

            if (dependencyContextOptions.AllowUnsafe.HasValue)
            {
                csharpCompilationOptions = csharpCompilationOptions.WithAllowUnsafe(
                    dependencyContextOptions.AllowUnsafe.Value);
            }

            var optimizationLevel = dependencyContextOptions.Optimize.HasValue
                ? dependencyContextOptions.Optimize.Value ?
                    OptimizationLevel.Release :
                    OptimizationLevel.Debug
                : IsDevelopment ?
                    OptimizationLevel.Debug :
                    OptimizationLevel.Release;

            csharpCompilationOptions = csharpCompilationOptions.WithOptimizationLevel(optimizationLevel);

            if (dependencyContextOptions.WarningsAsErrors.HasValue)
            {
                var reportDiagnostic = dependencyContextOptions.WarningsAsErrors.Value ?
                    ReportDiagnostic.Error :
                    ReportDiagnostic.Default;
                csharpCompilationOptions = csharpCompilationOptions.WithGeneralDiagnosticOption(reportDiagnostic);
            }

            return csharpCompilationOptions;
        }

        private CSharpParseOptions GetParseOptions(DependencyContextCompilationOptions dependencyContextOptions)
        {
            var configurationSymbol = IsDevelopment ? "DEBUG" : "RELEASE";
            var defines = dependencyContextOptions.Defines.Concat(new[] { configurationSymbol });

            var parseOptions = new CSharpParseOptions(preprocessorSymbols: defines);

            if (!string.IsNullOrEmpty(dependencyContextOptions.LanguageVersion))
            {
                if (LanguageVersionFacts.TryParse(dependencyContextOptions.LanguageVersion, out var languageVersion))
                {
                    parseOptions = parseOptions.WithLanguageVersion(languageVersion);
                }
                else
                {
                    Debug.Fail($"LanguageVersion {languageVersion} specified in the deps file could not be parsed.");
                }
            }

            return parseOptions;
        }

        /// <summary>
        /// Determines if the current platform supports full pdb generation.
        /// </summary>
        /// <returns><c>true</c> if full pdb generation is supported; <c>false</c> otherwise.</returns>
        private static bool CurrentPlatformSupportsFullPdbGeneration()
        {
            // Native pdb writer's CLSID
            const string SYM_WRITER_GUID = "0AE2DEB0-F901-478b-BB9F-881EE8066788";

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Cross-plat always produce portable pdbs.
                return false;
            }

            if (Type.GetType("Mono.Runtime") is not null)
            {
                return false;
            }

            try
            {
                // Check for the pdb writer component that roslyn uses to generate pdbs
                var type = Marshal.GetTypeFromCLSID(new Guid(SYM_WRITER_GUID));
                if (type is not null)
                {
                    // This line will throw if pdb generation is not supported.
                    _ = Activator.CreateInstance(type);
                    return true;
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }
    }
}
