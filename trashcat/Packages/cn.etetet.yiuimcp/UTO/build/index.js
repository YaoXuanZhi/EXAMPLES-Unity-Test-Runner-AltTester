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
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
const index_js_1 = require("@modelcontextprotocol/sdk/server/index.js");
const stdio_js_1 = require("@modelcontextprotocol/sdk/server/stdio.js");
const types_js_1 = require("@modelcontextprotocol/sdk/types.js");
const axios_1 = __importDefault(require("axios"));
const fs = __importStar(require("fs"));
const path = __importStar(require("path"));
const http = __importStar(require("http"));
// 端口默认 3212，为支持动态端口，从 UTO 目录下的 .port 读取
function getUnityPort() {
    try {
        const candidates = [
            path.resolve(__dirname, "../.port"),
            path.resolve(__dirname, "../../.port"),
        ];
        for (const p of candidates) {
            if (fs.existsSync(p)) {
                const content = fs.readFileSync(p, "utf-8").trim();
                const port = parseInt(content, 10);
                if (!isNaN(port)) {
                    return port;
                }
            }
        }
    }
    catch (e) {
        // Ignore
    }
    return 3212;
}
function getUnityUrl() {
    const port = getUnityPort();
    return `http://127.0.0.1:${port}`;
}
// 辅助函数：获取 InstanceId
async function getInstanceId() {
    try {
        const agent = new http.Agent({ keepAlive: false, maxSockets: 1 });
        const url = getUnityUrl();
        const response = await axios_1.default.get(`${url}/health`, {
            timeout: 3000,
            httpAgent: agent
        });
        agent.destroy();
        return response.data?.serverId || null;
    }
    catch (e) {
        return null;
    }
}
// 健康检查
async function healthCheck() {
    // 为每次请求创建新的 Agent
    const agent = new http.Agent({
        keepAlive: false,
        maxSockets: 1
    });
    try {
        const url = getUnityUrl();
        const response = await axios_1.default.get(`${url}/health`, {
            timeout: 3000,
            httpAgent: agent
        });
        // 立即销毁 Agent
        agent.destroy();
        return response.status === 200;
    }
    catch (e) {
        // 确保 Agent 被销毁
        agent.destroy();
        return false;
    }
}
// Unity RPC 调用
async function callUnityRpc(method, params = {}) {
    const url = getUnityUrl();
    const port = getUnityPort();
    // 为每次请求创建新的 Agent
    const agent = new http.Agent({
        keepAlive: false,
        maxSockets: 1
    });
    try {
        const response = await axios_1.default.post(`${url}/rpc`, {
            jsonrpc: "2.0",
            method: method,
            params: params,
            id: Date.now(),
        }, {
            timeout: 30000,
            httpAgent: agent,
            headers: {
                'Connection': 'close' // 强制关闭连接
            }
        });
        // 立即销毁 Agent
        agent.destroy();
        if (response.data.error) {
            throw new Error(response.data.error.message);
        }
        return response.data.result;
    }
    catch (error) {
        // 确保 Agent 被销毁
        agent.destroy();
        if (error.code === 'ECONNREFUSED') {
            throw new Error(`Unity 连接被拒绝 (端口: ${port}, Unity 是否运行?)`);
        }
        if (error.code === 'ETIMEDOUT' || error.code === 'ECONNABORTED') {
            throw new Error(`Unity 响应超时 (端口: ${port})`);
        }
        throw error;
    }
}
// MCP Server
const server = new index_js_1.Server({
    name: "uto",
    version: "1.0.0",
}, {
    capabilities: {
        tools: {},
    },
});
// ListTools - 尝试从 Unity MCP 获取，否则返回空列表
server.setRequestHandler(types_js_1.ListToolsRequestSchema, async () => {
    try {
        const result = await callUnityRpc("ListTools", {});
        if (result && result.tools) {
            return { tools: result.tools };
        }
    }
    catch (error) {
        // Unity MCP 不支持 ListTools，返回空列表
    }
    return { tools: [] };
});
// CallTool - 直接转发到 Unity MCP（心跳检测在 http-server 层处理）
server.setRequestHandler(types_js_1.CallToolRequestSchema, async (request) => {
    const { name, arguments: args } = request.params;
    // 直接转发到 Unity MCP
    try {
        const result = await callUnityRpc(name, args || {});
        if (result && result.success) {
            return {
                content: [{ type: "text", text: result.message || "Success" }]
            };
        }
        else {
            return {
                content: [{ type: "text", text: result?.message || "Unknown error" }],
                isError: true
            };
        }
    }
    catch (error) {
        return {
            content: [{ type: "text", text: `RPC Failed: ${error.message}` }],
            isError: true
        };
    }
});
// Start server
async function main() {
    const transport = new stdio_js_1.StdioServerTransport();
    await server.connect(transport);
    console.error("UTO Server running on stdio (pure proxy mode)");
}
// 根据启动参数决定运行模式
if (require.main === module) {
    const args = process.argv.slice(2);
    const isStdioMode = process.env.MCP_STDIO_MODE === '1';
    if (args.includes('--http') && !isStdioMode) {
        // HTTP 模式（无需传递端口，自动从 .port 文件读取）
        Promise.resolve().then(() => __importStar(require('./http-server.js'))).then(({ startHttpServer }) => {
            startHttpServer().catch((error) => {
                console.error("HTTP Server 启动失败:", error);
                process.exit(1);
            });
        });
    }
    else {
        // Stdio 模式
        main().catch((error) => {
            console.error("Fatal error:", error);
            process.exit(1);
        });
    }
}
