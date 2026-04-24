using System.IO;
using NUnit.Framework;

namespace Tests
{
    public partial class BindingRedirectsTests
    {
        [TestCase("7\\AppB", "8\\AppB", "Newtonsoft.Json", "12.0.0.0", "11.0.0.0")]
        public void NuGetDoesNotCrossSolutionBoundaries(string app1, string app2, string depAsmName, string version1, string version2)
        {
            Assert.That(version1, Is.Not.EqualTo(version2), "Test case invalid: versions must differ");

            PrepareAreaWithBuild("7", "7\\Single.sln");
            PrepareAreaWithBuild("8", "8\\SolutionA.sln", "8\\SolutionB.sln");

            using var noVersionControlScope = new NoGitVersionControl.Scope();

            var (bindingRedirects1, targetFiles1) = RunForApp(app1, depAsmName, version1);
            var (bindingRedirects2, targetFiles2) = RunForApp(app2, depAsmName, version2);

            NUnit.Framework.Legacy.FileAssert.AreEqual(bindingRedirects1, bindingRedirects2, "Binding redirects must be identical");
            NUnit.Framework.Legacy.FileAssert.AreEqual(targetFiles1, targetFiles2, "Target files must be identical");

            static (string, string) RunForApp(string app, string depAsmName, string version)
            {
                Assert.That(GetProjectDependencyVersion(app, depAsmName), Is.EqualTo(version), $"{app}: {depAsmName}.dll must resolve to {version}");

                var folder = Path.GetDirectoryName(app);
                var appName = Path.GetFileName(app);
                var bindingRedirects = $"{GlobalContext.OutputDir}\\BindingRedirects{folder}_{appName}.txt";
                var targetFiles = $"{GlobalContext.OutputDir}\\TargetFiles{folder}_{appName}.txt";
                Assert.That(RunTool($"{folder}\\{appName}", $"{folder}\\Solutions.txt", bindingRedirects, targetFiles, quiet: true), Is.Zero);
                return (bindingRedirects, targetFiles);
            }
        }
    }
}
