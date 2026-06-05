"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.mcpClient = void 0;
const child_process_1 = require("child_process");
const path = __importStar(require("path"));
/**
 * MCP Stdio 客户端
 * 用于启动 MCP Server 子进程并通过 Stdio 通信
 */
class MCPClient {
    constructor() {
        this.process = null;
        this.requestId = 0;
        this.pendingRequests = new Map();
        this.buffer = '';
        this.initialized = false;
    }
    /**
     * 启动 MCP Server 子进程
     */
    async start() {
        if (this.process) {
            console.log('[MCP Client] 已经启动，跳过');
            return;
        }
        console.log('[MCP Client] 正在启动 MCP Server 子进程...');
        // 启动当前 index.js 作为子进程（Stdio 模式）
        const indexPath = path.join(__dirname, 'index.js');
        this.process = (0, child_process_1.spawn)('node', [indexPath], {
            cwd: __dirname,
            stdio: ['pipe', 'pipe', 'inherit'], // stdin/stdout 用于 MCP，stderr 显示日志
            env: { ...process.env, MCP_STDIO_MODE: '1' } // 标记为 Stdio 模式
        });
        // 监听响应
        this.process.stdout.on('data', (data) => {
            this.handleResponse(data);
        });
        // 错误处理
        this.process.on('error', (err) => {
            console.error('[MCP Client] 子进程错误:', err);
        });
        this.process.on('exit', (code) => {
            console.log(`[MCP Client] 子进程退出，代码: ${code}`);
            this.process = null;
            this.initialized = false;
        });
        // 初始化握手
        await this.initialize();
        console.log('[MCP Client] MCP Server 子进程已启动并初始化完成');
    }
    /**
     * 调用 MCP 工具
     */
    async callTool(name, params) {
        if (!this.initialized) {
            throw new Error('MCP Client 未初始化');
        }
        const id = ++this.requestId;
        const request = {
            jsonrpc: "2.0",
            id,
            method: "tools/call",
            params: { name, arguments: params }
        };
        return new Promise((resolve, reject) => {
            // 超时保护（10 分钟）
            const timeout = setTimeout(() => {
                if (this.pendingRequests.has(id)) {
                    this.pendingRequests.delete(id);
                    reject(new Error(`请求超时: ${name}`));
                }
            }, 600000);
            this.pendingRequests.set(id, { resolve, reject, timeout });
            // 发送请求
            const jsonStr = JSON.stringify(request) + '\n';
            this.process.stdin.write(jsonStr);
        });
    }
    /**
     * 处理响应数据
     */
    handleResponse(data) {
        this.buffer += data.toString();
        const lines = this.buffer.split('\n');
        this.buffer = lines.pop() || ''; // 保留不完整的行
        for (const line of lines) {
            if (!line.trim())
                continue;
            try {
                const response = JSON.parse(line);
                const pending = this.pendingRequests.get(response.id);
                if (pending) {
                    clearTimeout(pending.timeout);
                    this.pendingRequests.delete(response.id);
                    if (response.error) {
                        pending.reject(new Error(response.error.message || JSON.stringify(response.error)));
                    }
                    else {
                        pending.resolve(response.result);
                    }
                }
            }
            catch (e) {
                // 忽略解析错误（可能是不完整的 JSON 或日志输出）
            }
        }
    }
    /**
     * 初始化握手
     */
    async initialize() {
        const id = ++this.requestId;
        const request = {
            jsonrpc: "2.0",
            id,
            method: "initialize",
            params: {
                protocolVersion: "2024-11-05",
                capabilities: {},
                clientInfo: { name: "uto-http-server", version: "1.0.0" }
            }
        };
        return new Promise((resolve, reject) => {
            const timeout = setTimeout(() => {
                if (this.pendingRequests.has(id)) {
                    this.pendingRequests.delete(id);
                    reject(new Error('初始化超时'));
                }
            }, 10000);
            this.pendingRequests.set(id, {
                resolve: (result) => {
                    this.initialized = true;
                    resolve(result);
                },
                reject,
                timeout
            });
            // 发送初始化请求
            const jsonStr = JSON.stringify(request) + '\n';
            this.process.stdin.write(jsonStr);
        });
    }
    /**
     * 停止 MCP Client
     */
    stop() {
        if (this.process) {
            this.process.kill();
            this.process = null;
            this.initialized = false;
            console.log('[MCP Client] 已停止');
        }
    }
}
// 导出单例
exports.mcpClient = new MCPClient();
