using System;
using System.Collections.Generic;
using UnityEngine;

namespace TEngine.Extensions.RedDot.View
{
    [Serializable]
    public sealed class RedDotTreeNodeConfig
    {
        [Tooltip("节点名称，只允许使用字母、数字、下划线和短横线。")]
        public string name = "NewNode";

        [TextArea]
        public string description;

        public RedDotDisplayType displayType = RedDotDisplayType.Dot;
        public RedDotRelationMode relationMode = RedDotRelationMode.AnyActive;
        public List<RedDotTreeNodeConfig> children = new();

        [NonSerialized] public string generatedPath;
        [NonSerialized] public int generatedId;

        public void RefreshPaths(string parentPath = "")
        {
            RefreshPaths(parentPath, new HashSet<RedDotTreeNodeConfig>());
        }

        private void RefreshPaths(
            string parentPath,
            HashSet<RedDotTreeNodeConfig> visited)
        {
            if (!visited.Add(this))
            {
                throw new InvalidOperationException(
                    $"RedDot config contains a cyclic node reference at '{name}'.");
            }

            generatedPath = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}";
            for (var i = 0; i < children.Count; i++)
            {
                children[i]?.RefreshPaths(generatedPath, visited);
            }
        }

        public void Flatten(List<RedDotTreeNodeConfig> result)
        {
            Flatten(result, new HashSet<RedDotTreeNodeConfig>());
        }

        private void Flatten(
            List<RedDotTreeNodeConfig> result,
            HashSet<RedDotTreeNodeConfig> visited)
        {
            if (!visited.Add(this))
            {
                throw new InvalidOperationException(
                    $"RedDot config contains a cyclic node reference at '{name}'.");
            }

            result.Add(this);
            for (var i = 0; i < children.Count; i++)
            {
                children[i]?.Flatten(result, visited);
            }
        }
    }

    [CreateAssetMenu(
        menuName = "TEngine Extensions/RedDot Tree Config",
        fileName = "RedDotTreeConfig")]
    public sealed class RedDotTreeConfig : ScriptableObject
    {
        [SerializeField] private List<RedDotTreeNodeConfig> rootNodes = new();
        [SerializeField] private string outputPath =
            "Generated/TEngine.Extensions/RedDotPathDefine.g.cs";
        [SerializeField] private string generatedNamespace = "TEngine.Extensions.RedDot.Generated";
        [SerializeField] private string generatedClassName = "RedDotPathDefine";

        public IReadOnlyList<RedDotTreeNodeConfig> RootNodes => rootNodes;
        public string OutputPath => outputPath;
        public string GeneratedNamespace => generatedNamespace;
        public string GeneratedClassName => generatedClassName;

        public void RefreshPaths()
        {
            for (var i = 0; i < rootNodes.Count; i++)
            {
                rootNodes[i]?.RefreshPaths();
            }
        }

        public List<RedDotTreeNodeConfig> GetAllNodes()
        {
            RefreshPaths();
            var result = new List<RedDotTreeNodeConfig>();
            for (var i = 0; i < rootNodes.Count; i++)
            {
                rootNodes[i]?.Flatten(result);
            }

            return result;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RefreshPaths();
        }
#endif
    }
}
