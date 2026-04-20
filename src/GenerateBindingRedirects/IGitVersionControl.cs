using Dayforce.CSharp.ProjectAssets;
using LibGit2Sharp;
using System;
using System.IO;

namespace GenerateBindingRedirects;

internal interface IGitVersionControl
{
    string WorkspaceRoot { get; set; }
    bool IsTracked(string filePath);
    string HEAD { get; }
}

internal class GitVersionControl : IGitVersionControl
{
    private readonly Lazy<Repository> m_repository;

    internal static IGitVersionControl Instance = new GitVersionControl();

    private GitVersionControl()
    {
        m_repository = new Lazy<Repository>(() => new Repository(WorkspaceRoot));
    }

    public string WorkspaceRoot { get; set; }

    public string HEAD => m_repository.Value.Head.Tip.Sha;

    public bool IsTracked(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }
        var wsPath = filePath.GetGitWorkspaceRoot();
        if (!string.Equals(wsPath, WorkspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        // Get the right file case, very important for git.
        filePath = Directory.GetFiles(Path.GetDirectoryName(filePath), Path.GetFileName(filePath))[0];
        filePath = filePath[wsPath.Length..].Replace('\\', '/');
        return m_repository.Value.Index[filePath] != null || m_repository.Value.Lookup("HEAD:" + filePath, ObjectType.Blob) != null;
    }
}
