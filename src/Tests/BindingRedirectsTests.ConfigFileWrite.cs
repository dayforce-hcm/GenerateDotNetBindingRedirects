using System;
using System.IO;
using System.Linq;
using GenerateBindingRedirects;
using NUnit.Framework;

namespace Tests
{
    public partial class BindingRedirectsTests
    {
        [TestCase("6\\MinimalApp", "6\\Solutions.txt", "6\\Minimal.sln", "app.config", false)]
        [TestCase("11\\LegacyApp", "11\\Solutions.txt", "11\\Legacy.sln", "app.config", true)]
        [TestCase("12\\LegacyWeb", "12\\Solutions.txt", "12\\LegacyWeb.sln", "web.config", false)]
        [TestCase("13\\SdkWeb", "13\\Solutions.txt", "13\\SdkWeb.sln", "web.config", false)]
        public void WriteCreatesNewConfigFile(string projectPath, string solutionsFile, string sln, string expectedConfigName, bool isLegacy)
        {
            PrepareArea(Path.GetDirectoryName(projectPath), sln);
            using var ctx = new ProjectTestContext(projectPath, solutionsFile, expectedConfigName);
            Assert.That(ctx.ConfigFile, Does.Not.Exist, $"{expectedConfigName} must not exist before the tool runs");
            if (isLegacy)
            {
                AssertFileDoesNotContain(ctx.ProjectFile, expectedConfigName, $"Legacy csproj must NOT reference {expectedConfigName} before the tool runs");
            }

            using var noVersionControlScope = new NoGitVersionControl.Scope();
            Assert.That(RunTool(projectPath, solutionsFile, writeBindingRedirects: true), Is.Zero);

            Assert.That(ctx.ConfigFile, Does.Exist, $"{expectedConfigName} was not created");
            AssertFileContains(ctx.ConfigFile, "Newtonsoft.Json");

            if (isLegacy)
            {
                AssertFileContains(ctx.ProjectFile, expectedConfigName, $"Legacy csproj must include {expectedConfigName}");
            }

            SaveVerboseLog(projectPath);
        }

        [TestCase("6\\MinimalApp", "6\\Solutions.txt", "6\\Minimal.sln", "runtime")]
        [TestCase("6\\MinimalApp", "6\\Solutions.txt", "6\\Minimal.sln", "assemblyBinding")]
        [TestCase("6\\MinimalApp", "6\\Solutions.txt", "6\\Minimal.sln", "dependentAssembly")]
        [TestCase("11\\LegacyApp", "11\\Solutions.txt", "11\\Legacy.sln", "runtime")]
        [TestCase("11\\LegacyApp", "11\\Solutions.txt", "11\\Legacy.sln", "assemblyBinding")]
        [TestCase("11\\LegacyApp", "11\\Solutions.txt", "11\\Legacy.sln", "dependentAssembly")]
        public void WriteOverwritesMismatchingConfigFile(string projectPath, string solutionsFile, string sln, string missingElement)
        {
            PrepareArea(Path.GetDirectoryName(projectPath), sln);
            using var ctx = new ProjectTestContext(projectPath, solutionsFile);
            var correctConfig = BuildConfigWithOtherStuff(ctx);

            var openTag = "<" + missingElement;
            var closeTag = "</" + missingElement + ">";
            var skip = false;
            var stripped = string.Join(Environment.NewLine, correctConfig.Split(Environment.NewLine).Where(line =>
            {
                if (skip)
                {
                    skip = !line.Contains(closeTag);
                    return false;
                }
                skip = line.Contains(openTag);
                return !skip;
            }));

            Assert.That(stripped, Does.Not.Contain("Newtonsoft.Json"), $"Stripped config (missing <{missingElement}>) must not contain binding redirects");

            File.WriteAllText(ctx.ConfigFile, stripped);
            using var noVersionControlScope = new NoGitVersionControl.Scope();
            Program.Run(ctx.ProjectFile, ctx.SlnFile, null, null, true);

            AssertFileContains(ctx.ConfigFile, "Newtonsoft.Json", "Config should have been restored with redirects");
        }

        [TestCase("6\\MinimalApp", "6\\Solutions.txt", "6\\Minimal.sln")]
        public void WriteDoesNotModifyCorrectConfig(string projectPath, string solutionsFile, string sln)
        {
            PrepareArea(Path.GetDirectoryName(projectPath), sln);
            using var ctx = new ProjectTestContext(projectPath, solutionsFile);
            var correctConfig = BuildConfigWithOtherStuff(ctx);

            File.WriteAllText(ctx.ConfigFile, correctConfig);
            var timestamp = File.GetLastWriteTimeUtc(ctx.ConfigFile);

            using var noVersionControlScope = new NoGitVersionControl.Scope();
            Program.Run(ctx.ProjectFile, ctx.SlnFile, null, null, true);

            Assert.That(File.GetLastWriteTimeUtc(ctx.ConfigFile), Is.EqualTo(timestamp));
        }

        private static string BuildConfigWithOtherStuff(ProjectTestContext ctx)
        {
            using var noVersionControlScope = new NoGitVersionControl.Scope();
            Program.Run(ctx.ProjectFile, ctx.SlnFile, null, null, true);

            var content = File.ReadAllText(ctx.ConfigFile);
            var insertionPoint = content.IndexOf("<runtime>");
            var withExtra = content.Insert(insertionPoint, "  <connectionStrings />" + Environment.NewLine + "  ");

            File.Delete(ctx.ConfigFile);
            File.Delete(ctx.GitIgnoreFile);

            return withExtra;
        }
    }
}
