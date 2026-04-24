using System;
using System.IO;
using GenerateBindingRedirects;
using NUnit.Framework;

namespace Tests
{
    public partial class BindingRedirectsTests
    {
        [TestCase("6\\MinimalApp", "6\\Solutions.txt", "6\\Minimal.sln")]
        public void AssertPassTrackedConfigWithCorrectRedirects(string projectPath, string solutionsFile, string sln)
        {
            PrepareArea(Path.GetDirectoryName(projectPath), sln);
            using var ctx = new ProjectTestContext(projectPath, solutionsFile);

            using (var noVersionControlScope = new NoGitVersionControl.Scope())
            {
                Program.Run(ctx.ProjectFile, ctx.SlnFile, null, null, true);
            }

            var correctConfig = File.ReadAllText(ctx.ConfigFile);
            var withExtra = correctConfig.Replace("<runtime>", "<connectionStrings />" + Environment.NewLine + "  <runtime>");
            File.WriteAllText(ctx.ConfigFile, withExtra);
            File.Delete(ctx.GitIgnoreFile);
            var timestamp = File.GetLastWriteTimeUtc(ctx.ConfigFile);

            using (var forceGitVersionControlScope = new ForceGitVersionControl.Scope())
            {
                Program.Run(ctx.ProjectFile, ctx.SlnFile, null, null, false, null, forceAssert: true);
            }

            Assert.That(File.GetLastWriteTimeUtc(ctx.ConfigFile), Is.EqualTo(timestamp), "Config should not be modified");
            Assert.That(ctx.GitIgnoreFile, Does.Not.Exist, ".gitignore should not be created for tracked config");
        }

        [TestCase("6\\MinimalApp", "6\\Solutions.txt", "6\\Minimal.sln")]
        public void AssertFailTrackedConfigWithOnlyRedirects(string projectPath, string solutionsFile, string sln)
        {
            PrepareArea(Path.GetDirectoryName(projectPath), sln);
            using var ctx = new ProjectTestContext(projectPath, solutionsFile);

            using (var noVersionControlScope = new NoGitVersionControl.Scope())
            {
                Program.Run(ctx.ProjectFile, ctx.SlnFile, null, null, true);
            }
            File.Delete(ctx.GitIgnoreFile);

            using var forceGitVersionControlScope = new ForceGitVersionControl.Scope();
            var exc = Assert.Throws<ApplicationException>(() =>
                Program.Run(ctx.ProjectFile, ctx.SlnFile, null, null, false, null, forceAssert: true));

            Assert.That(exc.Message, Does.Contain("only contains binding redirects"));
        }

        [TestCase("6\\MinimalApp", "6\\Solutions.txt", "6\\Minimal.sln")]
        public void ForceAssertFailTrackedConfigMismatchingRedirects(string projectPath, string solutionsFile, string sln)
        {
            PrepareArea(Path.GetDirectoryName(projectPath), sln);
            using var ctx = new ProjectTestContext(projectPath, solutionsFile);

            File.WriteAllText(ctx.ConfigFile, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <connectionStrings />
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

            using var forceGitVersionControlScope = new ForceGitVersionControl.Scope();
            var exc = Assert.Throws<ApplicationException>(() => Program.Run(ctx.ProjectFile, ctx.SlnFile, null, null, false, null, forceAssert: true));

            Assert.That(exc.Message, Does.Contain("does not have the expected set of binding redirects"));
        }

        [TestCase("6\\MinimalApp", "6\\Solutions.txt", "6\\Minimal.sln")]
        public void ForceAssertFailTrackedNoConfigFile(string projectPath, string solutionsFile, string sln)
        {
            PrepareArea(Path.GetDirectoryName(projectPath), sln);
            using var ctx = new ProjectTestContext(projectPath, solutionsFile);

            using var forceGitVersionControlScope = new ForceGitVersionControl.Scope();
            var exc = Assert.Throws<ApplicationException>(() => Program.Run(ctx.ProjectFile, ctx.SlnFile, null, null, false, null, forceAssert: true));

            Assert.That(exc.Message, Does.Contain("is expected to have some assembly binding redirects, but it does not exist"));
        }
    }
}
