///////////////////////////////////////////////////////////////////////////////
// SCRIPTS
///////////////////////////////////////////////////////////////////////////////
#load "./tools/Maxfire.CakeScripts/content/all.cake"

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////
var parameters = CakeScripts.GetParameters(
    Context,            // ICakeContext
    BuildSystem,        // BuildSystem alias
    new BuildSettings
    {
        MainRepositoryOwner = "maxild",
        RepositoryName = "Codegen",
        DeployToCIFeedUrl = "https://www.myget.org/F/maxfire-ci/api/v2/package", // MyGet feed url
        DeployToProdFeedUrl = "https://www.nuget.org/api/v2/package"             // NuGet.org feed url
    },
    new BuildPathSettings
    {
        SolutionFileName = "Codegen.sln"
    });
bool publishingError = false;
DotNetCoreMSBuildSettings msBuildSettings = null;

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(context =>
{
    if (parameters.Git.IsMasterBranch && context.Log.Verbosity != Verbosity.Diagnostic) {
        Information("Increasing verbosity to diagnostic.");
        context.Log.Verbosity = Verbosity.Diagnostic;
    }

    msBuildSettings = new DotNetCoreMSBuildSettings()
                        .WithProperty("RepositoryBranch", parameters.Git.Branch)           // gitflow branch
                        .WithProperty("RepositoryCommit", parameters.Git.Sha)              // full sha
                        .WithProperty("Version", parameters.VersionInfo.SemVer.ToLower())  // semver 2.0 compatible
                        //.WithProperty("Version", parameters.VersionInfo.NuGetVersion)    // padded with zeros, because of lexical nuget sort order
                        .WithProperty("AssemblyVersion", parameters.VersionInfo.AssemblyVersion)
                        .WithProperty("FileVersion", parameters.VersionInfo.AssemblyFileVersion);

    // Deterministic builds: normalize stored file paths
    if (parameters.IsRunningOnAppVeyor) {
        msBuildSettings = msBuildSettings.WithProperty("ContinuousIntegrationBuild", "true");
    }

    Information("Building version {0} of {1} ({2}, {3}) using version {4} of Cake and '{5}' of GitVersion. (IsTagPush: {6})",
        parameters.VersionInfo.SemVer.ToLower(),
        parameters.ProjectName,
        parameters.Configuration,
        parameters.Target,
        parameters.VersionInfo.CakeVersion,
        parameters.VersionInfo.GitVersionVersion,
        parameters.IsTagPush);
});

///////////////////////////////////////////////////////////////////////////////
// PRIMARY TASKS (direct targets)
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Package");

Task("Setup")
    .IsDependentOn("Generate-CommonAssemblyInfo");

Task("Travis")
    .IsDependentOn("Show-Info")
    .IsDependentOn("Test");

Task("AppVeyor")
    .IsDependentOn("Show-Info")
    .IsDependentOn("Print-AppVeyor-Environment-Variables")
    .IsDependentOn("Package")
    .IsDependentOn("Upload-AppVeyor-Artifacts")
    //.IsDependentOn("Publish")
    //.IsDependentOn("Publish-GitHub-Release")
    .Finally(() =>
{
    if (publishingError)
    {
        throw new Exception("An error occurred during the publishing of " + parameters.ProjectName + ".  All publishing tasks have been attempted.");
    }
});

Task("ReleaseNotes")
    .IsDependentOn("Create-Release-Notes");

Task("Clean")
    .IsDependentOn("Clear-Artifacts");

Task("Restore")
    .Does(() =>
{
    // This is required in order to build RazorLearningTests that has a ProjectReference to
    // ./src/submodules/razor-compiler/src/Microsoft.AspNetCore.Razor.Language/src/Microsoft.AspNetCore.Razor.Language.csproj
    //       dotnet restore src/submodules/razor-compiler/eng/tools/RepoTasks/RepoTasks.csproj
    // var project = Directory("./src/submodules/razor-compiler/eng/tools/RepoTasks") + File("RepoTasks.csproj");
    // DotNetCoreRestore(project);

    var settings = new DotNetCoreRestoreSettings
    {
        Verbosity = DotNetCoreVerbosity.Minimal,
    };

    DotNetCoreRestore(parameters.Paths.Files.Solution.FullPath, settings);
});

Task("Build")
    .IsDependentOn("Generate-Git-Source-File")
    .IsDependentOn("Generate-CommonAssemblyInfo")
    .IsDependentOn("Restore")
    .Does(() =>
{
    // Needed to force warnings to error on github build
    // See https://github.com/dotnet/roslyn/issues/43051#issuecomment-758862927
    // NOTE: Both /warnAsError and -warnAsError work
    var extraArgs = "-warnAsError";

    // NOTE: NoWarn can only be used to disable built-in compiler/sdk warnings.
    // NOTE: Both /nowarn:IDE0055;NETSDK1023 and -nowarn:IDE0055;NETSDK1023 work
    if (parameters.IsRunningOnAppVeyor)
        extraArgs += " -nowarn:IDE0055;NETSDK1023";

    DotNetCoreBuild(parameters.Paths.Files.Solution.FullPath, new DotNetCoreBuildSettings()
    {
        Configuration = parameters.Configuration,
        NoRestore = true,
        MSBuildSettings = msBuildSettings,

        ArgumentCustomization = args => args.Append(extraArgs)
    });
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    // Only testable projects (<IsTestProject>true</IsTestProject>) will be test-executed
    // We do not need to exclude everything under 'src/submodules',
    // because we use the single master solution
    DotNetCoreTest(parameters.Paths.Files.Solution.FullPath, new DotNetCoreTestSettings
    {
        // Skip all tests with either
        //   DatabaseTest in namespace and/or typename
        //               - or -
        //   Trait("Category", "DatabaseTest")
        // See https://docs.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests
        Filter = "FullyQualifiedName!~DatabaseTests|Category!=DatabaseTest",
        NoBuild = true,
        NoRestore = true,
        Configuration = parameters.Configuration
    });
});

Task("Package")
    .IsDependentOn("Test")
    .IsDependentOn("Create-Packages");

// Create packages without running tests
Task("Create-Packages")
    .IsDependentOn("Build")
    .IsDependentOn("Clear-Artifacts")
    .Does(() =>
{
    // Only packable projects (<IsPackable>true</IsPackable>) will produce nupkg's
    // We do not need to exclude everything under 'src/submodules',
    // because we use the single master solution
    DotNetCorePack(parameters.Paths.Files.Solution.FullPath, new DotNetCorePackSettings {
        Configuration = parameters.Configuration,
        OutputDirectory = parameters.Paths.Directories.Artifacts,
        NoBuild = true,
        NoRestore = true,
        MSBuildSettings = msBuildSettings
    });
});

Task("Upload-AppVeyor-Artifacts")
    .IsDependentOn("Upload-AppVeyor-Debug-Artifacts")
    .IsDependentOn("Upload-AppVeyor-Release-Artifacts");

///////////////////////////////////////////////////////////////////////////////
// SECONDARY TASKS (indirect targets)
///////////////////////////////////////////////////////////////////////////////

Task("Generate-Git-Source-File")
    .Does(() =>
{
    // No heredocs in c#, so using verbatim string
    string contents = $@"//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Cake.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Codegen
{{
    public static class Git
    {{
        private static readonly System.Lazy<Library.GitVersion> s_version = new System.Lazy<Library.GitVersion>(()
            => new Library.GitVersion(
                ""{parameters.VersionInfo.SemVer.ToLower()}"",
                ""{parameters.VersionInfo.SemVer.ToLower()}"",
                ""{parameters.VersionInfo.BuildVersion}"",
                ""{parameters.Git.Sha}"",
                ""{parameters.Git.CommitDate}"",
                ""{parameters.Git.Branch}""));
        public static Library.GitVersion CurrentVersion => s_version.Value;
    }}
}}";
    var file = File("GitVersionInfo.cs");
    var path = parameters.Paths.Directories.Src.CombineWithFilePath(file);
    System.IO.File.WriteAllText(path.FullPath, contents, Encoding.UTF8);
});

// Release artifacts are uploaded for release-line branches (master and support), and Debug
// artifacts are uploaded for non release-line branches (dev, feature etc.).
Task("Upload-AppVeyor-Debug-Artifacts")
    .IsDependentOn("Package")
    .WithCriteria(() => parameters.IsRunningOnAppVeyor)
    .WithCriteria(() => parameters.Git.IsDevelopmentLineBranch && parameters.ConfigurationIsDebug)
    .Does(() =>
{
    foreach (var package in GetFiles(parameters.Paths.Directories.Artifacts + "/*.nupkg"))
    {
        // appveyor PushArtifact <path> [options] (See https://www.appveyor.com/docs/build-worker-api/#push-artifact)
        AppVeyor.UploadArtifact(package);
    }
});

Task("Upload-AppVeyor-Release-Artifacts")
    .IsDependentOn("Package")
    .WithCriteria(() => parameters.IsRunningOnAppVeyor)
    .WithCriteria(() => parameters.Git.IsReleaseLineBranch && parameters.ConfigurationIsRelease)
    .Does(() =>
{
    foreach (var package in GetFiles(parameters.Paths.Directories.Artifacts + "/*.nupkg"))
    {
        // appveyor PushArtifact <path> [options] (See https://www.appveyor.com/docs/build-worker-api/#push-artifact)
        AppVeyor.UploadArtifact(package);
    }
});

// Task("Publish")
//     .IsDependentOn("Publish-CIFeed")
//     .IsDependentOn("Publish-ProdFeed");

// Debug builds are published to MyGet CI feed
// Task("Publish-CIFeed")
//     .IsDependentOn("Package")
//     .WithCriteria(() => parameters.ConfigurationIsDebug)
//     .WithCriteria(() => parameters.ShouldDeployToCIFeed)
//     .Does(() =>
// {
//     foreach (var package in GetFiles(parameters.Paths.Directories.Artifacts + "/*.nupkg"))
//     {
//         DotNetCoreNuGetPush(package.FullPath, new DotNetCoreNuGetPushSettings {
//             Source = parameters.CIFeed.SourceUrl,
//             ApiKey = parameters.CIFeed.ApiKey
//         });
//     }
// })
// .OnError(exception =>
// {
//     Information("Publish-MyGet Task failed, but continuing with next Task...");
//     publishingError = true;
// });

// Release builds are published to MyGet production feed
// Task("Publish-ProdFeed")
//     .IsDependentOn("Package")
//     .WithCriteria(() => parameters.ConfigurationIsRelease)
//     .WithCriteria(() => parameters.ShouldDeployToProdFeed)
//     .Does(() =>
// {
//     foreach (var package in GetFiles(parameters.Paths.Directories.Artifacts + "/*.nupkg"))
//     {
//         DotNetCoreNuGetPush(package.FullPath, new DotNetCoreNuGetPushSettings {
//             Source = parameters.ProdFeed.SourceUrl,
//             ApiKey = parameters.ProdFeed.ApiKey
//         });
//     }
// })
// .OnError(exception =>
// {
//     Information("Publish-ProdFeed task failed, but continuing with next task...");
//     publishingError = true;
// });

Task("Create-Release-Notes")
    .Does(() =>

{
    // This is both the title and tagName of the release (title can be edited on github.com)
    string milestone = Environment.GetEnvironmentVariable("GitHubMilestone") ??
                       parameters.VersionInfo.Milestone;
    Information("Creating draft release of version '{0}' on GitHub", milestone);
    GitReleaseManagerCreate(parameters.GitHub.GetRequiredToken(),
                            parameters.GitHub.RepositoryOwner, parameters.GitHub.RepositoryName,
        new GitReleaseManagerCreateSettings
        {
            Milestone         = milestone,
            Prerelease        = false,
            TargetCommitish   = "master"
        });
});

// Invoked on AppVeyor after draft release have been published on github.com
// Task("Publish-GitHub-Release")
//     .IsDependentOn("Package")
//     .WithCriteria(() => parameters.ShouldDeployToProdFeed)
//     .WithCriteria(() => parameters.ConfigurationIsRelease)
//     .Does(() =>
// {
//     foreach (var package in GetFiles(parameters.Paths.Directories.Artifacts + "/*.nupkg"))
//     {
//         GitReleaseManagerAddAssets(parameters.GitHub.GetRequiredToken(),
//                                    parameters.GitHub.RepositoryOwner, parameters.GitHub.RepositoryName,
//                                    parameters.VersionInfo.Milestone, package.FullPath);
//     }

//     // Close the milestone
//     GitReleaseManagerClose(parameters.GitHub.GetRequiredToken(),
//                            parameters.GitHub.RepositoryOwner, parameters.GitHub.RepositoryName,
//                            parameters.VersionInfo.Milestone);
// })
// .OnError(exception =>
// {
//     Information("Publish-GitHub-Release Task failed, but continuing with next Task...");
//     publishingError = true;
// });

Task("Clear-Artifacts")
    .Does(() =>
{
    parameters.ClearArtifacts();
});

Task("Show-Info")
    .Does(() =>
{
    parameters.PrintToLog();
});

Task("Print-AppVeyor-Environment-Variables")
    .WithCriteria(AppVeyor.IsRunningOnAppVeyor)
    .Does(() =>
{
    parameters.PrintAppVeyorEnvironmentVariables();
});

Task("Generate-CommonAssemblyInfo")
    .Does(() =>
{
    // No heredocs in c#, so using verbatim string (cannot use $"", because of Cake version)
    string contents = $@"//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Cake.
// </auto-generated>
//------------------------------------------------------------------------------

[assembly: System.Reflection.AssemblyCopyright(""Copyright Morten Maxild 2002-{System.DateTime.Now.Year}. All rights reserved."")]
[assembly: System.Reflection.AssemblyProduct(""{parameters.ProjectName}"")]
[assembly: System.Reflection.AssemblyDescription(""Codegen -- A set of tools to generate C# code."")]

[assembly: System.Reflection.AssemblyVersion(""{parameters.VersionInfo.AssemblyVersion}"")]
[assembly: System.Reflection.AssemblyFileVersion(""{parameters.VersionInfo.AssemblyFileVersion}"")]
[assembly: System.Reflection.AssemblyInformationalVersion(""{parameters.VersionInfo.AssemblyInformationalVersion}"")]

[assembly: System.Runtime.InteropServices.ComVisible(false)]

#if DEBUG
[assembly: System.Reflection.AssemblyConfiguration(""Debug"")]
#else
[assembly: System.Reflection.AssemblyConfiguration(""Release"")]
#endif
";
    System.IO.File.WriteAllText(parameters.Paths.Files.CommonAssemblyInfo.FullPath, contents, Encoding.UTF8);
});

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(parameters.Target);
