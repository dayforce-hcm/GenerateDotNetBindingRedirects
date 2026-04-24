using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GenerateBindingRedirects;
using Mono.Cecil;
using NUnit.Framework;
using Log = GenerateBindingRedirects.Log;

namespace Tests
{
    public partial class BindingRedirectsTests
    {
        private static readonly string WorkDir = Path.Combine(Path.GetTempPath(), "GenerateBindingRedirects-Tests-7A3F2B1C");

        public record struct DependencyAssertion(string Referrer, string ReferrerVersion, string Target, string ReferencedVersion, string DeployedVersion);

        private static string GetProjectDependencyVersion(string projectPath, string depAsmName) =>
            GetAssemblyVersion(Path.Combine(WorkDir, projectPath, "bin", "Debug", "net472", $"{depAsmName}.dll"));

        private static string GetAssemblyVersion(string assemblyPath)
        {
            using var module = ModuleDefinition.ReadModule(assemblyPath);
            return GetModuleVersion(module);
        }

        private static string GetModuleVersion(ModuleDefinition module) => module.Assembly.Name.Version.ToString();

        private static string GenerateExpectedBindingRedirects(DependencyAssertion[] assertions, Dictionary<string, string> binDlls)
        {
            var allVersionsByTarget = assertions
                .GroupBy(a => a.Target, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.SelectMany(a => new[] { Version.Parse(a.ReferencedVersion), Version.Parse(a.DeployedVersion) }).ToHashSet(), StringComparer.OrdinalIgnoreCase);
            var redirects = allVersionsByTarget
                .Where(kvp => kvp.Value.Count > 1)
                .Select(kvp => (Target: kvp.Key, MaxVersion: kvp.Value.Max()))
                .Select(r =>
                {
                    var asmName = System.Reflection.AssemblyName.GetAssemblyName(binDlls[r.Target]);
                    var publicKeyToken = Convert.ToHexString(asmName.GetPublicKeyToken());
                    var culture = CultureInfo.InvariantCulture.Equals(asmName.CultureInfo) ? "neutral" : asmName.CultureName;
                    return "      <dependentAssembly>" + Environment.NewLine +
                        $"        <assemblyIdentity name=\"{r.Target}\" publicKeyToken=\"{publicKeyToken}\" culture=\"{culture}\" />" + Environment.NewLine +
                        $"        <bindingRedirect oldVersion=\"0.0.0.0-{r.MaxVersion}\" newVersion=\"{r.MaxVersion}\" />" + Environment.NewLine +
                        "      </dependentAssembly>";
                })
                .OrderBy(o => o);
            return string.Join(Environment.NewLine, redirects);
        }

        private static Dictionary<string, string> GetBinDlls(string binDir) =>
            Directory.GetFiles(binDir, "*.dll").ToDictionary(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase);

        private static void VerifyILAssertions(DependencyAssertion[] assertions, Dictionary<string, string> binDlls)
        {
            foreach (var (referrer, referrerVersion, target, referencedVersion, deployedVersion) in assertions)
            {
                using var module = ModuleDefinition.ReadModule(binDlls[referrer]);
                Assert.That(GetModuleVersion(module), Is.EqualTo(referrerVersion), $"{referrer} version mismatch");

                var asmRef = module.AssemblyReferences.FirstOrDefault(r => r.Name == target);
                Assert.That(asmRef, Is.Not.Null, $"{referrer} must reference {target}");
                Assert.That(asmRef.Version.ToString(), Is.EqualTo(referencedVersion), $"{referrer} references {target} at wrong version");

                Assert.That(GetAssemblyVersion(binDlls[target]), Is.EqualTo(deployedVersion), $"Deployed {target}.dll has wrong version");
            }
        }

        private static void VerifyAllBinDllsAccountedFor(DependencyAssertion[] assertions, Dictionary<string, string> binDlls, string projectDll)
        {
            var accountedFor = assertions.Select(a => a.Target)
                .Concat(assertions.Select(a => a.Referrer))
                .Append(projectDll)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unaccounted = binDlls.Keys.Where(k => !accountedFor.Contains(k)).OrderBy(o => o).ToList();
            Assert.That(unaccounted, Is.Empty, $"Unaccounted DLLs in bin: {string.Join(", ", unaccounted)}");
        }

        private static void AssertFileContains(string filePath, string expected, string message = null) =>
            Assert.That(File.ReadAllText(filePath), Does.Contain(expected), message ?? $"{Path.GetFileName(filePath)} must contain \"{expected}\"");

        private static void AssertFileDoesNotContain(string filePath, string unexpected, string message = null) =>
            Assert.That(File.ReadAllText(filePath), Does.Not.Contain(unexpected), message ?? $"{Path.GetFileName(filePath)} must not contain \"{unexpected}\"");

        private static int RunTool(string projectPath, string solutionsFile,
            string bindingRedirectsFilePath = null, string targetFilesFilePath = null,
            bool writeBindingRedirects = false, string outDir = null,
            bool usePrivateProbingPath = false, bool quiet = false)
        {
            var args = new List<string>
            {
                "--test",
                "--projectFile", Path.Combine(WorkDir, projectPath, Path.GetFileName(projectPath) + ".csproj"),
                "--solutions", Path.Combine(WorkDir, solutionsFile),
            };
            if (!quiet)
            {
                args.Add($"-v:{GlobalContext.OutputDir}");
            }
            if (bindingRedirectsFilePath != null)
            {
                args.AddRange(["--bindingRedirects", bindingRedirectsFilePath]);
            }
            if (targetFilesFilePath != null)
            {
                args.AddRange(["--targetFiles", targetFilesFilePath]);
            }
            if (writeBindingRedirects)
            {
                args.Add("--writeBindingRedirects");
            }
            if (outDir != null)
            {
                args.Add($"--outDir={outDir}");
            }
            if (usePrivateProbingPath)
            {
                args.Add("--usePrivateProbingPath");
            }
            return Program.Main([.. args]);
        }

        private static void SaveVerboseLog(string projectPath)
        {
            var expectedDir = $"{GlobalContext.RootDir}\\Expected\\{projectPath}";
            Directory.CreateDirectory(expectedDir);
            File.Move(Log.LogFilePath, $"{expectedDir}\\Verbose.log", true);
        }

        private static void PrepareArea(string area, params string[] solutions)
        {
            CopyInputArea(area);
            foreach (var sln in solutions)
            {
                RestoreSolution(sln);
            }
        }

        private static void PrepareAreaWithBuild(string area, params string[] solutions)
        {
            CopyInputArea(area);
            foreach (var sln in solutions)
            {
                BuildSolution(sln);
            }
        }

        private static void CopyInputArea(string area)
        {
            var src = Path.Combine(GlobalContext.RootDir, "Input", area);
            var dst = Path.Combine(WorkDir, area);
            if (Directory.Exists(dst))
            {
                return;
            }
            Directory.CreateDirectory(Path.Combine(WorkDir, ".git"));
            CopyDirectory(src, dst);
        }

        private static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var file in Directory.GetFiles(src))
            {
                File.Copy(file, Path.Combine(dst, Path.GetFileName(file)));
            }
            foreach (var dir in Directory.GetDirectories(src))
            {
                var dirName = Path.GetFileName(dir);
                if (dirName == "obj" || dirName == "bin")
                {
                    continue;
                }
                CopyDirectory(dir, Path.Combine(dst, dirName));
            }
        }

        private static void RestoreSolution(string slnRelativePath)
        {
            var slnPath = Path.Combine(WorkDir, slnRelativePath);
            GlobalContext.MSBuildExe.RestoreNuGetPackages(slnPath);
        }

        private static void BuildSolution(string slnRelativePath)
        {
            var slnPath = Path.Combine(WorkDir, slnRelativePath);
            var exitCode = GlobalContext.MSBuildExe.RunProcess($"/v:m /nologo /restore /p:NuGetAudit=false /p:NuGetInteractive=false \"{slnPath}\"");
            Assert.That(exitCode, Is.Zero, $"Failed to build {slnRelativePath}");
        }

        /// <summary>
        /// Resolves standard paths for a test project in the work directory.
        /// Disposes by cleaning up generated files.
        /// </summary>
        private sealed class ProjectTestContext : IDisposable
        {
            public string ProjectDir { get; }
            public string ProjectFile { get; }
            public string ConfigFile { get; }
            public string GitIgnoreFile { get; }
            public string SlnFile { get; }

            public ProjectTestContext(string projectPath, string solutionsFile, string configName = "app.config")
            {
                ProjectDir = Path.Combine(WorkDir, projectPath);
                ProjectFile = Path.Combine(ProjectDir, Path.GetFileName(projectPath) + ".csproj");
                ConfigFile = Path.Combine(ProjectDir, configName);
                GitIgnoreFile = Path.Combine(ProjectDir, ".gitignore");
                SlnFile = Path.Combine(WorkDir, solutionsFile);
            }

            public void Dispose()
            {
                // No cleanup needed — the work dir is deleted in TearDown
            }
        }

        private sealed class TempDirectory : IDisposable
        {
            public string Path { get; }

            public TempDirectory(string path)
            {
                Path = path;
            }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, true);
                }
            }
        }
    }
}
