///////////////////////////////////////////////////////////////////////////////
// TOOLS
///////////////////////////////////////////////////////////////////////////////
#tool "nuget:?package=gitreleasemanager&version=0.8.0"

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
    new BuildSettings   // My personal overrides
    {
        MainRepositoryOwner = "maxild",
        RepositoryName = "Domus",
        DeployToCIFeedUrl = "https://www.myget.org/F/brf-ci/api/v2/package", // MyGet CI feed url
        DeployToProdFeedUrl = "https://www.myget.org/F/brf/api/v2/package"   // MyGet feed url
    });
bool publishingError = false;
DotNetCoreMSBuildSettings msBuildSettings = null;

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(context =>
{
    msBuildSettings = new DotNetCoreMSBuildSettings()
                        .WithProperty("RepositoryBranch", parameters.Git.Branch)        // gitflow branch
                        .WithProperty("RepositoryCommit", parameters.Git.Sha)           // full sha
                        //.WithProperty("Version", parameters.VersionInfo.SemVer)       // semver 2.0 compatible
                        .WithProperty("Version", parameters.VersionInfo.NuGetVersion)   // padded with zeros, because of lexical nuget sort order
                        .WithProperty("AssemblyVersion", parameters.VersionInfo.AssemblyVersion)
                        .WithProperty("FileVersion", parameters.VersionInfo.AssemblyFileVersion);
                        //.WithProperty("PackageReleaseNotes", string.Concat("\"", releaseNotes, "\""));

    // See https://github.com/dotnet/sdk/issues/335#issuecomment-346951034
    if (false == parameters.IsRunningOnWindows)
    {
        // Since Cake runs on Mono, it is straight forward to resolve the path to the Mono libs (reference assemblies).
        // Find where .../mono/5.2/mscorlib.dll is on your machine.
        var frameworkPathOverride = new FilePath(typeof(object).Assembly.Location).GetDirectory().FullPath + "/";

        // Use FrameworkPathOverride when not running on Windows. MSBuild uses
        // this property to locate the Framework libraries required to build your code.
        Information("Build will use FrameworkPathOverride={0} since not building on Windows.", frameworkPathOverride);
        msBuildSettings.WithProperty("FrameworkPathOverride", frameworkPathOverride);
    }

    Information("Building version {0} of {1} ({2}, {3}) using version {4} of Cake. (IsTagPush: {5})",
        parameters.VersionInfo.SemVer,
        parameters.ProjectName,
        parameters.Configuration,
        parameters.Target,
        parameters.VersionInfo.CakeVersion,
        parameters.IsTagPush);
});

///////////////////////////////////////////////////////////////////////////////
// PRIMARY TASKS (direct targets)
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Package");

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
    Information($"sln: {parameters.Paths.Files.Solution.FullPath}");
    DotNetCoreRestore(parameters.Paths.Files.Solution.FullPath, new DotNetCoreRestoreSettings
    {
        Verbosity = DotNetCoreVerbosity.Minimal,
        ConfigFile = "./NuGet.config",
        MSBuildSettings = msBuildSettings
    });
});

Task("Build")
    .IsDependentOn("Generate-CommonAssemblyInfo")
    .IsDependentOn("Restore")
    .Does(() =>
{
    DotNetCoreBuild(parameters.Paths.Files.Solution.FullPath, new DotNetCoreBuildSettings()
    {
        Configuration = parameters.Configuration,
        NoRestore = true,
        MSBuildSettings = msBuildSettings
    });
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    var testProjects = GetFiles($"./{parameters.Paths.Directories.Test}/**/*.Tests.csproj") +
                       GetFiles($"./{parameters.Paths.Directories.Test}/RazorLearningTests/RazorLearningTests.csproj");
    foreach(var project in testProjects)
    {
        foreach (var tfm in new [] {"net472", "netcoreapp2.1"})
        {
            DotNetCoreTest(project.ToString(), new DotNetCoreTestSettings
            {
                Framework = tfm,
                NoBuild = true,
                NoRestore = true,
                Configuration = parameters.Configuration
            });
        }

        // NOTE: .NET Framework / Mono (net472 on *nix and Mac OSX)
        // ========================================================
        // Microsoft does not officially support Mono via .NET Core SDK. Their support for .NET Core
        // on Linux and OS X starts and ends with .NET Core. Anyway we test on Mono for now, and maybe
        // remove Mono support soon.
        //
        // For Mono to support dotnet-xunit we have to put { "appDomain": "denied" } in config
        // See https://github.com/xunit/xunit/issues/1357#issuecomment-314416426
    }
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
    // Only packable projects will produce nupkg's
    var projects = GetFiles($"{parameters.Paths.Directories.Src}/**/*.csproj");
    foreach(var project in projects)
    {
        DotNetCorePack(project.FullPath, new DotNetCorePackSettings {
            Configuration = parameters.Configuration,
            OutputDirectory = parameters.Paths.Directories.Artifacts,
            NoBuild = true,
            NoRestore = true,
            MSBuildSettings = msBuildSettings
        });
    }
});

Task("Publish")
    .IsDependentOn("Publish-CIFeed")
    .IsDependentOn("Publish-ProdFeed");

Task("Upload-AppVeyor-Artifacts")
    .IsDependentOn("Upload-AppVeyor-Debug-Artifacts")
    .IsDependentOn("Upload-AppVeyor-Release-Artifacts");

///////////////////////////////////////////////////////////////////////////////
// SECONDARY TASKS (indirect targets)
///////////////////////////////////////////////////////////////////////////////

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

// Debug builds are published to MyGet CI feed
Task("Publish-CIFeed")
    .IsDependentOn("Package")
    .WithCriteria(() => parameters.ConfigurationIsDebug)
    .WithCriteria(() => parameters.ShouldDeployToCIFeed)
    .Does(() =>
{
    foreach (var package in GetFiles(parameters.Paths.Directories.Artifacts + "/*.nupkg"))
    {
        NuGetPush(package.FullPath, new NuGetPushSettings {
            Source = parameters.CIFeed.SourceUrl,
            ApiKey = parameters.CIFeed.ApiKey,
            ArgumentCustomization = args => args.Append("-NoSymbols")
        });
    }
})
.OnError(exception =>
{
    Information("Publish-MyGet Task failed, but continuing with next Task...");
    publishingError = true;
});

// Release builds are published to MyGet production feed
Task("Publish-ProdFeed")
    .IsDependentOn("Package")
    .WithCriteria(() => parameters.ConfigurationIsRelease)
    .WithCriteria(() => parameters.ShouldDeployToProdFeed)
    .Does(() =>
{
    foreach (var package in GetFiles(parameters.Paths.Directories.Artifacts + "/*.nupkg"))
    {
        NuGetPush(package.FullPath, new NuGetPushSettings {
            Source = parameters.ProdFeed.SourceUrl,
            ApiKey = parameters.ProdFeed.ApiKey,
            ArgumentCustomization = args => args.Append("-NoSymbols")
        });
    }
})
.OnError(exception =>
{
    Information("Publish-ProdFeed task failed, but continuing with next task...");
    publishingError = true;
});

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
Task("Publish-GitHub-Release")
    .IsDependentOn("Package")
    .WithCriteria(() => parameters.ShouldDeployToProdFeed)
    .WithCriteria(() => parameters.ConfigurationIsRelease)
    .Does(() =>
{
    foreach (var package in GetFiles(parameters.Paths.Directories.Artifacts + "/*.nupkg"))
    {
        GitReleaseManagerAddAssets(parameters.GitHub.GetRequiredToken(),
                                   parameters.GitHub.RepositoryOwner, parameters.GitHub.RepositoryName,
                                   parameters.VersionInfo.Milestone, package.FullPath);
    }

    // Close the milestone
    GitReleaseManagerClose(parameters.GitHub.GetRequiredToken(),
                           parameters.GitHub.RepositoryOwner, parameters.GitHub.RepositoryName,
                           parameters.VersionInfo.Milestone);
})
.OnError(exception =>
{
    Information("Publish-GitHub-Release Task failed, but continuing with next Task...");
    publishingError = true;
});

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
    string template = @"using System.Reflection;

//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Cake.
// </auto-generated>
//------------------------------------------------------------------------------

[assembly: AssemblyCompany(""BRFkredit a/s"")]
[assembly: AssemblyCopyright(""Copyright BRFkredit a/s 2002-{0}. All rights reserved."")]
[assembly: AssemblyProduct(""{1}"")]
[assembly: AssemblyDescription(""Domus -- A Library for .NET Framework and .NET Core"")]

[assembly: AssemblyVersion(""{2}"")]
[assembly: AssemblyFileVersion(""{3}"")]
[assembly: AssemblyInformationalVersion(""{4}"")]

[assembly: System.Runtime.InteropServices.ComVisible(false)]
[assembly: System.CLSCompliant(true)]

#if DEBUG
[assembly: AssemblyConfiguration(""Debug"")]
#else
[assembly: AssemblyConfiguration(""Release"")]
#endif
";

string contents = string.Format(template,
    DateTime.Now.Year,
    parameters.ProjectName,
    parameters.VersionInfo.AssemblyVersion,
    parameters.VersionInfo.AssemblyFileVersion,
    parameters.VersionInfo.AssemblyInformationalVersion);

    System.IO.File.WriteAllText(parameters.Paths.Files.CommonAssemblyInfo.FullPath, contents, Encoding.UTF8);
});

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(parameters.Target);
