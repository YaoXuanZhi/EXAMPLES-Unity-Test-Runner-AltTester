#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MBS.ParticlePreview
{
    public class ParticlePreviewFolder : ScriptableObject
    {
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 单例实例
        // Singleton instance
        static private ParticlePreviewFolder _Instance;
        static public ParticlePreviewFolder Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = ParticlePreviewTools.LoadSettingAsset<ParticlePreviewFolder>(ParticlePreviewDefine.FOLDERS_PATH);
                    if (_Instance == null)
                    {
                        _Instance = CreateInstance<ParticlePreviewFolder>();
                        ParticlePreviewTools.SaveSettingAsset(_Instance, ParticlePreviewDefine.FOLDERS_PATH);
                    }
                }
                return _Instance;
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 简单文件夹分页：直接拖入文件夹即可
        // Simple folder pages: just drag folders in
        public List<Object> SimpleFolderPages = new List<Object>();

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 获取有效的文件夹列表（过滤掉 null 条目）
        /// Get valid folder list (filter out null entries)
        /// </summary>
        private List<Object> GetValidFolders()
        {
            List<Object> valid = new List<Object>();
            foreach (var folder in SimpleFolderPages)
            {
                if (folder != null)
                    valid.Add(folder);
            }
            return valid;
        }

        /// <summary>
        /// 获取所有文件夹分页的名称列表
        /// Get all folder page names
        /// </summary>
        public List<string> GetPageNames()
        {
            List<string> names = new List<string>();
            foreach (var folder in GetValidFolders())
            {
                names.Add(folder.name);
            }
            return names;
        }

        /// <summary>
        /// 获取指定分页内的所有特效 Prefab（索引对应 GetPageNames 返回的列表）
        /// Get all VFX prefabs in the specified folder page (index matches GetPageNames list)
        /// </summary>
        public List<GameObject> GetPrefabs(int pageIndex)
        {
            List<GameObject> prefabs = new List<GameObject>();
            List<Object> validFolders = GetValidFolders();
            if (pageIndex < 0 || pageIndex >= validFolders.Count) return prefabs;

            Object folder = validFolders[pageIndex];
            if (folder == null) return prefabs;

            string folderPath = AssetDatabase.GetAssetPath(folder);
            if (!AssetDatabase.IsValidFolder(folderPath)) return prefabs;

            // 搜索文件夹内所有 Prefab（包含子目录）
            // Search all prefabs inside the folder (including subdirectories)
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (prefab == null) continue;

                // 检查是否包含 ParticleSystem 或 VFX Graph
                // Check if contains ParticleSystem or VFX Graph
                bool hasParticle = prefab.GetComponent<ParticleSystem>() != null ||
                                   prefab.GetComponentInChildren<ParticleSystem>(true) != null;
                bool hasVFX = prefab.GetComponent<UnityEngine.VFX.VisualEffect>() != null ||
                              prefab.GetComponentInChildren<UnityEngine.VFX.VisualEffect>(true) != null;

                if (hasParticle || hasVFX)
                {
                    prefabs.Add(prefab);
                }
            }

            return prefabs;
        }

        /// <summary>
        /// 移除指定有效索引对应的文件夹分页
        /// Remove folder page at the specified valid index
        /// </summary>
        public void RemovePage(int validIndex)
        {
            List<Object> validFolders = GetValidFolders();
            if (validIndex < 0 || validIndex >= validFolders.Count) return;

            Object target = validFolders[validIndex];
            SimpleFolderPages.Remove(target);

            // 保存修改到磁盘
            // Save changes to disk
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
