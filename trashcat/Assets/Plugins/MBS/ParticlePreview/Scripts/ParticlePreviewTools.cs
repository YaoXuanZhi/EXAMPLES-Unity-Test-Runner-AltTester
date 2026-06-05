#if UNITY_EDITOR
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace MBS.ParticlePreview
{
    public static class ParticlePreviewTools
    {
        private const int CurveSampleCount = 20; // 曲线采样精度 / Curve sample precision

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 加载 ScriptableObject 配置资源
        /// Load ScriptableObject setting asset
        /// </summary>
        static public T LoadSettingAsset<T>(string configPath, [CallerFilePath] string sourceFilePath = "") where T : ScriptableObject
        {
            string finalPath = GetFinalPath(configPath, sourceFilePath);
            return AssetDatabase.LoadAssetAtPath<T>(finalPath);
        }

        /// <summary>
        /// 保存 ScriptableObject 配置资源
        /// Save ScriptableObject setting asset
        /// </summary>
        static public void SaveSettingAsset<T>(T instance, string configPath, [CallerFilePath] string sourceFilePath = "") where T : ScriptableObject
        {
            string finalPath = GetFinalPath(configPath, sourceFilePath);

            string directory = System.IO.Path.GetDirectoryName(finalPath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            AssetDatabase.CreateAsset(instance, finalPath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 将相对路径转换为 Assets/ 开头的项目路径
        /// Convert relative path to Assets/ prefixed project path
        /// </summary>
        static private string GetFinalPath(string configPath, string sourceFilePath)
        {
            string finalPath = configPath;
            if (configPath.StartsWith(".."))
            {
                string sourceDir = System.IO.Path.GetDirectoryName(sourceFilePath);
                string fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(sourceDir, configPath));
                fullPath = fullPath.Replace('\\', '/');
                int assetsIndex = fullPath.IndexOf("Assets/");
                if (assetsIndex != -1)
                {
                    finalPath = fullPath.Substring(assetsIndex);
                }
            }
            return finalPath;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        public static float GetMaxValue(ParticleSystem.MinMaxCurve minMaxCurve)
        {
            switch (minMaxCurve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return minMaxCurve.constant;
                case ParticleSystemCurveMode.Curve:
                    return GetMaxValue(minMaxCurve.curve);
                case ParticleSystemCurveMode.TwoConstants:
                    return minMaxCurve.constantMax;
                case ParticleSystemCurveMode.TwoCurves:
                    var ret1 = GetMaxValue(minMaxCurve.curveMin);
                    var ret2 = GetMaxValue(minMaxCurve.curveMax);
                    return ret1 > ret2 ? ret1 : ret2;
            }
            return -1f;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        public static float GetMinValue(ParticleSystem.MinMaxCurve minMaxCurve)
        {
            switch (minMaxCurve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return minMaxCurve.constant;
                case ParticleSystemCurveMode.Curve:
                    return GetMinValue(minMaxCurve.curve);
                case ParticleSystemCurveMode.TwoConstants:
                    return minMaxCurve.constantMin;
                case ParticleSystemCurveMode.TwoCurves:
                    var ret1 = GetMinValue(minMaxCurve.curveMin);
                    var ret2 = GetMinValue(minMaxCurve.curveMax);
                    return ret1 < ret2 ? ret1 : ret2;
            }
            return -1f;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 获取 AnimationCurve 的最大值（通过采样获取更精确的结果）
        /// Get the max value of AnimationCurve (more accurate via sampling)
        /// </summary>
        public static float GetMaxValue(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) return 0f;

            var ret = float.MinValue;
            var frames = curve.keys; // 只获取一次，避免重复分配 / Get once to avoid repeated allocation

            // 先检查关键帧
            // Check keyframes first
            for (var i = 0; i < frames.Length; i++)
            {
                var value = frames[i].value;
                if (value > ret) ret = value;
            }

            // 采样曲线以捕获关键帧之间的峰值
            // Sample curve to capture peaks between keyframes
            if (frames.Length >= 2)
            {
                var startTime = frames[0].time;
                var endTime = frames[frames.Length - 1].time;
                var step = (endTime - startTime) / CurveSampleCount;
                for (var i = 0; i <= CurveSampleCount; i++)
                {
                    var value = curve.Evaluate(startTime + step * i);
                    if (value > ret) ret = value;
                }
            }

            return ret;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 获取 AnimationCurve 的最小值（通过采样获取更精确的结果）
        /// Get the min value of AnimationCurve (more accurate via sampling)
        /// </summary>
        public static float GetMinValue(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) return 0f;

            var ret = float.MaxValue;
            var frames = curve.keys; // 只获取一次，避免重复分配 / Get once to avoid repeated allocation

            // 先检查关键帧
            // Check keyframes first
            for (var i = 0; i < frames.Length; i++)
            {
                var value = frames[i].value;
                if (value < ret) ret = value;
            }

            // 采样曲线以捕获关键帧之间的谷值
            // Sample curve to capture valleys between keyframes
            if (frames.Length >= 2)
            {
                var startTime = frames[0].time;
                var endTime = frames[frames.Length - 1].time;
                var step = (endTime - startTime) / CurveSampleCount;
                for (var i = 0; i <= CurveSampleCount; i++)
                {
                    var value = curve.Evaluate(startTime + step * i);
                    if (value < ret) ret = value;
                }
            }

            return ret;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 获取 Burst 发射的最后时间点
        /// Get the last burst emission time
        /// </summary>
        private static float GetLastBurstTime(ParticleSystem.EmissionModule emission)
        {
            var burstCount = emission.burstCount;
            if (burstCount == 0) return 0f;

            var lastBurstTime = 0f;
            for (var i = 0; i < burstCount; i++)
            {
                var burst = emission.GetBurst(i);
                var burstTime = burst.time;
                if (burstTime > lastBurstTime)
                {
                    lastBurstTime = burstTime;
                }
            }
            return lastBurstTime;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 获取 Trail 模块的持续时间（注意：lifetime 是相对于粒子生命周期的比例）
        /// Get Trail module duration (note: lifetime is relative to particle lifetime)
        /// </summary>
        private static float GetTrailDuration(ParticleSystem.TrailModule trails, float particleLifetime)
        {
            if (!trails.enabled) return 0f;
            // trails.lifetime 是比例值（0~1），需要乘以粒子生命周期
            // trails.lifetime is a ratio (0~1), needs to be multiplied by particle lifetime
            return GetMaxValue(trails.lifetime) * particleLifetime;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        public static float GetDuration(ParticleSystem particle, bool allowLoop = true)
        {
            var emission = particle.emission;
            if (!emission.enabled) return 0f;

            if (particle.TryGetComponent<ParticleSystemRenderer>(out var renderer))
            {
                if (!renderer.enabled) return 0f;
            }
            else
            {
                return 0f;
            }

            var main = particle.main;
            var startDelay = GetMaxValue(main.startDelay);
            var startLifetime = GetMaxValue(main.startLifetime);
            var trailDuration = GetTrailDuration(particle.trails, startLifetime);

            // 循环特效：返回一个完整循环周期的时长（duration + 粒子生命周期 + 拖尾）
            // Looping effect: return duration of one complete cycle (duration + particle lifetime + trail)
            if (main.loop)
            {
                if (!allowLoop) return -1f;
                // 循环特效的一个周期 = 发射持续时间内最后发射的粒子消失的时间
                // One cycle of looping effect = time for last emitted particle to disappear
                return startDelay + main.duration + startLifetime + trailDuration;
            }

            float baseDuration;
            var rateOverTime = GetMinValue(emission.rateOverTime);
            if (rateOverTime <= 0)
            {
                // 使用 Burst 发射模式
                // Burst emission mode
                var lastBurstTime = GetLastBurstTime(emission);
                baseDuration = startDelay + lastBurstTime + startLifetime;
            }
            else
            {
                // 持续发射模式
                // Continuous emission mode
                baseDuration = startDelay + Mathf.Max(main.duration, startLifetime);
            }

            // 加上拖尾持续时间
            // Add trail duration
            return baseDuration + trailDuration;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 获取 SubEmitter 的额外持续时间（仅计算 Death 类型的子发射器）
        /// Get extra duration from SubEmitters (only calculates Death-type sub-emitters)
        /// </summary>
        private static float GetSubEmitterExtraDuration(ParticleSystem particle, bool allowLoop)
        {
            var subEmitters = particle.subEmitters;
            if (!subEmitters.enabled) return 0f;

            var maxExtraDuration = 0f;
            var subEmitterCount = subEmitters.subEmittersCount;

            for (var i = 0; i < subEmitterCount; i++)
            {
                var subPs = subEmitters.GetSubEmitterSystem(i);
                if (subPs == null) continue;

                var subType = subEmitters.GetSubEmitterType(i);
                var subDuration = GetDuration(subPs, allowLoop);

                // 只有 Death 类型的子发射器会在父粒子死亡后继续播放
                // Only Death-type sub-emitters continue after parent particle dies
                // Birth 类型与父粒子并行播放，已经包含在 GetComponentsInChildren 中
                // Birth-type plays in parallel with parent, already included via GetComponentsInChildren
                // Collision/Trigger/Manual 时间不确定，这里不额外计算
                // Collision/Trigger/Manual timing is uncertain, not calculated here
                if (subType == ParticleSystemSubEmitterType.Death)
                {
                    if (subDuration > maxExtraDuration)
                    {
                        maxExtraDuration = subDuration;
                    }
                }
            }

            return maxExtraDuration;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 尝试获取粒子特效 ParticleSystem 的播放时间长度
        /// Try to get ParticleSystem effect playback duration
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="includeChildren">是否包含所有子节点对象 / Whether to include all child objects</param>
        /// <param name="includeInactive">是否包含非激活对象 / Whether to include inactive objects</param>
        /// <param name="allowLoop">是否允许计算循环特效的时长，如果允许则计算一个周期，不允许则返回-1 / Whether to calculate looping effect duration (one cycle if allowed, -1 if not)</param>
        public static float GetParticleDuration(GameObject gameObject, bool includeChildren = true, bool includeInactive = false, bool allowLoop = true)
        {
            if (includeChildren)
            {
                var particles = gameObject.GetComponentsInChildren<ParticleSystem>(includeInactive);
                var duration = -1f;
                for (var i = 0; i < particles.Length; i++)
                {
                    var ps = particles[i];
                    var time = GetDuration(ps, allowLoop);

                    // 只计算 Death 类型子发射器的额外时间（其他类型已包含在 GetComponentsInChildren 中）
                    // Only calculate Death-type sub-emitter extra time (other types already included via GetComponentsInChildren)
                    var subEmitterExtraTime = GetSubEmitterExtraDuration(ps, allowLoop);
                    var totalTime = time + subEmitterExtraTime;

                    if (totalTime > duration)
                    {
                        duration = totalTime;
                    }
                }

                return duration;
            }
            else
            {
                var ps = gameObject.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var time = GetDuration(ps, allowLoop);
                    var subEmitterExtraTime = GetSubEmitterExtraDuration(ps, allowLoop);
                    return time + subEmitterExtraTime;
                }
                else
                {
                    return -1f;
                }
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 获取单个 VisualEffect 的播放时长
        /// Get playback duration of a single VisualEffect
        /// </summary>
        /// <param name="vfx">VisualEffect 组件 / VisualEffect component</param>
        /// <param name="allowLoop">是否允许计算循环特效的时长，如果允许则计算一个周期，不允许则返回-1 / Whether to calculate looping effect duration (one cycle if allowed, -1 if not)</param>
        /// <param name="durationPropertyName">用于存储时长的 Exposed Property 名称（可选） / Exposed Property name for duration (optional)</param>
        public static float GetDuration(VisualEffect vfx, bool allowLoop = true, string durationPropertyName = "Duration")
        {
            if (vfx == null || vfx.visualEffectAsset == null) return -1f;

            // 优先尝试从暴露的属性中获取时长
            // Try to get duration from exposed property first
            if (vfx.HasFloat(durationPropertyName))
            {
                return vfx.GetFloat(durationPropertyName);
            }

            // 尝试常见的时长属性名称
            // Try common duration property names
            string[] commonDurationNames = { "Duration", "duration", "TotalTime", "totalTime", "EffectDuration", "Lifetime", "lifetime" };
            foreach (var name in commonDurationNames)
            {
                if (vfx.HasFloat(name))
                {
                    return vfx.GetFloat(name);
                }
            }

            // VFX Graph 没有内置的时长属性，尝试从 asset 获取信息
            // VFX Graph has no built-in duration property, try to get info from asset
            // 注意：VFX Graph 的循环状态无法在运行时直接获取，需要通过属性判断
            // Note: VFX Graph loop state can't be obtained at runtime directly, needs property check
            // 如果有 "Loop" 属性且为 true，视为循环特效
            // If "Loop" property exists and is true, treat as looping effect
            if (vfx.HasBool("Loop") && vfx.GetBool("Loop"))
            {
                // 循环特效返回默认周期 3 秒
                // Looping effect returns default cycle of 3 seconds
                return allowLoop ? 3f : -1f;
            }

            // 无法获取时长，返回默认值 3 秒（VFX Graph 无法自动计算时长）
            // Cannot get duration, return default 3 seconds (VFX Graph cannot auto-calculate duration)
            return 3f;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 尝试获取 VFX Graph 特效的播放时间长度
        /// Try to get VFX Graph effect playback duration
        /// </summary>
        /// <param name="gameObject">包含 VisualEffect 的 GameObject / GameObject containing VisualEffect</param>
        /// <param name="includeChildren">是否包含所有子节点对象 / Whether to include all child objects</param>
        /// <param name="includeInactive">是否包含非激活对象 / Whether to include inactive objects</param>
        /// <param name="allowLoop">是否允许计算循环特效的时长，如果允许则计算一个周期，不允许则返回-1 / Whether to calculate looping effect duration (one cycle if allowed, -1 if not)</param>
        /// <param name="durationPropertyName">用于存储时长的 Exposed Property 名称（可选） / Exposed Property name for duration (optional)</param>
        public static float GetVfxDuration(GameObject gameObject, bool includeChildren = true, bool includeInactive = false, bool allowLoop = true, string durationPropertyName = "Duration")
        {
            if (includeChildren)
            {
                var vfxList = gameObject.GetComponentsInChildren<VisualEffect>(includeInactive);
                var duration = -1f;
                for (var i = 0; i < vfxList.Length; i++)
                {
                    var vfx = vfxList[i];
                    var time = GetDuration(vfx, allowLoop, durationPropertyName);
                    if (time > duration)
                    {
                        duration = time;
                    }
                }

                return duration;
            }
            else
            {
                var vfx = gameObject.GetComponent<VisualEffect>();
                if (vfx != null)
                {
                    return GetDuration(vfx, allowLoop, durationPropertyName);
                }
                else
                {
                    return -1f;
                }
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 获取特效的播放时间长度（自动检测 ParticleSystem 和 VFX Graph）
        /// Get effect playback duration (auto-detects ParticleSystem and VFX Graph)
        /// </summary>
        /// <param name="gameObject">特效 GameObject / Effect GameObject</param>
        /// <param name="includeChildren">是否包含所有子节点对象 / Whether to include all child objects</param>
        /// <param name="includeInactive">是否包含非激活对象 / Whether to include inactive objects</param>
        /// <param name="allowLoop">是否允许计算循环特效的时长，如果允许则计算一个周期，不允许则返回-1 / Whether to calculate looping effect duration (one cycle if allowed, -1 if not)</param>
        public static float GetEffectDuration(GameObject gameObject, bool includeChildren = true, bool includeInactive = false, bool allowLoop = true)
        {
            var particleDuration = GetParticleDuration(gameObject, includeChildren, includeInactive, allowLoop);
            var vfxDuration = GetVfxDuration(gameObject, includeChildren, includeInactive, allowLoop);

            // 返回两者中较大的值
            // Return the larger of the two
            return Mathf.Max(particleDuration, vfxDuration);
        }
    }
}
#endif