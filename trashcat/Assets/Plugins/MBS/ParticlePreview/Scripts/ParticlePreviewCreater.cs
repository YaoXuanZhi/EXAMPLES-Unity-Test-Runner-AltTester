#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace MBS.ParticlePreview
{
    public class ParticlePreviewCreater : EditorWindow
    {
        /// <summary>
        /// 打开VFX浏览器窗口
        /// Open VFX Browser window
        /// </summary>
        [MenuItem("Tools/VFX Browser")]
        [Shortcut("VFX Browser/Open VFX Browser", KeyCode.None, ShortcutModifiers.None)]
        static public void ShowWindow()
        {
            // 关闭现有窗口
            // Close the existing window
            var existingWindows = Resources.FindObjectsOfTypeAll<ParticlePreviewWindow>();
            foreach (var existingWindow in existingWindows) if (existingWindow != null) existingWindow.Close();
            if (existingWindows.Length > 0) return;

            var windowName = "VFX Browser";
            var window = GetWindow<ParticlePreviewWindow>(windowName);
            var icon = EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_Particle Effect" : "Particle Effect").image;
            window.titleContent = new GUIContent(windowName, icon);
            window.minSize = new Vector2(400, 300);
        }
    }
}
#endif