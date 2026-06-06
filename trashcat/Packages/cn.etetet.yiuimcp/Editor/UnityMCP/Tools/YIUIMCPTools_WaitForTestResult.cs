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
    public class WaitForTestResultParams : YIUIMCPBaseParams
    {
        // 基类默认 timeoutMs=30000，但本工具不做内部轮询，无需长超时
    }

    /// <summary>
    /// 检查测试是否完成，并返回当前结果。
    ///
    /// ── 设计思路 ──────────────────────────────────────────
    ///   不内置轮询（HTTP 30s 超时 + Domain Reload 断连让长轮询不可靠），
    ///   改为每次调用做一次状态检查：
    ///     - 测试已完成 → 返回完整结果 report
    ///     - 测试仍在运行 → 返回当前进度，调用方 10s 后重试
    ///     - 无活跃测试 → 返回提示
    ///
    /// ── 推荐链路 ──────────────────────────────────────────
    ///   1. RunTestRunner(testMode:"PlayMode")         → 启动测试
    ///   2. WaitForTestResult()                        → 如果返回"仍在运行":
    ///   3. (等待 10 秒)
    ///   4. WaitForTestResult()                        → 重复直到返回完整结果
    /// </summary>
    [YIUIMCPTools("WaitForTestResult", "检查测试是否完成。未完成则返回当前进度，调用方应 10s 后重试")]
    public class YIUIMCPTools_WaitForTestResult : YIUIMCPBaseExecutor<WaitForTestResultParams>
    {
        protected override async Task<YIUIMCPResult> Run(WaitForTestResultParams data)
        {
            await Task.CompletedTask;

            // =========================================================
            // 情况 1: 没有活跃测试
            // =========================================================
            if (!YIUIMCPTestRunnerBridge.HasActiveRun())
            {
                string progress = YIUIMCPTestRunnerBridge.GetProgressReport();
                string lastResult = YIUIMCPTestRunnerBridge.ReadResultFile();

                if (!string.IsNullOrEmpty(lastResult))
                {
                    return YIUIMCPResult.Success(
                        $"测试已完成！\n{progress}\n---\n{lastResult}");
                }

                return YIUIMCPResult.Success(
                    $"当前无活跃测试。请先使用 RunTestRunner 启动测试。\n{progress}");
            }

            // =========================================================
            // 情况 2: 有活跃测试，返回当前进度
            // =========================================================
            string current = YIUIMCPTestRunnerBridge.GetProgressReport();
            string partial = YIUIMCPTestRunnerBridge.ReadResultFile();

            return YIUIMCPResult.Success(
                $"测试仍在运行中。请 10 秒后再次调用 WaitForTestResult 检查。\n" +
                $"当前进度:\n{current}\n" +
                $"部分结果文件:\n---\n{partial}");
        }
    }
}
