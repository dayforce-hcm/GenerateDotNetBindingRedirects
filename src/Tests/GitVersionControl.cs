using GenerateBindingRedirects;
using System;

namespace Tests;

internal abstract class DummyGitVersionControl<T> : IGitVersionControl
    where T : DummyGitVersionControl<T>, new()
{
    internal static readonly T Instance = new();

    internal readonly struct Scope : IDisposable
    {
        private readonly IGitVersionControl m_prevInstance;

        public Scope()
        {
            m_prevInstance = GitVersionControl.Instance;
            GitVersionControl.Instance = Instance;
        }

        public readonly void Dispose() => GitVersionControl.Instance = m_prevInstance;
    }

    public string WorkspaceRoot { get; set; }
    public abstract bool IsTracked(string filePath);
    public string HEAD => null;
}

internal class NoGitVersionControl : DummyGitVersionControl<NoGitVersionControl>
{
    public override bool IsTracked(string filePath) => false;
}

internal class ForceGitVersionControl : DummyGitVersionControl<ForceGitVersionControl>
{
    public override bool IsTracked(string filePath) => true;
}
