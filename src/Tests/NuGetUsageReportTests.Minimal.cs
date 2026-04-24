using System.IO;
using GenerateNuGetUsageReport;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests
{
    [TestFixture]
    public class MinimalNuGetUsageReportTests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var slnPath = Path.Combine(GlobalContext.RootDir, "Input", "6", "Minimal.sln");
            GlobalContext.MSBuildExe.RestoreNuGetPackages(slnPath);
        }

        [TearDown]
        public void TearDown()
        {
            GlobalContext.CleanOutputDir();
        }

        [Test]
        public void GenerateReportContainsExpectedPackages()
        {
            Directory.CreateDirectory(GlobalContext.OutputDir);

            var reportFilePath = $"{GlobalContext.OutputDir}\\NuGetUsageReport-MinimalApp.json";

            Assert.That(Program.Main([
                "-s", $"{GlobalContext.RootDir}\\Input\\6\\Solutions.txt",
                "-f", $"{GlobalContext.RootDir}\\Input\\6\\MinimalApp\\MinimalApp.csproj",
                "-u", GlobalContext.OutputDir
            ]), Is.Zero);

            Assert.That(reportFilePath, Does.Exist);

            var report = JObject.Parse(File.ReadAllText(reportFilePath));
            var packages = report["Packages"] as JObject;
            Assert.That(packages, Is.Not.Null);

            Assert.That(packages.ContainsKey("Newtonsoft.Json"), "Report must contain Newtonsoft.Json");
            Assert.That(packages.ContainsKey("IdentityModel"), "Report must contain IdentityModel");

            var newtonsoft = packages["Newtonsoft.Json"];
            Assert.That(newtonsoft["NuGetVersion"]?.ToString(), Is.EqualTo("12.0.3"));
            Assert.That(newtonsoft["RuntimeAssemblies"]?.First?.ToString(), Is.EqualTo("Newtonsoft.Json.dll"));
            Assert.That(newtonsoft["Metadata"]?["Authors"]?.ToString(), Is.Not.Null.And.Not.Empty);

            var identityModel = packages["IdentityModel"];
            Assert.That(identityModel["NuGetVersion"]?.ToString(), Is.EqualTo("3.10.10"));
            Assert.That(identityModel["RuntimeAssemblies"]?.First?.ToString(), Is.EqualTo("IdentityModel.dll"));
        }

        [Test]
        public void GenerateReportCompatMode()
        {
            Directory.CreateDirectory(GlobalContext.OutputDir);

            var reportFilePath = $"{GlobalContext.OutputDir}\\NuGetUsageReport-MinimalApp.json";

            Assert.That(Program.Main([
                "-s", $"{GlobalContext.RootDir}\\Input\\6\\Solutions.txt",
                "-f", $"{GlobalContext.RootDir}\\Input\\6\\MinimalApp\\MinimalApp.csproj",
                "-u", GlobalContext.OutputDir,
                "--compat"
            ]), Is.Zero);

            Assert.That(reportFilePath, Does.Exist);

            var report = JObject.Parse(File.ReadAllText(reportFilePath));

            // In compat mode, the top-level is the packages map directly (no PackageFolders wrapper)
            Assert.That(report.ContainsKey("Newtonsoft.Json"), "Compat mode: top-level must contain packages directly");
            Assert.That(report.ContainsKey("PackageFolders"), Is.False, "Compat mode: no PackageFolders wrapper");
        }

        [Test]
        public void GenerateReportExcludesPackagesWithNoRuntimeAssemblies()
        {
            Directory.CreateDirectory(GlobalContext.OutputDir);

            var reportFilePath = $"{GlobalContext.OutputDir}\\NuGetUsageReport-MinimalApp.json";

            Assert.That(Program.Main([
                "-s", $"{GlobalContext.RootDir}\\Input\\6\\Solutions.txt",
                "-f", $"{GlobalContext.RootDir}\\Input\\6\\MinimalApp\\MinimalApp.csproj",
                "-u", GlobalContext.OutputDir
            ]), Is.Zero);

            var report = JObject.Parse(File.ReadAllText(reportFilePath));
            var packages = report["Packages"] as JObject;

            // System.Text.Encodings.Web has runtime assemblies and should be included
            Assert.That(packages.ContainsKey("System.Text.Encodings.Web"),
                "Packages with runtime assemblies must be in the report");

            // Verify no package in the report has zero runtime assemblies
            foreach (var (name, value) in packages)
            {
                var runtimeAssemblies = value["RuntimeAssemblies"];
                Assert.That(runtimeAssemblies, Is.Not.Null.And.Not.Empty,
                    $"Package {name} should have runtime assemblies");
            }
        }
    }
}
