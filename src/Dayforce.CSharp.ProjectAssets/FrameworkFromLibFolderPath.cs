using System.IO;
using NuGet.Frameworks;

namespace Dayforce.CSharp.ProjectAssets
{
    public class FrameworkFromLibFolderPath(string libFolderPath) : IFrameworkSpecific
    {
        public readonly string LibFolderPath = libFolderPath;

        public NuGetFramework TargetFramework { get; } = NuGetFramework.ParseFolder(Path.GetFileName(libFolderPath));

        public override string ToString() => TargetFramework.ToString();
    }
}