using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace YIUIFramework.Editor.MCP
{
    /// <summary>
    /// Test Runner 桥接层 — 跨越 Domain Reload 的持久化测试执行
    ///
    /// 核心思路：
    /// 1. [InitializeOnLoad] 保证每次域重载后重新挂载回调
    /// 2. EditorPrefs 存储活跃测试状态（域重载后不丢失）
    /// 3. 文件追加记录测试进度，即使 MCP 连接中断也能保留结果
    /// 4. 监听 PlayModeStateChange 记录状态变化时间线
    ///
    /// 参考：StarForceTestRunnerBridge
    /// </summary>
    [InitializeOnLoad]
    public static class YIUIMCPTestRunnerBridge
    {
        private const string KEY_ACTIVE_RUN_ID = "YIUIMCP.TestRunnerBridge.ActiveRunId";
        private const string KEY_ACTIVE_MODE   = "YIUIMCP.TestRunnerBridge.ActiveMode";
        private const string KEY_RESULT_FILE   = "YIUIMCP.TestRunnerBridge.ActiveResultFile";

        private static TestRunnerApi s_Api;
        private static PersistentCallbacks s_Callback;

        static YIUIMCPTestRunnerBridge()
        {
            // 每次域重载后重新挂载回调
            EnsureCallbackRegistered();

            // 如果有未完成的测试运行，记录重载事件
            if (HasActiveRun())
            {
                AppendActiveLine("domainReload:reloaded");
                AppendActiveLine($"reloadedAt:{DateTime.Now:O}");
            }
        }

        /// <summary>
        /// 获取默认结果文件路径
        /// </summary>
        public static string GetDefaultResultFilePath(ETestMode mode)
        {
            string dir = Path.Combine(Application.dataPath, "../TestResults");
            string fileName = mode == ETestMode.PlayMode ? "PlayMode.result.txt" : "EditMode.result.txt";
            return Path.GetFullPath(Path.Combine(dir, fileName));
        }

        /// <summary>
        /// 启动测试运行，结果写入默认文件
        /// </summary>
        public static string StartTestRun(ETestMode mode)
        {
            return StartTestRunToFile(mode, GetDefaultResultFilePath(mode));
        }

        /// <summary>
        /// 启动测试运行，结果写入指定文件
        /// </summary>
        public static string StartTestRunToFile(ETestMode mode, string resultFilePath)
        {
            if (string.IsNullOrEmpty(resultFilePath))
            {
                throw new ArgumentException("resultFilePath is invalid.", nameof(resultFilePath));
            }

            EnsureCallbackRegistered();

            // 创建目录
            string directory = Path.GetDirectoryName(resultFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 生成运行 ID，存入 EditorPrefs
            string runId = Guid.NewGuid().ToString("N");
            EditorPrefs.SetString(KEY_ACTIVE_RUN_ID, runId);
            EditorPrefs.SetString(KEY_ACTIVE_MODE, mode.ToString());
            EditorPrefs.SetString(KEY_RESULT_FILE, resultFilePath);

            // 写入文件头
            File.WriteAllText(resultFilePath, string.Empty, Encoding.UTF8);
            AppendLine(resultFilePath, $"status:starting");
            AppendLine(resultFilePath, $"runId:{runId}");
            AppendLine(resultFilePath, $"mode:{mode}");
            AppendLine(resultFilePath, $"startedAt:{DateTime.Now:O}");
            AppendLine(resultFilePath, "---");

            // 执行测试
            var testMode = mode == ETestMode.PlayMode ? TestMode.PlayMode : TestMode.EditMode;
            s_Api.Execute(new ExecutionSettings
            {
                filters = new[]
                {
                    new Filter { testMode = testMode }
                }
            });

            YIUIMCPLog.Log($"[TestRunnerBridge] 测试已启动，模式: {mode}，结果文件: {resultFilePath}");
            return resultFilePath;
        }

        /// <summary>
        /// 是否有活跃的测试运行
        /// </summary>
        public static bool HasActiveRun()
        {
            return EditorPrefs.HasKey(KEY_ACTIVE_RUN_ID) && EditorPrefs.HasKey(KEY_RESULT_FILE);
        }

        /// <summary>
        /// 获取当前活跃测试的结果文件路径
        /// </summary>
        public static string GetActiveResultFilePath()
        {
            return EditorPrefs.GetString(KEY_RESULT_FILE, string.Empty);
        }

        /// <summary>
        /// 获取当前活跃测试的模式
        /// </summary>
        public static ETestMode GetActiveTestMode()
        {
            string modeStr = EditorPrefs.GetString(KEY_ACTIVE_MODE, "EditMode");
            return modeStr == "PlayMode" ? ETestMode.PlayMode : ETestMode.EditMode;
        }

        /// <summary>
        /// 读取结果文件内容
        /// </summary>
        public static string ReadResultFile()
        {
            string path = GetActiveResultFilePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return string.Empty;
            }

            try
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception e)
            {
                return $"读取结果文件失败: {e.Message}";
            }
        }

        private static void EnsureCallbackRegistered()
        {
            if (s_Api == null)
            {
                s_Api = ScriptableObject.CreateInstance<TestRunnerApi>();
                s_Api.hideFlags = HideFlags.HideAndDontSave;
            }

            if (s_Callback != null)
            {
                return;
            }

            s_Callback = ScriptableObject.CreateInstance<PersistentCallbacks>();
            s_Callback.hideFlags = HideFlags.HideAndDontSave;

            // 注册回调，优先级 1000 确保优先收到事件
            s_Api.RegisterCallbacks(s_Callback, 1000);

            // 监听 PlayMode 状态变化
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            YIUIMCPLog.Log("[TestRunnerBridge] 回调已注册");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!HasActiveRun())
            {
                return;
            }

            AppendActiveLine($"playModeState:{state}");
        }

        private static void AppendActiveLine(string line)
        {
            string resultFilePath = GetActiveResultFilePath();
            if (string.IsNullOrEmpty(resultFilePath))
            {
                return;
            }

            AppendLine(resultFilePath, line);
        }

        private static void AppendLine(string filePath, string line)
        {
            try
            {
                File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogError($"[TestRunnerBridge] 写入结果文件失败: {e.Message}");
            }
        }

        private static void ClearActiveRun()
        {
            EditorPrefs.DeleteKey(KEY_ACTIVE_RUN_ID);
            EditorPrefs.DeleteKey(KEY_ACTIVE_MODE);
            EditorPrefs.DeleteKey(KEY_RESULT_FILE);
        }

        // ===================================================================
        // 持久化回调 — ScriptableObject 在域重载后由 TestRunnerApi 保持引用
        // ===================================================================
        private sealed class PersistentCallbacks : ScriptableObject, ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                if (!HasActiveRun())
                {
                    return;
                }

                AppendActiveLine("status:running");
                AppendActiveLine($"testCount:{testsToRun.TestCaseCount}");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                if (!HasActiveRun())
                {
                    return;
                }

                AppendActiveLine("status:finished");
                AppendActiveLine($"finishedAt:{DateTime.Now:O}");
                AppendActiveLine($"total:{result.PassCount + result.FailCount + result.SkipCount}|pass:{result.PassCount}|fail:{result.FailCount}|skip:{result.SkipCount}");
                AppendActiveLine($"resultState:{result.ResultState}");

                YIUIMCPLog.Log($"[TestRunnerBridge] 测试完成: Pass={result.PassCount} Fail={result.FailCount} Skip={result.SkipCount}");

                ClearActiveRun();
            }

            public void TestStarted(ITestAdaptor test)
            {
                if (!HasActiveRun() || test == null || test.HasChildren)
                {
                    return;
                }

                AppendActiveLine($"test-start:{test.FullName}");
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!HasActiveRun() || result == null || result.HasChildren)
                {
                    return;
                }

                string status = result.TestStatus == TestStatus.Passed ? "PASS" :
                               result.TestStatus == TestStatus.Failed ? "FAIL" : "SKIP";

                AppendActiveLine($"test:{result.FullName}|{status}|{result.Duration:F3}s");

                if (result.TestStatus == TestStatus.Failed && !string.IsNullOrEmpty(result.Message))
                {
                    AppendActiveLine($"failure:{result.FullName}|{result.Message}");
                }

                // 也输出到 Unity Console
                if (result.TestStatus == TestStatus.Failed)
                {
                    YIUIMCPLog.LogError($"[{status}] {result.FullName}");
                    if (!string.IsNullOrEmpty(result.Message))
                    {
                        YIUIMCPLog.LogError($"  {result.Message}");
                    }
                }
                else
                {
                    YIUIMCPLog.Log($"[{status}] {result.FullName}");
                }
            }
        }
    }
}
