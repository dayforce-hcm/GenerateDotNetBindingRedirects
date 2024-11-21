using Dayforce.CSharp.ProjectAssets;
using LibGit2Sharp;
using System.IO;

namespace GenerateBindingRedirects;

internal interface IGitVersionControl
{
    bool IsTracked(string filePath);
}

internal class GitVersionControl : IGitVersionControl
{
    internal static IGitVersionControl Instance = new GitVersionControl();

    public bool IsTracked(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }
        var wsPath = filePath.GetGitWorkspaceRoot();
        if (wsPath == null)
        {
            return false;
        }
        // Get the right file case, very important for git.
        filePath = Directory.GetFiles(Path.GetDirectoryName(filePath), Path.GetFileName(filePath))[0];
        filePath = filePath[wsPath.Length..].Replace('\\', '/');
        var repo = new Repository(wsPath);
        return repo.Index[filePath] != null || repo.Lookup("HEAD:" + filePath, ObjectType.Blob) != null;
    }
}
