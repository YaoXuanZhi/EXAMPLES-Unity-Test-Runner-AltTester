using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace YIUIFramework.Editor.MCP
{
    /// <summary>
    /// 运行测试的参数
    /// </summary>
    [Serializable]
#if YIUI
    [HideLabel]
    [HideReferenceObjectPicker]
#endif
    public class RunTestRunnerParams : YIUIMCPBaseParams
    {
#if YIUI
        [LabelText("测试模式")]
#endif
        public ETestMode testMode = ETestMode.PlayMode;

#if YIUI
        [LabelText("结果输出路径（留空使用默认路径）")]
#endif
        public string outputPath = "";
    }

    /// <summary>
    /// 测试模式枚举
    /// </summary>
    public enum ETestMode
    {
        EditMode,
        PlayMode
    }

    /// <summary>
    /// YIUIMCP运行Test Runner的工具
    /// 使用 YIUIMCPTestRunnerBridge 执行测试，桥接层跨越 Domain Reload 持久化结果
    ///
    /// 启动后立即返回，不阻塞 RPC 管线。
    /// 测试完成后使用 WaitForTestResult 阻塞等待最终结果。
    /// </summary>
    [YIUIMCPTools("RunTestRunner", "运行Unity Test Runner测试，启动后立即返回。使用 WaitForTestResult 等待完成")]
    public class YIUIMCPTools_RunTestRunner : YIUIMCPBaseExecutor<RunTestRunnerParams>
    {
        [MenuItem("Tools/Run PlayMode Tests", priority = 100)]
        public static void RunPlayModeTestsMenu()
        {
            YIUIMCPTestRunnerBridge.StartTestRun(ETestMode.PlayMode);
        }

        [MenuItem("Tools/Run EditMode Tests", priority = 101)]
        public static void RunEditModeTestsMenu()
        {
            YIUIMCPTestRunnerBridge.StartTestRun(ETestMode.EditMode);
        }

        protected override async Task<YIUIMCPResult> Run(RunTestRunnerParams data)
        {
            await Task.CompletedTask;

            // === 检查是否已有测试在运行 ===
            if (YIUIMCPTestRunnerBridge.HasActiveRun())
            {
                string existingFile = YIUIMCPTestRunnerBridge.GetActiveResultFilePath();
                return YIUIMCPResult.FailureLog(
                    $"已有测试在运行中，结果文件: {existingFile}\n" +
                    $"请等待当前测试完成后再启动新测试，或使用 GetTestResult 查看当前进度。");
            }

            // === 通过桥接层启动测试 ===
            string resultFilePath = !string.IsNullOrEmpty(data.outputPath)
                ? YIUIMCPTestRunnerBridge.StartTestRunToFile(data.testMode, data.outputPath)
                : YIUIMCPTestRunnerBridge.StartTestRun(data.testMode);

            string testModeLabel = data.testMode == ETestMode.PlayMode ? "PlayMode" : "EditMode";
            return YIUIMCPResult.Success(
                $"测试已启动\n" +
                $"模式: {testModeLabel}\n" +
                $"结果文件: {resultFilePath}\n" +
                $"---\n" +
                $"测试运行中。使用 GetTestResult 查看实时进度，或使用 WaitForTestResult 等待完成并获取结果。");
        }
    }
}
