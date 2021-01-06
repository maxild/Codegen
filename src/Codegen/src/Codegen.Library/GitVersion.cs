using System;
using System.Text;

namespace Codegen.Library
{
    /// <summary>
    /// A record (immutable object) of GIT information about the commit that was used to build the source.
    /// The tooling (cg-data and cg-csharp) will show this information when --version or --info is executed.
    /// </summary>
    public sealed record GitVersion(
        string Version,
        string NuGetVersion,
        string BuildVersion,
        string CommitId,
        string CommitDate,
        string BranchName)
    {
        public string ToInfoString(string firstLine)
        {
            var sb = new StringBuilder();
            sb.AppendLine(firstLine);
            sb.AppendLine("  Version information:");
            sb.AppendLine($"    Version:        {Version}");
            sb.AppendLine($"    NugetVersion:   {NuGetVersion}");
            sb.AppendLine($"    Build:          {BuildVersion}");
            sb.AppendLine("  GIT information:");
            sb.AppendLine($"    Commit:         {CommitId}");
            sb.AppendLine($"    Date:           {CommitDate}");
            sb.AppendLine($"    Branch:         {BranchName}");
            return sb.ToString();
        }

        public bool Equals(GitVersion? other, GitVersionComparison comparison)
        {
            return comparison == GitVersionComparison.OnlyVersion
                ? string.Equals(Version, other?.Version, StringComparison.Ordinal)
                : Equals(other);
        }
    }

    public enum GitVersionComparison
    {
        AllFields = 0,
        OnlyVersion
    }
}
