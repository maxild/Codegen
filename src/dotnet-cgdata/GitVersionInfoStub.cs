#if !GIT_VERSION_INFO_EXISTS
#pragma warning disable IDE0130 // Namespace "Codegen" does not match folder structure, expected "Codegen.Database.CLI"
namespace Codegen
#pragma warning restore IDE0130 // Namespace "Codegen" does not match folder structure, expected "Codegen.Database.CLI"
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
