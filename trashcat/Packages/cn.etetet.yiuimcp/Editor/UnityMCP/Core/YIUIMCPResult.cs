using System.Collections.Generic;
using Newtonsoft.Json;

namespace YIUIFramework.Editor.MCP
{
    /// <summary>
    /// MCP 工具描述符 — 用于 ListTools 返回结构
    /// </summary>
    public class YIUIMCPToolDescriptor
    {
        /// <summary>
        /// 工具名称（对应 method 字段）
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// 工具描述
        /// </summary>
        public string description { get; set; }

        /// <summary>
        /// 可选：JSON Schema 格式的参数描述
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object inputSchema { get; set; }
    }

    /// <summary>
    /// MCP 原子工具的统一返回结构
    /// 没有特殊情况不允许修改
    /// </summary>
    public struct YIUIMCPResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool success { get; set; }

        /// <summary>
        /// 给 AI 看的信息 (包含成功数据或失败原因)
        /// </summary>
        public string message { get; set; }

        /// <summary>
        /// 工具列表（仅 ListTools 等查询类方法使用）
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<YIUIMCPToolDescriptor> tools { get; set; }

        public static YIUIMCPResult Success(string message = "success")
        {
            return new YIUIMCPResult
            {
                success = true,
                message = message
            };
        }

        public static YIUIMCPResult SuccessLog(string message = "success")
        {
            YIUIMCPLog.Log(message);
            return new YIUIMCPResult
            {
                success = true,
                message = message
            };
        }

        public static YIUIMCPResult Failure(string message)
        {
            return new YIUIMCPResult
            {
                success = false,
                message = message
            };
        }

        public static YIUIMCPResult FailureLog(string message)
        {
            YIUIMCPLog.LogError(message);
            return new YIUIMCPResult
            {
                success = false,
                message = message
            };
        }

        /// <summary>
        /// 创建工具列表查询结果
        /// </summary>
        public static YIUIMCPResult ToolsList(List<YIUIMCPToolDescriptor> toolsList)
        {
            return new YIUIMCPResult
            {
                success = true,
                message = $"Found {toolsList.Count} tools",
                tools = toolsList
            };
        }
    }
}