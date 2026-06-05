#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MBS.ParticlePreview
{
    public partial class ParticlePreviewWindow : EditorWindow
    {
        /************************************************************************************************************************/
        #region 事件处理 / Event Handling
        /************************************************************************************************************************/

        /// <summary>
        /// 处理全局事件
        /// Handle global events
        /// </summary>
        private void HandleGlobalEvents()
        {
            Event e = Event.current;

            // 记录左键状态（基于事件，避免 Input.GetMouseButton 在 EditorWindow 中不稳定）
            // Track left button state by events to avoid unstable Input.GetMouseButton in EditorWindow
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                _IsLeftMouseHeld = true;
            }
            else if (e.type == EventType.MouseDrag && e.button == 0)
            {
                _IsLeftMouseHeld = true;
            }
            else if ((e.type == EventType.MouseUp || e.rawType == EventType.MouseUp) && e.button == 0)
            {
                _IsLeftMouseHeld = false;
            }
            else if (e.type == EventType.DragExited || e.type == EventType.DragPerform)
            {
                _IsLeftMouseHeld = false;
            }
            else if (e.type == EventType.MouseLeaveWindow)
            {
                _IsLeftMouseHeld = false;
            }

            // 记录右键状态（基于事件，确保在 EditorWindow 中状态稳定）
            // Track right button state by events to ensure stability in EditorWindow
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                _IsRightMouseHeld = true;
            }
            else if (e.type == EventType.MouseDrag && e.button == 1)
            {
                _IsRightMouseHeld = true;
            }
            else if ((e.type == EventType.MouseUp || e.rawType == EventType.MouseUp) && e.button == 1)
            {
                _IsRightMouseHeld = false;
            }
            else if (e.type == EventType.MouseLeaveWindow)
            {
                _IsRightMouseHeld = false;
            }

            // 全局兜底：中键释放或鼠标离开窗口时，强制重置拖拽/平移状态
            // Global fallback: force reset drag/pan state on middle-button up or mouse leave window
            // 防止鼠标移出格子到工具栏等区域时 MouseUp 被其他控件消耗导致状态卡住
            // Prevents state stuck when MouseUp is consumed by other controls (toolbar, etc.)
            // 注意：仅处理 rawType 为 MouseUp 但 type 不是 MouseUp 的情况（即事件被其他控件消耗后的残留）
            // Note: only handle cases where rawType is MouseUp but type is not (event consumed by other controls)
            if (e.rawType == EventType.MouseUp && e.type != EventType.MouseUp)
            {
                if (e.button == 2 && _IsPanning)
                {
                    _IsPanning = false;
                    _HasPanned = false;
                    _DragTargetIdx = -1;
                }
                if (e.button == 1 && _IsDragging)
                {
                    _IsDragging = false;
                    _HasDragged = false;
                    _DragTargetIdx = -1;
                }
            }
            if (e.type == EventType.MouseLeaveWindow)
            {
                if (_IsPanning)
                {
                    _IsPanning = false;
                    _HasPanned = false;
                    _DragTargetIdx = -1;
                }
                if (_IsDragging)
                {
                    _IsDragging = false;
                    _HasDragged = false;
                    _DragTargetIdx = -1;
                }
            }

            // 左键按下：取消Unity当前选中
            // Left-click down: deselect Unity selection
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Selection.activeObject = null;
                _SelectedIdx = -1;
            }

            // 左键释放空白处：刷新所有特效（不取消选中）
            // Left-click release on empty area: refresh all effects (keep selection)
            if (e.type == EventType.MouseUp && e.button == 0 && !_IsDragging)
            {
                RefreshAllEffects();
                Repaint();
            }

            // Ctrl + 滚轮 或 右键 + 滚轮：快速调整全局镜头距离
            // Ctrl + Scroll or RightMouse + Scroll: adjust global camera distance
            if (e.type == EventType.ScrollWheel && (e.control || _IsRightMouseHeld))
            {
                _CamDistMul = Mathf.Clamp(_CamDistMul - e.delta.y * 0.05f, 0.1f, 3f);
                foreach (var ctx in _Contexts.Values)
                {
                    ctx.SetDistanceMultiplier(_CamDistMul);
                }
                Repaint();
                e.Use();
            }

            // 按住 Ctrl 或鼠标左键时临时暂停特效移动，放开继续
            // Hold Ctrl or left mouse button to temporarily pause effect movement, release to resume
            bool ctrlPressed = e.control;
            bool leftMousePressed = _IsLeftMouseHeld;
            bool shouldPauseMove = ctrlPressed || leftMousePressed;
            if (shouldPauseMove != _MovePausedByCtrl)
            {
                _MovePausedByCtrl = shouldPauseMove;
                foreach (var ctx in _Contexts.Values)
                {
                    ctx.SetMovePaused(_MovePausedByCtrl);
                }
            }
        }

        /// <summary>
        /// 处理单个格子的鼠标事件
        /// Handle mouse events for a single cell
        /// </summary>
        private void HandleCellEvents(Rect rect, ParticlePreviewContext context, int index)
        {
            Event e = Event.current;

            // 处理右键拖拽旋转中的情况（即使鼠标移出了格子）
            // Handle right-button drag rotation (even when mouse leaves cell)
            if (_IsDragging && index == _DragTargetIdx)
            {
                if (e.type == EventType.MouseDrag && e.button == 1)
                {
                    Vector2 delta = e.mousePosition - _DragStart;
                    context.AdjustRotation(delta.x * 0.5f, delta.y * 0.5f);
                    _DragStart = e.mousePosition;
                    _HasDragged = true;
                    Repaint();
                    e.Use();
                }
                else if (e.type == EventType.MouseUp && e.button == 1)
                {
                    if (!_HasDragged)
                    {
                        if (index >= 0 && index < _FolderPrefabCache.Count && _FolderPrefabCache[index] != null)
                        {
                            Selection.activeObject = _FolderPrefabCache[index];
                            EditorGUIUtility.PingObject(_FolderPrefabCache[index]);
                        }
                    }
                    _IsDragging = false;
                    _HasDragged = false;
                    _DragTargetIdx = -1;
                    e.Use();
                }
                return;
            }

            // 处理中键拖拽平移中的情况（即使鼠标移出了格子）
            // Handle middle-button drag pan (even when mouse leaves cell)
            if (_IsPanning && index == _DragTargetIdx)
            {
                if (e.type == EventType.MouseDrag && e.button == 2)
                {
                    Vector2 delta = e.mousePosition - _DragStart;
                    context.AdjustPan(-delta.x * 0.003f, delta.y * 0.003f);
                    _DragStart = e.mousePosition;
                    _HasPanned = true;
                    Repaint();
                    e.Use();
                }
                else if ((e.type == EventType.MouseUp || e.rawType == EventType.MouseUp) && e.button == 2)
                {
                    _IsPanning = false;
                    _HasPanned = false;
                    _DragTargetIdx = -1;
                    if (e.type != EventType.Used) e.Use();
                }
                return;
            }

            if (!rect.Contains(e.mousePosition)) return;

            // 左键拖拽：将 Prefab 拖入场景
            // Left-click drag: drag prefab into scene
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                GameObject prefab = _FolderPrefabCache[index];
                if (prefab != null)
                {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = new Object[] { prefab };
                    DragAndDrop.StartDrag(prefab.name);
                    e.Use();
                }
            }
            // 右键按下：选中格子 + 开始拖拽旋转
            // Right-click down: select cell + start drag rotation
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                _SelectedIdx = index;
                _DragTargetIdx = index;
                _IsDragging = true;
                _HasDragged = false;
                _DragStart = e.mousePosition;
                Repaint();
                e.Use();
            }
            // 中键按下：开始平移（不需要选中）
            // Middle-click down: start pan (no selection required)
            else if (e.type == EventType.MouseDown && e.button == 2)
            {
                _DragTargetIdx = index;
                _IsPanning = true;
                _HasPanned = false;
                _DragStart = e.mousePosition;
                Repaint();
                e.Use();
            }
        }

        /// <summary>
        /// 处理文件夹拖放到窗口，自动添加到 Folder 配置
        /// Handle folder drag-drop onto window, auto-add to Folder config
        /// </summary>
        private void HandleFolderDragDrop()
        {
            Event current = Event.current;
            if (current == null) return;
            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform) return;

            bool hasFolder = false;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (AssetDatabase.IsValidFolder(path))
                {
                    hasFolder = true;
                    break;
                }
            }

            if (!hasFolder) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                var folder = ParticlePreviewFolder.Instance;
                bool changed = false;

                foreach (var obj in DragAndDrop.objectReferences)
                {
                    string path = AssetDatabase.GetAssetPath(obj);
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        if (!folder.SimpleFolderPages.Contains(obj))
                        {
                            folder.SimpleFolderPages.Add(obj);
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(folder);
                    // 强制刷新按钮和内容
                    // Force refresh buttons and contents
                    _LastPageID = -2;
                }
            }

            current.Use();
        }

        /************************************************************************************************************************/
        #endregion
        /************************************************************************************************************************/
    }
}
#endif