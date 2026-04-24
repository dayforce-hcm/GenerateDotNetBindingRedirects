using System;
using System.IO;
using GenerateBindingRedirects;
using NUnit.Framework;

namespace Tests
{
    public partial class BindingRedirectsTests
    {
        [TestCase("6\\MinimalApp", "6\\Solutions.txt", "6\\Minimal.sln")]
        public void AssertCreatesConfigWhenUntrackedWithGitIgnore(string projectPath, string solutionsFile, string sln)
        {
            PrepareArea(Path.GetDirectoryName(projectPath), sln);
            using var ctx = new ProjectTestContext(projectPath, solutionsFile);
            Assert.That(ctx.ConfigFile, Does.Not.Exist, "Config must not exist before the tool runs");
            File.WriteAllText(ctx.GitIgnoreFile, "app.config\r\n");

            using var noVersionControlScope = new NoGitVersionControl.Scope();
            Program.Run(ctx.ProjectFile, ctx.SlnFile, null, null, false, null, true);

            Assert.That(ctx.ConfigFile, Does.Exist, "Assert mode should create config when untracked");
        }

        [TestCase("6\\MinimalApp", "6\\Solutions.txt", "6\\Minimal.sln")]
        public void AssertFailsWhenUntrackedNoGitIgnore(string projectPath, string solutionsFile, string sln)
        {
            PrepareArea(Path.GetDirectoryName(projectPath), sln);
            using var ctx = new ProjectTestContext(projectPath, solutionsFile);

            using var noVersionControlScope = new NoGitVersionControl.Scope();
            var exc = Assert.Throws<ApplicationException>(() =>
                Program.Run(ctx.ProjectFile, ctx.SlnFile, null, null, false, null, true));

            Assert.That(exc.Message, Does.Contain(".gitignore"));
            Assert.That(ctx.ConfigFile, Does.Not.Exist);
        }

        [TestCase("6\\MinimalApp", "6\\Solutions.txt", "6\\Minimal.sln")]
        public void OnlyRedirectsConfigPassesWhenUntrackedWithGitIgnore(string projectPath, string solutionsFile, string sln)
        {
            PrepareArea(Path.GetDirectoryName(projectPath), sln);
            using var ctx = new ProjectTestContext(projectPath, solutionsFile);

            using (var noVersionControlScope = new NoGitVersionControl.Scope())
            {
                Program.Run(ctx.ProjectFile, ctx.SlnFile, null, null, true);
            }
            File.WriteAllText(ctx.GitIgnoreFile, "app.config\r\n");

            using (var noVersionControlScope = new NoGitVersionControl.Scope())
            {
                Program.Run(ctx.ProjectFile, ctx.SlnFile, null, null, false, null, true);
            }
        }

        [TestCase("6\\MinimalApp", "6\\Solutions.txt", "6\\Minimal.sln")]
        public void ForceAssertFailUntrackedMismatchingWithGitIgnore(string projectPath, string solutionsFile, string sln)
        {
            PrepareArea(Path.GetDirectoryName(projectPath), sln);
            using var ctx = new ProjectTestContext(projectPath, solutionsFile);
            WriteMismatchingConfig(ctx.ConfigFile);
            File.WriteAllText(ctx.GitIgnoreFile, "app.config\r\n");

            using var noVersionControlScope = new NoGitVersionControl.Scope();
            var exc = Assert.Throws<ApplicationException>(() =>
                Program.Run(ctx.ProjectFile, ctx.SlnFile, null, null, false, null, forceAssert: true));

            Assert.That(exc.Message, Does.Contain("does not have the expected set of binding redirects"));
        }

        [TestCase("6\\MinimalApp", "6\\Solutions.txt", "6\\Minimal.sln")]
        public void ForceAssertFailUntrackedMismatchingNoGitIgnore(string projectPath, string solutionsFile, string sln)
        {
            PrepareArea(Path.GetDirectoryName(projectPath), sln);
            using var ctx = new ProjectTestContext(projectPath, solutionsFile);
            WriteMismatchingConfig(ctx.ConfigFile);

            using var noVersionControlScope = new NoGitVersionControl.Scope();
            var exc = Assert.Throws<ApplicationException>(() =>
                Program.Run(ctx.ProjectFile, ctx.SlnFile, null, null, false, null, forceAssert: true));

            Assert.That(exc.Message, Does.Contain(".gitignore"));
        }

        private static void WriteMismatchingConfig(string configFile)
        {
            File.WriteAllText(configFile, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <runtime>
    <assemblyBinding xmlns=""urn:schemas-microsoft-com:asm.v1"">
      <dependentAssembly>
        <assemblyIdentity name=""Bogus"" publicKeyToken=""1234567890123456"" culture=""neutral"" />
        <bindingRedirect oldVersion=""0.0.0.0-1.0.0.0"" newVersion=""1.0.0.0"" />
      </dependentAssembly>
    </assemblyBinding>
  </runtime>
</configuration>
");
        }
    }
}
