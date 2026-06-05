#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MBS.ParticlePreview
{
    public class ParticlePreviewSetting : ScriptableObject
    {
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 单例实例
        // Singleton instance
        static private ParticlePreviewSetting _Instance;
        static public ParticlePreviewSetting Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = ParticlePreviewTools.LoadSettingAsset<ParticlePreviewSetting>(ParticlePreviewDefine.SETTINGS_PATH);
                    if (_Instance == null)
                    {
                        _Instance = CreateInstance<ParticlePreviewSetting>();
                        _Instance.Reset();
                        ParticlePreviewTools.SaveSettingAsset(_Instance, ParticlePreviewDefine.SETTINGS_PATH);
                    }
                }
                return _Instance;
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 通用设置
        // General settings
        [Header("General Settings")]
        [Tooltip("Because each device has different configurations, the default playback speed may not be suitable for everyone. This value will be used as the initial speed during playback and can be adjusted through the toolbar.")]
        public float DefaultSpeed = 0.5f;
        [Tooltip("Default background color intensity.")]
        public float DefaultColor = 0.3f;
        [Tooltip("Time compensation for playback.")]
        [Min(0)] public float DefaulInterval = 0f;
        public bool ShowTapSpeed = true;
        public bool ShowTapGrid = true;
        public bool ShowTapColor = true;
        public bool ShowTapScale = true;

        public bool ShowTapFPS = true;
        public bool ShowTapGroup = true;
        public bool ShowTapFolder = true;
        [Tooltip("Split sliders into a second row.")]
        [HideInInspector]
        public bool TitleSplitRow = true;
        [HideInInspector]
        public bool ShowRotateAxis = true;

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 分页按钮设置
        // Page button settings
        [Header("Page Button")]
        public int PageButtonHeight = 28;
        public int PageButtonTextSize = 12;
        public int PageButtonRowCount = 5;
        public Color PageButtonSelectedColor = new Color(0.2f, 0.6f, 1f, 1f);

        //──────────────────────────────────────────────────────────────────────────────────────────────────────────────
        // 拖尾设置
        // Tail settings
        [Header("Tail Settings")]
        public float TailMoveSpeed = 10f;
        [Tooltip("Duration to accelerate from 0 to target speed on replay (seconds)")]
        public float TailAccelDuration = 1.0f;

        //──────────────────────────────────────────────────────────────────────────────────────────────────────────────
        // 子目录分组设置
        // Subdirectory grouping settings
        [Header("Subdirectory Grouping")]
        [Tooltip("Section header title text color.")]
        public Color SubdirectoryTitleColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        [Tooltip("Section header background color.")]
        public Color SubdirectorySectionColor = new Color(0.15f, 0.15f, 0.15f, 1f);

        /// <summary>
        /// 是否启用子目录分组（通过 EditorPrefs 持久化）
        /// Whether subdirectory grouping is enabled (persisted via EditorPrefs)
        /// </summary>
        public bool UseSubdirectory
        {
            get => EditorPrefs.GetBool(ParticlePreviewDefine.PREF_USE_SUBDIRECTORY, false);
            set => EditorPrefs.SetBool(ParticlePreviewDefine.PREF_USE_SUBDIRECTORY, value);
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────────────
        // 圆角设置
        // Rounded corner settings
        [Header("Rounded Corners")]
        [Tooltip("Enable rounded corners on preview cells")]
        public bool RoundedCorners = false;
        [Tooltip("Corner radius as a proportion of cell size (0.02 - 0.2)")]
        [Range(0.02f, 0.2f)]
        public float CellCornerRatio = 0.08f;

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 重置为默认值
        /// Reset to default values
        /// </summary>
        public void Reset()
        {
            DefaultSpeed = 0.5f;
            DefaultColor = 0.3f;
            DefaulInterval = 0f;
            ShowTapSpeed = true;
            ShowTapGrid = true;
            ShowTapColor = true;
            ShowTapScale = true;

            ShowTapFPS = true;
            ShowTapGroup = true;
            ShowTapFolder = true;
            TitleSplitRow = true;
            ShowRotateAxis = true;

            PageButtonHeight = 28;
            PageButtonTextSize = 12;
            PageButtonRowCount = 5;
            PageButtonSelectedColor = new Color(0.2f, 0.6f, 1f, 1f);

            TailMoveSpeed = 10f;
            TailAccelDuration = 1.0f;

            RoundedCorners = false;
            CellCornerRatio = 0.08f;

            UseSubdirectory = false;
            SubdirectoryTitleColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            SubdirectorySectionColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        }
    }

    /************************************************************************************************************************/
    /************************************************************************************************************************/
    /************************************************************************************************************************/
    /************************************************************************************************************************/
    /************************************************************************************************************************/

    [CustomEditor(typeof(ParticlePreviewSetting))]
    public class ParticlePreviewSettingEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // 绘制默认的 Inspector 界面
            // Draw default Inspector interface
            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 250f;
            base.OnInspectorGUI();
            EditorGUIUtility.labelWidth = originalLabelWidth;

            // 获取目标对象
            // Get target object
            ParticlePreviewSetting settings = (ParticlePreviewSetting)target;

            // 添加一些空间
            // Add some space
            GUILayout.Space(20);

            // 绘制一个醒目的重置按钮
            // Draw a prominent reset button
            if (GUILayout.Button("Reset All Settings to Default", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Reset Settings?",
                    "Are you sure you want to reset all settings to their default values? This action can be undone.",
                    "Yes, Reset", "Cancel"))
                {
                    // 在修改之前记录对象状态，以便撤销
                    // Record object state before modification for undo
                    Undo.RecordObject(settings, "Reset Settings");

                    // 调用 Reset 方法
                    // Call Reset method
                    settings.Reset();

                    // 标记为已修改（这一步对于 ScriptableObject 很重要）
                    // Mark as dirty (important for ScriptableObject)
                    EditorUtility.SetDirty(settings);
                }
            }

            //──────────────────────────────────────────────────────────────────────────────────────────────────────
            GUILayout.Space(20);
            GUILayout.Label("Feedback", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("❤️ Love it! Rate Us", GUILayout.Height(30)))
            {
                string url = "https://assetstore.unity.com/packages/slug/355556";
                EditorGUIUtility.systemCopyBuffer = url;
                Application.OpenURL(url);
            }
            if (GUILayout.Button("🌧️ Need Help?", GUILayout.Height(30)))
            {
                string email = "FcsVorfeed@mbs-studio.com";
                EditorGUIUtility.systemCopyBuffer = email;
                Application.OpenURL("mailto:" + email);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Rating helps us survive as indie devs. Issues? Email us first!", MessageType.None);
        }
    }

    /************************************************************************************************************************/
    public class ParticlePreviewSettingsProvider : SettingsProvider
    {
        public ParticlePreviewSettingsProvider(string path, SettingsScope scopes, System.Collections.Generic.IEnumerable<string> keywords = null) : base(path, scopes, keywords) { }

        private Editor _editor;
        public override void OnGUI(string searchContext)
        {
            if (_editor == null)
            {
                _editor = Editor.CreateEditor(ParticlePreviewSetting.Instance);
            }
            _editor.OnInspectorGUI();
        }

        [SettingsProvider]
        public static SettingsProvider CreateParticlePrevieweSettingsProvider()
        {
            var provider = new ParticlePreviewSettingsProvider("Project/MBS/ParticlePreview", SettingsScope.Project, new[] { "MBS", "Particle", "Previewer" });
            return provider;
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            if (_editor != null)
            {
                Object.DestroyImmediate(_editor);
                _editor = null;
            }
        }
    }
}
#endif