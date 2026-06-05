#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MBS.ParticlePreview
{
    public partial class ParticlePreviewWindow : EditorWindow
    {
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制工具栏
        /// Draw toolbar
        /// </summary>
        private void DrawToolbar()
        {
            // 绘制一个不可见的Toggle来占用默认焦点
            // Draw an invisible toggle to occupy default focus.
            GUI.SetNextControlName("FocusDummy");
            EditorGUI.Toggle(new Rect(-10, -10, 0, 0), false);

            // 窗口获焦后将焦点转到 Dummy，避免其他控件意外获焦
            // After window focus, move focus to dummy to prevent accidental control focus
            if (_NeedFocusDummy)
            {
                _NeedFocusDummy = false;
                GUI.FocusControl("FocusDummy");
            }

            // 检查是否有滑动条需要显示
            // Check if any slider needs to be shown
            bool hasSliders = ParticlePreviewSetting.Instance.ShowTapSpeed
                           || ParticlePreviewSetting.Instance.ShowTapGrid
                           || ParticlePreviewSetting.Instance.ShowTapColor
                           || ParticlePreviewSetting.Instance.ShowTapScale;

            // ── 滑动条行（仅在有滑动条需要显示时绘制）
            // ── Slider row (only drawn when sliders are visible)
            if (hasSliders)
            {
                GUILayout.BeginHorizontal(EditorStyles.helpBox);
                {
                    // 修正标签和滑动条相对按钮偏上1像素的问题
                    // Fix labels and sliders being 1px higher than buttons
                    var splitRow = ParticlePreviewSetting.Instance.TitleSplitRow;
                    var offsetFix = splitRow ? 0 : 1;

                    var fixedSlider = new GUIStyle(GUI.skin.horizontalSlider);
                    var fixedLabel = new GUIStyle(EditorStyles.label);
                    fixedSlider.margin.top += 1 + offsetFix;
                    fixedLabel.margin.top += 2 + offsetFix;

                    DrawSliderControls(fixedSlider, fixedLabel);
                    GUILayout.Space(1);
                }
                GUILayout.EndHorizontal();
            }

            // // 移动开关（点击切换移动模式）
            // // Move toggle (click to switch move mode)
            // var moveIcon = EditorGUIUtility.IconContent(_MoveEnabled ? "d_MoveTool" : "d_MoveTool On").image;
            // var moveTooltip = _MoveEnabled ? "当前: 移动已开启 (点击关闭)\nCurrent: Move ON (click to turn OFF)" : "当前: 移动已关闭 (点击开启)\nCurrent: Move OFF (click to turn ON)";
            // if (GUILayout.Button(new GUIContent(moveIcon, moveTooltip), EditorStyles.toolbarButton, GUILayout.Width(25)))
            // {
            //     _MoveEnabled = !_MoveEnabled;
            //     _MoveSpeed = _MoveEnabled ? ParticlePreviewSetting.Instance.MoveSpeed : 0f;
            //     foreach (var ctx in _Contexts.Values)
            //     {
            //         ctx.SetMoveSpeed(_MoveSpeed);
            //     }
            // }

            // // 重新播放按钮
            // // Refresh/Play button
            // var refreshIcon = EditorGUIUtility.isProSkin ? "d_PlayButton" : "PlayButton";
            // var refreshContent = new GUIContent(" Play", EditorGUIUtility.IconContent(refreshIcon).image);
            // if (GUILayout.Button(refreshContent, EditorStyles.toolbarButton))
            // {
            //     RefreshAllEffects();
            // }

            // // 清空按钮
            // // Clear button
            // Color originalColor = GUI.backgroundColor;
            // GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            // if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            // {
            //     CleanupAll();
            //     _FolderPrefabCache.Clear();
            //     _LastPageID = -2;
            //     _SelectedIdx = -1;
            // }
            // GUI.backgroundColor = originalColor;

            // GUILayout.Space(6);

            // // 分页按钮区域开关
            // // Toggle folder buttons visibility
            // _ShowFolderButtons = GUILayout.Toggle(_ShowFolderButtons, new GUIContent("📁", "显示/隐藏分页按钮\nShow/Hide folder page buttons"), EditorStyles.toolbarButton, GUILayout.Width(25));
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制滑动条控件（Speed / Grid / Color / Scale）
        /// Draw slider controls (Speed / Grid / Color / Scale)
        /// </summary>
        private void DrawSliderControls(GUIStyle fixedSlider, GUIStyle fixedLabel)
        {
            if (ParticlePreviewSetting.Instance.ShowTapSpeed)
            {
                GUILayout.Space(4);

                // 速率（点击标签重置为默认值 1.0）
                // Speed (click label to reset to default 1.0)
                DrawClickableLabel("Speed", "Click to reset to default", 38, () => _Speed = 1f);
                _Speed = GUILayout.HorizontalSlider(_Speed, 0.1f, 3f, fixedSlider, GUI.skin.horizontalSliderThumb, GUILayout.ExpandWidth(true), GUILayout.MinWidth(60));
                GUILayout.Label(_Speed.ToString("F1"), fixedLabel, GUILayout.Width(25));
                DrawToolbarSeparator();
            }

            // // 间隔（点击标签重置为默认值 1.0）
            // DrawClickableLabel("Interval", "点击重置为默认值", 48, () => _LoopInterval = 1.0f);
            // _LoopInterval = GUILayout.HorizontalSlider(_LoopInterval, 0.5f, 3f, GUILayout.ExpandWidth(true), GUILayout.MinWidth(60));
            // GUILayout.Label(_LoopInterval.ToString("F1") + "x", GUILayout.Width(30));

            // DrawToolbarSeparator();

            if (ParticlePreviewSetting.Instance.ShowTapGrid)
            {
                // 同屏格子数（点击标签重置为默认值 16）
                // Grid count (click label to reset to default 16)
                DrawClickableLabel("Grid", "Click to reset to default", 34, () => _GridCount = 16);
                _GridCount = (int)GUILayout.HorizontalSlider(_GridCount, 1, 50, fixedSlider, GUI.skin.horizontalSliderThumb, GUILayout.ExpandWidth(true), GUILayout.MinWidth(60));
                GUILayout.Label(_GridCount.ToString(), fixedLabel, GUILayout.Width(20));
                DrawToolbarSeparator();
            }

            if (ParticlePreviewSetting.Instance.ShowTapColor)
            {
                // 背景色（点击标签重置为默认值 0.35）
                // Background color (click label to reset to default 0.35)
                DrawClickableLabel("Color", "Click to reset to default", 42, () => _BgGray = 1f);
                _BgGray = GUILayout.HorizontalSlider(_BgGray, 0f, 2f, fixedSlider, GUI.skin.horizontalSliderThumb, GUILayout.ExpandWidth(true), GUILayout.MinWidth(60));
                GUILayout.Label(_BgGray.ToString("F2"), fixedLabel, GUILayout.Width(30));
                Color bgColor = new Color(_BgGray * ParticlePreviewSetting.Instance.DefaultColor, _BgGray * ParticlePreviewSetting.Instance.DefaultColor, _BgGray * ParticlePreviewSetting.Instance.DefaultColor, 1f);
                foreach (var ctx in _Contexts.Values)
                {
                    ctx.SetBackgroundColor(bgColor);
                }
                DrawToolbarSeparator();
            }

            if (ParticlePreviewSetting.Instance.ShowTapScale)
            {
                // 镜头距离系数（点击标签重置为默认值 1.0）
                // Camera distance multiplier (click label to reset to default 1.0)
                DrawClickableLabel("Scale", "Click to reset to default", 42, () => _CamDistMul = 1.0f);
                _CamDistMul = GUILayout.HorizontalSlider(_CamDistMul, 0.1f, 3f, fixedSlider, GUI.skin.horizontalSliderThumb, GUILayout.ExpandWidth(true), GUILayout.MinWidth(60));
                GUILayout.Label(_CamDistMul.ToString("F1"), fixedLabel, GUILayout.Width(25));
                foreach (var ctx in _Contexts.Values)
                {
                    ctx.SetDistanceMultiplier(_CamDistMul);
                }
                // DrawToolbarSeparator();
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 保存 Toolbar 设置到 EditorPrefs
        /// Save toolbar settings to EditorPrefs
        /// </summary>
        private void SaveToolbarSettings()
        {
            EditorPrefs.SetFloat(ParticlePreviewDefine.PREF_SPEED, _Speed);
            EditorPrefs.SetFloat(ParticlePreviewDefine.PREF_BG_GRAY, _BgGray);
            EditorPrefs.SetInt(ParticlePreviewDefine.PREF_GRID_COUNT, _GridCount);
            EditorPrefs.SetFloat(ParticlePreviewDefine.PREF_CAM_DIST_MUL, _CamDistMul);
            EditorPrefs.SetBool(ParticlePreviewDefine.PREF_HIGH_FPS, ParticlePreviewContext.HighFrameRateMode);
            EditorPrefs.SetBool(ParticlePreviewDefine.PREF_SHOW_HELP_TIPS, _ShowHelpTips);
            EditorPrefs.SetBool(ParticlePreviewDefine.PREF_SHOW_NAME, _ShowName);
        }

        /// <summary>
        /// 从 EditorPrefs 加载 Toolbar 设置
        /// Load toolbar settings from EditorPrefs
        /// </summary>
        private void LoadToolbarSettings()
        {
            _Speed = EditorPrefs.GetFloat(ParticlePreviewDefine.PREF_SPEED, 1f);
            _BgGray = EditorPrefs.GetFloat(ParticlePreviewDefine.PREF_BG_GRAY, 1f);
            _GridCount = EditorPrefs.GetInt(ParticlePreviewDefine.PREF_GRID_COUNT, 16);
            _CamDistMul = EditorPrefs.GetFloat(ParticlePreviewDefine.PREF_CAM_DIST_MUL, 1.0f);
            _ShowHelpTips = EditorPrefs.GetBool(ParticlePreviewDefine.PREF_SHOW_HELP_TIPS, true);
            _ShowName = EditorPrefs.GetBool(ParticlePreviewDefine.PREF_SHOW_NAME, false);
            ParticlePreviewContext.HighFrameRateMode = EditorPrefs.GetBool(ParticlePreviewDefine.PREF_HIGH_FPS, true);
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制可点击的标签（点击时触发回调）
        /// Draw clickable label (triggers callback on click)
        /// </summary>
        private void DrawClickableLabel(string text, string tooltip, float width, System.Action onClick)
        {
            bool splitRow = ParticlePreviewSetting.Instance.TitleSplitRow;

            // 修正分行时标签和滑动条相对于按钮偏上的问题
            float contentOffsetFix = splitRow ? 1f : 2f;
            float singleLineHeightFix = splitRow ? 2f : 4f;
            float labelRectHeightFix = splitRow ? 0.5f : 2f;

            var centeredStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
            centeredStyle.contentOffset = new Vector2(0, contentOffsetFix);
            GUILayout.Label(new GUIContent(text, tooltip), centeredStyle, GUILayout.Width(width));
            Rect labelRect = GUILayoutUtility.GetLastRect();

            // 将高亮/点击区域扩展到与 helpBox 行等高
            // Expand highlight/click rect to match helpBox row height
            float toolbarHeight = EditorGUIUtility.singleLineHeight + singleLineHeightFix;
            float yCenter = labelRect.y + labelRect.height * 0.5f + labelRectHeightFix; // 调整中心点以匹配 helpBox 的视觉中心
            Rect hitRect = new Rect(labelRect.x, yCenter - toolbarHeight * 0.5f, width, toolbarHeight);

            EditorGUIUtility.AddCursorRect(hitRect, MouseCursor.Link);

            // 鼠标悬停时绘制高亮背景
            // Draw highlight background on mouse hover
            bool isHovering = hitRect.Contains(Event.current.mousePosition);
            if (isHovering)
            {
                if (Event.current.type == EventType.Repaint)
                {
                    var highlightColor = EditorGUIUtility.isProSkin
                        ? new Color(1f, 1f, 1f, 0.08f)
                        : new Color(0f, 0f, 0f, 0.08f);
                    EditorGUI.DrawRect(hitRect, highlightColor);
                }
                Repaint();
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && hitRect.Contains(Event.current.mousePosition))
            {
                // 记录撤销状态（在修改前）
                // Record undo state (before modification)
                Undo.RecordObject(this, "Reset " + text);
                onClick?.Invoke();
                Event.current.Use();
                ShowNotification(new GUIContent("Reset " + text + " to default value"));
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制工具栏分割线
        /// Draw toolbar separator
        /// </summary>
        private void DrawToolbarSeparator()
        {
            GUILayout.Space(1);
            GUILayout.Label(GUIContent.none, EditorStyles.toolbarButton, GUILayout.Width(0.5f), GUILayout.Height(16));
            GUILayout.Space(1);
        }
    }
}
#endif