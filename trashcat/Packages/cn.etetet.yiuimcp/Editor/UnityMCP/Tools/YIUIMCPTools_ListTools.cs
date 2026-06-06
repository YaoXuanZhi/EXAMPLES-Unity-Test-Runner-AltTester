using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace YIUIFramework.Editor.MCP
{
    /// <summary>
    /// ListTools — 返回所有已注册的 MCP 工具列表
    /// 供 UTO 调用，实现动态工具发现
    /// </summary>
    [YIUIMCPTools("ListTools", "获取所有可用的 MCP 工具列表")]
    public class YIUIMCPTools_ListTools : YIUIMCPBaseExecutor<YIUIMCPBaseParams>
    {
        protected override Task<YIUIMCPResult> Run(YIUIMCPBaseParams data)
        {
            var toolsList = new List<YIUIMCPToolDescriptor>();

            foreach (var kvp in YIUIMCPToolsRegistry.Tools)
            {
                var toolInfo = kvp.Value;
                var descriptor = new YIUIMCPToolDescriptor
                {
                    name = toolInfo.Name,
                    description = toolInfo.Description,
                    inputSchema = GenerateInputSchema(toolInfo.ParamType)
                };
                toolsList.Add(descriptor);
            }

            return Task.FromResult(YIUIMCPResult.ToolsList(toolsList));
        }

        /// <summary>
        /// 从参数类型反射生成简单的 JSON Schema
        /// </summary>
        private static object GenerateInputSchema(Type paramType)
        {
            if (paramType == null || paramType == typeof(YIUIMCPBaseParams))
            {
                return null;
            }

            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            // 收集 YIUIMCPBaseParams 中用户可配置的字段
            //（timeoutMs / delayBeforeMs / delayAfterMs 默认隐藏，但不属于工具本身的语义参数）
            // 我们只暴露工具本身定义的参数，跳过基类字段

            foreach (var prop in paramType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (prop.Name == "ChangeTimeoutMs") continue;

                var schema = GetPropertySchema(prop);
                if (schema != null)
                {
                    properties[ToCamelCase(prop.Name)] = schema;

                    // 值类型且非 nullable 的视为必填
                    if (prop.PropertyType.IsValueType && !IsNullable(prop))
                    {
                        required.Add(ToCamelCase(prop.Name));
                    }
                }
            }

            if (properties.Count == 0)
            {
                return null;
            }

            var schemaObj = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties
            };

            if (required.Count > 0)
            {
                schemaObj["required"] = required;
            }

            return schemaObj;
        }

        private static Dictionary<string, object> GetPropertySchema(PropertyInfo prop)
        {
            var type = prop.PropertyType;

            // 处理 Nullable<T>
            if (IsNullable(prop))
            {
                type = Nullable.GetUnderlyingType(type);
            }

            string jsonType;
            var schema = new Dictionary<string, object>();

            if (type == typeof(string))
            {
                jsonType = "string";
            }
            else if (type == typeof(bool))
            {
                jsonType = "boolean";
            }
            else if (type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double))
            {
                jsonType = "number";
                // 对于 int/long 限制为 integer
                if (type == typeof(int) || type == typeof(long))
                {
                    schema["type"] = "integer";
                }
                else
                {
                    schema["type"] = "number";
                }
            }
            else if (type.IsEnum)
            {
                jsonType = "string";
                var enumValues = Enum.GetNames(type);
                schema["enum"] = enumValues;
            }
            else
            {
                return null; // 不支持的类型
            }

            schema["type"] = jsonType;
            schema["description"] = GetPropertyDescription(prop);

            return schema;
        }

        private static bool IsNullable(PropertyInfo prop)
        {
            return prop.PropertyType.IsGenericType &&
                   prop.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        private static string GetPropertyDescription(PropertyInfo prop)
        {
            // 尝试读取 LabelText 特性（Sirenix.OdinInspector）
            foreach (var attr in prop.GetCustomAttributesData())
            {
                if (attr.AttributeType.Name == "LabelTextAttribute")
                {
                    if (attr.ConstructorArguments.Count > 0)
                    {
                        var arg = attr.ConstructorArguments[0].Value;
                        if (arg != null)
                        {
                            return arg.ToString();
                        }
                    }
                }
            }

            return prop.Name;
        }

        private static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 2)
            {
                return name?.ToLowerInvariant();
            }

            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
