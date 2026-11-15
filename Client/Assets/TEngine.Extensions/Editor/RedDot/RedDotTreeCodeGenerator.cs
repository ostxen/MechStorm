#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TEngine.Extensions.RedDot;
using TEngine.Extensions.RedDot.View;
using UnityEditor;
using UnityEngine;

namespace TEngine.Extensions.RedDot.Editor
{
    internal static class RedDotTreeCodeGenerator
    {
        [MenuItem("TEngine Extensions/RedDot/Generate Selected Config")]
        private static void GenerateSelected()
        {
            if (Selection.activeObject is RedDotTreeConfig config)
            {
                Generate(config);
            }
            else
            {
                Debug.LogWarning("Select a RedDotTreeConfig asset first.");
            }
        }

        [MenuItem("TEngine Extensions/RedDot/Generate All Configs")]
        private static void GenerateAll()
        {
            var guids = AssetDatabase.FindAssets("t:RedDotTreeConfig");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var config = AssetDatabase.LoadAssetAtPath<RedDotTreeConfig>(path);
                if (config != null)
                {
                    Generate(config);
                }
            }
        }

        [MenuItem("TEngine Extensions/RedDot/Generate Selected Config", true)]
        private static bool ValidateGenerateSelected()
        {
            return Selection.activeObject is RedDotTreeConfig;
        }

        private static void Generate(RedDotTreeConfig config)
        {
            var nodes = config.GetAllNodes();
            ValidateNodes(nodes);
            AssignIds(nodes);

            var content = new StringBuilder();
            content.AppendLine("// AUTO-GENERATED FILE. DO NOT EDIT MANUALLY.");
            content.AppendLine($"// Source: {AssetDatabase.GetAssetPath(config)}");
            content.AppendLine();
            content.AppendLine($"namespace {config.GeneratedNamespace}");
            content.AppendLine("{");
            content.AppendLine($"    public static class {config.GeneratedClassName}");
            content.AppendLine("    {");

            for (var i = 0; i < config.RootNodes.Count; i++)
            {
                AppendNode(content, config.RootNodes[i], 2);
            }

            content.AppendLine();
            content.AppendLine("        public static void RegisterAll(");
            content.AppendLine("            TEngine.Extensions.RedDot.RedDotRegistry registry)");
            content.AppendLine("        {");
            for (var i = 0; i < config.RootNodes.Count; i++)
            {
                AppendRegistration(content, config.RootNodes[i], "            ");
            }

            content.AppendLine("        }");
            content.AppendLine("    }");
            content.AppendLine("}");

            var outputPath = config.OutputPath.Replace('\\', '/').TrimStart('/');
            if (outputPath.StartsWith("../", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("RedDot output path must stay under Assets.");
            }

            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, outputPath));
            var assetsRoot = Path.GetFullPath(Application.dataPath);
            if (!fullPath.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("RedDot output path must stay under Assets.");
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var oldContent = File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
            var newContent = content.ToString();
            if (string.Equals(oldContent, newContent, StringComparison.Ordinal))
            {
                return;
            }

            File.WriteAllText(fullPath, newContent, new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log($"Generated RedDot paths: Assets/{outputPath}");
        }

        private static void AssignIds(IReadOnlyList<RedDotTreeNodeConfig> nodes)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                nodes[i].generatedId = i + 1;
            }
        }

        private static void ValidateNodes(IReadOnlyList<RedDotTreeNodeConfig> nodes)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var identifiers = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (string.IsNullOrWhiteSpace(node.name))
                {
                    throw new InvalidOperationException("RedDot node name cannot be empty.");
                }

                if (!paths.Add(node.generatedPath))
                {
                    throw new InvalidOperationException(
                        $"Duplicate RedDot path: {node.generatedPath}");
                }

                var identifier = GetIdentifier(node);
                if (identifiers.TryGetValue(identifier, out var existingPath))
                {
                    throw new InvalidOperationException(
                        $"RedDot paths '{existingPath}' and '{node.generatedPath}' produce the same identifier '{identifier}'.");
                }

                identifiers.Add(identifier, node.generatedPath);
            }
        }

        private static void AppendNode(
            StringBuilder content,
            RedDotTreeNodeConfig node,
            int indentLevel)
        {
            var indent = new string(' ', indentLevel * 4);
            var identifier = GetIdentifier(node);
            content.AppendLine($"{indent}public const int {identifier}Id = {node.generatedId};");
            content.AppendLine($"{indent}public const string {identifier}Path = \"{Escape(node.generatedPath)}\";");
            content.AppendLine($"{indent}public const TEngine.Extensions.RedDot.RedDotDisplayType {identifier}DisplayType = TEngine.Extensions.RedDot.RedDotDisplayType.{node.displayType};");
            for (var i = 0; i < node.children.Count; i++)
            {
                AppendNode(content, node.children[i], indentLevel + 1);
            }
        }

        private static void AppendRegistration(
            StringBuilder content,
            RedDotTreeNodeConfig node,
            string indent)
        {
            var identifier = GetIdentifier(node);
            content.Append(indent)
                .Append("registry.Register(")
                .Append(identifier).Append("Path, ")
                .Append(identifier).AppendLine("DisplayType);");

            if (node.children.Count > 0)
            {
                var childPaths = new StringBuilder();
                for (var i = 0; i < node.children.Count; i++)
                {
                    if (i > 0)
                    {
                        childPaths.Append(", ");
                    }

                    childPaths.Append(GetIdentifier(node.children[i])).Append("Path");
                }

                content.Append(indent)
                    .Append("registry.DefineRelation(")
                    .Append(identifier).Append("Path, ")
                    .Append("TEngine.Extensions.RedDot.RedDotRelationMode.")
                    .Append(node.relationMode).Append(", ")
                    .Append(identifier).Append("DisplayType, ")
                    .Append(childPaths)
                    .AppendLine(");");
            }

            for (var i = 0; i < node.children.Count; i++)
            {
                AppendRegistration(content, node.children[i], indent);
            }
        }

        private static string GetIdentifier(RedDotTreeNodeConfig node)
        {
            return SanitizeIdentifier(node.generatedPath);
        }

        private static string SanitizeIdentifier(string value)
        {
            var result = new StringBuilder();
            var capitalize = true;
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (char.IsLetterOrDigit(character))
                {
                    result.Append(capitalize ? char.ToUpperInvariant(character) : character);
                    capitalize = false;
                }
                else
                {
                    capitalize = true;
                }
            }

            if (result.Length == 0)
            {
                return "Unknown";
            }

            if (char.IsDigit(result[0]))
            {
                result.Insert(0, '_');
            }

            return result.ToString();
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
