#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MBS.ParticlePreview
{
    public class ParticlePreviewAxis : EditorWindow
    {
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制坐标轴 Gizmo（类似 Scene 视图右上角的坐标轴指示器）
        /// Draw axis gizmo (similar to Scene view's top-right axis indicator)
        /// </summary>
        /// <param name="cellRect">格子区域 / Cell rectangle</param>
        /// <param name="context">预览上下文 / Preview context</param>
        static public void DrawAxisGizmo(Rect cellRect, ParticlePreviewContext context)
        {
            if (Event.current.type != EventType.Repaint) return;

            float camPitch = context.CamPitch;
            float camYaw = context.CamYaw;

            // Gizmo 参数
            // Gizmo parameters
            float gizmoSize = Mathf.Min(cellRect.width, cellRect.height) * 0.25f;
            float axisLen = gizmoSize * 0.42f;
            float labelOffset = 11f;
            // 坐标轴中心跟随特效位置中心（含平移偏移）
            // Axis gizmo center follows effect position (including pan offset)
            Vector2 screenOffset = context.GetEffectScreenOffset(cellRect.height);
            Vector2 center = cellRect.center + screenOffset;

            // 根据相机旋转计算三个轴在屏幕空间的投影方向
            // Calculate screen-space projection of three axes based on camera rotation
            Quaternion camRot = Quaternion.Euler(camPitch, camYaw, 0);
            // 相机看向 -Z 方向，所以需要反转
            // Camera looks toward -Z, so we invert
            Matrix4x4 viewMatrix = Matrix4x4.Rotate(Quaternion.Inverse(camRot));

            // 世界坐标轴方向
            // World axis directions
            Vector3 xWorld = viewMatrix.MultiplyVector(Vector3.right);
            Vector3 yWorld = viewMatrix.MultiplyVector(Vector3.up);
            Vector3 zWorld = viewMatrix.MultiplyVector(Vector3.forward);

            // 投影到屏幕空间（X 向右，Y 在 GUI 中向下所以取反）
            // Project to screen space (X goes right, Y is flipped in GUI)
            Vector2 xScreen = new Vector2(xWorld.x, -xWorld.y) * axisLen;
            Vector2 yScreen = new Vector2(yWorld.x, -yWorld.y) * axisLen;
            Vector2 zScreen = new Vector2(zWorld.x, -zWorld.y) * axisLen;

            // 按深度排序绘制（远的先画，近的后画）
            // Sort by depth (draw far axes first, near axes last)
            float xDepth = xWorld.z;
            float yDepth = yWorld.z;
            float zDepth = zWorld.z;

            // 构建排序数组
            // Build sorting array
            var axes = new (Vector2 dir, Color color, string label, float depth)[]
            {
                (xScreen, new Color(1f, 0.2f, 0.2f), "X", xDepth),
                (yScreen, new Color(0.4f, 1f, 0.2f), "Y", yDepth),
                (zScreen, new Color(0.3f, 0.5f, 1f), "Z", zDepth),
            };
            System.Array.Sort(axes, (a, b) => a.depth.CompareTo(b.depth));

            // 准备标签样式
            // Prepare label style
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = Mathf.Max(9, (int)(gizmoSize * 0.22f)),
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };

            // 逐轴绘制
            // Draw each axis
            foreach (var (dir, color, label, depth) in axes)
            {
                // 越远的轴越暗淡
                // Farther axes appear dimmer
                float alpha = Mathf.Lerp(0.6f, 1f, Mathf.InverseLerp(-1f, 1f, depth));
                Color lineColor = new Color(color.r, color.g, color.b, alpha);

                Vector2 endPoint = center + dir;

                // 绘制轴线
                // Draw axis line
                DrawGUILine(center, endPoint, lineColor, 3f);

                // 绘制末端小圆点
                // Draw endpoint dot
                DrawFilledCircle(endPoint, 4f, lineColor);

                // 绘制轴标签
                // Draw axis label
                Vector2 labelDir = dir.normalized;
                Vector2 labelPos = endPoint + labelDir * labelOffset;
                Rect labelRect = new Rect(labelPos.x - 8, labelPos.y - 8, 16, 16);

                Color oldColor = GUI.color;
                GUI.color = lineColor;
                labelStyle.normal.textColor = lineColor;
                GUI.Label(labelRect, label, labelStyle);
                GUI.color = oldColor;
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 使用矩阵旋转 + EditorGUI.DrawRect 绘制一条 GUI 空间的线段
        /// Draw a line in GUI space using matrix rotation + EditorGUI.DrawRect
        /// 不使用 GL 绘制，避免在 ScrollView 中坐标偏移
        /// Avoids GL drawing which causes coordinate offset inside ScrollView
        /// </summary>
        static public void DrawGUILine(Vector2 from, Vector2 to, Color color, float width)
        {
            if (Event.current.type != EventType.Repaint) return;

            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.01f) return;

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            // 保存当前 GUI 矩阵，旋转绘制后恢复
            // Save current GUI matrix, rotate to draw, then restore
            Matrix4x4 savedMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, from);
            EditorGUI.DrawRect(new Rect(from.x, from.y - width * 0.5f, length, width), color);
            GUI.matrix = savedMatrix;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 使用多层旋转矩形近似绘制实心圆
        /// Draw a filled circle approximated with multiple rotated rects
        /// 不使用 GL 绘制，避免在 ScrollView 中坐标偏移
        /// Avoids GL drawing which causes coordinate offset inside ScrollView
        /// </summary>
        static public void DrawFilledCircle(Vector2 center, float radius, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;

            // 用多条不同角度的细长矩形条叠在一起近似实心圆
            // Overlay multiple rotated thin rects to approximate a filled circle
            // 每条矩形：宽 = 直径，高 = 根据角度间隔计算使相邻条恰好覆盖
            // Each rect: width = diameter, height = calculated so adjacent rects just overlap
            int steps = 9;
            float angleStep = 180f / steps;
            float diameter = radius * 2f;
            // 矩形高度 = 2R * sin(angleStep)，略微放大确保无缝覆盖
            // Rect height = 2R * sin(angleStep), slightly enlarged to ensure seamless coverage
            float thickness = 2f * radius * Mathf.Sin(angleStep * Mathf.Deg2Rad) * 1.15f;

            for (int i = 0; i < steps; i++)
            {
                Matrix4x4 savedMatrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(i * angleStep, center);
                EditorGUI.DrawRect(new Rect(center.x - radius, center.y - thickness * 0.5f, diameter, thickness), color);
                GUI.matrix = savedMatrix;
            }
        }
    }
}
#endif