using System.IO;
using GenerateBindingRedirects;
using NUnit.Framework;

namespace Tests
{
    public partial class BindingRedirectsTests
    {
        [TestCase("6\\MinimalApp", "6\\Solutions.txt", "6\\Minimal.sln")]
        public void PrivateProbingPathGeneratesCodeBaseAndCopiesDlls(string projectPath, string solutionsFile, string sln)
        {
            PrepareArea(Path.GetDirectoryName(projectPath), sln);
            using var ctx = new ProjectTestContext(projectPath, solutionsFile);
            using var outDir = new TempDirectory($"{GlobalContext.OutputDir}\\probing");
            Assert.That(ctx.ConfigFile, Does.Not.Exist, "Config must not exist before the tool runs");

            using var noVersionControlScope = new NoGitVersionControl.Scope();
            var bindingRedirectsFilePath = $"{GlobalContext.OutputDir}\\BindingRedirects.txt";
            Program.Run(ctx.ProjectFile, ctx.SlnFile, null, bindingRedirectsFilePath, true, outDir.Path, usePrivateProbingPath: true);

            AssertFileContains(bindingRedirectsFilePath, "<codeBase");
            AssertFileContains(bindingRedirectsFilePath, "_BindingRedirects/Newtonsoft.Json/");

            Assert.That(ctx.ConfigFile, Does.Exist);
            AssertFileContains(ctx.ConfigFile, "<codeBase");

            var newtonsoftDir = Directory.GetDirectories(outDir.Path + "\\_BindingRedirects\\Newtonsoft.Json");
            Assert.That(newtonsoftDir, Has.Length.EqualTo(1));
            Assert.That(Path.Combine(newtonsoftDir[0], "Newtonsoft.Json.dll"), Does.Exist);
        }

        [Test]
        public void PrivateProbingPathCopiesConfluentKafkaNativeLibraries()
        {
            PrepareArea("16", "16\\Kafka.sln");
            using var ctx = new ProjectTestContext("16\\KafkaApp", "16\\Solutions.txt");
            using var outDir = new TempDirectory($"{GlobalContext.OutputDir}\\kafka-probing");
            Assert.That(ctx.ConfigFile, Does.Not.Exist, "Config must not exist before the tool runs");

            using var noVersionControlScope = new NoGitVersionControl.Scope();
            Program.Run(ctx.ProjectFile, ctx.SlnFile, null, null, true, outDir.Path, usePrivateProbingPath: true);

            var kafkaDirs = Directory.GetDirectories(outDir.Path + "\\_BindingRedirects\\Confluent.Kafka");
            Assert.That(kafkaDirs, Has.Length.EqualTo(1));
            Assert.That(Path.Combine(kafkaDirs[0], "Confluent.Kafka.dll"), Does.Exist);

            var librdkafkaDir = Path.Combine(kafkaDirs[0], "librdkafka");
            Assert.That(Path.Combine(librdkafkaDir, "x64", "librdkafka.dll"), Does.Exist);
            Assert.That(Path.Combine(librdkafkaDir, "x86", "librdkafka.dll"), Does.Exist);
        }
    }
}
