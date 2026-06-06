using System;
using System.Threading.Tasks;

namespace YIUIFramework.Editor.MCP
{
    /// <summary>
    /// 获取测试结果参数
    /// </summary>
    [Serializable]
#if YIUI
    [HideLabel]
    [HideReferenceObjectPicker]
#endif
    public class GetTestResultParams : YIUIMCPBaseParams
    {
#if YIUI
        [LabelText("测试结果文件路径（留空使用默认路径）")]
#endif
        public string resultFilePath = "";

#if YIUI
        [LabelText("测试模式（默认路径时使用）")]
#endif
        public ETestMode testMode = ETestMode.PlayMode;
    }

    /// <summary>
    /// 获取 Test Runner 测试结果的工具
    /// 优先读取 YIUIMCPTestRunnerBridge 的实时进度报告，
    /// 测试完成后自动附加完整结果文件内容。
    /// </summary>
    [YIUIMCPTools("GetTestResult", "获取 Test Runner 测试结果（含实时进度）")]
    public class YIUIMCPTools_GetTestResult : YIUIMCPBaseExecutor<GetTestResultParams>
    {
        protected override Task<YIUIMCPResult> Run(GetTestResultParams data)
        {
            // 使用桥接层的进度报告（含实时计数 + 完整结果）
            string progressReport = YIUIMCPTestRunnerBridge.GetProgressReport();

            if (!string.IsNullOrEmpty(progressReport) && progressReport != "空闲")
            {
                return Task.FromResult(YIUIMCPResult.Success(progressReport));
            }

            // 退化：从默认结果文件读取
            string filePath = data.resultFilePath;
            if (string.IsNullOrEmpty(filePath))
            {
                if (YIUIMCPTestRunnerBridge.HasActiveRun())
                {
                    filePath = YIUIMCPTestRunnerBridge.GetActiveResultFilePath();
                }
                else
                {
                    filePath = YIUIMCPTestRunnerBridge.GetDefaultResultFilePath(data.testMode);
                }
            }

            if (string.IsNullOrEmpty(filePath))
            {
                return Task.FromResult(YIUIMCPResult.FailureLog("未找到结果文件路径"));
            }

            if (!System.IO.File.Exists(filePath))
            {
                return Task.FromResult(YIUIMCPResult.FailureLog($"结果文件不存在: {filePath}"));
            }

            try
            {
                string content = System.IO.File.ReadAllText(filePath);
                string status = YIUIMCPTestRunnerBridge.HasActiveRun() ? "测试仍在运行中" : "测试已完成";
                string result = $"=== 测试结果 ({status}) ===\n文件: {filePath}\n---\n{content}";
                return Task.FromResult(YIUIMCPResult.Success(result));
            }
            catch (Exception e)
            {
                return Task.FromResult(YIUIMCPResult.FailureLog($"读取结果文件失败: {e.Message}"));
            }
        }
    }
}
