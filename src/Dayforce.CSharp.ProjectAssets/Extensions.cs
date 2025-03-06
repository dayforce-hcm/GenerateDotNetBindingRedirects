using Newtonsoft.Json;
using NuGet.Packaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dayforce.CSharp.ProjectAssets
{
    public static class Extensions
    {
        private static readonly JsonSerializerSettings s_jsonSerializerSettings = new()
        {
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        public static readonly string[] ArrayWithEmptyLine = [""];

        public static void ForEach<T>(this IEnumerable<T> items, Action<T> action)
        {
            foreach (var item in items)
            {
                action(item);
            }
        }

        public static int FindIndex<T>(this IList<T> source, int startIndex, Predicate<T> match)
        {
            for (int i = startIndex; i < source.Count; ++i)
            {
                if (match(source[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        public static bool IsExecutable(this string path) => path.EndsWith(".dll", C.IGNORE_CASE) || path.EndsWith(".exe", C.IGNORE_CASE);

        public static void SaveAllText(this string text, string filePath)
        {
            if (File.Exists(filePath))
            {
                var oldText = File.ReadAllText(filePath);
                if (text.Equals(oldText, C.IGNORE_CASE))
                {
                    return;
                }
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            }
            File.WriteAllText(filePath, text);
        }

        public static void SaveAllLines(this IEnumerable<string> lines, string filePath) => string.Join(Environment.NewLine, lines.Concat(ArrayWithEmptyLine)).SaveAllText(filePath);

        public static void GenerateNuGetUsageReport(this ProjectAssets projectAssets, string projectName, string nuGetUsageReport, bool compat = false)
        {
            if (Directory.Exists(nuGetUsageReport) || nuGetUsageReport[^1] == '\\')
            {
                nuGetUsageReport = nuGetUsageReport + (nuGetUsageReport[^1] == '\\' ? "" : "\\") + "NuGetUsageReport-" + projectName + ".json";
            }

            var map = projectAssets
                .Libraries
                .Where(o => o.Value.Type == C.PACKAGE && o.Value.HasRuntimeAssemblies)
                .ToDictionary(o => o.Key, o =>
                {
                    string nuGetVersion = o.Value.Version.ToString();
                    return new
                    {
                        NuGetVersion = nuGetVersion,
                        Metadata = GetMetadata(projectAssets.PackageFolders, o.Key, nuGetVersion, compat),
                        RuntimeAssemblies = o.Value.Library.RuntimeAssemblies.Select(o => Path.GetFileName(o.Path))
                    };
                });

            JsonConvert.SerializeObject(compat ? map : new 
            {
                projectAssets.PackageFolders,
                Packages = map
            }, Formatting.Indented, s_jsonSerializerSettings).SaveAllText(nuGetUsageReport);
        }

        private static object GetMetadata(List<string> packageFolders, string packageId, string nuGetVersion, bool compat)
        {
            var nuSpecFileName = packageId + ".nuspec";
            for (var i = 0; i < packageFolders.Count; ++i)
            {
                var nuSpecFile = Path.Combine(packageFolders[i], packageId, nuGetVersion, nuSpecFileName);
                if (File.Exists(nuSpecFile))
                {
                    var nuSpecReader = new NuspecReader(nuSpecFile);
                    return new
                    {
                        Authors = nuSpecReader.GetAuthors(),
                        ProjectUrl = nuSpecReader.GetProjectUrl(),
                        PackageFolderIndex = compat ? 0 : i,
                    };
                }
            }
            return null;
        }

        public static string GetGitWorkspaceRoot(this string path)
        {
            string tmp;
            path = Path.GetFullPath(path);
            while (path.Length > 3 && !Directory.Exists(tmp = path + "\\.git") && !File.Exists(tmp))
            {
                path = Path.GetDirectoryName(path);
            }
            if (path.Length <= 3)
            {
                return null;
            }
            return path + '\\';
        }

        public static string GetRelativeToGitWorkspaceRoot(this string path)
        {
            var wsRoot = path.GetGitWorkspaceRoot();
            return wsRoot == null ? path : path[wsRoot.Length..];
        }
    }
}
