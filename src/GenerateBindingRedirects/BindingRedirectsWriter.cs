using Dayforce.CSharp.ProjectAssets;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace GenerateBindingRedirects
{
    public class BindingRedirectsWriter(ProjectContext pc)
    {
        [Flags]
        private enum ActualAppConfigStatus
        {
            Normal = 0,
            NotSpecified = 1,
            FileNotFound = 2,
            Linked = 4
        }

        private delegate bool IsInAssertModeDelegate();

        private const string ASSEMBLY_BINDING_XMLNS = "urn:schemas-microsoft-com:asm.v1";
        private static readonly XmlWriterSettings s_xmlWriterSettings = new()
        {
            Encoding = new UTF8Encoding(false)
        };

        private readonly ProjectContext m_pc = pc;
        private string ExpectedConfigFilePath => m_pc.ExpectedConfigFilePath;
        private string ActualConfigFilePath => m_pc.ActualConfigFilePath;
        private string ProjectFilePath => m_pc.ProjectFilePath;

        public void WriteBindingRedirects(string bindingRedirects, bool assert, bool forceAssert)
        {
            var actualAppConfigStatus = ActualAppConfigStatus.Normal;
            if (ExpectedConfigFilePath.EndsWith("app.config", C.IGNORE_CASE))
            {
                if (ActualConfigFilePath == null)
                {
                    actualAppConfigStatus |= ActualAppConfigStatus.NotSpecified;
                }
                else
                {
                    if (!File.Exists(ActualConfigFilePath))
                    {
                        actualAppConfigStatus |= ActualAppConfigStatus.FileNotFound;
                    }
                    if (!ActualConfigFilePath.Equals(ExpectedConfigFilePath, C.IGNORE_CASE))
                    {
                        actualAppConfigStatus |= ActualAppConfigStatus.Linked;
                        if ((actualAppConfigStatus & ActualAppConfigStatus.FileNotFound) == 0)
                        {
                            File.Copy(ActualConfigFilePath, ExpectedConfigFilePath, true);
                        }
                    }
                }
            }

            if (!WriteConfigFile(bindingRedirects, assert, forceAssert) ||
                actualAppConfigStatus == ActualAppConfigStatus.Normal ||
                actualAppConfigStatus == ActualAppConfigStatus.FileNotFound)
            {
                return;
            }

            if (!m_pc.SDKStyle)
            {
                AddAppConfigToProjectFile(actualAppConfigStatus);
            }
        }

        private bool IsConfigFileTrackedByGit => GitVersionControl.Instance.IsTracked(ExpectedConfigFilePath);

        private void UpdateOrAssertGitIgnore(bool assert)
        {
            var gitIgnoreFilePath = Path.Combine(Path.GetDirectoryName(ExpectedConfigFilePath), ".gitignore");
            var verb = "create";
            var actualStatus = "not found";
            var expectedStatus = "a new file";
            Action<string, string> updateAction = File.WriteAllText;
            if (File.Exists(gitIgnoreFilePath))
            {
                var gitIgnoreLines = File.ReadAllLines(gitIgnoreFilePath);
                if (gitIgnoreLines.Contains("app.config", C.IgnoreCase))
                {
                    return;
                }

                verb = "update";
                actualStatus = "does not ignore app.config";
                expectedStatus = "modified";
                updateAction = File.AppendAllText;
            }

            if (assert)
            {
                var relGitIgnoreFilePath = gitIgnoreFilePath.GetRelativeToGitWorkspaceRoot();
                if (relGitIgnoreFilePath == gitIgnoreFilePath)
                {
                    throw new ApplicationException($"{gitIgnoreFilePath} {actualStatus}. " +
                        $"The local build is expected to automatically {verb} the {gitIgnoreFilePath} file.");

                }

                throw new ApplicationException($"{relGitIgnoreFilePath} {actualStatus}. " +
                    $"The local build is expected to automatically {verb} the {relGitIgnoreFilePath} file, which causes the git status to show it as {expectedStatus}. " +
                    $"Looks like {relGitIgnoreFilePath} was omitted explicitly from the commit. Please, include it.");
            }

            updateAction(gitIgnoreFilePath, "app.config\r\n");
        }

        private void AddAppConfigToProjectFile(ActualAppConfigStatus actualAppConfigStatus)
        {
            var (doc, nsmgr) = GetProjectXmlDocument(ProjectFilePath);
            var node = doc.CreateElement("None", doc.DocumentElement.Attributes["xmlns"].Value);
            var attr = doc.CreateAttribute("Include");
            attr.Value = "app.config";
            node.Attributes.Append(attr);
            if (actualAppConfigStatus == ActualAppConfigStatus.NotSpecified)
            {
                var itemGroup = doc.SelectSingleNode("/p:Project/p:ItemGroup[count(./*) > 0]", nsmgr);
                itemGroup.AppendChild(doc.CreateWhitespace("  "));
                itemGroup.AppendChild(node);
                itemGroup.AppendChild(doc.CreateWhitespace(Environment.NewLine + "  "));
            }
            else
            {
                var oldAttr = ProjectContext.LocateAppConfigInProjectXml(doc.CreateNavigator(), nsmgr);
                oldAttr.MoveToParent();
                var oldNode = (XmlNode)oldAttr.UnderlyingObject;
                oldNode.ParentNode.ReplaceChild(node, oldNode);
            }
            doc.Save(ProjectFilePath);
        }

        private bool WriteConfigFile(string bindingRedirects, bool assert, bool forceAssert)
        {
            bool updateGitIgnore = false;
            bool assertGitIgnore = false;
            bool failIfOnlyBindingsInConfig = false;
            if (assert || forceAssert)
            {
                if (IsConfigFileTrackedByGit)
                {
                    forceAssert = true;
                    failIfOnlyBindingsInConfig = true;
                }
                else
                {
                    assert = false;
                    assertGitIgnore = true;
                }
            }
            else
            {
                if (IsConfigFileTrackedByGit)
                {
                    failIfOnlyBindingsInConfig = true;
                }
                else
                {
                    updateGitIgnore = true;
                }
            }

            if (!File.Exists(ExpectedConfigFilePath))
            {
                if (string.IsNullOrEmpty(bindingRedirects))
                {
                    return false;
                }
                if (forceAssert)
                {
                    var configPath = ExpectedConfigFilePath.GetRelativeToGitWorkspaceRoot();
                    throw new ApplicationException($"{configPath} is expected to have some assembly binding redirects, but it does not exist.");
                }
                HandleGitIgnore(updateGitIgnore, assertGitIgnore);
                WriteNewConfigFile(bindingRedirects);
                return true;
            }

            var doc = new XmlDocument
            {
                PreserveWhitespace = true
            };
            doc.Load(ExpectedConfigFilePath);

            if (failIfOnlyBindingsInConfig && IsOnlyBindingsInConfig(doc, out var isEmpty))
            {
                var configPath = ExpectedConfigFilePath.GetRelativeToGitWorkspaceRoot();
                throw new ApplicationException($"{configPath} {(isEmpty ? "is empty" : "only contains binding redirects")}. Remove this file from the version control. " +
                    $"Make sure to commit the .gitignore file instead. It is created or updated automatically by the local build, if that file does not exist or does not " +
                    $"ignore app.config already.");
            }

            HandleGitIgnore(updateGitIgnore, assertGitIgnore);

            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("b", ASSEMBLY_BINDING_XMLNS);
            var assemblyBinding = doc.SelectSingleNode("/configuration/runtime/b:assemblyBinding", nsmgr);
            if (assemblyBinding == null)
            {
                if (bindingRedirects.Length == 0)
                {
                    return false;
                }
                if (forceAssert)
                {
                    var configPath = ExpectedConfigFilePath.GetRelativeToGitWorkspaceRoot();
                    throw new ApplicationException($"{configPath} is expected to have some assembly binding redirects, but it has none.");
                }

                var cfg = doc.SelectSingleNode("/configuration");
                if (cfg == null)
                {
                    WriteNewConfigFile(bindingRedirects);
                    return true;
                }

                XmlNode runtime = cfg.ChildNodes.OfType<XmlElement>().FirstOrDefault(n => n.LocalName == "runtime");
                if (runtime == null)
                {
                    cfg.AppendChild(doc.CreateWhitespace("  "));
                    runtime = cfg.AppendChild(doc.CreateElement("runtime"));
                    runtime.AppendChild(doc.CreateWhitespace(Environment.NewLine + "  "));
                    cfg.AppendChild(doc.CreateWhitespace(Environment.NewLine));
                }
                assemblyBinding = runtime.ChildNodes.OfType<XmlElement>().FirstOrDefault(n => n.LocalName == "assemblyBinding");
                if (assemblyBinding == null)
                {
                    runtime.AppendChild(doc.CreateWhitespace("  "));
                    assemblyBinding = runtime.AppendChild(doc.CreateElement("assemblyBinding", ASSEMBLY_BINDING_XMLNS));
                    var attr = doc.CreateAttribute("xmlns", "http://www.w3.org/2000/xmlns/");
                    attr.Value = ASSEMBLY_BINDING_XMLNS;
                    assemblyBinding.Attributes.Append(attr);
                    runtime.AppendChild(doc.CreateWhitespace(Environment.NewLine + "  "));
                }
            }

            var newInnerXml = Environment.NewLine +
                bindingRedirects +
                Environment.NewLine + "    ";
            var curInnerXml = assemblyBinding.OuterXml
                .Replace(@"<assemblyBinding xmlns=""urn:schemas-microsoft-com:asm.v1"">", "")
                .Replace(@"</assemblyBinding>", "");

            if (forceAssert)
            {
                if (curInnerXml == newInnerXml)
                {
                    return false;
                }
                var configPath = ExpectedConfigFilePath.GetRelativeToGitWorkspaceRoot();
                throw new ApplicationException($"{configPath} does not have the expected set of binding redirects.");
            }

            if (curInnerXml != newInnerXml)
            {
                assemblyBinding.InnerXml = newInnerXml;
                using var writer = XmlWriter.Create(ExpectedConfigFilePath, s_xmlWriterSettings);
                doc.Save(writer);
            }

            return true;
        }

        private void HandleGitIgnore(bool updateGitIgnore, bool assertGitIgnore)
        {
            if (assertGitIgnore || updateGitIgnore)
            {
                UpdateOrAssertGitIgnore(assertGitIgnore);
            }
        }

        private static bool IsOnlyBindingsInConfig(XmlDocument doc, out bool isEmpty)
        {
            isEmpty = false;

            var cfg = doc.SelectSingleNode("/configuration");
            if (cfg == null)
            {
                isEmpty = true;
                return true;
            }

            var childNodes = cfg.ChildNodes.OfType<XmlElement>().ToList();
            if (childNodes.Count == 0)
            {
                isEmpty = true;
                return true;
            }
            if (childNodes.Count > 1 || childNodes[0].LocalName != "runtime")
            {
                return false;
            }

            var runtime = childNodes[0];
            childNodes = [.. runtime.ChildNodes.OfType<XmlElement>()];
            if (childNodes.Count == 0)
            {
                isEmpty = true;
                return true;
            }
            if (childNodes.Count > 1 || childNodes[0].LocalName != "assemblyBinding")
            {
                return false;
            }

            var assemblyBinding = childNodes[0];
            childNodes = [.. assemblyBinding.ChildNodes.OfType<XmlElement>()];
            if (childNodes.Count == 0)
            {
                isEmpty = true;
                return true;
            }

            return childNodes.All(o => o.LocalName == "dependentAssembly");
        }

        private void WriteNewConfigFile(string bindingRedirects)
        {
            const string newConfigFileFormat = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <runtime>
    <assemblyBinding xmlns=""urn:schemas-microsoft-com:asm.v1"">
{0}
    </assemblyBinding>
  </runtime>
</configuration>
";
            File.WriteAllText(ExpectedConfigFilePath, string.Format(newConfigFileFormat, bindingRedirects));
        }

        private static (XmlDocument, XmlNamespaceManager) GetProjectXmlDocument(string projectFile)
        {
            var doc = new XmlDocument
            {
                PreserveWhitespace = true
            };
            doc.Load(projectFile);
            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            var ns = doc.DocumentElement.Attributes["xmlns"].Value;
            nsmgr.AddNamespace("p", ns);
            return (doc, nsmgr);
        }
    }
}
