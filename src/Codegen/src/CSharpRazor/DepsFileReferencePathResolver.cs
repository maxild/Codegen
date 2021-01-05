using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CSharpRazor
{
    // Credit: https://github.com/aspnet/AspNetCore/blob/release/2.2/src/Mvc/src/Microsoft.AspNetCore.Mvc.Core/ApplicationParts/AssemblyPart.cs

    public class DepsFileReferencePathResolver : PreservedCompilationContextLoader
    {
        public DepsFileReferencePathResolver(Assembly assembly) : base(assembly)
        {
        }

        /// <summary>
        /// Gets the reference paths of the entry <see cref="System.Reflection.Assembly"/> (i.e. the application),
        /// that are resolved by reading the deps.json file of the application (appbase refs, package refs etc),
        /// that are to be used during runtime compilation of Razor pages.
        /// </summary>
        public IReadOnlyList<string> GetReferencePaths()
        {
            var dependencyContext = GetDependencyContext();
            if (dependencyContext is not null)
            {
                var paths = dependencyContext.CompileLibraries
                    .SelectMany(library => library.ResolveReferencePaths())
                    .ToList();

                if (paths.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Can't load metadata reference from the entry assembly. " +
                        "Make sure PreserveCompilationContext is set to true in *.csproj file");
                }

                return paths;
            }

            throw new InvalidOperationException(
                $"DependencyContextLoader could not resolve the DependencyContext of the entry point Assembly -- DependencyContext.Load(Assembly) returned null. Assembly.IsDynamic == {Assembly.IsDynamic}");
        }
    }
}
