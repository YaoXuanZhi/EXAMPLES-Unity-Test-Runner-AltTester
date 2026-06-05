#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static MBS.ParticlePreview.ParticlePreviewDefine;

namespace MBS.ParticlePreview
{
    public partial class ParticlePreviewWindow : EditorWindow
    {
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 常量 / Constants
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private const float CELL_PADDING = 4f;       // 格子之间的间距 / Cell padding
        private const float DEFAULT_MOVE_SPEED = 5f; // 默认移动速度 / Default move speed
        private const float SCROLLBAR_WIDTH = 14f;   // 滚动条预留宽度 / Scrollbar reserved width

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 设置项 / Settings
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private int _GridCount = 16;         // 同屏显示的格子数（1-50） / Grid count on screen
        private float _BgGray = 0.3f;        // 背景灰度值（0-1） / Background gray value
        private float _CamDistMul = 1.0f;    // 镜头距离系数（0.1-3.0） / Camera distance multiplier
        private float _Fps = 30f;            // 目标刷新帧率 / Target refresh FPS
        private float _LoopInterval = 1.0f;  // 循环间隔倍率 / Loop interval multiplier
        private float _MoveSpeed = 5f;       // 移动速度 / Move speed
        private float _Speed = 1f;           // 播放速率倍数 / Playback speed multiplier
        private bool _MoveEnabled = true;    // 移动开关 / Move toggle
        private bool _MovePausedByCtrl = false;  // 输入导致的临时暂停 / Temporary pause by input
        private bool _IsLeftMouseHeld = false;   // 左键是否按住 / Is left mouse held
        private bool _IsRightMouseHeld = false;  // 右键是否按住 / Is right mouse held

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 计时 / Timing
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private double _LastFrameTime; // 上一帧的时间戳 / Last frame timestamp
        private float _DeltaTime;      // 当前帧间隔时间 / Current delta time

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // UI 状态 / UI State
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private int _SelectedIdx = -1;      // 当前选中的格子索引 / Selected cell index
        private float _LastScrollY = -1f;   // 上次滚动位置 / Last scroll position
        private bool _ShowDebug = false;    // 是否显示调试信息 / Show debug info
        private bool _ShowName = false;     // 是否显示名称标签 / Show name label
        private bool _ShowHelpTips = true;  // 是否显示帮助提示 / Show help tips
        private GUIStyle _BoxStyle;         // 格子背景样式 / Cell box style
        private GUIStyle _PageBtnStyle;     // 分页按钮样式 / Page button style
        private GUIStyle _StatusLeftStyle;  // 状态栏左侧样式 / Status bar left style
        private GUIStyle _StatusRightStyle; // 状态栏右侧样式 / Status bar right style
        private GUIStyle _HelpTipsStyle;    // 帮助提示文字样式 / Help tips text style
        private GUIStyle _NameLabelStyle;   // 名称标签样式 / Name label style
        private Dictionary<int, Vector2> _PageScrollPos = new Dictionary<int, Vector2>(); // 每个分页滚动位置 / Per-page scroll

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 拖拽状态 / Drag State
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private int _DragTargetIdx = -1; // 拖拽目标格子索引 / Drag target cell index
        private Vector2 _DragStart;      // 拖拽起始位置 / Drag start position
        private bool _HasDragged = false;// 是否发生过拖拽 / Has dragged
        private bool _HasPanned = false; // 是否发生过平移 / Has panned
        private bool _IsDragging = false;// 是否正在右键拖拽 / Is right-drag rotating
        private bool _IsPanning = false; // 是否正在中键平移 / Is middle-drag panning

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 分页状态 / Page State
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private int _CurrentPageID = -1;  // 当前分页索引 / Current page index
        private int _LastPageID = -2;     // 上次分页索引 / Last page index
        private bool _ShowFolderButtons = true; // 显示分页按钮 / Show folder buttons
        private List<GameObject> _FolderPrefabCache = new List<GameObject>(); // 分页 Prefab 缓存 / Page prefab cache

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 运行时缓存 / Runtime Cache
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private int _CachedContextCount; // 缓存 Context 数 / Cached context count
        private int _CachedTotalCount;   // 缓存总数 / Cached total count
        private int _CachedTotalRows;    // 缓存总行数 / Cached total rows
        private int _CachedVisibleCount; // 缓存可见数 / Cached visible count
        private HashSet<GameObject> _VisibleSet = new HashSet<GameObject>(); // 可见 Prefab 集合 / Visible prefab set
        private List<GameObject> _RemoveBuffer = new List<GameObject>();     // 待移除缓冲 / Remove buffer
        private Dictionary<GameObject, ParticlePreviewContext> _Contexts = new Dictionary<GameObject, ParticlePreviewContext>(); // 预览上下文 / Preview contexts

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 内部状态 / Internal State
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private bool _IsCleanedUp = false;   // 是否已清理 / Is cleaned up
        private bool _NeedFocusDummy = false; // 窗口获焦时需要转移焦点到 Dummy / Need to focus dummy on window focus

        /************************************************************************************************************************/

        /// <summary>
        /// 窗口启用时初始化
        /// Initialize when window is enabled
        /// </summary>
        private void OnEnable()
        {
            _LastFrameTime = EditorApplication.timeSinceStartup;
            _DeltaTime = 0f;
            _IsCleanedUp = false;  // 重置清理标记 / Reset cleanup flag

            // 从 EditorPrefs 恢复 Toolbar 设置
            // Restore toolbar settings from EditorPrefs
            LoadToolbarSettings();

            // 注册编辑器更新回调，用于控制刷新帧率
            // Register editor update callback for frame rate control
            EditorApplication.update += OnEditorUpdate;
            // 注册编辑器退出事件，确保资源被清理
            // Register editor quit event to ensure resource cleanup
            EditorApplication.quitting += OnEditorQuitting;
            // 注册 Play Mode 状态变化事件，切换时取消选中
            // Register Play Mode state change event to deselect on switch
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        /// <summary>
        /// 窗口禁用时清理资源
        /// Cleanup resources when window is disabled
        /// </summary>
        private void OnDisable()
        {
            PerformCleanup();
        }

        /// <summary>
        /// 窗口销毁时清理资源（确保在所有情况下都能清理）
        /// Cleanup resources on window destroy (ensure cleanup in all cases)
        /// </summary>
        private void OnDestroy()
        {
            PerformCleanup();
        }

        /// <summary>
        /// 编辑器退出时清理资源
        /// Cleanup resources when editor quits
        /// </summary>
        private void OnEditorQuitting()
        {
            PerformCleanup();
        }

        /// <summary>
        /// 程序集重载前清理资源（避免热重载时资源泄漏）
        /// Cleanup resources before assembly reload (avoid resource leak on hot reload)
        /// </summary>
        private void OnBeforeAssemblyReload()
        {
            PerformCleanup();
        }

        /// <summary>
        /// Play Mode 状态变化时取消选中文件夹并清理资源
        /// Deselect folder and cleanup resources when Play Mode state changes
        /// </summary>
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // 切换运行状态时强制取消选中
            // Force deselect when switching play state
            _CurrentPageID = -1;
            _LastPageID = -2;
            _FolderPrefabCache.Clear();
            _SelectedIdx = -1;
            ParticlePreviewGrouper.ClearCache();
            CleanupAll();
        }

        /// <summary>
        /// 执行清理操作（统一清理入口，避免重复清理）
        /// Perform cleanup (unified cleanup entry, avoids duplicate cleanup)
        /// </summary>
        private void PerformCleanup()
        {
            // 检查是否已经清理过，避免重复清理
            // Check if already cleaned up to avoid duplicate cleanup
            if (_IsCleanedUp) return;
            _IsCleanedUp = true;

            // 保存 Toolbar 设置到 EditorPrefs
            // Save toolbar settings to EditorPrefs
            SaveToolbarSettings();

            // 重置分页状态（避免重编译后蓝色边框残留）
            // Reset page state (prevent blue border lingering after recompilation)
            _CurrentPageID = -1;
            _LastPageID = -2;
            _SelectedIdx = -1;

            // 移除事件监听
            // Remove event listeners
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;

            // 清除静态缓存的窗口引用（避免引用已销毁的窗口）
            // Clear static cached window reference (avoid referencing destroyed window)
            ParticlePreviewContext.ClearCachedWindow();

            // 清理所有预览上下文
            // Cleanup all preview contexts
            CleanupAll();

            // 清空其他缓存，释放 Prefab 引用
            // Clear other caches to release Prefab references
            _FolderPrefabCache.Clear();
            _PageScrollPos.Clear();
            ParticlePreviewGrouper.ClearCache();

            // 清理圆角遮罩纹理
            // Cleanup corner mask textures
            ParticlePreviewMasker.CleanupCornerMasks();

            // 强制 GC 回收（预览窗口会产生大量临时对象和 RenderTexture）
            // Force GC collect (preview window generates many temporary objects and RenderTextures)
            EditorUtility.UnloadUnusedAssetsImmediate();
            System.GC.Collect();
        }

        /************************************************************************************************************************/

        /// <summary>
        /// 编辑器更新回调，控制刷新帧率
        /// Editor update callback, controls refresh frame rate
        /// 通过限制刷新频率来降低 CPU 占用
        /// Reduces CPU usage by limiting refresh frequency
        /// </summary>
        private void OnEditorUpdate()
        {
            double currentTime = EditorApplication.timeSinceStartup;
            double timePerFrame = 1.0 / _Fps;

            // 达到目标帧间隔时才刷新
            // Only refresh when target frame interval is reached
            if (currentTime - _LastFrameTime >= timePerFrame)
            {
                _DeltaTime = (float)(currentTime - _LastFrameTime);
                _DeltaTime = Mathf.Clamp(_DeltaTime, 0f, 0.1f);  // 限制最大 deltaTime，防止卡顿时动画跳帧 / Clamp max deltaTime to prevent frame skipping on lag
                _LastFrameTime = currentTime;
                Repaint();  // 触发 OnGUI 重绘 / Trigger OnGUI repaint
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 窗口获得焦点时，标记需要将焦点转移到 FocusDummy
        /// When window gains focus, mark to transfer focus to FocusDummy
        /// </summary>
        private void OnFocus()
        {
            _NeedFocusDummy = true;
            // Debug.Log("ParticlePreviewWindow gained focus, will transfer focus to dummy.");
        }

        /************************************************************************************************************************/

        private void OnGUI()
        {
            if (_BoxStyle == null)
            {
                _BoxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(0, 0, 0, 0) };
            }

            HandleGlobalEvents();
            DrawToolbar();
            DrawFolderButtons();
            DrawGridArea();
            DrawHelpTipsBar();
            DrawStatusBar();
            HandleFolderDragDrop();
        }

        /************************************************************************************************************************/
        #region 绘制方法 / Drawing Methods
        /************************************************************************************************************************/

        /// <summary>
        /// 绘制文件夹分页按钮区域
        /// Draw folder page buttons area
        /// </summary>
        private void DrawFolderButtons()
        {
            if (!_ShowFolderButtons) return;
            var folder = ParticlePreviewFolder.Instance;
            var setting = ParticlePreviewSetting.Instance;
            List<string> pageNames = folder.GetPageNames();

            // 没有配置文件夹分页时不绘制
            // Don't draw when no folder pages configured
            if (pageNames.Count <= 0) return;

            // 按钮样式（懒加载缓存）
            // Button style (lazy-init cache)
            if (_PageBtnStyle == null)
            {
                _PageBtnStyle = new GUIStyle(GUI.skin.button);
            }
            _PageBtnStyle.wordWrap = false;
            _PageBtnStyle.clipping = TextClipping.Clip;
            _PageBtnStyle.fontSize = setting.PageButtonTextSize;
            _PageBtnStyle.alignment = TextAnchor.MiddleCenter;

            EditorGUILayout.BeginVertical();

            float availableWidth = position.width - 30;
            if (availableWidth < 1f) availableWidth = 1f;

            int rowCount = Mathf.Max(1, setting.PageButtonRowCount);
            float buttonWidth = availableWidth / rowCount;
            float buttonHeight = setting.PageButtonHeight;
            if (buttonWidth < 1f) buttonWidth = 1f;

            int newSelection = _CurrentPageID;
            int totalButtons = pageNames.Count;
            int rows = Mathf.CeilToInt((float)totalButtons / rowCount);

            for (int r = 0; r < rows; r++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < rowCount; c++)
                {
                    int index = r * rowCount + c;
                    if (index >= totalButtons)
                    {
                        GUILayout.Space(buttonWidth);
                        continue;
                    }

                    // 预分配按钮区域，手动处理鼠标事件
                    // Reserve button rect, handle mouse events manually
                    Rect btnRect = GUILayoutUtility.GetRect(new GUIContent(pageNames[index]), _PageBtnStyle, GUILayout.Height(buttonHeight), GUILayout.Width(buttonWidth));

                    // 拦截右键 MouseDown，阻止 GUI.Button 响应右键
                    // Intercept right-click MouseDown to prevent GUI.Button from handling it
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && btnRect.Contains(Event.current.mousePosition))
                    {
                        Event.current.Use();
                    }

                    // 右键弹出上下文菜单（删除分页）
                    // Right-click to show context menu (delete page)
                    if (Event.current.type == EventType.ContextClick && btnRect.Contains(Event.current.mousePosition))
                    {
                        int capturedIndex = index;
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Delete"), false, () =>
                        {
                            // 从 SimpleFolderPages 移除
                            // Remove from SimpleFolderPages
                            ParticlePreviewFolder.Instance.RemovePage(capturedIndex);

                            // 重置分页状态
                            // Reset page state
                            _CurrentPageID = -1;
                            _LastPageID = -2;
                            _FolderPrefabCache.Clear();
                            _SelectedIdx = -1;
                            ParticlePreviewGrouper.ClearCache();
                            CleanupAll();
                            Repaint();
                        });
                        menu.ShowAsContext();
                        Event.current.Use();
                    }

                    // 绘制按钮并处理左键点击（切换选中状态）
                    // Draw button and handle left-click (toggle selection)
                    if (GUI.Button(btnRect, pageNames[index], _PageBtnStyle))
                    {
                        newSelection = (_CurrentPageID == index) ? -1 : index;
                    }

                    if (_CurrentPageID == index)
                    {
                        ParticlePreviewMasker.DrawInnerBorder(btnRect, setting.PageButtonSelectedColor, 2f);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            // 更新选择
            // Update selection
            if (_CurrentPageID != newSelection)
            {
                _CurrentPageID = newSelection;
                _LastPageID = -2; // 强制刷新
                CleanupAll();
                _SelectedIdx = -1;
                ParticlePreviewGrouper.ClearCache();
                // 取消选中时清空缓存
                // Clear cache when deselected
                if (_CurrentPageID < 0)
                {
                    _FolderPrefabCache.Clear();
                }
            }

            // 检测是否需要刷新分页内容
            // Check whether to refresh page contents
            if (_CurrentPageID >= 0 && _LastPageID != _CurrentPageID)
            {
                _LastPageID = _CurrentPageID;
                _FolderPrefabCache = folder.GetPrefabs(_CurrentPageID);

                // 构建子目录分组缓存
                // Build subdirectory grouping cache
                ParticlePreviewGrouper.BuildGroupedCache(_FolderPrefabCache, ParticlePreviewSetting.Instance.UseSubdirectory);
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制网格预览区域
        /// Draw grid preview area
        /// 使用虚拟滚动技术，只渲染可见的格子，支持大量特效的流畅预览
        /// Uses virtual scrolling to only render visible cells, supports smooth preview of many effects
        /// 当启用子目录分组时，使用 GUILayout 分段渲染模式
        /// When subdirectory grouping is enabled, uses GUILayout segment rendering mode
        /// </summary>
        private void DrawGridArea()
        {
            if (_FolderPrefabCache == null || _FolderPrefabCache.Count == 0) return;

            // 可用尺寸
            // Available dimensions
            float availableWidth = position.width - SCROLLBAR_WIDTH;
            float scrollViewHeight = Mathf.Max(100f, position.height - 120f);  // 预估可视区域高度

            // 计算最佳行列布局
            // Calculate optimal row-column layout
            int targetCount = Mathf.Max(1, _GridCount);  // 目标同屏格子数
            float aspectRatio = availableWidth / scrollViewHeight;  // 可视区域宽高比

            // 根据宽高比和目标格子数计算最佳列数
            // Calculate optimal columns based on aspect ratio and target grid count
            // 公式：cols ≈ sqrt(N * aspectRatio)，这样可以让格子尽量保持正方形
            // Formula: cols ≈ sqrt(N * aspectRatio), keeps cells roughly square
            int columns = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(targetCount * aspectRatio)));
            columns = Mathf.Min(columns, targetCount);  // 列数不超过目标数

            // 格子大小直接由宽度决定，填满整个宽度
            // Cell size determined by width, fills entire width
            float actualCellSize = availableWidth / columns;

            // 确保格子有最小尺寸
            // Ensure cells have minimum size
            actualCellSize = Mathf.Max(actualCellSize, 50f);

            // 计算 deltaTime（应用播放速率）
            // Calculate deltaTime (apply playback speed)
            float ratio = ParticlePreviewSetting.Instance.DefaultSpeed;
            float dt = _DeltaTime * _Speed * ratio;     // 应用速率后的 deltaTime / Speed-applied deltaTime
            float rawDt = _DeltaTime;                   // 原始 deltaTime / Raw deltaTime

            // 判断是否启用分组模式
            // Check if grouping mode is enabled
            var groupedCache = ParticlePreviewGrouper.GroupedCache;
            bool isGroupedMode = ParticlePreviewSetting.Instance.UseSubdirectory && groupedCache != null && groupedCache.Count > 0;

            if (isGroupedMode)
            {
                DrawGridAreaGrouped(availableWidth, scrollViewHeight, actualCellSize, columns, dt, rawDt, groupedCache);
            }
            else
            {
                DrawGridAreaFlat(availableWidth, scrollViewHeight, actualCellSize, columns, dt, rawDt);
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制非分组模式的网格区域（使用虚拟滚动）
        /// Draw non-grouped grid area (using virtual scrolling)
        /// </summary>
        private void DrawGridAreaFlat(float availableWidth, float scrollViewHeight, float actualCellSize, int columns, float dt, float rawDt)
        {
            int totalCount = _FolderPrefabCache.Count;
            int rows = Mathf.CeilToInt((float)totalCount / columns);
            float totalHeight = rows * actualCellSize;

            int pageKey = Mathf.Max(0, _CurrentPageID);
            _PageScrollPos.TryGetValue(pageKey, out var scrollPos);
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
            _PageScrollPos[pageKey] = scrollPos;
            {
                // 预留内容空间（让滚动条知道总高度）
                // Reserve content space (let scrollbar know total height)
                GUILayoutUtility.GetRect(availableWidth, totalHeight);

                // 计算真正可见的行范围（用于决定哪些格子需要播放动画）
                // Calculate truly visible row range (for deciding which cells need animation)
                int firstTrueVisibleRow = Mathf.Max(0, Mathf.FloorToInt(scrollPos.y / actualCellSize));
                int lastTrueVisibleRow = Mathf.Min(rows - 1, Mathf.CeilToInt((scrollPos.y + scrollViewHeight) / actualCellSize));

                // 渲染时增加上下各 1 行缓冲，减少滚动时的闪烁
                // Add 1 row buffer above and below to reduce flickering when scrolling
                int firstVisibleRow = Mathf.Max(0, firstTrueVisibleRow - 1);
                int lastVisibleRow = Mathf.Min(rows - 1, lastTrueVisibleRow + 1);

                // 清空并重新记录可见的 Prefab
                // Clear and re-record visible Prefabs
                _VisibleSet.Clear();
                int trueVisibleCount = 0;

                for (int row = firstVisibleRow; row <= lastVisibleRow; row++)
                {
                    bool isTrueVisible = (row >= firstTrueVisibleRow && row <= lastTrueVisibleRow);

                    for (int col = 0; col < columns; col++)
                    {
                        int index = row * columns + col;
                        if (index >= totalCount) break;

                        GameObject prefab = _FolderPrefabCache[index];
                        if (prefab == null) continue;

                        _VisibleSet.Add(prefab);
                        if (isTrueVisible) trueVisibleCount++;

                        Rect cellRect = new Rect(
                            col * actualCellSize + CELL_PADDING / 2,
                            row * actualCellSize + CELL_PADDING / 2,
                            actualCellSize - CELL_PADDING,
                            actualCellSize - CELL_PADDING
                        );

                        DrawSingleCell(cellRect, prefab, dt, rawDt, _LoopInterval, index, isTrueVisible);
                    }
                }

                // 清理不可见的 Context（释放 PreviewRenderUtility 资源）
                // Cleanup invisible Contexts (release PreviewRenderUtility resources)
                CleanupInvisibleContexts();

                // 缓存状态栏数据
                // Cache status bar data
                _CachedTotalCount = totalCount;
                _CachedVisibleCount = trueVisibleCount;
                _CachedContextCount = _Contexts.Count;
                _CachedTotalRows = rows;

                // 调试信息
                // Debug info
                if (_ShowDebug)
                {
                    string debugInfo = $"Scroll: {scrollPos.y:F0} | Rows: {firstTrueVisibleRow}-{lastTrueVisibleRow}/{rows}";
                    GUI.Label(new Rect(10, scrollPos.y + 5, 400, 20), debugInfo, EditorStyles.helpBox);
                }
            }
            GUILayout.EndScrollView();
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制分组模式的网格区域（使用预计算偏移 + 虚拟滚动，避免大量 GUILayout 调用导致卡顿）
        /// Draw grouped grid area (pre-calculated offsets + virtual scrolling, avoids GUILayout overhead)
        /// </summary>
        private void DrawGridAreaGrouped(float availableWidth, float scrollViewHeight, float actualCellSize, int columns, float dt, float rawDt, List<ParticleSubfolderGroup> groupedCache)
        {
            // 分组标题栏高度常量
            // Section header height constants
            const float HEADER_HEIGHT = 22f;
            const float HEADER_SPACE = 4f;
            const float HEADER_PADDING = 4f;
            float sectionHeight = HEADER_HEIGHT + HEADER_SPACE;

            int totalCount = _FolderPrefabCache.Count;
            int segCount = groupedCache.Count;

            // ── 第一步：预计算每个 Segment 的 Y 偏移和总高度 ──
            // ── Step 1: Pre-calculate Y offset and total height for each segment ──
            float totalHeight = 0f;
            float[] segYOffsets = new float[segCount];
            for (int seg = 0; seg < segCount; seg++)
            {
                segYOffsets[seg] = totalHeight;
                totalHeight += sectionHeight;
                if (!ParticlePreviewGrouper.CollapsedSubfolders.Contains(groupedCache[seg].FolderPath))
                {
                    int segRows = Mathf.CeilToInt((float)groupedCache[seg].Count / columns);
                    totalHeight += segRows * actualCellSize;
                }
            }

            // ── 第二步：使用虚拟滚动（仅一次 GUILayout 占位调用）──
            // ── Step 2: Virtual scrolling (single GUILayout reservation call) ──
            int pageKey = Mathf.Max(0, _CurrentPageID);
            _PageScrollPos.TryGetValue(pageKey, out var scrollPos);
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
            _PageScrollPos[pageKey] = scrollPos;
            {
                GUILayoutUtility.GetRect(availableWidth, totalHeight);

                float viewTop = scrollPos.y;
                float viewBottom = scrollPos.y + scrollViewHeight;

                _VisibleSet.Clear();
                int trueVisibleCount = 0;
                bool needRepaint = false;

                // ── 第三步：只绘制可见的 Segment 和格子 ──
                // ── Step 3: Only draw visible segments and cells ──
                for (int seg = 0; seg < segCount; seg++)
                {
                    var group = groupedCache[seg];
                    float segY = segYOffsets[seg];
                    bool isCollapsed = ParticlePreviewGrouper.CollapsedSubfolders.Contains(group.FolderPath);

                    // 计算该 Segment 的总高度
                    // Calculate total height for this segment
                    float segHeight = sectionHeight;
                    if (!isCollapsed)
                    {
                        int segRowsCalc = Mathf.CeilToInt((float)group.Count / columns);
                        segHeight += segRowsCalc * actualCellSize;
                    }

                    // 如果整个 Segment 不在可视范围内（含缓冲），跳过
                    // Skip if entire segment is outside visible range (with buffer)
                    if (segY + segHeight < viewTop - actualCellSize * 2 || segY > viewBottom + actualCellSize * 2)
                        continue;

                    // 绘制分组标题栏（使用绝对坐标）
                    // Draw section header (using absolute coordinates)
                    Rect headerRect = new Rect(HEADER_PADDING, segY, availableWidth - HEADER_PADDING * 2, HEADER_HEIGHT);
                    if (ParticlePreviewGrouper.DrawSubfolderSectionHeaderAbsolute(headerRect, group.DisplayName, group.FolderPath))
                    {
                        needRepaint = true;
                    }

                    if (isCollapsed) continue;

                    // 绘制该 Segment 内的格子（虚拟滚动，只绘制可见行）
                    // Draw cells within this segment (virtual scrolling, only visible rows)
                    float contentStartY = segY + sectionHeight;
                    int segStart = group.StartIndex;
                    int segItemCount = group.Count;
                    int segRows = Mathf.CeilToInt((float)segItemCount / columns);

                    // 计算该 Segment 内可见行范围
                    // Calculate visible row range within this segment
                    int firstRow = Mathf.Max(0, Mathf.FloorToInt((viewTop - contentStartY - actualCellSize) / actualCellSize));
                    int lastRow = Mathf.Min(segRows - 1, Mathf.CeilToInt((viewBottom - contentStartY + actualCellSize) / actualCellSize));

                    for (int r = firstRow; r <= lastRow; r++)
                    {
                        float rowY = contentStartY + r * actualCellSize;
                        bool isTrueVisible = (rowY + actualCellSize >= viewTop) && (rowY <= viewBottom);

                        for (int col = 0; col < columns; col++)
                        {
                            int localIdx = r * columns + col;
                            if (localIdx >= segItemCount) break;
                            int globalIdx = segStart + localIdx;
                            if (globalIdx >= totalCount) break;

                            GameObject prefab = _FolderPrefabCache[globalIdx];
                            if (prefab == null) continue;

                            _VisibleSet.Add(prefab);
                            if (isTrueVisible) trueVisibleCount++;

                            Rect cellRect = new Rect(
                                col * actualCellSize + CELL_PADDING / 2,
                                rowY + CELL_PADDING / 2,
                                actualCellSize - CELL_PADDING,
                                actualCellSize - CELL_PADDING
                            );

                            DrawSingleCell(cellRect, prefab, dt, rawDt, _LoopInterval, globalIdx, isTrueVisible);
                        }
                    }
                }

                // 清理不可见的 Context
                // Cleanup invisible Contexts
                CleanupInvisibleContexts();

                // 缓存状态栏数据
                // Cache status bar data
                _CachedTotalCount = totalCount;
                _CachedVisibleCount = trueVisibleCount;
                _CachedContextCount = _Contexts.Count;
                _CachedTotalRows = 0;

                if (needRepaint)
                {
                    // 折叠状态变化时需要重新计算偏移
                    // Recalculate offsets when collapse state changes
                    Repaint();
                }
            }
            GUILayout.EndScrollView();
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 清理不可见的 Context（释放 PreviewRenderUtility 资源）
        /// Cleanup invisible Contexts (release PreviewRenderUtility resources)
        /// </summary>
        private void CleanupInvisibleContexts()
        {
            _RemoveBuffer.Clear();
            foreach (var kvp in _Contexts)
            {
                if (!_VisibleSet.Contains(kvp.Key))
                {
                    kvp.Value.Cleanup();
                    _RemoveBuffer.Add(kvp.Key);
                }
            }
            foreach (var key in _RemoveBuffer)
            {
                _Contexts.Remove(key);
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制单个预览格子
        /// Draw single preview cell
        /// </summary>
        /// <param name="rect">格子的绘制区域 / Cell drawing area</param>
        /// <param name="prefab">要预览的 Prefab / Prefab to preview</param>
        /// <param name="deltaTime">应用速率后的帧间隔（用于粒子和动画模拟） / Speed-applied delta time (for particle and animation simulation)</param>
        /// <param name="rawDeltaTime">原始帧间隔（用于循环计时） / Raw delta time (for loop timing)</param>
        /// <param name="loopInterval">循环间隔倍率 / Loop interval multiplier</param>
        /// <param name="index">格子索引 / Cell index</param>
        /// <param name="shouldSimulate">是否需要模拟动画（不可见的格子不需要模拟） / Whether to simulate animation (invisible cells don't need simulation)</param>
        private void DrawSingleCell(Rect rect, GameObject prefab, float deltaTime, float rawDeltaTime, float loopInterval, int index, bool shouldSimulate)
        {
            // 懒加载 Context（首次显示时才创建）
            // Lazy-load Context (only created on first display)
            if (!_Contexts.TryGetValue(prefab, out ParticlePreviewContext context))
            {
                context = new ParticlePreviewContext(prefab);
                _Contexts[prefab] = context;

                // 应用当前的移动速度设置（SetMoveSpeed 内部会自动处理移动模式的开关）
                // Apply current move speed setting
                context.SetMoveSpeed(_MoveEnabled ? ParticlePreviewSetting.Instance.TailMoveSpeed : 0f);
            }

            // 处理鼠标事件
            // Handle mouse events
            HandleCellEvents(rect, context, index);

            // 只在 Repaint 事件时绘制（避免重复绘制）
            // Only draw on Repaint event (avoid duplicate drawing)
            if (Event.current.type == EventType.Repaint)
            {
                // 只有真正可见的格子才进行粒子/动画模拟
                // Only truly visible cells perform particle/animation simulation
                // 如果正在拖拽旋转或平移相机，暂停该格子的模拟
                // Pause simulation for cells being drag-rotated or pan-dragged
                bool isPaused = (index == _DragTargetIdx) && (_IsDragging || _IsPanning);
                if (shouldSimulate && !isPaused)
                {
                    context.StepSimulation(deltaTime, rawDeltaTime, loopInterval);
                }

                // 渲染预览画面
                // Render preview image
                Texture tex = context.Render((int)rect.width, (int)rect.height);

                // 绘制背景
                // Draw background
                GUI.Box(rect, GUIContent.none, _BoxStyle);

                // 绘制特效画面
                // Draw effect image
                if (tex != null)
                {
                    DrawTextureOpaque(rect, tex);
                }

                // 绘制选中边框
                // Draw selection border
                if (index == _SelectedIdx)
                {
                    ParticlePreviewMasker.DrawInnerBorder(rect, new Color(0.2f, 0.6f, 1f, 1f), 3f);
                }

                // 绘制名称标签（半透明黑条 + 白色文字）
                // Draw name label (semi-transparent black bar + white text)
                if (_ShowName)
                {
                    if (_NameLabelStyle == null)
                    {
                        _NameLabelStyle = new GUIStyle(EditorStyles.label)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontSize = 11,
                            normal = { textColor = Color.white },
                        };
                    }
                    float labelHeight = EditorGUIUtility.singleLineHeight;
                    Rect labelRect = new Rect(rect.x, rect.y + rect.height - labelHeight, rect.width, labelHeight);
                    EditorGUI.DrawRect(labelRect, new Color(0, 0, 0, 0.25f));
                    GUI.Label(labelRect, prefab.name, _NameLabelStyle);
                }

                // 绘制坐标轴 Gizmo（仅在右键拖拽旋转该格子时显示）
                // Draw axis gizmo (only shown when right-drag rotating this cell)
                if (_IsDragging && index == _DragTargetIdx)
                {
                    if (ParticlePreviewSetting.Instance.ShowRotateAxis)
                        ParticlePreviewAxis.DrawAxisGizmo(rect, context);
                }

                // 绘制调试信息
                // Draw debug info
                if (_ShowDebug)
                {
                    // 右上角可见性图标
                    // Top-right visibility icon
                    Rect iconRect = new Rect(rect.x + rect.width - 24, rect.y + 4, 20, 20);
                    Color oldColor = GUI.color;
                    GUI.color = tex != null ? Color.green : Color.red;
                    GUI.Label(iconRect, tex != null ? "●" : "○", EditorStyles.boldLabel);
                    GUI.color = oldColor;

                    // 左上角显示 Y 坐标
                    // Top-left show Y coordinate
                    Rect debugRect = new Rect(rect.x + 4, rect.y + 4, 80, 18);
                    GUI.Label(debugRect, $"Y:{rect.y:F0}", EditorStyles.miniLabel);
                }

                // 绘制圆角遮罩（在所有内容之上，用窗口背景色遮盖四角，形成圆角矩形视觉效果）
                // Draw rounded corner mask (on top of all content, cover corners with window bg color for rounded-rect look)
                if (ParticlePreviewSetting.Instance.RoundedCorners)
                {
                    float cornerRadius = Mathf.Min(rect.width, rect.height) * ParticlePreviewSetting.Instance.CellCornerRatio;
                    ParticlePreviewMasker.DrawRoundedCornerMask(rect, cornerRadius);
                }
            }
        }

        /************************************************************************************************************************/
        #endregion
        /************************************************************************************************************************/

        /************************************************************************************************************************/
        #region 辅助方法 / Helper Methods
        /************************************************************************************************************************/

        /// <summary>
        /// 刷新所有特效
        /// Refresh all effects
        /// 如果滚动位置变化了，会强制重建所有 Context（解决快速滚动导致的显示异常）
        /// If scroll position changed, forces rebuild of all Contexts (fixes display issues from fast scrolling)
        /// 否则只重新播放当前可见的特效
        /// Otherwise only replays currently visible effects
        /// </summary>
        private void RefreshAllEffects()
        {
            int pageKey = Mathf.Max(0, _CurrentPageID);
            _PageScrollPos.TryGetValue(pageKey, out var scrollPos);

            if (Mathf.Abs(scrollPos.y - _LastScrollY) > 1f)
            {
                // 滚动位置变化了，强制重建所有 Context
                // Scroll position changed, force rebuild all Contexts
                CleanupAll();
                _VisibleSet.Clear();
            }
            else
            {
                // 滚动位置没变，只重新播放当前可见的特效
                // Scroll position unchanged, only restart visible effects
                foreach (var ctx in _Contexts.Values)
                {
                    ctx.Restart();
                }
            }

            _LastScrollY = scrollPos.y;
            Repaint();
        }

        /// <summary>
        /// 清理所有预览上下文，释放资源
        /// Cleanup all preview contexts, release resources
        /// </summary>
        private void CleanupAll()
        {
            foreach (var kvp in _Contexts)
            {
                kvp.Value.Cleanup();
            }
            _Contexts.Clear();

            // 清空辅助集合，避免持有已销毁 Prefab 的引用
            // Clear helper collections to avoid holding references to destroyed Prefabs
            _VisibleSet.Clear();
            _RemoveBuffer.Clear();
        }

        /************************************************************************************************************************/
        #endregion
        /************************************************************************************************************************/
    }
}
#endif