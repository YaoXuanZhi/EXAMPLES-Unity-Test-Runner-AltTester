#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MBS.ParticlePreview
{
    public class ParticlePreviewMasker : EditorWindow
    {
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制内边框
        /// Draw inner border
        /// </summary>
        static public void DrawInnerBorder(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), color);
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 圆角遮罩纹理（静态缓存，所有格子共用）
        // Rounded corner mask textures (static cache, shared by all cells)
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        static private Texture2D _CornerMaskTL; // 左上角遮罩 / Top-left corner mask
        static private Texture2D _CornerMaskTR; // 右上角遮罩 / Top-right corner mask
        static private Texture2D _CornerMaskBL; // 左下角遮罩 / Bottom-left corner mask
        static private Texture2D _CornerMaskBR; // 右下角遮罩 / Bottom-right corner mask
        private const int CORNER_MASK_SIZE = 32; // 遮罩纹理分辨率 / Mask texture resolution

        /// <summary>
        /// 确保圆角遮罩纹理已创建（懒加载，仅创建一次）
        /// Ensure corner mask textures are created (lazy-loaded, created only once)
        /// </summary>
        static public void EnsureCornerMasks()
        {
            if (_CornerMaskTL != null) return;

            _CornerMaskTL = CreateCornerMask(false, false);
            _CornerMaskTR = CreateCornerMask(true, false);
            _CornerMaskBL = CreateCornerMask(false, true);
            _CornerMaskBR = CreateCornerMask(true, true);
        }

        /// <summary>
        /// 创建单个圆角遮罩纹理
        /// Create a single corner mask texture
        /// 纹理为白色，Alpha 通道表示遮罩区域（1=遮盖 0=透明）
        /// Texture is white, alpha channel defines mask area (1=covered 0=transparent)
        /// </summary>
        /// <param name="flipX">是否水平翻转（用于右侧角） / Flip horizontally (for right-side corners)</param>
        /// <param name="flipY">是否垂直翻转（用于底部角） / Flip vertically (for bottom corners)</param>
        static public Texture2D CreateCornerMask(bool flipX, bool flipY)
        {
            int size = CORNER_MASK_SIZE;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            // 圆心在归一化绘制空间中的位置（指向格子内侧）
            // Circle center in normalized draw space (pointing toward cell interior)
            // 左上角: center=(1,1)  右上角: center=(0,1)
            // 左下角: center=(1,0)  右下角: center=(0,0)
            float cx = flipX ? 0f : 1f;
            float cy = flipY ? 0f : 1f;

            var pixels = new Color32[size * size];
            for (int ty = 0; ty < size; ty++)
            {
                for (int tx = 0; tx < size; tx++)
                {
                    // 纹理坐标 → 归一化绘制坐标（纹理 Y 轴与 GUI Y 轴相反）
                    // Texture coords → normalized draw coords (texture Y is flipped vs GUI Y)
                    float u = (tx + 0.5f) / size;
                    float v = 1f - (ty + 0.5f) / size;

                    // 到圆心的距离（归一化空间中圆半径 = 1）
                    // Distance to circle center (circle radius = 1 in normalized space)
                    float du = u - cx;
                    float dv = v - cy;
                    float dist = Mathf.Sqrt(du * du + dv * dv);

                    // 抗锯齿：约 1.5 像素的平滑过渡带
                    // Anti-aliasing: ~1.5 pixel smooth transition band
                    float edgeSoftness = 1.5f / size;
                    float alpha = Mathf.Clamp01((dist - 1f + edgeSoftness) / (2f * edgeSoftness));

                    pixels[ty * size + tx] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);  // 标记为不可读以节省内存 / Mark as non-readable to save memory
            return tex;
        }

        /// <summary>
        /// 清理圆角遮罩纹理（在窗口关闭或域重载时调用）
        /// Cleanup corner mask textures (called on window close or domain reload)
        /// </summary>
        static public void CleanupCornerMasks()
        {
            if (_CornerMaskTL != null) { Object.DestroyImmediate(_CornerMaskTL); _CornerMaskTL = null; }
            if (_CornerMaskTR != null) { Object.DestroyImmediate(_CornerMaskTR); _CornerMaskTR = null; }
            if (_CornerMaskBL != null) { Object.DestroyImmediate(_CornerMaskBL); _CornerMaskBL = null; }
            if (_CornerMaskBR != null) { Object.DestroyImmediate(_CornerMaskBR); _CornerMaskBR = null; }
        }

        /// <summary>
        /// 绘制圆角遮罩（在格子四角覆盖与窗口背景同色的扇形区域）
        /// Draw rounded corner masks (cover cell corners with window-background-colored arcs)
        /// 使用预生成的纹理，零运行时像素计算，性能开销极低
        /// Uses pre-generated textures, zero per-frame pixel computation, minimal performance cost
        /// </summary>
        /// <param name="rect">格子绘制区域 / Cell drawing area</param>
        /// <param name="radius">圆角半径（像素） / Corner radius in pixels</param>
        static public void DrawRoundedCornerMask(Rect rect, float radius)
        {
            if (radius < 1f) return;
            EnsureCornerMasks();

            // 获取编辑器窗口背景色作为遮罩颜色
            // Get editor window background color as mask color
            Color maskColor = EditorGUIUtility.isProSkin
                ? new Color(56f / 255f, 56f / 255f, 56f / 255f, 1f)
                : new Color(194f / 255f, 194f / 255f, 194f / 255f, 1f);

            Color oldColor = GUI.color;
            GUI.color = maskColor;

            // 将 rect 对齐到像素网格，避免浮点坐标导致的亚像素偏移（滚动/缩放时出现 1px 缝隙）
            // Snap rect to pixel grid to avoid sub-pixel offset (causes 1px gaps when scrolling/resizing)
            float x0 = Mathf.Floor(rect.x);
            float y0 = Mathf.Floor(rect.y);
            float x1 = Mathf.Ceil(rect.xMax);
            float y1 = Mathf.Ceil(rect.yMax);
            float r = Mathf.Ceil(radius);

            // 四角分别绘制遮罩纹理
            // Draw mask textures at each corner
            GUI.DrawTexture(new Rect(x0, y0, r, r), _CornerMaskTL);
            GUI.DrawTexture(new Rect(x1 - r, y0, r, r), _CornerMaskTR);
            GUI.DrawTexture(new Rect(x0, y1 - r, r, r), _CornerMaskBL);
            GUI.DrawTexture(new Rect(x1 - r, y1 - r, r, r), _CornerMaskBR);

            GUI.color = oldColor;
        }
    }
}
#endif