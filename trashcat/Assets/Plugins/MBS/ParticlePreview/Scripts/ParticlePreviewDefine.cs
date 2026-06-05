#if UNITY_EDITOR
namespace MBS.ParticlePreview
{
    public class ParticlePreviewDefine
    {
        // 配置文件路径（相对于 Scripts 目录）
        // Config file paths (relative to Scripts directory)
        public const string SETTINGS_PATH = "../Settings/ParticlePreviewSetting.asset";
        public const string FOLDERS_PATH = "../Settings/ParticlePreviewFolder.asset";

        // 编辑器持久化键（Toolbar 设置）
        // EditorPrefs keys (Toolbar settings)
        public const string PREF_SPEED = "MBS_ParticlePreview_Speed";
        public const string PREF_BG_GRAY = "MBS_ParticlePreview_BgGray";
        public const string PREF_GRID_COUNT = "MBS_ParticlePreview_GridCount";
        public const string PREF_CAM_DIST_MUL = "MBS_ParticlePreview_CamDistMul";
        public const string PREF_HIGH_FPS = "MBS_ParticlePreview_HighFps";
        public const string PREF_SHOW_HELP_TIPS = "MBS_ParticlePreview_ShowHelpTips";
        public const string PREF_SHOW_NAME = "MBS_ParticlePreview_ShowName";
        public const string PREF_USE_SUBDIRECTORY = "MBS_ParticlePreview_UseSubdirectory";

        /// <summary>
        /// 子目录分组数据结构
        /// Subfolder group data structure
        /// </summary>
        [System.Serializable]
        public class ParticleSubfolderGroup
        {
            public string DisplayName; // 显示名称 / Display name
            public string FolderPath;  // 文件夹路径 / Folder path
            public int StartIndex;     // 起始索引 / Start index
            public int Count;          // 资源数量 / Item count
        }
    }
}
#endif