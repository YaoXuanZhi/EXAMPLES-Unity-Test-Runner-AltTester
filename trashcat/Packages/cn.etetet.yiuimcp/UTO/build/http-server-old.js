"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.startHttpServer = startHttpServer;
const express_1 = __importDefault(require("express"));
const cors_1 = __importDefault(require("cors"));
const mcp_client_1 = require("./mcp-client");
const heartbeat_manager_1 = require("./heartbeat-manager");
const app = (0, express_1.default)();
app.use((0, cors_1.default)());
app.use(express_1.default.json());
// 全局心跳管理器
const heartbeatManager = new heartbeat_manager_1.HeartbeatManager('http://127.0.0.1:3212');
/**
 * 健康检查端点
 */
app.get('/health', (req, res) => {
    res.json({
        status: 'ok',
        service: 'uto-http-server',
        version: '1.0.0'
    });
});
/**
 * 统一调用端点
 * POST /call
 * Body: { tool: string, params: object }
 */
app.post('/call', async (req, res) => {
    const { tool, params } = req.body;
    if (!tool) {
        return res.status(400).json({
            success: false,
            error: '缺少必需字段: tool'
        });
    }
    const timestamp = new Date().toISOString().split('T')[1].split('.')[0];
    console.log(`[${timestamp}] [HTTP] 调用工具: ${tool}`);
    console.log(`[${timestamp}] [HTTP] 参数: ${JSON.stringify(params)}`);
    // 记录开始时间
    const startTime = Date.now();
    try {
        const result = await mcp_client_1.mcpClient.callTool(tool, params || {});
        // 计算耗时
        const duration = Date.now() - startTime;
        // 提取文本结果
        let resultText = '';
        let isError = false;
        if (result.content && Array.isArray(result.content)) {
            resultText = result.content.map((c) => c.text).join('\n');
        }
        else {
            resultText = JSON.stringify(result);
        }
        if (result.isError !== undefined) {
            isError = result.isError;
        }
        console.log(`[${timestamp}] [HTTP] 成功: ${tool}，耗时: ${duration}ms`);
        res.json({
            success: true,
            result: resultText,
            isError: isError,
            duration: duration,
            durationSeconds: (duration / 1000).toFixed(2)
        });
    }
    catch (error) {
        // 计算耗时（失败情况）
        const duration = Date.now() - startTime;
        console.error(`[${timestamp}] [HTTP] 失败: ${tool} - ${error.message}，耗时: ${duration}ms`);
        res.json({
            success: false,
            error: error.message,
            duration: duration,
            durationSeconds: (duration / 1000).toFixed(2),
            debug: {
                tool: tool,
                params: params,
                paramsType: typeof params,
                paramsJson: JSON.stringify(params)
            }
        });
    }
});
/**
 * 批量调用端点
 * POST /batch
 * Body: { tools: [{ name: string, arguments: object }] }
 */
app.post('/batch', async (req, res) => {
    const { tools } = req.body;
    if (!Array.isArray(tools)) {
        return res.status(400).json({
            success: false,
            error: '缺少必需字段: tools (array)'
        });
    }
    const timestamp = new Date().toISOString().split('T')[1].split('.')[0];
    console.log(`[${timestamp}] [HTTP] 批量调用: ${tools.length} 个工具`);
    // 记录开始时间
    const startTime = Date.now();
    // 在开始前获取当前的 InstanceId
    let lastInstanceId = null;
    try {
        const axios = require('axios');
        const healthResponse = await axios.get('http://127.0.0.1:3212/health', { timeout: 3000 });
        lastInstanceId = healthResponse.data?.serverId;
        console.log(`[${timestamp}] [HTTP] 初始 InstanceId: ${lastInstanceId}`);
    }
    catch (e) {
        console.log(`[${timestamp}] [HTTP] 无法获取初始 InstanceId`);
    }
    const results = [];
    for (let i = 0; i < tools.length; i++) {
        const { name, arguments: args } = tools[i];
        if (!name) {
            const failedDuration = Date.now() - startTime;
            return res.json({
                success: false,
                results: results,
                failedAt: i,
                error: `第 ${i + 1} 个工具缺少 name 字段`,
                totalDuration: failedDuration,
                totalDurationSeconds: (failedDuration / 1000).toFixed(2)
            });
        }
        console.log(`[${timestamp}] [HTTP] [${i + 1}/${tools.length}] 执行: ${name}`);
        try {
            // 如果是 Wait 工具，传递上一步的结果和上一次的 InstanceId
            let toolArgs = args || {};
            if (name === "Wait" && i > 0 && results.length > 0) {
                const prevResult = results[results.length - 1];
                toolArgs = {
                    ...toolArgs,
                    _prevResult: prevResult,
                    _oldInstanceId: lastInstanceId // 使用上一次记录的 InstanceId
                };
            }
            const result = await mcp_client_1.mcpClient.callTool(name, toolArgs);
            // 提取文本结果
            let resultText = '';
            let isError = false;
            if (result.content && Array.isArray(result.content)) {
                resultText = result.content.map((c) => c.text).join('\n');
            }
            else {
                resultText = JSON.stringify(result);
            }
            if (result.isError !== undefined) {
                isError = result.isError;
            }
            results.push({
                tool: name,
                success: true,
                result: resultText,
                isError: isError
            });
            console.log(`[${timestamp}] [HTTP] [${i + 1}/${tools.length}] 成功: ${name}`);
            // 智能错误处理：如果收到 "Unknown error"，自动处理 Domain Reload
            if (isError && resultText.includes("Unknown error")) {
                console.log(`[${timestamp}] [HTTP] 检测到 Unknown error，可能是 Domain Reload`);
                // 检查下一个工具是否是 Wait
                if (i + 1 < tools.length && tools[i + 1].name === "Wait") {
                    console.log(`[${timestamp}] [HTTP] 下一个工具是 Wait，继续执行`);
                    continue; // 继续执行 Wait 工具
                }
                else {
                    // 没有 Wait 工具，停止执行
                    const failedDuration = Date.now() - startTime;
                    return res.json({
                        success: false,
                        results: results,
                        failedAt: i,
                        error: `工具 ${name} 执行失败: ${resultText}`,
                        totalDuration: failedDuration,
                        totalDurationSeconds: (failedDuration / 1000).toFixed(2)
                    });
                }
            }
            // 如果工具返回其他错误，停止执行
            if (isError) {
                console.log(`[${timestamp}] [HTTP] 工具 ${name} 返回错误，停止执行`);
                const failedDuration = Date.now() - startTime;
                return res.json({
                    success: false,
                    results: results,
                    failedAt: i,
                    error: `工具 ${name} 执行失败: ${resultText}`,
                    totalDuration: failedDuration,
                    totalDurationSeconds: (failedDuration / 1000).toFixed(2)
                });
            }
        }
        catch (error) {
            console.error(`[${timestamp}] [HTTP] [${i + 1}/${tools.length}] 失败: ${name} - ${error.message}`);
            results.push({
                tool: name,
                success: false,
                error: error.message
            });
            const failedDuration = Date.now() - startTime;
            return res.json({
                success: false,
                results: results,
                failedAt: i,
                error: `工具 ${name} 执行失败: ${error.message}`,
                totalDuration: failedDuration,
                totalDurationSeconds: (failedDuration / 1000).toFixed(2)
            });
        }
    }
    // 计算总耗时
    const endTime = Date.now();
    const totalDuration = endTime - startTime;
    // 全部成功
    console.log(`[${timestamp}] [HTTP] 批量调用完成: ${tools.length} 个工具全部成功，总耗时: ${totalDuration}ms`);
    res.json({
        success: true,
        results: results,
        totalDuration: totalDuration,
        totalDurationSeconds: (totalDuration / 1000).toFixed(2)
    });
});
/**
 * 获取可用工具列表（可选）
 */
app.get('/tools', async (req, res) => {
    try {
        // 这里可以调用 MCP 的 tools/list
        // 暂时返回硬编码列表
        res.json({
            success: true,
            tools: [
                { name: 'Compile', description: '智能编译 Unity 项目' },
                { name: 'Log', description: '写入日志到 Unity Console' },
                { name: 'LogError', description: '写入错误日志到 Unity Console' }
            ]
        });
    }
    catch (error) {
        res.json({
            success: false,
            error: error.message
        });
    }
});
/**
 * 启动 HTTP Server
 */
async function startHttpServer(port = 8090) {
    console.log('[HTTP] 正在启动 UTO HTTP Server...');
    // 先启动 MCP Client
    await mcp_client_1.mcpClient.start();
    console.log('[HTTP] MCP Client 已启动');
    // 启动心跳检测
    heartbeatManager.startHeartbeat();
    // 再启动 HTTP Server
    return new Promise((resolve) => {
        app.listen(port, () => {
            console.log(`[HTTP] ✅ UTO HTTP Server 运行在 http://localhost:${port}`);
            console.log(`[HTTP] 可用端点:`);
            console.log(`[HTTP]   - GET  /health  (健康检查)`);
            console.log(`[HTTP]   - POST /call    (调用单个工具)`);
            console.log(`[HTTP]   - POST /batch   (批量调用工具)`);
            console.log(`[HTTP]   - GET  /tools   (工具列表)`);
            resolve();
        });
    });
}
