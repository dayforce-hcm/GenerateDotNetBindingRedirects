using Dayforce.CSharp.ProjectAssets;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Tests
{
    public static class Extensions
    {
        public static string GetTempDirectoryName()
        {
            var name = Path.GetTempFileName();
            File.Delete(name);
            return name + "\\";
        }

        public static string GetMSBuildExe()
        {
            var drives = new[] { 'C', 'D', 'E', 'F' };
            var flavours = new Dictionary<string, string> {
                ["BuildTools"] = "Program Files (x86)",
                ["Enterprise"] = "Program Files",
                ["Professional"] = "Program Files",
                ["Community"] = "Program Files",
            };
            var years = new[] { 2022, 2019 };
            foreach (var year in years)
            {
                foreach (var drive in drives)
                {
                    foreach (var (flavour, programFilesDir) in flavours)
                    {
                        var path = @$"{drive}:\{programFilesDir}\Microsoft Visual Studio\{year}\{flavour}\MSBuild\Current\bin\msbuild.exe";
                        if (File.Exists(path))
                        {
                            return path;
                        }
                    }
                }
            }
            return null;
        }

        public static int RunProcess(this string exe, string arguments)
        {
#pragma warning disable CA1416 // Validate platform compatibility
            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    Arguments = arguments,
                    FileName = exe,
                    CreateNoWindow = true,
                    LoadUserProfile = false,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                },
            };
#pragma warning restore CA1416 // Validate platform compatibility
            p.Start();
            p.WaitForExit();
            return p.ExitCode;
        }

        public static void RestoreNuGetPackages(this string msBuildExe, string slnFilePath)
        {
            var file = Path.GetTempFileName();
            string binLogArg = "";
            string systemDebug = Environment.GetEnvironmentVariable("SYSTEM_DEBUG");
            if (systemDebug != null && systemDebug.Equals("true", C.IGNORE_CASE))
            {
                var binLogDir = Environment.GetEnvironmentVariable("BUILD_ARTIFACTSTAGINGDIRECTORY");
                Assert.That(binLogDir, Is.Not.Null.Or.Empty);
                binLogArg = $@" /bl:{binLogDir}\binlogs\unit-tests-restore.{Path.GetFileNameWithoutExtension(slnFilePath)}.binlog";
            }
            var exitCode = msBuildExe.RunProcess($"/t:Restore /v:m /m /nologo /noConsoleLogger {slnFilePath} /fl /p:NuGetAudit=false /p:NuGetInteractive=false{binLogArg} /flp:LogFile={file};Verbosity=minimal");
            TestContext.Progress.WriteLine(File.ReadAllText(file));
            File.Delete(file);
            Assert.That(exitCode, Is.Zero);
        }
    }
}
