#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static MBS.ParticlePreview.ParticlePreviewDefine;

namespace MBS.ParticlePreview
{
    /// <summary>
    /// 粒子预览器子目录分组绘制器，负责按子文件夹对 Prefab 进行分组并绘制分隔标题栏
    /// Particle previewer subfolder group drawer, groups Prefabs by subdirectory and draws section headers
    /// </summary>
    public class ParticlePreviewGrouper
    {
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 子目录分组缓存
        // Subfolder grouping cache
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        static public List<ParticleSubfolderGroup> GroupedCache;
        static public HashSet<string> CollapsedSubfolders = new HashSet<string>();

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 清空缓存
        /// Clear cache
        /// </summary>
        static public void ClearCache()
        {
            GroupedCache = null;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 构建按子目录分组的缓存数据，保持原始资源顺序不变，仅将同目录的资源聚合到一起
        /// Build grouped cache by subdirectories, preserving original order, only regroup same-dir assets
        /// </summary>
        /// <param name="prefabs">需要分组的 Prefab 列表（会被重新排序） / Prefab list to group (will be reordered)</param>
        /// <param name="enabled">是否启用分组 / Whether grouping is enabled</param>
        static public void BuildGroupedCache(List<GameObject> prefabs, bool enabled)
        {
            if (prefabs == null || prefabs.Count == 0 || !enabled)
            {
                GroupedCache = null;
                return;
            }

            // 按原始顺序扫描，将同目录的资源聚合到一起，保留每个目录内资源的原始相对顺序
            // Scan in original order, group same-dir assets together while preserving their relative order
            var dirOrder = new List<string>();
            var dirItems = new Dictionary<string, List<GameObject>>();
            for (int i = 0; i < prefabs.Count; i++)
            {
                var obj = prefabs[i];
                string assetPath = obj != null ? AssetDatabase.GetAssetPath(obj) : "";
                string dir = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? "";
                if (!dirItems.ContainsKey(dir))
                {
                    dirOrder.Add(dir);
                    dirItems[dir] = new List<GameObject>();
                }
                dirItems[dir].Add(obj);
            }

            // 按目录首次出现顺序重组列表和分组
            // Reassemble list and groups in directory first-appearance order
            prefabs.Clear();
            GroupedCache = new List<ParticleSubfolderGroup>();
            foreach (var dir in dirOrder)
            {
                var items = dirItems[dir];
                string displayName = System.IO.Path.GetFileName(dir);
                if (string.IsNullOrEmpty(displayName)) displayName = dir;
                var group = new ParticleSubfolderGroup
                {
                    FolderPath = dir,
                    DisplayName = displayName,
                    StartIndex = prefabs.Count,
                    Count = items.Count
                };
                GroupedCache.Add(group);
                prefabs.AddRange(items);
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 缓存 GUIStyle，避免每帧重复创建
        // Cache GUIStyle to avoid repeated creation per frame
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        static private GUIStyle _FoldoutStyle;
        static private Color _CachedTitleColor;

        /// <summary>
        /// 绘制子目录分隔带标题栏（GUILayout 版本），点击可展开/收起对应子目录的资源格子
        /// Draw subfolder section header bar (GUILayout version), click to expand/collapse subfolder's asset grid
        /// </summary>
        /// <param name="displayName">分组显示名称 / Group display name</param>
        /// <param name="folderPath">文件夹路径（用于折叠状态标识） / Folder path (for collapse state key)</param>
        /// <returns>是否需要重绘 / Whether repaint is needed</returns>
        static public bool DrawSubfolderSectionHeader(string displayName, string folderPath)
        {
            float height = 22f;
            float padding = 4f;

            Rect rect = GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true));
            rect.x += padding;
            rect.width -= padding * 2;

            bool repaint = DrawSubfolderSectionHeaderCore(rect, displayName, folderPath);
            GUILayout.Space(4f);
            return repaint;
        }

        /// <summary>
        /// 绘制子目录分隔带标题栏（绝对坐标版本），用于虚拟滚动场景
        /// Draw subfolder section header bar (absolute coordinate version), for virtual scrolling
        /// </summary>
        /// <param name="rect">标题栏绘制区域 / Header drawing area</param>
        /// <param name="displayName">分组显示名称 / Group display name</param>
        /// <param name="folderPath">文件夹路径（用于折叠状态标识） / Folder path (for collapse state key)</param>
        /// <returns>是否需要重绘 / Whether repaint is needed</returns>
        static public bool DrawSubfolderSectionHeaderAbsolute(Rect rect, string displayName, string folderPath)
        {
            return DrawSubfolderSectionHeaderCore(rect, displayName, folderPath);
        }

        /// <summary>
        /// 绘制子目录分隔带标题栏的核心实现
        /// Core implementation for drawing subfolder section header bar
        /// </summary>
        static private bool DrawSubfolderSectionHeaderCore(Rect rect, string displayName, string folderPath)
        {
            bool repaint = false;

            // 绘制深色背景
            // Draw dark background
            EditorGUI.DrawRect(rect, ParticlePreviewSetting.Instance.SubdirectorySectionColor);

            bool isCollapsed = CollapsedSubfolders.Contains(folderPath);

            // 初始化或更新折叠箭头样式
            // Initialize or update foldout style
            EnsureFoldoutStyle();

            Rect foldoutRect = new Rect(rect.x + 6, rect.y + 1, rect.width - 12, rect.height - 2);
            bool expanded = EditorGUI.Foldout(foldoutRect, !isCollapsed, " " + displayName, true, _FoldoutStyle);

            // 更新折叠状态
            // Update collapsed state
            if (expanded && isCollapsed)
            {
                CollapsedSubfolders.Remove(folderPath);
                repaint = true;
            }
            else if (!expanded && !isCollapsed)
            {
                CollapsedSubfolders.Add(folderPath);
                repaint = true;
            }

            return repaint;
        }

        /// <summary>
        /// 确保折叠箭头样式已初始化
        /// Ensure foldout style is initialized
        /// </summary>
        static private void EnsureFoldoutStyle()
        {
            var titleColor = ParticlePreviewSetting.Instance.SubdirectoryTitleColor;
            if (_FoldoutStyle == null || _CachedTitleColor != titleColor)
            {
                _FoldoutStyle = new GUIStyle(EditorStyles.foldout);
                _FoldoutStyle.fontStyle = FontStyle.Bold;
                _FoldoutStyle.fontSize = 11;
                _FoldoutStyle.normal.textColor = titleColor;
                _FoldoutStyle.onNormal.textColor = titleColor;
                _FoldoutStyle.active.textColor = titleColor;
                _FoldoutStyle.onActive.textColor = titleColor;
                _FoldoutStyle.focused.textColor = titleColor;
                _FoldoutStyle.onFocused.textColor = titleColor;
                _CachedTitleColor = titleColor;
            }
        }
    }
}
#endif
