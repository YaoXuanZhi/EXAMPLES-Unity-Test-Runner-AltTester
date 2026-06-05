#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MBS.ParticlePreview
{
    public partial class ParticlePreviewWindow : EditorWindow
    {
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 帮助提示内容（每条一行，方便添加）
        // Help tips content (one per line for easy editing)
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        static private readonly string[] _HelpTips = new string[]
        {
            "Left-Click: Replay particle",
            "Left-Hold: Stop movement",
            "Right-Click: Select asset",
            "Right-Drag: Rotate camera",
            "Middle-Drag: Pan effect",
            "HoldCtrl + Scroll Wheel: Zoom in / out",
            "HoldRight + Scroll Wheel: Zoom in / out",
            "Click Status Bar: Toggle this help tips",
        };

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 状态栏按钮区域起始X坐标（用于计算帮助提示点击区域）
        // Status bar buttons area start X position (for help tips click area calculation)
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private float _StatusBtnStartX;

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制底部状态栏（仅在选中了文件夹分页时显示）
        /// Draw bottom status bar (only visible when a folder page is selected)
        /// </summary>
        private void DrawStatusBar()
        {
            // 懒加载 GUIStyle
            // Lazy-load GUIStyle
            if (_StatusLeftStyle == null)
            {
                _StatusLeftStyle = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip };
                _StatusRightStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight, clipping = TextClipping.Clip };
            }

            GUILayout.BeginHorizontal(EditorStyles.helpBox);

            // 左侧：帮助图标 + 选中信息
            // Left: help icon + selected info
            var helpIcon = EditorGUIUtility.IconContent("d__Help");
            GUILayout.Label(helpIcon, GUILayout.ExpandWidth(false));

            string selectedInfo;
            if (_SelectedIdx >= 0 && _SelectedIdx < _FolderPrefabCache.Count && _FolderPrefabCache[_SelectedIdx] != null)
            {
                selectedInfo = $"{_SelectedIdx + 1}/{_CachedTotalCount} | {_FolderPrefabCache[_SelectedIdx].name}";
            }
            else
            {
                selectedInfo = $"Total: {_CachedTotalCount}";
            }
            EditorGUILayout.LabelField(selectedInfo, _StatusLeftStyle, GUILayout.MinWidth(0));

            // 右侧：功能按钮（从工具栏移动至此）
            // Right: function buttons (moved from toolbar)
            GUILayout.FlexibleSpace();
            if (Event.current.type == EventType.Repaint) _StatusBtnStartX = GUILayoutUtility.GetLastRect().xMax;
            DrawStatusBarButtons();
            GUILayout.Space(1);

            GUILayout.EndHorizontal();

            // 悬停高亮 + 点击切换帮助提示（排除右侧按钮区域）
            // Hover highlight + click to toggle help tips (exclude right-side buttons area)
            var statusRect = GUILayoutUtility.GetLastRect();
            var clickWidth = _StatusBtnStartX > statusRect.x ? _StatusBtnStartX - statusRect.x : statusRect.width;
            var helpClickRect = new Rect(statusRect.x, statusRect.y, clickWidth, statusRect.height);
            var isHover = helpClickRect.Contains(Event.current.mousePosition);

            // 鼠标悬停时绘制高亮遮罩（仅覆盖帮助提示点击区域）
            // Draw highlight overlay on mouse hover (only cover help tips click area)
            if (isHover && Event.current.type == EventType.Repaint)
            {
                var prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.08f);
                GUI.DrawTexture(helpClickRect, EditorGUIUtility.whiteTexture);
                GUI.color = prevColor;
            }

            EditorGUIUtility.AddCursorRect(helpClickRect, MouseCursor.Link);
            if (Event.current.type == EventType.MouseDown && isHover)
            {
                if (_CurrentPageID >= 0)
                {
                    _ShowHelpTips = !_ShowHelpTips;
                    Event.current.Use();
                }
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制状态栏右侧的功能按钮（FPS / Group / Folder / Settings）
        /// Draw function buttons on the right side of status bar (FPS / Group / Folder / Settings)
        /// </summary>
        private void DrawStatusBarButtons()
        {
            // 高帧率模式切换按钮
            // High frame rate mode toggle button
            if (ParticlePreviewSetting.Instance.ShowTapFPS)
            {
                var modeIcon = EditorGUIUtility.IconContent(ParticlePreviewContext.HighFrameRateMode ? "IN foldout act" : "ArrowNavigationRight").image;
                var modeTooltip = ParticlePreviewContext.HighFrameRateMode ? "Current: High FPS" : "Current: Default";
                if (GUILayout.Button(new GUIContent("", modeIcon, modeTooltip), EditorStyles.toolbarButton, GUILayout.Width(30)))
                {
                    ParticlePreviewContext.HighFrameRateMode = !ParticlePreviewContext.HighFrameRateMode;
                    ShowNotification(new GUIContent(ParticlePreviewContext.HighFrameRateMode ? "Switched to High FPS mode" : "Switched to Default mode"));
                }
            }

            // 名称标签开关
            // Name label toggle
            Rect titleToggleRect = EditorGUILayout.GetControlRect(GUILayout.Width(16));
            titleToggleRect.y += 1f;
            _ShowName = EditorGUI.Toggle(titleToggleRect, _ShowName);
            Rect titleLabelRect = EditorGUILayout.GetControlRect(GUILayout.Width(32));
            titleLabelRect.y += 0.5f;
            GUI.Label(titleLabelRect, "Title", EditorStyles.label);

            // 子目录分组开关
            // Subdirectory grouping toggle
            if (ParticlePreviewSetting.Instance.ShowTapGroup)
            {
                Rect groupToggleRect = EditorGUILayout.GetControlRect(GUILayout.Width(16));
                groupToggleRect.y += 1f;
                bool oldGroupValue = ParticlePreviewSetting.Instance.UseSubdirectory;
                ParticlePreviewSetting.Instance.UseSubdirectory = EditorGUI.Toggle(groupToggleRect, oldGroupValue);
                Rect groupLabelRect = EditorGUILayout.GetControlRect(GUILayout.Width(42));
                groupLabelRect.y += 0.5f;
                GUI.Label(groupLabelRect, "Group", EditorStyles.label);
                if (oldGroupValue != ParticlePreviewSetting.Instance.UseSubdirectory)
                {
                    // 切换分组模式时重建缓存
                    // Rebuild cache when toggling group mode
                    ParticlePreviewGrouper.BuildGroupedCache(_FolderPrefabCache, ParticlePreviewSetting.Instance.UseSubdirectory);
                    CleanupAll();
                    _SelectedIdx = -1;
                }
            }

            // Folder 配置按钮（图标）
            // Folder config button (icon)
            if (ParticlePreviewSetting.Instance.ShowTapFolder)
            {
                var folderIcon = EditorGUIUtility.isProSkin ? "d_Project" : "Project";
                var folderContent = new GUIContent(" Folder", EditorGUIUtility.IconContent(folderIcon).image, "Open folder config");
                if (GUILayout.Button(folderContent, EditorStyles.toolbarButton, GUILayout.MaxWidth(80)))
                {
                    Selection.activeObject = ParticlePreviewFolder.Instance;
                    EditorGUIUtility.PingObject(ParticlePreviewFolder.Instance);
                }
            }

            // Setting 配置按钮（图标）
            // Setting config button (icon)
            var settingsIcon = EditorGUIUtility.isProSkin ? "d_Settings" : "Settings";
            var settingsContent = new GUIContent("", EditorGUIUtility.IconContent(settingsIcon).image, "Open setting config");
            if (GUILayout.Button(settingsContent, EditorStyles.toolbarButton, GUILayout.MaxWidth(35)))
            {
                Selection.activeObject = ParticlePreviewSetting.Instance;
                EditorGUIUtility.PingObject(ParticlePreviewSetting.Instance);
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制帮助提示条（状态栏上方的圆角黑条）
        /// Draw help tips bar (rounded black bar above the status bar)
        /// </summary>
        private void DrawHelpTipsBar()
        {
            // 没有选中文件夹分页时不显示帮助提示
            // Don't show help tips when no folder page is selected
            if (_CurrentPageID < 0) return;
            if (!_ShowHelpTips) return;

            // 懒加载帮助提示样式
            // Lazy-load help tips style
            if (_HelpTipsStyle == null)
            {
                _HelpTipsStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    wordWrap = false,
                    richText = true,
                    normal = { textColor = new Color(0.85f, 0.85f, 0.85f, 1f) },
                    padding = new RectOffset(6, 6, 2, 2),
                };
            }

            // 计算内容高度
            // Calculate content height
            float lineHeight = _HelpTipsStyle.lineHeight + _HelpTipsStyle.padding.vertical;
            float totalHeight = _HelpTips.Length * lineHeight + 10f;

            // 绘制圆角黑色背景
            // Draw rounded black background
            var bgRect = GUILayoutUtility.GetRect(0f, totalHeight, GUILayout.ExpandWidth(true));
            bgRect.x += 4f;
            bgRect.width -= 8f;

            // 使用 GUI.DrawTexture 配合圆角
            // Use GUI.DrawTexture with rounding
            var prevColor = GUI.color;
            GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
            GUI.Box(bgRect, GUIContent.none, EditorStyles.helpBox);
            GUI.color = prevColor;

            // 逐行绘制提示文字
            // Draw tip text line by line
            float y = bgRect.y + 5f;
            for (int i = 0; i < _HelpTips.Length; i++)
            {
                var lineRect = new Rect(bgRect.x + 8f, y, bgRect.width - 16f, lineHeight);
                GUI.Label(lineRect, $"<b>·</b>  {_HelpTips[i]}", _HelpTipsStyle);
                y += lineHeight;
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制预览纹理（忽略alpha通道，避免半透明边缘问题）
        /// Draw preview texture (ignore alpha channel to avoid translucent edge issues)
        /// </summary>
        private void DrawTextureOpaque(Rect rect, Texture tex)
        {
            if (Event.current.type != EventType.Repaint) return;

            // 使用 EditorGUI.DrawPreviewTexture 绘制，它会自动处理颜色空间转换
            // Use EditorGUI.DrawPreviewTexture which handles color space conversion automatically
            EditorGUI.DrawPreviewTexture(rect, tex, null, ScaleMode.ScaleToFit);
        }
    }
}
#endif