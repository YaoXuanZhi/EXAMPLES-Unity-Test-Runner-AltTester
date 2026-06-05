#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
// using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;

namespace MBS.ParticlePreview
{
    /// <summary>
    /// VFX 预览上下文 - 负责单个特效的渲染和模拟
    /// VFX preview context - handles rendering and simulation of a single effect
    /// 使用 PreviewRenderUtility 在独立的渲染环境中预览特效
    /// Uses PreviewRenderUtility to preview effects in an isolated render environment
    /// 支持粒子系统和 Animator 动画的同步播放
    /// Supports synchronized playback of particle systems and Animator animations
    /// </summary>
    public class ParticlePreviewContext
    {
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 渲染相关 / Rendering
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private PreviewRenderUtility _Preview; // 预览渲染工具 / Preview render utility
        private GameObject _BgQuad;   // 背景平面 / Background quad
        private GameObject _Instance; // 特效实例 / Effect instance
        private Material _BgMat;      // 背景材质 / Background material

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 相机参数 / Camera Parameters
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private float _CamDist = 5.0f;   // 相机距离 / Camera distance
        private float _CamDistMul = 1.0f;// 距离系数 / Distance multiplier
        private float _CamPitch = 30f;   // 俯仰角 / Pitch angle
        private float _CamYaw = -135f;   // 旋转角 / Yaw angle
        private Color _BgColor = new Color(0.2f, 0.2f, 0.2f, 1f); // 背景颜色 / Background color
        private Vector3 _Center = Vector3.zero;    // 看向中心点 / Look-at center
        private Vector3 _PanOffset = Vector3.zero; // 鼠标中键拖拽的平移偏移 / Middle-mouse-drag pan offset

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 播放控制 / Playback Control
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private float _CachedDuration = -1f; // 缓存时长 / Cached duration
        private float _Time = 0f;            // 当前播放时间 / Current time
        private ParticleSystem _MainPS;      // 主粒子系统 / Main particle system
        private List<Animator> _Animators = new List<Animator>();             // 所有动画器 / All animators
        private List<ParticleSystem> _Particles = new List<ParticleSystem>(); // 所有粒子系统 / All particles
        private List<ParticleSystem> _RootParticles = new List<ParticleSystem>(); // 根粒子系统 / Root particles
        private List<TrailRenderer> _VfxTrails = new List<TrailRenderer>();   // 所有拖尾 / All trails
        private List<VisualEffect> _VfxGraphs = new List<VisualEffect>();     // 所有 VFX Graph / All VFX graphs

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 移动模式 / Move Mode
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private float _MoveSpeed = 5f;       // 目标移动速度 / Target move speed
        private float _MoveAccelElapsed = 0f; // 加速已经过时间 / Acceleration elapsed time
        private bool _MovePaused = false;     // 移动暂停 / Move paused
        private bool _MovingMode = false;     // 移动模式开关 / Move mode toggle
        private Vector3 _InitialCenter;       // 初始中心点 / Initial center
        private Vector3 _MoveDirection = Vector3.forward; // 移动方向 / Move direction

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 静态缓存 / Static Cache
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        static private ParticlePreviewWindow _CachedWindow; // 缓存窗口引用 / Cached window reference

        /// <summary>
        /// 高帧率模式开关（启用后每帧都会触发窗口重绘，适用于 VFX Graph 等需要频繁刷新的特效）
        /// High frame rate mode (triggers window repaint every frame for VFX Graph)
        /// </summary>
        static public bool HighFrameRateMode { get; set; } = true;

        /************************************************************************************************************************/

        /// <summary>
        /// 创建预览上下文
        /// Create preview context
        /// </summary>
        /// <param name="prefab">要预览的特效 Prefab / The effect prefab to preview</param>
        public ParticlePreviewContext(GameObject prefab)
        {
            try
            {
                // 初始化渲染工具
                // Initialize render utility
                _Preview = new PreviewRenderUtility();
                _Preview.camera.fieldOfView = 60;
                _Preview.camera.nearClipPlane = 0.1f;
                _Preview.camera.farClipPlane = 200f;
                _Preview.camera.clearFlags = CameraClearFlags.SolidColor;
                _Preview.camera.backgroundColor = _BgColor;
                _Preview.camera.allowHDR = true;    // 启用 HDR / Enable HDR
                _Preview.camera.allowMSAA = false;  // 禁用 MSAA（可能影响半透明） / Disable MSAA (may affect transparency)

                // 配置 URP 相机
                // var urpCameraData = _Preview.camera.gameObject.GetComponent<UniversalAdditionalCameraData>();
                // if (urpCameraData == null)
                // {
                //     urpCameraData = _Preview.camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                // }
                // urpCameraData.requiresColorTexture = true;   // 需要颜色纹理（用于扰曲效果）
                // urpCameraData.requiresDepthTexture = true;   // 需要深度纹理
                // urpCameraData.renderShadows = false;         // 禁用阴影（提高性能）
                // urpCameraData.antialiasing = AntialiasingMode.None;  // 禁用后处理抗锯齿

                // 创建特效实例
                // Create effect instance
                _Instance = (GameObject)Object.Instantiate(prefab);
                _Instance.transform.position = Vector3.zero;
                _Instance.transform.rotation = Quaternion.identity;
                _Instance.hideFlags = HideFlags.HideAndDontSave;  // 隐藏并且不保存 / Hide and don't save
                _Preview.AddSingleGO(_Instance);

                // 创建背景平面（用于扰曲效果采样 Opaque Texture）
                // Create background quad (for distortion effect to sample Opaque Texture)
                CreateBackgroundQuad();

                // 获取组件
                // Get components
                _MainPS = _Instance.GetComponent<ParticleSystem>();
                _Instance.GetComponentsInChildren(true, _Particles);
                _Instance.GetComponentsInChildren(true, _Animators);
                _Instance.GetComponentsInChildren(true, _VfxGraphs);
                _Instance.GetComponentsInChildren(true, _VfxTrails);

                // 收集根粒子系统（父对象不是粒子系统的粒子系统）
                // Collect root particle systems (particle systems whose parent is not a particle system)
                foreach (var ps in _Particles)
                {
                    if (ps == null) continue;
                    var parent = ps.transform.parent;
                    // 如果父对象为空，或者父对象没有粒子系统，则认为是根粒子系统
                    // If parent is null or has no particle system, treat as root particle system
                    if (parent == null || parent.GetComponent<ParticleSystem>() == null)
                    {
                        _RootParticles.Add(ps);
                    }
                }

                // 初始化
                // Initialize
                CalculateBounds();       // 计算包围盒，设置相机位置 / Calculate bounds, set camera position
                InitializeAnimators();   // 初始化 Animator / Initialize Animators
                CacheDuration();         // 缓存特效时长 / Cache effect duration
                PlayEffect();            // 开始播放特效 / Start playing effect
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ParticlePreviewContext initialization failed: {prefab?.name}\n{e}");
                Cleanup();
            }
        }

        /************************************************************************************************************************/

        /// <summary>
        /// 创建背景平面
        /// Create background quad
        /// 这个平面放在特效后面，用于扰曲效果采样 Opaque Texture
        /// This quad is placed behind the effect for distortion sampling of Opaque Texture
        /// 如果没有这个平面，扰曲效果会采样到透明背景，导致显示异常
        /// Without this quad, distortion would sample transparent background causing display issues
        /// </summary>
        private void CreateBackgroundQuad()
        {
            // 创建背景平面
            // Create background quad
            _BgQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _BgQuad.name = "_PreviewBackground";
            _BgQuad.hideFlags = HideFlags.HideAndDontSave;

            // 移除碰撞体（不需要物理交互）
            // Remove collider (no physics interaction needed)
            var collider = _BgQuad.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            // 创建 Unlit 材质（不受光照影响）
            // Create Unlit material (not affected by lighting)
            // 根据当前渲染管线选择正确的 Shader（避免 Built-in 管线下加载 URP Shader 导致粉红色）
            // Select correct shader based on current render pipeline (avoid pink on Built-in with URP shader)
            Shader unlitShader = null;
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
            {
                unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (unlitShader == null)
            {
                unlitShader = Shader.Find("Unlit/Color");
            }

            _BgMat = new Material(unlitShader);
            _BgMat.color = _BgColor;
            _BgMat.hideFlags = HideFlags.HideAndDontSave;
            _BgMat.renderQueue = 1000;  // 确保在不透明队列（Geometry 之前） / Ensure in opaque queue (before Geometry)

            var renderer = _BgQuad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _BgMat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _Preview.AddSingleGO(_BgQuad);
        }

        /// <summary>
        /// 更新背景平面的位置和方向
        /// Update background quad position and orientation
        /// 让背景平面始终面向相机，并放在特效后面
        /// Keep the quad always facing the camera and behind the effect
        /// </summary>
        private void UpdateBackgroundQuad(Quaternion cameraRotation)
        {
            if (_BgQuad == null) return;

            // 计算背景平面的位置（在特效后面）
            // Calculate background quad position (behind the effect)
            Vector3 backDir = cameraRotation * Vector3.forward;
            _BgQuad.transform.position = _Center + backDir * (_CamDist * 1.5f);
            _BgQuad.transform.rotation = cameraRotation;
            _BgQuad.transform.localScale = Vector3.one * (_CamDist * 3f);  // 足够大以覆盖整个视野 / Large enough to cover the entire view

            // 更新背景色
            // Update background color
            if (_BgMat != null)
            {
                _BgMat.color = _BgColor;
            }
        }

        /// <summary>
        /// 计算特效的包围盒，用于设置相机位置
        /// Calculate effect bounding box for camera positioning
        /// 忽略 ParticleSystemRenderer，因为粒子的包围盒会动态变化
        /// Ignores ParticleSystemRenderer because particle bounds change dynamically
        /// </summary>
        private void CalculateBounds()
        {
            if (_Instance == null) return;

            var renderers = _Instance.GetComponentsInChildren<Renderer>();
            bool firstBound = false;
            Bounds bounds = new Bounds(_Instance.transform.position, Vector3.one);

            foreach (var r in renderers)
            {
                // 跳过粒子系统渲染器
                // Skip particle system renderers
                if (r is ParticleSystemRenderer) continue;

                if (!firstBound)
                {
                    bounds = r.bounds;
                    firstBound = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            _Center = bounds.center;                                    // 相机看向的中心点 / Camera look-at center
            _InitialCenter = _Center;                                   // 保存初始中心点（用于重置） / Save initial center (for reset)
            _CamDist = Mathf.Max(2f, bounds.extents.magnitude * 8f);    // 相机距离（根据包围盒大小自动调整） / Camera distance (auto-adjusted by bounds size)
        }

        /// <summary>
        /// 初始化所有 Animator
        /// Initialize all Animators
        /// 设置为始终更新模式，确保在编辑器中可以正常播放
        /// Set to always-update mode to ensure proper playback in editor
        /// </summary>
        private void InitializeAnimators()
        {
            foreach (var animator in _Animators)
            {
                if (animator == null) continue;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // 始终更新，即使不可见 / Always update, even if not visible
                animator.updateMode = AnimatorUpdateMode.Normal;            // 正常更新模式 / Normal update mode
            }
        }

        /// <summary>
        /// 播放特效（粒子 + 动画）
        /// Play effect (particles + animations)
        /// </summary>
        private void PlayEffect()
        {
            _Time = 0f;             // 重置播放时间 / Reset playback time
            _MoveAccelElapsed = 0f; // 重置加速计时（移动速度从 0 开始渐变） / Reset acceleration timer (speed ramps from 0)

            // 移动模式下：先清除所有粒子和 Trail，再重置位置
            // In move mode: clear all particles and trails first, then reset position
            // 这样可以避免残留粒子/Trail 被“拉”到新位置产生异常拖尾
            // This avoids residual particles/trails being "pulled" to new position causing abnormal trails
            if (_MovingMode && _Instance != null)
            {
                // 先停止并清除所有粒子
                // Stop and clear all particles first
                foreach (var ps in _Particles)
                {
                    if (ps == null) continue;
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Clear(false);  // 强制清除所有粒子（同时清除粒子系统自带的 Trail） / Force clear all particles (also clears built-in trails)
                }

                // 清除所有 TrailRenderer 组件的拖尾
                // Clear all TrailRenderer component trails
                foreach (var trail in _VfxTrails)
                {
                    if (trail == null) continue;
                    trail.emitting = false;  // 停止发射拖尾 / Stop emitting trail
                }

                // 清除 VFX Graph 粒子
                // Clear VFX Graph particles
                foreach (var vfx in _VfxGraphs)
                {
                    if (vfx == null || vfx.visualEffectAsset == null) continue;
                    vfx.Reinit();  // 重新初始化会清除所有粒子 / Reinitialize clears all particles
                }

                // 然后重置位置（保留鼠标中键拖拽的平移偏移）
                // Then reset position (preserve middle-mouse-drag pan offset)
                _Instance.transform.position = Vector3.zero;
                _Center = _InitialCenter + _PanOffset;
            }

            // 播放粒子系统
            // Play particle system
            if (_MainPS != null)
            {
                _MainPS.Simulate(0f, true, true);   // 重置并模拟 0 秒 / Reset and simulate 0 seconds
                _MainPS.Play(true);                  // 开始播放 / Start playing
            }
            else
            {
                // 如果根节点没有粒子系统，则遍历所有子节点
                // If root has no particle system, iterate all child nodes
                foreach (var ps in _Particles)
                {
                    if (ps == null) continue;
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Play(false);
                }
            }

            // 恢复发射拖尾
            // Resume trail emission
            if (_MovingMode && _Instance != null)
            {
                foreach (var trail in _VfxTrails)
                {
                    if (trail == null) continue;
                    trail.Clear();
                    trail.emitting = true;
                }
            }

            // 播放动画
            // Play animations
            PlayAnimators();

            // 播放 VFX Graph
            // Play VFX Graph
            PlayVFXGraphs();
        }

        /// <summary>
        /// 播放所有 Animator 动画
        /// Play all Animator animations
        /// 重置并播放第一个动画片段
        /// Reset and play the first animation clip
        /// </summary>
        private void PlayAnimators()
        {
            foreach (var animator in _Animators)
            {
                if (animator == null || animator.runtimeAnimatorController == null) continue;

                // 重置 Animator 状态
                // Reset Animator state
                animator.Rebind();
                animator.Update(0f);

                // 尝试播放默认状态或第一个可用的动画
                // Try to play default state or first available animation
                var clips = animator.runtimeAnimatorController.animationClips;
                if (clips != null && clips.Length > 0)
                {
                    animator.Play(clips[0].name, 0, 0f);
                }
            }
        }

        /// <summary>
        /// 播放所有 VFX Graph 特效
        /// Play all VFX Graph effects
        /// 重置并重新播放
        /// Reset and replay
        /// </summary>
        private void PlayVFXGraphs()
        {
            foreach (var vfx in _VfxGraphs)
            {
                if (vfx == null || vfx.visualEffectAsset == null) continue;

                // 重置并播放 VFX Graph
                // Reset and play VFX Graph
                vfx.Reinit();       // 重新初始化（清除所有粒子） / Reinitialize (clear all particles)
                vfx.Play();         // 开始播放 / Start playing
                vfx.pause = false;  // 不暂停 / Don't pause
            }
        }

        /// <summary>
        /// 缓存特效时长（在初始化时调用一次，避免每帧重复计算）
        /// Cache effect duration (called once during init to avoid per-frame recalculation)
        /// </summary>
        private void CacheDuration()
        {
            if (_Instance == null)
            {
                _CachedDuration = 3f;
                return;
            }

            // 使用 ParticlePreviewTools 计算特效时长
            // Use ParticlePreviewTools to calculate effect duration
            _CachedDuration = ParticlePreviewTools.GetEffectDuration(_Instance, true, true, true);

            // 如果无法获取有效时长，使用默认值
            // Use default value if valid duration cannot be obtained
            if (_CachedDuration <= 0f)
            {
                _CachedDuration = 3f;
            }

            // 额外增加 2 秒缓冲时间
            // Add 2 seconds of buffer time
            _CachedDuration += 2f;
        }

        /// <summary>
        /// 获取特效的循环时长（使用缓存值）
        /// Get effect loop duration (using cached value)
        /// </summary>
        private float GetLoopDuration()
        {
            return _CachedDuration + ParticlePreviewSetting.Instance.DefaulInterval;
        }

        /// <summary>
        /// 刷新 VFX 预览窗口（使用缓存的窗口引用，避免每帧 GetWindow）
        /// Repaint VFX preview window (uses cached window reference to avoid per-frame GetWindow)
        /// </summary>
        private static void RepaintPreviewWindow()
        {
            // 缓存无效时才重新获取
            // Re-fetch only when cache is invalid
            if (_CachedWindow == null)
            {
                var windows = Resources.FindObjectsOfTypeAll<ParticlePreviewWindow>();
                if (windows.Length > 0)
                {
                    _CachedWindow = windows[0];
                }
            }

            // Repaint 只是标记窗口脏，Unity 会自动合并多次调用
            // Repaint just marks window dirty, Unity automatically merges multiple calls
            if (_CachedWindow != null)
            {
                _CachedWindow.Repaint();
            }
        }

        /// <summary>
        /// 清除缓存的窗口引用（在窗口关闭时调用）
        /// Clear cached window reference (called when window closes)
        /// </summary>
        public static void ClearCachedWindow()
        {
            _CachedWindow = null;
        }

        /************************************************************************************************************************/
        #region 公开接口 / Public Interface
        /************************************************************************************************************************/

        /// <summary>
        /// 重新播放特效
        /// Restart effect playback
        /// </summary>
        public void Restart()
        {
            PlayEffect();
        }

        /// <summary>
        /// 调整相机缩放
        /// Adjust camera zoom
        /// </summary>
        /// <param name="delta">缩放量（正值拉远，负值拉近） / Zoom amount (positive = zoom out, negative = zoom in)</param>
        public void AdjustZoom(float delta)
        {
            _CamDist = Mathf.Max(0.5f, _CamDist + delta);
        }

        /// <summary>
        /// 相机俯仰角（只读）
        /// Camera pitch angle (read-only)
        /// </summary>
        public float CamPitch => _CamPitch;

        /// <summary>
        /// 相机旋转角（只读）
        /// Camera yaw angle (read-only)
        /// </summary>
        public float CamYaw => _CamYaw;

        /// <summary>
        /// 获取特效中心在渲染画面中的像素偏移（相对于格子中心）
        /// Get effect center's pixel offset in rendered image (relative to cell center)
        /// </summary>
        /// <param name="cellHeight">格子高度（像素） / Cell height in pixels</param>
        /// <returns>像素偏移量（X向右，Y向下） / Pixel offset (X=right, Y=down)</returns>
        public Vector2 GetEffectScreenOffset(float cellHeight)
        {
            // 特效中心相对于相机注视点的世界空间偏移 = -_PanOffset
            // Effect center's world-space offset from camera look-at = -_PanOffset
            Vector3 offset = -_PanOffset;
            Quaternion rot = Quaternion.Euler(_CamPitch, _CamYaw, 0);
            float xProj = Vector3.Dot(offset, rot * Vector3.right);
            float yProj = Vector3.Dot(offset, rot * Vector3.up);

            // 将世界空间投影转换为像素偏移（基于透视相机 FOV=60°）
            // Convert world-space projection to pixel offset (perspective camera FOV=60)
            float actualDist = _CamDist / _CamDistMul;
            float halfWorldH = actualDist * Mathf.Tan(30f * Mathf.Deg2Rad);
            float pixelPerWorld = cellHeight * 0.5f / halfWorldH;

            return new Vector2(xProj * pixelPerWorld, -yProj * pixelPerWorld);
        }

        /// <summary>
        /// 调整相机旋转
        /// Adjust camera rotation
        /// </summary>
        /// <param name="yawDelta">左右旋转增量 / Horizontal rotation delta</param>
        /// <param name="pitchDelta">仰俯旋转增量 / Vertical rotation delta</param>
        public void AdjustRotation(float yawDelta, float pitchDelta)
        {
            _CamYaw += yawDelta;
            _CamPitch = Mathf.Clamp(_CamPitch + pitchDelta, -89f, 89f);  // 限制仰俯角范围 / Clamp pitch angle range
        }

        /// <summary>
        /// 调整相机平移
        /// Adjust camera pan
        /// </summary>
        /// <param name="x">水平平移量 / Horizontal pan amount</param>
        /// <param name="y">垂直平移量 / Vertical pan amount</param>
        public void AdjustPan(float x, float y)
        {
            var cam = _Preview?.camera;
            if (cam == null) return;
            // 根据相机朝向计算平移方向
            // Calculate pan direction based on camera orientation
            Vector3 offset = cam.transform.right * x * _CamDist + cam.transform.up * y * _CamDist;
            _Center += offset;
            _PanOffset += offset; // 累积平移偏移，重播时保留 / Accumulate pan offset, preserved across replays
        }

        /// <summary>
        /// 重置相机角度到默认值
        /// Reset camera angle to defaults
        /// </summary>
        public void ResetCamera()
        {
            _CamPitch = 30f;
            _CamYaw = -135f;
        }

        /// <summary>
        /// 设置背景颜色
        /// Set background color
        /// </summary>
        public void SetBackgroundColor(Color color)
        {
            _BgColor = color;
            if (_Preview != null)
            {
                _Preview.camera.backgroundColor = color;
            }
        }

        /// <summary>
        /// 设置镜头距离系数
        /// Set camera distance multiplier
        /// </summary>
        public void SetDistanceMultiplier(float multiplier)
        {
            _CamDistMul = multiplier;
        }

        /// <summary>
        /// 设置移动模式（让特效向前移动以显示拖尾效果）
        /// Set move mode (move effect forward to display trail effect)
        /// </summary>
        /// <param name="enabled">是否启用移动模式 / Whether to enable move mode</param>
        public void SetMovingMode(bool enabled)
        {
            _MovingMode = enabled;
            if (!enabled && _Instance != null)
            {
                // 关闭移动模式时重置位置（保留鼠标中键拖拽的平移偏移）
                // Reset position when disabling move mode (preserve middle-mouse-drag pan offset)
                _Instance.transform.position = Vector3.zero;
                _Center = _InitialCenter + _PanOffset;
            }
        }

        /// <summary>
        /// 获取移动模式状态
        /// Get move mode state
        /// </summary>
        public bool IsMovingMode => _MovingMode;

        /// <summary>
        /// 设置移动速度（同时自动控制移动模式开关）
        /// Set move speed (also auto-controls move mode toggle)
        /// 速度为 0 时自动关闭移动模式，速度 > 0 时自动开启移动模式
        /// Speed 0 auto-disables move mode, speed > 0 auto-enables move mode
        /// </summary>
        /// <param name="speed">移动速度（单位/秒），0 = 停止移动 / Move speed (units/sec), 0 = stop</param>
        public void SetMoveSpeed(float speed)
        {
            float newSpeed = Mathf.Max(0f, speed);
            bool willMove = newSpeed > 0f;

            _MoveSpeed = newSpeed;

            // 直接根据速度设置移动模式，确保状态同步
            // Set move mode directly based on speed, ensuring state sync
            if (willMove && !_MovingMode)
            {
                SetMovingMode(true);
            }
            else if (!willMove && _MovingMode)
            {
                SetMovingMode(false);
            }
        }

        /// <summary>
        /// 获取移动速度
        /// Get move speed
        /// </summary>
        public float MoveSpeed => _MoveSpeed;

        /// <summary>
        /// 设置移动暂停状态（用于按住 Ctrl 时临时暂停移动）
        /// Set move pause state (for temporarily pausing when Ctrl is held)
        /// </summary>
        /// <param name="paused">是否暂停移动 / Whether to pause movement</param>
        public void SetMovePaused(bool paused)
        {
            _MovePaused = paused;
        }

        /// <summary>
        /// 获取移动暂停状态
        /// Get move pause state
        /// </summary>
        public bool IsMovePaused => _MovePaused;

        /// <summary>
        /// 设置移动方向
        /// Set move direction
        /// </summary>
        /// <param name="direction">移动方向（会被归一化） / Move direction (will be normalized)</param>
        public void SetMoveDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude > 0.001f)
            {
                _MoveDirection = direction.normalized;
            }
        }

        /// <summary>
        /// 每帧模拟更新
        /// Per-frame simulation update
        /// </summary>
        /// <param name="deltaTime">应用速率后的帧间隔（用于粒子和动画模拟） / Speed-applied delta time (for particle and animation simulation)</param>
        /// <param name="rawDeltaTime">原始帧间隔（用于循环计时，不受速率影响） / Raw delta time (for loop timing, unaffected by speed)</param>
        /// <param name="loopIntervalRate">循环间隔倍率（相对于特效时长） / Loop interval multiplier (relative to effect duration)</param>
        public void StepSimulation(float deltaTime, float rawDeltaTime, float loopIntervalRate)
        {
            if (_Instance == null) return;

            // 循环时间使用原始 deltaTime，不受速率影响
            // Loop time uses raw deltaTime, unaffected by speed rate
            _Time += rawDeltaTime;

            // 循环间隔 = 特效原始循环时间 × 间隔倍率
            // Loop interval = original loop duration × interval multiplier
            if (_Time >= GetLoopDuration() * loopIntervalRate)
            {
                PlayEffect();  // 达到循环时间，重新播放 / Reached loop time, replay
            }

            // 移动模式：让特效和相机一起向前移动（用于显示拖尾效果）
            // Move mode: move effect and camera together forward (for displaying trail effect)
            // 如果移动被暂停（按住 Ctrl），则跳过移动但保持模拟
            // If move is paused (Ctrl held), skip movement but keep simulation
            if (_MovingMode && !_MovePaused)
            {
                // 加速阶段：从 0 渐变到目标速度，暂停不重置加速进度
                // Acceleration phase: lerp from 0 to target speed, pause doesn't reset progress
                _MoveAccelElapsed += deltaTime;
                float accelDuration = ParticlePreviewSetting.Instance.TailAccelDuration;
                float speedRatio = accelDuration > 0f ? Mathf.Clamp01(_MoveAccelElapsed / accelDuration) : 1f;
                float currentSpeed = _MoveSpeed * speedRatio;

                Vector3 movement = _MoveDirection * currentSpeed * deltaTime;
                _Instance.transform.position += movement;
                _Center += movement;  // 相机跟随移动 / Camera follows movement
            }

            // 粒子模拟：只对根粒子系统调用 Simulate，让它递归处理子对象
            // Particle simulation: only call Simulate on root particles, recursively handling children
            foreach (var ps in _RootParticles)
            {
                if (ps == null) continue;
                ps.Simulate(deltaTime, true, false, true);  // 增量模拟，递归子对象 / Incremental simulation, recursing children
            }

            // 更新动画
            // Update animations
            foreach (var animator in _Animators)
            {
                if (animator == null || animator.runtimeAnimatorController == null) continue;
                animator.Update(deltaTime);
            }

            // // 更新 VFX Graph（VFX Graph 使用 Simulate 方法进行编辑器模式下的模拟）
            // foreach (var vfx in _VfxGraphs)
            // {
            //     if (vfx == null || vfx.visualEffectAsset == null) continue;
            //     // 在 pause 状态下调用 Simulate 进行手动模拟
            //     // stepCount 使用默认值让 VFX 自动计算合适的模拟步数
            //     vfx.Simulate(deltaTime, 1);
            // }

            // 高帧率模式下触发窗口重绘（适用于 VFX Graph 等需要频繁刷新的特效）
            // Trigger window repaint in high FPS mode (for VFX Graph effects needing frequent refresh)
            if (HighFrameRateMode)
            {
                RepaintPreviewWindow();
            }
        }

        /// <summary>
        /// 渲染预览画面
        /// Render preview image
        /// </summary>
        /// <param name="width">渲染宽度 / Render width</param>
        /// <param name="height">渲染高度 / Render height</param>
        /// <returns>渲染结果纹理 / Rendered texture</returns>
        public Texture Render(int width, int height)
        {
            if (_Preview == null || _Instance == null) return null;

            _Preview.camera.backgroundColor = _BgColor;
            _Preview.BeginPreview(new Rect(0, 0, width, height), GUIStyle.none);

            // 设置相机位置和方向（应用距离系数，系数越大越近）
            // Set camera position and rotation (apply distance multiplier, larger = closer)
            float actualDist = _CamDist / _CamDistMul;
            Quaternion cameraRotation = Quaternion.Euler(_CamPitch, _CamYaw, 0);
            Vector3 camPos = _Center - cameraRotation * Vector3.forward * actualDist;

            _Preview.camera.transform.position = camPos;
            _Preview.camera.transform.rotation = cameraRotation;
            _Preview.camera.nearClipPlane = 0.01f;
            _Preview.camera.farClipPlane = 100f;

            // 更新背景平面位置
            // Update background quad position
            UpdateBackgroundQuad(cameraRotation);

            // 设置光源
            // Set light source
            _Preview.lights[0].intensity = 1.2f;
            _Preview.lights[0].transform.rotation = Quaternion.Euler(60f, -120f, 0f);

            // 渲染
            // Render
            _Preview.camera.Render();
            return _Preview.EndPreview();
        }

        /// <summary>
        /// 清理所有资源
        /// Cleanup all resources
        /// 必须在不再使用时调用，否则会导致内存泄漏
        /// Must be called when no longer needed, otherwise causes memory leaks
        /// </summary>
        public void Cleanup()
        {
            _Particles.Clear();
            _RootParticles.Clear();
            _Animators.Clear();
            _VfxGraphs.Clear();
            _VfxTrails.Clear();
            _MainPS = null;

            // 销毁背景平面
            // Destroy background quad
            if (_BgQuad != null)
            {
                Object.DestroyImmediate(_BgQuad);
                _BgQuad = null;
            }

            // 销毁背景材质
            // Destroy background material
            if (_BgMat != null)
            {
                Object.DestroyImmediate(_BgMat);
                _BgMat = null;
            }

            // 销毁特效实例
            // Destroy effect instance
            if (_Instance != null)
            {
                Object.DestroyImmediate(_Instance);
                _Instance = null;
            }

            // 清理预览渲染工具
            // Cleanup preview render utility
            if (_Preview != null)
            {
                _Preview.Cleanup();
                _Preview = null;
            }
        }

        /// <summary>
        /// 析构函数
        /// Destructor
        /// 如果忘记调用 Cleanup，这里会尝试清理并警告
        /// If Cleanup was forgotten, attempts cleanup here with a warning
        /// </summary>
        ~ParticlePreviewContext()
        {
            if (_Preview != null || _Instance != null)
            {
                Debug.LogWarning("ParticlePreviewContext was not properly cleaned up, attempting to release resources...");
                // 注意：在析构函数中调用 Cleanup 可能不安全（涉及 Unity 对象销毁）
                // Note: calling Cleanup in destructor may be unsafe (involves Unity object destruction)
                // 但这是最后的保护措施，比完全不清理要好
                // But this is the last resort, better than not cleaning up at all
                try
                {
                    // 只清理非 Unity 对象的资源
                    // Only clean up non-Unity object resources
                    _Particles?.Clear();
                    _RootParticles?.Clear();
                    _Animators?.Clear();
                    _VfxGraphs?.Clear();
                    _VfxTrails?.Clear();
                    _MainPS = null;

                    // PreviewRenderUtility 的 Cleanup 必须在主线程调用
                    // PreviewRenderUtility's Cleanup must be called on main thread
                    // 这里只能标记为 null，让 Unity 自己处理
                    // Can only set to null here, let Unity handle it
                    _Preview = null;
                    _Instance = null;
                    _BgQuad = null;
                    _BgMat = null;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"ParticlePreviewContext cleanup failed during finalization: {e}");
                }
            }
        }

        /************************************************************************************************************************/
        #endregion
        /************************************************************************************************************************/
    }
}
#endif