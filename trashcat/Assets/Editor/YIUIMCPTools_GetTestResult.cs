using System;
using System.IO;
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
    /// 读取 YIUIMCPTestRunnerBridge 写入的结果文件
    /// </summary>
    [YIUIMCPTools("GetTestResult", "获取 Test Runner 测试结果")]
    public class YIUIMCPTools_GetTestResult : YIUIMCPBaseExecutor<GetTestResultParams>
    {
        protected override Task<YIUIMCPResult> Run(GetTestResultParams data)
        {
            string filePath = data.resultFilePath;

            // 未指定路径时，先看是否有活跃运行的结果文件
            if (string.IsNullOrEmpty(filePath))
            {
                if (YIUIMCPTestRunnerBridge.HasActiveRun())
                {
                    filePath = YIUIMCPTestRunnerBridge.GetActiveResultFilePath();
                }
                else
                {
                    // 使用默认路径
                    filePath = YIUIMCPTestRunnerBridge.GetDefaultResultFilePath(data.testMode);
                }
            }

            if (string.IsNullOrEmpty(filePath))
            {
                return Task.FromResult(YIUIMCPResult.FailureLog("未找到结果文件路径"));
            }

            if (!File.Exists(filePath))
            {
                return Task.FromResult(YIUIMCPResult.FailureLog($"结果文件不存在: {filePath}"));
            }

            try
            {
                string content = File.ReadAllText(filePath);
                bool isRunning = YIUIMCPTestRunnerBridge.HasActiveRun();

                string status = isRunning ? "测试仍在运行中" : "测试已完成";
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
