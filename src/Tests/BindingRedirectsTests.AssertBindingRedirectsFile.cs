using System;
using System.IO;
using GenerateBindingRedirects;
using NUnit.Framework;

namespace Tests
{
    public partial class BindingRedirectsTests
    {
        private static void WithConfigFile(string projectPath, string solutionsFile, string sln, Action<ProjectTestContext> action)
        {
            PrepareArea(Path.GetDirectoryName(projectPath), sln);
            using var ctx = new ProjectTestContext(projectPath, solutionsFile);
            using var noVersionControlScope = new NoGitVersionControl.Scope();
            Program.Run(ctx.ProjectFile, ctx.SlnFile, null, null, true);
            action(ctx);
        }

        [TestCase("6\\MinimalApp", "6\\Solutions.txt", "6\\Minimal.sln")]
        public void AssertPassWithCorrectBindingRedirectsFile(string projectPath, string solutionsFile, string sln)
        {
            WithConfigFile(projectPath, solutionsFile, sln, ctx =>
            {
                var bindingRedirectsFilePath = $"{GlobalContext.OutputDir}\\BindingRedirects.txt";
                Program.Run(ctx.ProjectFile, ctx.SlnFile, null, bindingRedirectsFilePath, false);
                var timestamp = File.GetLastWriteTimeUtc(bindingRedirectsFilePath);

                Program.Run(ctx.ProjectFile, ctx.SlnFile, null, bindingRedirectsFilePath, false, null, forceAssert: true);

                Assert.That(File.GetLastWriteTimeUtc(bindingRedirectsFilePath), Is.EqualTo(timestamp), "BindingRedirects.txt should not be modified by forceAssert");
            });
        }

        [TestCase("6\\MinimalApp", "6\\Solutions.txt", "6\\Minimal.sln")]
        public void AssertFailWithNoBindingRedirectsFile(string projectPath, string solutionsFile, string sln)
        {
            WithConfigFile(projectPath, solutionsFile, sln, ctx =>
            {
                var nonExistingFilePath = $"{GlobalContext.OutputDir}\\NonExistent.txt";

                var exc = Assert.Throws<ApplicationException>(() => Program.Run(ctx.ProjectFile, ctx.SlnFile, null, nonExistingFilePath, false, null, true));
                Assert.That(exc.Message, Does.Contain("does not exist"));
            });
        }

        [TestCase("6\\MinimalApp", "6\\Solutions.txt", "6\\Minimal.sln")]
        public void AssertFailWithMismatchingBindingRedirectsFile(string projectPath, string solutionsFile, string sln)
        {
            WithConfigFile(projectPath, solutionsFile, sln, ctx =>
            {
                var bindingRedirectsFilePath = $"{GlobalContext.OutputDir}\\BindingRedirects.txt";
                Program.Run(ctx.ProjectFile, ctx.SlnFile, null, bindingRedirectsFilePath, false);

                var lines = File.ReadAllLines(bindingRedirectsFilePath);
                File.WriteAllLines(bindingRedirectsFilePath, lines[4..]);

                var exc = Assert.Throws<ApplicationException>(() => Program.Run(ctx.ProjectFile, ctx.SlnFile, null, bindingRedirectsFilePath, false, null, true));
                Assert.That(exc.Message, Does.Contain("do not match"));
            });
        }
    }
}
