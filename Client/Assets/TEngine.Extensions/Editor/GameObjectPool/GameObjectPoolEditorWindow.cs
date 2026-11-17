using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TEngine.Extensions.GameObjectPool.Editor
{
    public sealed class GameObjectPoolEditorWindow : EditorWindow
    {
        private IReadOnlyList<GameObjectPoolSnapshot> _snapshots;
        private Vector2 _scrollPosition;

        [MenuItem("TEngine Extensions/Object Pool Monitor")]
        private static void Open()
        {
            GetWindow<GameObjectPoolEditorWindow>("GameObject Pool");
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("刷新", GUILayout.Width(80f)))
                {
                    Refresh();
                }

                if (GUILayout.Button("清理过期", GUILayout.Width(100f)))
                {
                    GameObjectPoolRegistry.CleanupAll();
                    Refresh();
                }
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            if (_snapshots == null || _snapshots.Count == 0)
            {
                EditorGUILayout.HelpBox("当前没有已注册的 GameObjectPool。", MessageType.Info);
            }
            else
            {
                foreach (var snapshot in _snapshots)
                {
                    DrawSnapshot(snapshot);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void Refresh()
        {
            _snapshots = GameObjectPoolRegistry.GetSnapshots();
            Repaint();
        }

        private static void DrawSnapshot(GameObjectPoolSnapshot snapshot)
        {
            foreach (var group in snapshot.Groups)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(group.Name, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("匹配", group.Key);
                    EditorGUILayout.LabelField(
                        "总数 / 活跃 / 空闲",
                        string.Format(
                            "{0} / {1} / {2}",
                            group.TotalCount,
                            group.ActiveCount,
                            group.AvailableCount));
                    EditorGUILayout.LabelField("容量", group.Capacity.ToString());
                    EditorGUILayout.LabelField("过期时间", group.ExpireTime.ToString("0.###") + " 秒");
                    EditorGUILayout.LabelField(
                        "自动销毁时间",
                        group.AutoDestroyTime < 0f
                            ? "禁用"
                            : group.AutoDestroyTime.ToString("0.###") + " 秒");
                    EditorGUILayout.LabelField("常驻", group.DontDestroy ? "是" : "否");
                    EditorGUILayout.LabelField(
                        "已加载 key",
                        group.LoadedKeys.Count == 0
                            ? "(无)"
                            : string.Join(", ", group.LoadedKeys));
                }
            }
        }
    }
}
