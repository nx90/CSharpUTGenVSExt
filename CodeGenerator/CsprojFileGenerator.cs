using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace CSharpUnitTestGeneratorExt.CodeGenerator
{
    public class CsprojFileGenerator : CodeGeneratorBase
    {
        private string outputFilePath;  
        private XmlDocument sourceProjectCsprojFileContent;  
        private List<string> projectReferences;
  
        public CsprojFileGenerator(string outputFilePath, XmlDocument sourceProjectCsprojFileContent, List<string> projectReferences) : base(0, "")
        {
            this.outputFilePath = outputFilePath;
            this.sourceProjectCsprojFileContent = sourceProjectCsprojFileContent;
            this.projectReferences = projectReferences;
        }

        public override string GetOutputCodeBlock()
        {
            XmlElement projectElement = sourceProjectCsprojFileContent.DocumentElement;
            string sdk = projectElement.GetAttribute("Sdk");

            XmlNode propertyGroupNode = projectElement.SelectSingleNode("PropertyGroup");
            string targetFramework = propertyGroupNode.SelectSingleNode("TargetFramework").InnerText;

            if (projectElement == null || string.IsNullOrEmpty(sdk) || string.IsNullOrEmpty(targetFramework))
            {
                throw new Exception("Invalid csproj file");
            }

            var index = this.projectReferences.IndexOf(this.outputFilePath);
            if (index > -1)
            {
                this.projectReferences.RemoveAt(index);
            }

            XmlNodeList nodes = sourceProjectCsprojFileContent.GetElementsByTagName("PackageReference");

            Version newtonsoftVer = null;
            foreach (XmlNode node in nodes)
            {
                // 检查是否是Newtonsoft.Json
                if (node.Attributes["Include"] != null && node.Attributes["Include"].Value == "Newtonsoft.Json")
                {
                    newtonsoftVer = new Version(node.Attributes["Version"].Value);
                }
            }

            var projRefsList = UpdatePackageReference(this.projectReferences);
            return BuildCsprojFile(sdk, targetFramework, projRefsList, newtonsoftVer);
        }

        private List<string> UpdatePackageReference(List<string> csprojFilePaths)
        {
            // XmlDocument xmlDoc = new XmlDocument();
            List<string> projectReferences = new List<string>();

            foreach (string csprojFilePath in csprojFilePaths)
            {
                if (string.IsNullOrEmpty(csprojFilePath)) continue;
                string relativePath = MakeRelativePath(this.outputFilePath, csprojFilePath);
                // XmlElement projectReferenceElement = xmlDoc.CreateElement("ProjectReference");
                // projectReferenceElement.SetAttribute("Include", relativePath);
                projectReferences.Add(relativePath);
            }
            return projectReferences;
        }

        private string BuildCsprojFile(string sdk, string targetFramework, List<string> projectReferences, Version maxNewtonsoftVersion)
        {
            Version newtonsoftVer = maxNewtonsoftVersion ?? new Version("13.0.1");
            var xmlDoc = new XmlDocument();
            var declaration = xmlDoc.CreateXmlDeclaration("1.0", "utf-8", null);
            xmlDoc.AppendChild(declaration);

            var projectElement = xmlDoc.CreateElement("Project");
            xmlDoc.AppendChild(projectElement);
            projectElement.SetAttribute("Sdk", sdk);

            var propertyGroupElement = xmlDoc.CreateElement("PropertyGroup");
            projectElement.AppendChild(propertyGroupElement);

            var targetFrameworkElement = xmlDoc.CreateElement("TargetFramework");
            targetFrameworkElement.InnerText = targetFramework;
            propertyGroupElement.AppendChild(targetFrameworkElement);

            var isPackableElement = xmlDoc.CreateElement("IsPackable");
            isPackableElement.InnerText = "false";
            propertyGroupElement.AppendChild(isPackableElement);

            var debugTypeElement = xmlDoc.CreateElement("DebugType");
            debugTypeElement.InnerText = "portable";
            propertyGroupElement.AppendChild(debugTypeElement);

            // Add other elements such as PackageReference as needed...  
            var itemGroupElement = xmlDoc.CreateElement("ItemGroup");
            string[,] packages = new string[,]
            {
                { "Microsoft.NET.Test.Sdk", "16.7.1" },
                { "MSTest.TestFramework", "2.1.1" },
                { "MSTest.TestAdapter", "2.1.1" },
                { "Moq", "4.16.1" },
                { "coverlet.collector", "1.3.0" },
                { "Newtonsoft.Json", newtonsoftVer.ToString() },
                { "FluentAssertions", "6.7.0" }
            };

            // Create and append each PackageReference element to the first ItemGroup  
            for (int i = 0; i < packages.GetLength(0); i++)
            {
                XmlElement packageReference = xmlDoc.CreateElement("PackageReference");
                packageReference.SetAttribute("Include", packages[i, 0]);
                packageReference.SetAttribute("Version", packages[i, 1]);
                itemGroupElement.AppendChild(packageReference);
            }
            projectElement.AppendChild(itemGroupElement);

            foreach (var projectReference in projectReferences)
            {
                XmlElement projectReferenceElement = xmlDoc.CreateElement("ProjectReference");
                projectReferenceElement.SetAttribute("Include", projectReference);
                itemGroupElement.AppendChild(projectReferenceElement);
            }

            StringBuilder sb = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "    ";
            settings.NewLineChars = "\r\n";
            settings.NewLineHandling = NewLineHandling.Replace;

            using (XmlWriter writer = XmlWriter.Create(sb, settings))
            {
                xmlDoc.Save(writer);
            }

            return sb.ToString();
        }

        private string MakeRelativePath(string fromPath, string toPath)
        {
            if (string.IsNullOrEmpty(fromPath)) throw new ArgumentNullException("fromPath");
            if (string.IsNullOrEmpty(toPath)) throw new ArgumentNullException("toPath");

            Uri fromUri = new Uri(AppendDirectorySeparatorChar(fromPath));
            Uri toUri = new Uri(AppendDirectorySeparatorChar(toPath));

            if (fromUri.Scheme != toUri.Scheme) { return toPath; } // path can't be made relative.  

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (toUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }

        private string AppendDirectorySeparatorChar(string path)
        {
            // Append a slash only if the path is a directory and does not have a slash at the end.  
            if (!Path.HasExtension(path) &&
                !path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path + Path.DirectorySeparatorChar;
            }

            return path;
        }
    }
}
