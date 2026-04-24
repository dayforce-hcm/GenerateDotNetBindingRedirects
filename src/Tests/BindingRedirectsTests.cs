using System;
using System.Collections.Generic;
using System.IO;
using Dayforce.CSharp.ProjectAssets;
using GenerateBindingRedirects;
using NuGet.Packaging.Core;
using NUnit.Framework;

namespace Tests
{
    [TestFixture]
    public partial class BindingRedirectsTests
    {
        [SetUp]
        public void Setup()
        {
            if (Directory.Exists(WorkDir))
            {
                Directory.Delete(WorkDir, true);
            }
            Directory.CreateDirectory(GlobalContext.OutputDir);
        }

        [TearDown]
        public void TearDown()
        {
            GlobalContext.CleanOutputDir();

            if (Directory.Exists(WorkDir))
            {
                Directory.Delete(WorkDir, true);
            }
        }

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

            Assert.That(result, Is.Null, $"Expected null for known .NET Standard facade assembly {assemblyName} v{asmVersion}");
        }

        [Test]
        public void GetAssemblyBindingRedirect_FrameworkAssembly_Skipped()
        {
            var assemblyName = "System.IO.Compression";
            var unresolvedVersion = Version.Parse("4.2.0.0");
            var resolvedVersion = Version.Parse("4.1.0.0");
            var packageDependency = new PackageDependency(assemblyName);
            var unresolvedDep = new NuGetDependency(packageDependency, [RuntimeAssembly.Unresolved]);

            var dependenciesByVersion = new Dictionary<Version, Dictionary<RuntimeAssembly, Dictionary<NuGetDependency, List<LibraryItem>>>>
            {
                [unresolvedVersion] = new()
                {
                    [RuntimeAssembly.Unresolved] = new() { [unresolvedDep] = [] }
                },
                [resolvedVersion] = new()
                {
                    [RuntimeAssembly.Unresolved] = new() { [unresolvedDep] = [] }
                }
            };

            var frameworkRedistList = new Dictionary<(string, Version), AssemblyBindingRedirect>
            {
                [(assemblyName, unresolvedVersion)] = new AssemblyBindingRedirect(assemblyName, unresolvedVersion, "neutral", "b77a5c561934e089"),
                [(assemblyName, resolvedVersion)] = new AssemblyBindingRedirect(assemblyName, resolvedVersion, "neutral", "b77a5c561934e089"),
            };

            var result = Program.GetAssemblyBindingRedirect(assemblyName, dependenciesByVersion, frameworkRedistList);

            Assert.That(result, Is.Null, "Framework assemblies in the redist list must be skipped");
        }

        [Test]
        public void QuietModeProducesNoVerboseLog()
        {
            PrepareArea("6", "6\\Minimal.sln");

            using var noVersionControlScope = new NoGitVersionControl.Scope();
            var bindingRedirectsFilePath = $"{GlobalContext.OutputDir}\\BindingRedirects.txt";
            Assert.That(RunTool("6\\MinimalApp", "6\\Solutions.txt", bindingRedirectsFilePath, quiet: true), Is.Zero);

            AssertFileContains(bindingRedirectsFilePath, "Newtonsoft.Json");

            var verboseLogs = Directory.GetFiles(GlobalContext.OutputDir, "verbose.log", SearchOption.AllDirectories);
            Assert.That(verboseLogs, Is.Empty, "Quiet mode must not create a verbose log");
        }
    }
}
