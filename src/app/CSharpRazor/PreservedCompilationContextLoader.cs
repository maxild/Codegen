using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyModel;

namespace CSharpRazor;

/// <summary>
/// PreserveCompilationContext does two things today:
///    1. Write compilation info to .deps file
///    2. Copy reference-only assemblies to build/publish refs/ folder
/// Both can be revoked using this abstract base class
/// </summary>
public abstract class PreservedCompilationContextLoader
{
    protected PreservedCompilationContextLoader(Assembly assembly)
    {
        Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
    }

    /// <summary>
    /// This should be your entry point <see cref="Assembly"/>.
    /// </summary>
    public Assembly Assembly { get; }

    private DependencyContext? _dependencyContext;

    protected DependencyContext? GetDependencyContext()
    {
        // reading .deps file for all the references that the assembly was compiled
        // against (i.e. reference assemblies) and the compilation options used during
        // that compilation.
        return _dependencyContext ??= GetDependencyContextHelper();
    }

    private DependencyContext? GetDependencyContextHelper()
    {
        if (Assembly.IsDynamic)
        {
            // Skip the loading process for dynamic assemblies. This prevents DependencyContext.Load(Assembly)
            // from reading the '*.deps.json' file from either manifest resources or the assembly location,
            // which will fail.
            return null;
        }

        return DependencyContext.Load(Assembly);
    }

    private bool? _isDevelopment;

    protected bool IsDevelopment =>
        _isDevelopment ?? (_isDevelopment = IsAssemblyDebugBuild()).GetValueOrDefault();

    private bool IsAssemblyDebugBuild()
    {
        return Assembly.GetCustomAttributes(inherit: false)
            .OfType<DebuggableAttribute>()
            .Select(da => da.IsJITTrackingEnabled)
            .FirstOrDefault();
    }

    //public static bool IsInDebugMode(System.Reflection.Assembly Assembly)
    //{
    //    var attributes = Assembly.GetCustomAttributes(typeof(System.Diagnostics.DebuggableAttribute), false);
    //    if (attributes.Length > 0)
    //    {
    //        var debuggable = attributes[0] as System.Diagnostics.DebuggableAttribute;
    //        if (debuggable is not null)
    //            return (debuggable.DebuggingFlags & System.Diagnostics.DebuggableAttribute.DebuggingModes.Default) == System.Diagnostics.DebuggableAttribute.DebuggingModes.Default;
    //        else
    //            return false;
    //    }
    //    else
    //        return false;
    //}
}
