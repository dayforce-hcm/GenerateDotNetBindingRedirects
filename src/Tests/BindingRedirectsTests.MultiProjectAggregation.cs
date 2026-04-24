using System.IO;
using NUnit.Framework;

namespace Tests
{
    public partial class BindingRedirectsTests
    {
        [Test]
        public void CrossSolutionPackageVersionAggregation()
        {
            PrepareAreaWithBuild("14", "14\\SolutionA.sln", "14\\SolutionB.sln");

            var libAAssets = Path.Combine(WorkDir, "14", "LibA", "obj", "project.assets.json");
            var appBAssets = Path.Combine(WorkDir, "14", "AppB", "obj", "project.assets.json");
            AssertFileContains(libAAssets, "System.Text.Encodings.Web/4.7.2");
            AssertFileContains(appBAssets, "System.Text.Encodings.Web/4.5.0");
            AssertFileDoesNotContain(appBAssets, "System.Text.Encodings.Web/4.7.2", "AppB's NuGet must not see LibA's higher version");

            using var noVersionControlScope = new NoGitVersionControl.Scope();
            var bindingRedirectsFilePath = $"{GlobalContext.OutputDir}\\BindingRedirects.txt";
            Assert.That(RunTool("14\\AppB", "14\\Solutions.txt", bindingRedirectsFilePath), Is.Zero);

            AssertFileContains(bindingRedirectsFilePath, "System.Text.Encodings.Web");
            AssertFileContains(bindingRedirectsFilePath, "4.0.5.1");

            SaveVerboseLog("14\\AppB");
        }

        [Test]
        public void NuGetVersionTiebreakerPicksHigherPackageVersion()
        {
            PrepareAreaWithBuild("15", "15\\SolutionA.sln", "15\\SolutionB.sln");

            var libAAssets = Path.Combine(WorkDir, "15", "LibA", "obj", "project.assets.json");
            var appBAssets = Path.Combine(WorkDir, "15", "AppB", "obj", "project.assets.json");
            AssertFileContains(libAAssets, "Newtonsoft.Json/12.0.1");
            AssertFileContains(appBAssets, "Newtonsoft.Json/12.0.3");

            using var noVersionControlScope = new NoGitVersionControl.Scope();
            var bindingRedirectsFilePath = $"{GlobalContext.OutputDir}\\BindingRedirects.txt";
            var targetFilesPath = $"{GlobalContext.OutputDir}\\TargetFiles.txt";
            Assert.That(RunTool("15\\AppB", "15\\Solutions.txt", bindingRedirectsFilePath, targetFilesPath), Is.Zero);

            AssertFileContains(targetFilesPath, "Newtonsoft.Json\\12.0.3\\", "Tiebreaker must pick NuGet version 12.0.3 over 12.0.1");
            AssertFileDoesNotContain(targetFilesPath, "Newtonsoft.Json\\12.0.1\\");

            SaveVerboseLog("15\\AppB");
        }
    }
}
