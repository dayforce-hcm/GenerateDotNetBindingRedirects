using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dayforce.CSharp.ProjectAssets;
using GenerateBindingRedirects;
using NuGet.Packaging.Core;
using NUnit.Framework;
using Log = GenerateBindingRedirects.Log;

namespace Tests
{
    [TestFixture]
    public class BindingRedirectsTests
    {
        private static readonly bool s_updateExpectedResults = false;

        public static IEnumerable<TestCaseData> ManyTestCases => (new (string, bool, bool)[]
        {
            ("1\\BJE", true, false),
            ("1\\ReportingSvc", false, false),
            ("1\\RuleEngineTests", false, false),
            ("1\\UnitTests", true, true),
            ("2\\CandidatePortal", false, false),
            ("2\\MyDayforce", false, false),
            ("3\\Api", false, false),
            ("3\\OData", false, false),
            ("4\\Clock", false, false)
        }).Select(CreateTestCase);

        public static TestCaseData[] AFewTestCases =>
        [
            CreateTestCase(("1\\DataSvc", false, false)),
            CreateTestCase(("1\\CommonTests", false, true))
        ];

        public static TestCaseData[] ATestCase =>
        [
            CreateTestCase(("1\\DataSvc", false, false))
        ];

        public static TestCaseData[] NewConfigFileTestCase =>
        [
            CreateTestCase(("1\\UnitTests", true, true))
        ];

        private readonly List<string> m_tempFilePaths = [];

        private static TestCaseData CreateTestCase((string Path, bool NewAppConfig, bool ModifiesProjectFile) a)
        {
            return new TestCaseData(
                $"{GlobalContext.RootDir}\\Input\\{a.Path}\\{Path.GetFileName(a.Path)}.csproj",
                $"{GlobalContext.RootDir}\\Expected\\{a.Path}",
                a.NewAppConfig,
                a.ModifiesProjectFile,
                $"src\\Tests\\Input\\{a.Path}\\.gitignore")
            {
                TestName = $"{{m}}({a.Path})"
            };
        }

        private string GetConfigFile(string projectFilePath, bool newAppConfig = false, string configFileFlavour = null)
        {
            var configFile = Path.GetFullPath($"{projectFilePath}\\..\\{(projectFilePath.EndsWith("BJE.csproj") || projectFilePath.EndsWith("Tests.csproj") ? "app" : "web")}{configFileFlavour}.config");
            if (newAppConfig)
            {
                Assert.That(configFile, Does.Not.Exist, "The config file is not supposed to exist before the test.");
            }
            else if (!s_updateExpectedResults)
            {
                Assert.That(configFile, Does.Exist, "Failed to locate the config file.");
            }
            if (configFileFlavour != null)
            {
                var finalConfigFile = configFile.Replace(configFileFlavour, "");
                File.Copy(configFile, finalConfigFile, true);
                m_tempFilePaths.Add(configFile = finalConfigFile);
            }
            return configFile;
        }

        [OneTimeSetUp]
        public static void OneTimeSetUp()
        {
            Array.ForEach(File.ReadAllLines($"{GlobalContext.RootDir}\\Input\\Solutions.txt"),
                slnFileName => GlobalContext.MSBuildExe.RestoreNuGetPackages(GlobalContext.RootDir + "\\Input\\" + slnFileName));
        }

        [SetUp]
        public void Setup()
        {
            // Skip setup for unit tests without test case arguments
            if (TestContext.CurrentContext.Test.Arguments.Length < 4)
            {
                return;
            }

            bool modifiesProjectFile = (bool)TestContext.CurrentContext.Test.Arguments[3];
            if (modifiesProjectFile)
            {
                var projectFile = (string)TestContext.CurrentContext.Test.Arguments[0];
                string projectFileBackup = $"{projectFile}.backup";
                if (!File.Exists(projectFileBackup))
                {
                    File.Copy(projectFile, projectFileBackup);
                }
            }

            Directory.CreateDirectory(GlobalContext.OutputDir);
        }

        [TearDown]
        public void TearDown()
        {
            // Skip teardown for unit tests without test case arguments
            if (TestContext.CurrentContext.Test.Arguments.Length < 4)
            {
                return;
            }

            GlobalContext.CleanOutputDir();

            bool modifiesProjectFile = (bool)TestContext.CurrentContext.Test.Arguments[3];
            if (modifiesProjectFile)
            {
                var projectFile = (string)TestContext.CurrentContext.Test.Arguments[0];
                string projectFileBackup = $"{projectFile}.backup";
                if (File.Exists(projectFileBackup))
                {
                    File.Move(projectFileBackup, projectFile, true);
                }
            }

            bool newAppConfig = (bool)TestContext.CurrentContext.Test.Arguments[2];
            if (newAppConfig)
            {
                var projectFile = (string)TestContext.CurrentContext.Test.Arguments[0];
                File.Delete($"{projectFile}\\..\\app.config");
                File.Delete($"{projectFile}\\..\\.gitignore");
            }
            m_tempFilePaths.ForEach(File.Delete);
            m_tempFilePaths.Clear();
        }

        [TestCaseSource(nameof(ManyTestCases))]
        public void Generate(string projectFilePath, string expectedDir, bool newAppConfig, bool _, string _2)
        {
            var actualTargetFilesFilePath = $"{GlobalContext.OutputDir}\\TargetFiles.txt";
            Assert.That(actualTargetFilesFilePath, Does.Not.Exist);

            var actualBindingRedirectsFilePath = $"{GlobalContext.OutputDir}\\BindingRedirects.txt";
            Assert.That(actualBindingRedirectsFilePath, Does.Not.Exist);
            string configFile = GetConfigFile(projectFilePath, newAppConfig);
            string gitIgnoreFilePath = configFile + "\\..\\.gitignore";
            var expectedConfigFileTimestamp = s_updateExpectedResults || newAppConfig ? default : File.GetLastWriteTimeUtc(configFile);

            Assert.That(gitIgnoreFilePath, Does.Not.Exist);
            m_tempFilePaths.Add(gitIgnoreFilePath);

            using var noVersionControlScope = new NoGitVersionControl.Scope();

            var args = new List<string>
            {
                "--test",
                "--projectFile",
                projectFilePath,
                "--targetFiles",
                actualTargetFilesFilePath,
                "--bindingRedirects",
                actualBindingRedirectsFilePath,
                "--solutions",
                $"{GlobalContext.RootDir}\\Input\\Solutions.txt",
                "--writeBindingRedirects",
                $"-v:{GlobalContext.OutputDir}"
            };

            if (projectFilePath.EndsWith("Tests.csproj"))
            {
                args.Add("--usePrivateProbingPath");
                args.Add("--outDir=bin");
            }

            Assert.That(Program.Main([.. args]), Is.Zero);
            Assert.That(Log.LogFilePath, Is.Not.Null, "Actual verbose log file not found.");
            Assert.That(gitIgnoreFilePath, Does.Exist);

            if (s_updateExpectedResults)
            {
                Directory.CreateDirectory(expectedDir);
                File.Move(actualTargetFilesFilePath, $"{expectedDir}\\TargetFiles.txt", true);
                File.Move(actualBindingRedirectsFilePath, $"{expectedDir}\\BindingRedirects.txt", true);
            }
            else
            {
                NUnit.Framework.Legacy.FileAssert.AreEqual($"{expectedDir}\\TargetFiles.txt", actualTargetFilesFilePath, "Target files do not match");
                NUnit.Framework.Legacy.FileAssert.AreEqual($"{expectedDir}\\BindingRedirects.txt", actualBindingRedirectsFilePath, "Binding Redirects do not match");
                if (newAppConfig)
                {
                    Assert.That(configFile, Does.Exist, $"The config file {configFile} was not created.");
                }
                else
                {
                    Assert.That(expectedConfigFileTimestamp, Is.EqualTo(File.GetLastWriteTimeUtc(configFile)), $"The config file {configFile} was modified.");
                }
            }
            File.Move(Log.LogFilePath, $"{expectedDir}\\Verbose.log", true);
        }

        [TestCaseSource(nameof(AFewTestCases))]
        public void GenerateCreateNewConfigFile(string projectFilePath, string _, bool _1, bool _2, string _3)
        {
            if (s_updateExpectedResults)
            {
                Assert.Ignore($"The test is irrelevant when updating the expected results.");
                return;
            }

            var expectedConfigFile = $"{GlobalContext.OutputDir}\\Config.xml";
            var actualConfigFile = GetConfigFile(projectFilePath, configFileFlavour: "-only-binding-redirects");
            File.Move(actualConfigFile, expectedConfigFile);
            Assert.That(actualConfigFile, Does.Not.Exist);

            var gitIgnoreFile = actualConfigFile + @"\..\.gitignore";
            Assert.That(gitIgnoreFile, Does.Not.Exist);
            m_tempFilePaths.Add(gitIgnoreFile);

            try
            {
                var (outDir, usePrivateProbingPath) = projectFilePath.EndsWith("Tests.csproj") ? ("bin", true) : default;

                Program.Run(
                    projectFilePath,
                    $"{GlobalContext.RootDir}\\Input\\Solutions.txt",
                    null, null, true, outDir, usePrivateProbingPath: usePrivateProbingPath);

                NUnit.Framework.Legacy.FileAssert.AreEqual(expectedConfigFile, actualConfigFile);
            }
            finally
            {
                File.Move(expectedConfigFile, actualConfigFile, true);
            }

            Assert.That(gitIgnoreFile, Does.Exist);
        }

        [TestCaseSource(nameof(ATestCase))]
        public void GenerateOverwriteMismatchingConfigFileNoRuntimeElement(string projectFilePath, string _, bool _1, bool _2, string _3)
        {
            GenerateOverwriteMismatchingConfigFile(projectFilePath, "runtime");
        }

        [TestCaseSource(nameof(ATestCase))]
        public void GenerateOverwriteMismatchingConfigFileNoAssemblyBindingElement(string projectFilePath, string _, bool _1, bool _2, string _3)
        {
            GenerateOverwriteMismatchingConfigFile(projectFilePath, "assemblyBinding");
        }

        [TestCaseSource(nameof(ATestCase))]
        public void GenerateOverwriteMismatchingConfigFileNoDependentAssemblyElement(string projectFilePath, string _, bool _1, bool _2, string _3)
        {
            GenerateOverwriteMismatchingConfigFile(projectFilePath, "dependentAssembly");
        }

        private void GenerateOverwriteMismatchingConfigFile(string projectFilePath, string elementName)
        {
            if (s_updateExpectedResults)
            {
                Assert.Ignore($"The test is irrelevant when updating the expected results.");
                return;
            }

            var openTag = "<" + elementName;
            var closeTag = "</" + elementName + ">";

            var expectedConfigFile = $"{GlobalContext.OutputDir}\\Config.xml";
            var actualConfigFile = GetConfigFile(projectFilePath, configFileFlavour: "-with-other-stuff");
            File.Move(actualConfigFile, expectedConfigFile);
            var skip = false;
            File.WriteAllLines(actualConfigFile, File.ReadLines(expectedConfigFile).Where(line =>
            {
                if (skip)
                {
                    skip = !line.Contains(closeTag);
                    return false;
                }
                skip = line.Contains(openTag);
                return !skip;
            }));
            NUnit.Framework.Legacy.FileAssert.AreNotEqual(expectedConfigFile, actualConfigFile);

            var gitIgnoreFile = actualConfigFile + @"\..\.gitignore";
            Assert.That(gitIgnoreFile, Does.Not.Exist);
            m_tempFilePaths.Add(gitIgnoreFile);

            try
            {
                var (outDir, usePrivateProbingPath) = projectFilePath.EndsWith("Tests.csproj") ? ("bin", true) : default;

                Program.Run(
                    projectFilePath,
                    $"{GlobalContext.RootDir}\\Input\\Solutions.txt",
                    null, null, true, outDir, usePrivateProbingPath: usePrivateProbingPath);

                NUnit.Framework.Legacy.FileAssert.AreEqual(expectedConfigFile, actualConfigFile);
            }
            finally
            {
                File.Move(expectedConfigFile, actualConfigFile, true);
            }

            Assert.That(gitIgnoreFile, Does.Exist);
        }

        [TestCaseSource(nameof(AFewTestCases))]
        public void AssertPassInVersionControl(string projectFilePath, string expectedDir, bool _1, bool _2, string _3)
        {
            if (s_updateExpectedResults)
            {
                Assert.Ignore($"The test is irrelevant when updating the expected results.");
                return;
            }

            using var forceGitVersionControlScope = new ForceGitVersionControl.Scope();

            var actualTargetFilesFilePath = $"{GlobalContext.OutputDir}\\TargetFiles.txt";
            var bindingRedirectsFilePath = $"{expectedDir}\\BindingRedirects.txt";
            var expectedBindingRedirectsFileTimestamp = File.GetLastWriteTimeUtc(bindingRedirectsFilePath);
            var configFile = GetConfigFile(projectFilePath, configFileFlavour: "-with-other-stuff");
            var expectedConfigFileTimestamp = File.GetLastWriteTimeUtc(configFile);

            var gitIgnoreFile = configFile + @"\..\.gitignore";
            Assert.That(gitIgnoreFile, Does.Not.Exist);

            var (outDir, usePrivateProbingPath) = projectFilePath.EndsWith("Tests.csproj") ? ("bin", true) : default;

            Program.Run(
                projectFilePath,
                $"{GlobalContext.RootDir}\\Input\\Solutions.txt",
                actualTargetFilesFilePath,
                bindingRedirectsFilePath,
                true, outDir, true, true, usePrivateProbingPath: usePrivateProbingPath);

            NUnit.Framework.Legacy.FileAssert.AreEqual($"{expectedDir}\\TargetFiles.txt", actualTargetFilesFilePath, "Target files do not match");
            Assert.That(expectedBindingRedirectsFileTimestamp, Is.EqualTo(File.GetLastWriteTimeUtc(bindingRedirectsFilePath)), $"The binding redirects file {bindingRedirectsFilePath} was modified.");
            Assert.That(expectedConfigFileTimestamp, Is.EqualTo(File.GetLastWriteTimeUtc(configFile)), $"The config file {configFile} was modified.");
            Assert.That(gitIgnoreFile, Does.Not.Exist);
        }

        [TestCaseSource(nameof(ATestCase))]
        public void FailOnlyBindingRedirectsInVersionControl(string projectFilePath, string expectedDir, bool _1, bool _2, string _3)
        {
            if (s_updateExpectedResults)
            {
                Assert.Ignore($"The test is irrelevant when updating the expected results.");
                return;
            }

            using var forceGitVersionControlScope = new ForceGitVersionControl.Scope();

            var actualTargetFilesFilePath = $"{GlobalContext.OutputDir}\\TargetFiles.txt";
            var bindingRedirectsFilePath = $"{expectedDir}\\BindingRedirects.txt";
            var expectedBindingRedirectsFileTimestamp = File.GetLastWriteTimeUtc(bindingRedirectsFilePath);
            var configFile = GetConfigFile(projectFilePath, configFileFlavour: "-only-binding-redirects");
            var expectedConfigFileTimestamp = File.GetLastWriteTimeUtc(configFile);

            var gitIgnoreFile = configFile + @"\..\.gitignore";
            Assert.That(gitIgnoreFile, Does.Not.Exist);

            var (outDir, usePrivateProbingPath) = projectFilePath.EndsWith("Tests.csproj") ? ("bin", true) : default;

            var exc = Assert.Throws<ApplicationException>(() => Program.Run(
                projectFilePath,
                $"{GlobalContext.RootDir}\\Input\\Solutions.txt",
                actualTargetFilesFilePath,
                bindingRedirectsFilePath,
                true, outDir, true, true, usePrivateProbingPath: usePrivateProbingPath));

            Assert.That($"{configFile.GetRelativeToGitWorkspaceRoot()} only contains binding redirects. Remove this file from the version control. Make sure to commit the .gitignore file instead. It is created or updated automatically by the local build, if that file does not exist or does not ignore app.config already.", Is.EqualTo(exc.Message));
            Assert.That(gitIgnoreFile, Does.Not.Exist);
        }

        [TestCaseSource(nameof(ATestCase))]
        public void PassOnlyBindingRedirectsOutsideVersionControl(string projectFilePath, string expectedDir, bool _1, bool _2, string _3)
        {
            if (s_updateExpectedResults)
            {
                Assert.Ignore($"The test is irrelevant when updating the expected results.");
                return;
            }

            var actualTargetFilesFilePath = $"{GlobalContext.OutputDir}\\TargetFiles.txt";
            var bindingRedirectsFilePath = $"{expectedDir}\\BindingRedirects.txt";
            var expectedBindingRedirectsFileTimestamp = File.GetLastWriteTimeUtc(bindingRedirectsFilePath);
            var configFile = GetConfigFile(projectFilePath, configFileFlavour: "-only-binding-redirects");
            var expectedConfigFileTimestamp = File.GetLastWriteTimeUtc(configFile);

            var gitIgnoreFile = configFile + @"\..\.gitignore";
            Assert.That(gitIgnoreFile, Does.Not.Exist);
            m_tempFilePaths.Add(gitIgnoreFile);

            File.WriteAllText(gitIgnoreFile, "app.config\r\n");

            var (outDir, usePrivateProbingPath) = projectFilePath.EndsWith("Tests.csproj") ? ("bin", true) : default;

            Program.Run(
                projectFilePath,
                $"{GlobalContext.RootDir}\\Input\\Solutions.txt",
                actualTargetFilesFilePath,
                bindingRedirectsFilePath,
                true, outDir, true, true, usePrivateProbingPath: usePrivateProbingPath);
        }

        [TestCaseSource(nameof(ATestCase))]
        public void AssertFailNoBindingRedirectsTxtFile(string projectFilePath, string _, bool _1, bool _2, string _3)
        {
            if (s_updateExpectedResults)
            {
                Assert.Ignore($"The test is irrelevant when updating the expected results.");
                return;
            }

            var nonExistingBindingRedirectsFilePath = $"{GlobalContext.OutputDir}\\BindingRedirects.txt";
            Assert.That(nonExistingBindingRedirectsFilePath, Does.Not.Exist);

            var exc = Assert.Throws<ApplicationException>(() => Program.Run(
                projectFilePath,
                $"{GlobalContext.RootDir}\\Input\\Solutions.txt",
                null,
                nonExistingBindingRedirectsFilePath,
                false, null, true));

            Assert.That($"Found some binding redirects, but {nonExistingBindingRedirectsFilePath} does not exist.", Is.EqualTo(exc.Message));
        }

        [TestCaseSource(nameof(ATestCase))]
        public void AssertFailMismatchingBindingRedirectsInTxtFile(string projectFilePath, string expectedDir, bool _1, bool _2, string _3)
        {
            if (s_updateExpectedResults)
            {
                Assert.Ignore($"The test is irrelevant when updating the expected results.");
                return;
            }

            var mismatchingBindingRedirectsFilePath = $"{GlobalContext.OutputDir}\\BindingRedirects.txt";
            string expectedBindingRedirectsFilePath = $"{expectedDir}\\BindingRedirects.txt";
            File.WriteAllLines(mismatchingBindingRedirectsFilePath, File.ReadAllLines(expectedBindingRedirectsFilePath).Skip(4));
            NUnit.Framework.Legacy.FileAssert.AreNotEqual(expectedBindingRedirectsFilePath, mismatchingBindingRedirectsFilePath);

            var exc = Assert.Throws<ApplicationException>(() => Program.Run(
                projectFilePath,
                $"{GlobalContext.RootDir}\\Input\\Solutions.txt",
                null,
                mismatchingBindingRedirectsFilePath,
                false, null, true));

            Assert.That($"Actual binding redirects in {mismatchingBindingRedirectsFilePath} do not match the expectation.", Is.EqualTo(exc.Message));
        }

        [TestCaseSource(nameof(NewConfigFileTestCase))]
        public void ForceAssertFailNoConfigFile(string projectFilePath, string _, bool _1, bool _2, string _3)
        {
            if (s_updateExpectedResults)
            {
                Assert.Ignore($"The test is irrelevant when updating the expected results.");
                return;
            }

            var configFilePath = Path.GetFullPath($"{projectFilePath}\\..\\app.config");
            Assert.That(configFilePath, Does.Not.Exist);

            var exc = Assert.Throws<ApplicationException>(() => Program.Run(
                projectFilePath,
                $"{GlobalContext.RootDir}\\Input\\Solutions.txt",
                null,
                null,
                false, null, forceAssert: true));

            Assert.That($"{configFilePath.GetRelativeToGitWorkspaceRoot()} is expected to have some assembly binding redirects, but it does not exist.", Is.EqualTo(exc.Message));
        }

        [TestCaseSource(nameof(NewConfigFileTestCase))]
        public void AssertNoFailNewConfigFileWithGitIgnore(string projectFilePath, string _, bool _1, bool _2, string _3)
        {
            if (s_updateExpectedResults)
            {
                Assert.Ignore($"The test is irrelevant when updating the expected results.");
                return;
            }

            var configFilePath = Path.GetFullPath($"{projectFilePath}\\..\\app.config");
            Assert.That(configFilePath, Does.Not.Exist);

            var gitIgnoreFilePath = configFilePath + "\\..\\.gitignore";
            Assert.That(gitIgnoreFilePath, Does.Not.Exist);
            File.WriteAllText(gitIgnoreFilePath, "app.config\r\n");

            Program.Run(
                projectFilePath,
                $"{GlobalContext.RootDir}\\Input\\Solutions.txt",
                null,
                null,
                false, null, true);

            Assert.That(configFilePath, Does.Exist);
            Assert.That(gitIgnoreFilePath, Does.Exist);
        }

        [TestCaseSource(nameof(NewConfigFileTestCase))]
        public void AssertFailNewConfigFileNoGitIgnore(string projectFilePath, string _, bool _1, bool _2, string expectedRelGitConfigPath)
        {
            if (s_updateExpectedResults)
            {
                Assert.Ignore($"The test is irrelevant when updating the expected results.");
                return;
            }

            var configFilePath = Path.GetFullPath($"{projectFilePath}\\..\\app.config");
            var gitIgnoreFilePath = configFilePath + "\\..\\.gitignore";
            Assert.That(configFilePath, Does.Not.Exist);
            Assert.That(gitIgnoreFilePath, Does.Not.Exist);

            var exc = Assert.Throws<ApplicationException>(() => Program.Run(
                projectFilePath,
                $"{GlobalContext.RootDir}\\Input\\Solutions.txt",
                null,
                null,
                false, null, true));

            Assert.That(configFilePath, Does.Not.Exist);
            Assert.That(gitIgnoreFilePath, Does.Not.Exist);

            Assert.That($"{expectedRelGitConfigPath} not found. The local build is expected to automatically create the {expectedRelGitConfigPath} file, which causes the git status to show it as a new file. Looks like {expectedRelGitConfigPath} was omitted explicitly from the commit. Please, include it.", Is.EqualTo(exc.Message));
        }

        [TestCaseSource(nameof(NewConfigFileTestCase))]
        public void ForceAssertFailMismatchingBindingRedirectsInConfigFileWithGitIgnore(string projectFilePath, string _, bool _1, bool _2, string _3)
        {
            if (s_updateExpectedResults)
            {
                Assert.Ignore($"The test is irrelevant when updating the expected results.");
                return;
            }

            var configFilePath = Path.GetFullPath($"{projectFilePath}\\..\\app.config");
            var gitIgnoreFilePath = configFilePath + "\\..\\.gitignore";
            Assert.That(configFilePath, Does.Not.Exist);
            Assert.That(gitIgnoreFilePath, Does.Not.Exist);
            File.Copy($"{configFilePath}\\..\\MismatchingApp.config", configFilePath);
            File.WriteAllText(gitIgnoreFilePath, "app.config\r\n");

            var exc = Assert.Throws<ApplicationException>(() => Program.Run(
                projectFilePath,
                $"{GlobalContext.RootDir}\\Input\\Solutions.txt",
                null,
                null,
                false, null, forceAssert: true));

            Assert.That($"{configFilePath.GetRelativeToGitWorkspaceRoot()} does not have the expected set of binding redirects.", Is.EqualTo(exc.Message));
        }

        [TestCaseSource(nameof(NewConfigFileTestCase))]
        public void ForceAssertFailMismatchingBindingRedirectsInConfigFileNoGitIgnore(string projectFilePath, string _, bool _1, bool _2, string expectedRelGitConfigPath)
        {
            if (s_updateExpectedResults)
            {
                Assert.Ignore($"The test is irrelevant when updating the expected results.");
                return;
            }

            var configFilePath = Path.GetFullPath($"{projectFilePath}\\..\\app.config");
            var gitIgnoreFilePath = configFilePath + "\\..\\.gitignore";
            Assert.That(configFilePath, Does.Not.Exist);
            Assert.That(gitIgnoreFilePath, Does.Not.Exist);
            File.Copy($"{configFilePath}\\..\\MismatchingApp.config", configFilePath);

            var exc = Assert.Throws<ApplicationException>(() => Program.Run(
                projectFilePath,
                $"{GlobalContext.RootDir}\\Input\\Solutions.txt",
                null,
                null,
                false, null, forceAssert: true));

            Assert.That($"{expectedRelGitConfigPath} not found. The local build is expected to automatically create the {expectedRelGitConfigPath} file, which causes the git status to show it as a new file. Looks like {expectedRelGitConfigPath} was omitted explicitly from the commit. Please, include it.", Is.EqualTo(exc.Message));
        }

        /// <summary>
        /// Tests that System.ValueTuple (a known .NET Standard facade assembly) is correctly skipped
        /// when unresolved, hitting the new Log.WriteVerbose statement:
        /// "NewAssemblyBindingRedirect : skip known .NET Standard facade assembly {asmName}, Version = {maxAsmVersion}"
        /// </summary>
        [Test]
        public void GetAssemblyBindingRedirect_SystemValueTupleUnresolved_SkipsWithVerboseLog()
        {
            var assemblyName = "System.ValueTuple";
            var asmVersion = Version.Parse("4.0.5.0");
            var packageDependency = new PackageDependency(assemblyName);
            var mockDependency = new NuGetDependency(packageDependency, [RuntimeAssembly.Unresolved]);

            var dependenciesByVersion = new Dictionary<Version, Dictionary<RuntimeAssembly, Dictionary<NuGetDependency, List<LibraryItem>>>>
            {
                [asmVersion] = new Dictionary<RuntimeAssembly, Dictionary<NuGetDependency, List<LibraryItem>>>
                {
                    [RuntimeAssembly.Unresolved] = new Dictionary<NuGetDependency, List<LibraryItem>>
                    {
                        [mockDependency] = []
                    }
                }
            };

            var frameworkRedistList = new Dictionary<(string, Version), AssemblyBindingRedirect>();

            var result = Program.GetAssemblyBindingRedirect(assemblyName, dependenciesByVersion, frameworkRedistList);

            Assert.That(result, Is.Null, $"Expected null for known .NET Standard facade assembly {assemblyName} v{asmVersion}. " + "This proves the new KnownNetStandardFacades check with Log.WriteVerbose was executed successfully.");
        }
    }
}