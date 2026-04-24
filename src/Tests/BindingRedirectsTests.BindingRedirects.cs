using System.IO;
using NUnit.Framework;

namespace Tests
{
    public partial class BindingRedirectsTests
    {
        public static TestCaseData[] MoreTestCases =>
        [
            new TestCaseData("6\\MinimalApp", "6\\Solutions.txt", new[] { "6\\Minimal.sln" }, new DependencyAssertion[] {
                new("IdentityModel", "3.10.10.0", "Newtonsoft.Json", "11.0.0.0", "12.0.0.0"),
                new("IdentityModel", "3.10.10.0", "System.Text.Encodings.Web", "4.0.3.0", "4.0.3.0"),
            }) { TestName = "{m}(Classical redirect)" },
            new TestCaseData("6\\TransitiveApp", "6\\TransitiveSolutions.txt", new[] { "6\\Transitive.sln" }, new DependencyAssertion[] {
                new("System.Threading.Channels", "7.0.0.0", "System.Threading.Tasks.Extensions", "4.2.0.1", "4.2.4.0"),
                new("Microsoft.Bcl.AsyncInterfaces", "10.0.0.5", "System.Threading.Tasks.Extensions", "4.2.4.0", "4.2.4.0"),
                new("System.Threading.Tasks.Extensions", "4.2.4.0", "System.Runtime.CompilerServices.Unsafe", "6.0.3.0", "6.0.3.0"),
            }) { TestName = "{m}(No redirect thru superseded transitive dependency)" },
            new TestCaseData("7\\AppB", "7\\Solutions.txt", new[] { "7\\Single.sln" }, new DependencyAssertion[] {
                new("LibA", "1.0.0.0", "Newtonsoft.Json", "12.0.0.0", "12.0.0.0"),
                new("IdentityModel", "3.10.10.0", "Newtonsoft.Json", "11.0.0.0", "12.0.0.0"),
                new("IdentityModel", "3.10.10.0", "System.Text.Encodings.Web", "4.0.3.0", "4.0.3.0"),
            }) { TestName = "{m}(Redirect thru project reference)" },
            new TestCaseData("8\\AppB", "8\\Solutions.txt", new[] { "8\\SolutionA.sln", "8\\SolutionB.sln" }, new DependencyAssertion[] {
                new("LibA", "1.0.0.0", "Newtonsoft.Json", "12.0.0.0", "11.0.0.0"),
                new("IdentityModel", "3.10.10.0", "Newtonsoft.Json", "11.0.0.0", "11.0.0.0"),
                new("IdentityModel", "3.10.10.0", "System.Text.Encodings.Web", "4.0.3.0", "4.0.3.0"),
            }) { TestName = "{m}(Redirect thru assembly reference across solutions)" },
        ];

        [TestCaseSource(nameof(MoreTestCases))]
        public void T(string projectPath, string solutionsFile, string[] solutions, DependencyAssertion[] assertions)
        {
            var area = Path.GetDirectoryName(projectPath);
            PrepareAreaWithBuild(area, solutions);

            var binDir = Path.Combine(WorkDir, projectPath, "bin", "Debug", "net472");
            var projectDll = Path.GetFileName(projectPath);
            var binDlls = GetBinDlls(binDir);

            VerifyAllBinDllsAccountedFor(assertions, binDlls, projectDll);
            VerifyILAssertions(assertions, binDlls);

            var expectedBindingRedirects = GenerateExpectedBindingRedirects(assertions, binDlls);

            using var noVersionControlScope = new NoGitVersionControl.Scope();
            var bindingRedirectsFilePath = $"{GlobalContext.OutputDir}\\BindingRedirects.txt";
            Assert.That(RunTool(projectPath, solutionsFile, bindingRedirectsFilePath), Is.Zero);

            var actualBindingRedirects = File.ReadAllText(bindingRedirectsFilePath).TrimEnd();
            Assert.That(actualBindingRedirects, Is.EqualTo(expectedBindingRedirects), "Tool output does not match expected binding redirects derived from first principles");

            SaveVerboseLog(projectPath);
        }
    }
}
