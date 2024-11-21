using GenerateBindingRedirects;
using System;

namespace Tests;

internal class NoGitVersionControl : IGitVersionControl
{
    internal static readonly NoGitVersionControl Instance = new();

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

    private NoGitVersionControl() { }
    public bool IsTracked(string filePath) => false;
}

internal class ForceGitVersionControl : IGitVersionControl
{
    internal static readonly ForceGitVersionControl Instance = new();

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

    private ForceGitVersionControl() { }
    public bool IsTracked(string filePath) => true;
}
