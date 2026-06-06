using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace YIUIFramework.Editor.MCP
{
    /// <summary>
    /// Test Runner 桥接层 — 跨越 Domain Reload 的持久化测试执行 + 实时进度追踪
    ///
    /// 核心思路（参考 Locus 方案）：
    /// 1. [InitializeOnLoad] 每次域重载后重新挂载回调
    /// 2. SessionState 存储实时进度计数器（域重载不丢失，不写入磁盘）
    /// 3. 每条 TestFinished 回调立即更新 SessionState 计数 + 追加 TSV 累积文件
    /// 4. EditorApplication.update 轮询 + IsRunActive 反射检测完成状态
    /// 5. 累积文件 + IsRunActive 兜底 RunFinished 丢失场景
    /// </summary>
    [InitializeOnLoad]
    public static class YIUIMCPTestRunnerBridge
    {
        // ── EditorPrefs 键（持久化路径信息，域重载后恢复） ─────────────────
        private const string PREFS_RUN_ID       = "YIUIMCP.TestRunner.RunId";
        private const string PREFS_RESULT_FILE  = "YIUIMCP.TestRunner.ResultFile";
        private const string PREFS_MODE         = "YIUIMCP.TestRunner.Mode";

        // ── SessionState 键（实时进度，域重载不丢失） ────────────────────
        private const string SS_PROGRESS_STATE      = "YIUIMCP.TestRunner.Progress.State";
        private const string SS_PROGRESS_TOTAL      = "YIUIMCP.TestRunner.Progress.Total";
        private const string SS_PROGRESS_COMPLETED  = "YIUIMCP.TestRunner.Progress.Completed";
        private const string SS_PROGRESS_PASSED     = "YIUIMCP.TestRunner.Progress.Passed";
        private const string SS_PROGRESS_FAILED     = "YIUIMCP.TestRunner.Progress.Failed";
        private const string SS_PROGRESS_CURRENT    = "YIUIMCP.TestRunner.Progress.CurrentTest";
        private const string SS_PROGRESS_UPDATED_AT = "YIUIMCP.TestRunner.Progress.UpdatedAtMs";
        private const string SS_PROGRESS_SUMMARY    = "YIUIMCP.TestRunner.Progress.Summary";
        private const string SS_PROGRESS_ERROR      = "YIUIMCP.TestRunner.Progress.Error";

        // ── 实例状态 ────────────────────────────────────────────────────
        private static TestRunnerApi s_Api;
        private static PersistentCallbacks s_Callback;

        // ── 反射 API（域重载后重新解析） ────────────────────────────────
        private static Type      s_TestRunnerApiType;
        private static MethodInfo s_IsRunActiveMethod;
        private static bool      s_ApiResolved;

        // ── 轮询状态 ────────────────────────────────────────────────────
        private static bool   s_PollingActive;
        private static double s_LastPollTime;
        private const  double PollIntervalSeconds = 1.0;
        private static double s_RunStartedTime;   // 记录运行开始时间，用于超时兜底
        private const  double RecoveryTimeoutSeconds = 300; // 5 分钟无响应才兜底

        static YIUIMCPTestRunnerBridge()
        {
            EnsureCallbackRegistered();

            // 域重载后，如果有未完成的测试，恢复轮询并记录重载事件
            if (HasActiveRun())
            {
                AppendActiveLine("domainReload:reloaded");
                AppendActiveLine($"reloadedAt:{DateTime.Now:O}");
                StartPolling();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // 公开 API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>获取默认结果文件路径</summary>
        public static string GetDefaultResultFilePath(ETestMode mode)
        {
            string dir = Path.Combine(Application.dataPath, "../TestResults");
            string fileName = mode == ETestMode.PlayMode ? "PlayMode.result.txt" : "EditMode.result.txt";
            return Path.GetFullPath(Path.Combine(dir, fileName));
        }

        /// <summary>启动测试运行，结果写入默认文件</summary>
        public static string StartTestRun(ETestMode mode)
        {
            return StartTestRunToFile(mode, GetDefaultResultFilePath(mode));
        }

        /// <summary>启动测试运行，结果写入指定文件</summary>
        public static string StartTestRunToFile(ETestMode mode, string resultFilePath)
        {
            if (string.IsNullOrEmpty(resultFilePath))
                throw new ArgumentException("resultFilePath is invalid.", nameof(resultFilePath));

            EnsureCallbackRegistered();

            // 创建目录
            string directory = Path.GetDirectoryName(resultFilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // 生成运行 ID，写入 EditorPrefs
            string runId = Guid.NewGuid().ToString("N");
            EditorPrefs.SetString(PREFS_RUN_ID, runId);
            EditorPrefs.SetString(PREFS_MODE, mode.ToString());
            EditorPrefs.SetString(PREFS_RESULT_FILE, resultFilePath);

            // 初始化 SessionState 进度
            SessionState.SetString(SS_PROGRESS_STATE, "starting");
            SessionState.SetInt(SS_PROGRESS_TOTAL, 0);
            SessionState.SetInt(SS_PROGRESS_COMPLETED, 0);
            SessionState.SetInt(SS_PROGRESS_PASSED, 0);
            SessionState.SetInt(SS_PROGRESS_FAILED, 0);
            SessionState.SetString(SS_PROGRESS_CURRENT, "");
            SessionState.SetString(SS_PROGRESS_SUMMARY, "");
            SessionState.SetString(SS_PROGRESS_ERROR, "");
            SessionState.SetString(SS_PROGRESS_UPDATED_AT,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());

            // 写入结果文件头
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
                filters = new[] { new Filter { testMode = testMode } }
            });

            YIUIMCPLog.Log($"[TestRunnerBridge] 测试已启动，模式: {mode}，结果文件: {resultFilePath}");

            // 启动进度轮询
            StartPolling();

            return resultFilePath;
        }

        /// <summary>是否有活跃的测试运行</summary>
        public static bool HasActiveRun()
        {
            return EditorPrefs.HasKey(PREFS_RUN_ID);
        }

        /// <summary>获取当前活跃测试的结果文件路径</summary>
        public static string GetActiveResultFilePath()
        {
            return EditorPrefs.GetString(PREFS_RESULT_FILE, "");
        }

        /// <summary>获取当前活跃测试的模式</summary>
        public static ETestMode GetActiveTestMode()
        {
            string modeStr = EditorPrefs.GetString(PREFS_MODE, "EditMode");
            return modeStr == "PlayMode" ? ETestMode.PlayMode : ETestMode.EditMode;
        }

        /// <summary>获取累积测试结果的 TSV 文件路径</summary>
        private static string GetAccumFilePath(string runId)
        {
            string dir = Path.Combine(Application.dataPath, "../TestResults");
            return Path.GetFullPath(Path.Combine(dir, $"accum-{runId}.tsv"));
        }

        /// <summary>读取结果文件内容</summary>
        public static string ReadResultFile()
        {
            // 优先当前活跃运行的结果文件
            string path = GetActiveResultFilePath();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try { return File.ReadAllText(path, Encoding.UTF8); }
                catch (Exception e) { return $"读取结果文件失败: {e.Message}"; }
            }

            // 活跃运行已结束，检查默认路径是否有结果文件
            string defaultPath = GetDefaultResultFilePath(ETestMode.PlayMode);
            if (File.Exists(defaultPath))
            {
                try { return File.ReadAllText(defaultPath, Encoding.UTF8); }
                catch { }
            }

            defaultPath = GetDefaultResultFilePath(ETestMode.EditMode);
            if (File.Exists(defaultPath))
            {
                try { return File.ReadAllText(defaultPath, Encoding.UTF8); }
                catch { }
            }

            return "";
        }

        /// <summary>获取实时进度报告</summary>
        public static string GetProgressReport()
        {
            string state     = SessionState.GetString(SS_PROGRESS_STATE, "idle");
            int total        = SessionState.GetInt(SS_PROGRESS_TOTAL, 0);
            int completed    = SessionState.GetInt(SS_PROGRESS_COMPLETED, 0);
            int passed       = SessionState.GetInt(SS_PROGRESS_PASSED, 0);
            int failed       = SessionState.GetInt(SS_PROGRESS_FAILED, 0);
            string current   = SessionState.GetString(SS_PROGRESS_CURRENT, "");
            string summary   = SessionState.GetString(SS_PROGRESS_SUMMARY, "");
            string error     = SessionState.GetString(SS_PROGRESS_ERROR, "");
            string updatedAt = SessionState.GetString(SS_PROGRESS_UPDATED_AT, "");

            string runId     = EditorPrefs.GetString(PREFS_RUN_ID, "");
            string mode      = EditorPrefs.GetString(PREFS_MODE, "");
            string resultFile = EditorPrefs.GetString(PREFS_RESULT_FILE, "");

            string statusLabel = (state switch
            {
                "starting" => "启动中",
                "running"  => "运行中",
                "finished" => "已完成",
                "error"    => "出错",
                _          => "空闲"
            });

            var sb = new StringBuilder();
            sb.AppendLine($"状态: {statusLabel}");
            sb.AppendLine($"模式: {mode}");
            sb.AppendLine($"运行ID: {runId}");
            if (!string.IsNullOrEmpty(resultFile))
                sb.AppendLine($"结果文件: {resultFile}");

            if (total > 0 || completed > 0)
            {
                sb.AppendLine($"---");
                int pct = total > 0 ? (completed * 100 / total) : 0;
                sb.AppendLine($"进度: {completed}/{total} ({pct}%)");
                sb.AppendLine($"通过: {passed}");
                sb.AppendLine($"失败: {failed}");

                if (!string.IsNullOrEmpty(current))
                    sb.AppendLine($"当前测试: {current}");
            }

            if (!string.IsNullOrEmpty(summary))
                sb.AppendLine($"\n汇总: {summary}");

            if (!string.IsNullOrEmpty(error))
                sb.AppendLine($"\n错误: {error}");

            if (!string.IsNullOrEmpty(updatedAt))
            {
                long ms = long.Parse(updatedAt);
                var dt = DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime();
                sb.AppendLine($"\n更新时间: {dt:HH:mm:ss}");
            }

            // 如果已完成，追加完整的文件结果
            if (state == "finished" && !string.IsNullOrEmpty(resultFile) && File.Exists(resultFile))
            {
                try
                {
                    string fileContent = File.ReadAllText(resultFile, Encoding.UTF8);
                    sb.AppendLine($"\n=== 完整结果 ===\n{fileContent}");
                }
                catch { }
            }

            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════════════════
        // 回调注册
        // ═══════════════════════════════════════════════════════════════

        private static void EnsureCallbackRegistered()
        {
            if (s_Api == null)
            {
                s_Api = ScriptableObject.CreateInstance<TestRunnerApi>();
                s_Api.hideFlags = HideFlags.HideAndDontSave;
            }
            if (s_Callback != null)
                return;

            s_Callback = ScriptableObject.CreateInstance<PersistentCallbacks>();
            s_Callback.hideFlags = HideFlags.HideAndDontSave;
            s_Api.RegisterCallbacks(s_Callback, 1000);

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            YIUIMCPLog.Log("[TestRunnerBridge] 回调已注册");
        }

        // ═══════════════════════════════════════════════════════════════
        // 反射 API 解析（域重载后重新解析）
        // ═══════════════════════════════════════════════════════════════

        private static void EnsureApiResolved()
        {
            if (s_ApiResolved && s_TestRunnerApiType != null)
                return;

            try
            {
                s_TestRunnerApiType = Type.GetType(
                    "UnityEditor.TestTools.TestRunner.Api.TestRunnerApi, UnityEditor.TestRunner");

                if (s_TestRunnerApiType != null)
                {
                    s_IsRunActiveMethod = s_TestRunnerApiType.GetMethod(
                        "IsRunActive", BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(string) }, null);
                }

                s_ApiResolved = s_TestRunnerApiType != null;
            }
            catch
            {
                s_ApiResolved = false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // 轮询 — 检测完成 + 兜底 RunFinished 丢失
        // ═══════════════════════════════════════════════════════════════

        private static void StartPolling()
        {
            if (s_PollingActive) return;
            s_PollingActive    = true;
            s_LastPollTime     = EditorApplication.timeSinceStartup;
            s_RunStartedTime   = EditorApplication.timeSinceStartup;
            EditorApplication.update += PollCallback;
        }

        private static void StopPolling()
        {
            if (!s_PollingActive) return;
            s_PollingActive  = false;
            EditorApplication.update -= PollCallback;
        }

        private static void PollCallback()
        {
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - s_LastPollTime < PollIntervalSeconds) return;
                s_LastPollTime = now;

                string state = SessionState.GetString(SS_PROGRESS_STATE, "");
                if (state == "finished" || state == "error")
                {
                    StopPolling();
                    return;
                }

                // 域重载后重新挂载回调
                EnsureCallbackRegistered();

                // 如果有未完成的测试在跑但 SessionState 未初始化（域重载导致），恢复状态
                string runId = EditorPrefs.GetString(PREFS_RUN_ID, "");
                if (string.IsNullOrEmpty(runId)) return;

                if (string.IsNullOrEmpty(state) || state == "idle")
                {
                    // 域重载后 SessionState 被重置，但我们从文件知道有运行中的测试
                    SessionState.SetString(SS_PROGRESS_STATE, "running");
                    int testCount = ExtractTestCountFromResultFile();
                    if (testCount > 0)
                        SessionState.SetInt(SS_PROGRESS_TOTAL, testCount);
                }

                // 检查 IsRunActive（仅用于进度显示，不做兜底恢复）
                EnsureApiResolved();
                bool active = true;
                if (s_IsRunActiveMethod != null)
                {
                    try
                    {
                        object val = s_IsRunActiveMethod.Invoke(null, new object[] { runId });
                        active = val is bool b && b;
                    }
                    catch { }
                }

                // 只有在长期超时后且 IsRunActive=false 时才兜底
                if (!active && state == "running")
                {
                    double elapsed = now - s_RunStartedTime;
                    if (elapsed >= RecoveryTimeoutSeconds)
                    {
                        string summary = RecoverSummaryFromAccumFile(runId);
                        SessionState.SetString(SS_PROGRESS_SUMMARY, summary);
                        SessionState.SetString(SS_PROGRESS_STATE, "finished");
                        AppendActiveLine("status:finished (recovered by poll)");
                        AppendActiveLine($"finishedAt:{DateTime.Now:O}");
                        AppendActiveLine(summary);
                        ClearActiveRun();
                        StopPolling();
                        YIUIMCPLog.Log($"[TestRunnerBridge] 轮询恢复完成: {summary}");
                    }
                }

                // 检查 EditorApplication 是否还在 PlayMode
                if (EditorApplication.isPlaying)
                {
                    // 还在播放模式，测试仍在进行
                    UpdateProgress(current: $"(playing) {SessionState.GetString(SS_PROGRESS_CURRENT, "")}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TestRunnerBridge] 轮询异常: {ex.Message}");
            }
        }

        /// <summary>从结果文件提取 testCount 行</summary>
        private static int ExtractTestCountFromResultFile()
        {
            try
            {
                string path = GetActiveResultFilePath();
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return 0;
                foreach (string line in File.ReadAllLines(path))
                {
                    if (line.StartsWith("testCount:"))
                    {
                        int count;
                        if (int.TryParse(line.Substring("testCount:".Length), out count))
                            return count;
                    }
                }
            }
            catch { }
            return 0;
        }

        /// <summary>从累积 TSV 文件恢复汇总</summary>
        private static string RecoverSummaryFromAccumFile(string runId)
        {
            try
            {
                string file = GetAccumFilePath(runId);
                if (!File.Exists(file))
                    return "finished (no individual results)";

                int passed = 0, failed = 0, skipped = 0;
                foreach (string line in File.ReadAllLines(file))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] parts = line.Split('\t');
                    if (parts.Length < 1) continue;
                    string s = parts[0].ToLowerInvariant();
                    if (s.Contains("pass") || s == "passed")
                        passed++;
                    else if (s.Contains("skip") || s.Contains("inconclusive"))
                        skipped++;
                    else
                        failed++;
                }

                return $"total:{passed + failed + skipped}|pass:{passed}|fail:{failed}|skip:{skipped} (recovered)";
            }
            catch
            {
                return "finished (summary unavailable)";
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PlayMode 状态变化
        // ═══════════════════════════════════════════════════════════════

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!HasActiveRun()) return;
            AppendActiveLine($"playModeState:{state}");
        }

        // ═══════════════════════════════════════════════════════════════
        // 文件写入
        // ═══════════════════════════════════════════════════════════════

        private static void AppendActiveLine(string line)
        {
            string path = GetActiveResultFilePath();
            if (string.IsNullOrEmpty(path)) return;
            AppendLine(path, line);
        }

        private static void AppendLine(string filePath, string line)
        {
            try { File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8); }
            catch (Exception e) { Debug.LogError($"[TestRunnerBridge] 写入失败: {e.Message}"); }
        }

        private static void ClearActiveRun()
        {
            EditorPrefs.DeleteKey(PREFS_RUN_ID);
            EditorPrefs.DeleteKey(PREFS_MODE);
            EditorPrefs.DeleteKey(PREFS_RESULT_FILE);
        }

        /// <summary>更新进度 SessionState</summary>
        private static void UpdateProgress(string state = null, int? completed = null, int? passed = null,
            int? failed = null, string current = null, string summary = null, string error = null)
        {
            if (state != null)
                SessionState.SetString(SS_PROGRESS_STATE, state);
            if (completed.HasValue)
                SessionState.SetInt(SS_PROGRESS_COMPLETED, completed.Value);
            if (passed.HasValue)
                SessionState.SetInt(SS_PROGRESS_PASSED, passed.Value);
            if (failed.HasValue)
                SessionState.SetInt(SS_PROGRESS_FAILED, failed.Value);
            if (current != null)
                SessionState.SetString(SS_PROGRESS_CURRENT, current);
            if (summary != null)
                SessionState.SetString(SS_PROGRESS_SUMMARY, summary);
            if (error != null)
                SessionState.SetString(SS_PROGRESS_ERROR, error);

            SessionState.SetString(SS_PROGRESS_UPDATED_AT,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
        }

        // ═══════════════════════════════════════════════════════════════
        // 持久化回调 — 每测试写入累积文件 + 更新 SessionState
        // ═══════════════════════════════════════════════════════════════

        private sealed class PersistentCallbacks : ScriptableObject, ICallbacks
        {
            private int _completed;

            public void RunStarted(ITestAdaptor testsToRun)
            {
                if (!HasActiveRun()) return;

                int total = testsToRun.TestCaseCount;
                SessionState.SetInt(SS_PROGRESS_TOTAL, total);
                _completed = 0;

                AppendActiveLine("status:running");
                AppendActiveLine($"testCount:{total}");
                UpdateProgress("running");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                if (!HasActiveRun()) return;

                string summary = $"total:{result.PassCount + result.FailCount + result.SkipCount}|" +
                                 $"pass:{result.PassCount}|fail:{result.FailCount}|skip:{result.SkipCount}";

                AppendActiveLine("status:finished");
                AppendActiveLine($"finishedAt:{DateTime.Now:O}");
                AppendActiveLine(summary);
                AppendActiveLine($"resultState:{result.ResultState}");

                YIUIMCPLog.Log($"[TestRunnerBridge] 测试完成: Pass={result.PassCount} Fail={result.FailCount} Skip={result.SkipCount}");

                SessionState.SetInt(SS_PROGRESS_COMPLETED,
                    result.PassCount + result.FailCount + result.SkipCount);
                UpdateProgress("finished", summary: summary);
                ClearActiveRun();
            }

            public void TestStarted(ITestAdaptor test)
            {
                if (!HasActiveRun() || test == null || test.HasChildren) return;

                AppendActiveLine($"test-start:{test.FullName}");
                UpdateProgress(current: test.FullName);
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!HasActiveRun() || result == null || result.HasChildren) return;

                _completed++;

                string status = result.TestStatus == TestStatus.Passed ? "PASS" :
                               result.TestStatus == TestStatus.Failed ? "FAIL" : "SKIP";

                int passed = SessionState.GetInt(SS_PROGRESS_PASSED, 0);
                int failed = SessionState.GetInt(SS_PROGRESS_FAILED, 0);

                if (result.TestStatus == TestStatus.Passed) passed++;
                else if (result.TestStatus == TestStatus.Failed) failed++;

                // 写结果文件
                AppendActiveLine($"test:{result.FullName}|{status}|{result.Duration:F3}s");
                if (result.TestStatus == TestStatus.Failed && !string.IsNullOrEmpty(result.Message))
                    AppendActiveLine($"failure:{result.FullName}|{result.Message}");

                // 写累积 TSV 文件（兜底用）
                try
                {
                    string runId = EditorPrefs.GetString(PREFS_RUN_ID, "");
                    if (!string.IsNullOrEmpty(runId))
                    {
                        string accFile = GetAccumFilePath(runId);
                        string dir = Path.GetDirectoryName(accFile);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                        string line = string.Join("\t",
                            result.TestStatus.ToString(),
                            result.FullName ?? "",
                            (result.Message ?? "").Replace('\n', ' '),
                            result.Duration.ToString("F3"),
                            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());

                        File.AppendAllText(accFile, line + "\n", new UTF8Encoding(false));
                    }
                }
                catch { /* best-effort */ }

                // 更新 SessionState 进度
                UpdateProgress(
                    completed: _completed,
                    passed: passed,
                    failed: failed,
                    current: ""
                );

                // 输出到 Console
                if (result.TestStatus == TestStatus.Failed)
                {
                    YIUIMCPLog.LogError($"[{status}] {result.FullName}");
                    if (!string.IsNullOrEmpty(result.Message))
                        YIUIMCPLog.LogError($"  {result.Message}");
                }
                else
                {
                    YIUIMCPLog.Log($"[{status}] {result.FullName}");
                }
            }
        }
    }
}
