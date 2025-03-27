using Aliyun.Api.LOG.Data;
using CsvHelper.Configuration;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Management;

namespace Logger
{
    public static class Helpers
    {
        public static readonly IDictionary<string, string> MachineConfiguration = GetMachineConfiguration();
        public static List<LogItem> ParseLogFile(string logStore, string filePath)
        {
            var logs = new List<LogItem>();

            // 解析 CSV 类型日志
            if (logStore == "dispatchercsv" || logStore == "mlscenariocsv")
            {
                var reader = new StreamReader(filePath);
                var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));

                // 读取 CSV 数据记录
                var records = csv.GetRecords<dynamic>();
                foreach (var record in records)
                {
                    var logItem = new LogItem();
                    var contents = new Dictionary<string, string>();

                    // 遍历每个字段，确保字段的值非空并且清理列头
                    foreach (var property in (IDictionary<string, object>)record)
                    {
                        // 清洗 header 和内容
                        var cleanKey = CleanHeader(property.Key);  // 清洗 header

                        // 跳过 'time' 字段，不去掉空格
                        var cleanValue = property.Key.Equals("time", StringComparison.OrdinalIgnoreCase)
                            ? property.Value?.ToString()
                            : property.Value?.ToString()?.Replace(" ", "");  // 去掉值中的所有空格

                        // 只保留非空的内容
                        if (!string.IsNullOrEmpty(cleanValue))
                        {
                            contents[cleanKey] = cleanValue;
                        }
                    }

                    // 假设 CSV 中有时间字段 "time"，并将其作为日志时间
                    if (contents.TryGetValue("time", out string timestamp))
                    {
                        contents.Remove("time");
                        logItem.Time = ParseLogTime(timestamp); // 使用 ParseLogTime 设置时间戳
                    }

                    // 将日志内容推入 LogItem
                    foreach (var kv in contents)
                    {
                        logItem.PushBack(kv.Key, kv.Value);
                    }

                    logs.Add(logItem);
                }
            }
            // 解析日志行
            else if (logStore == "dispatcherlog")
            {
                foreach (var line in File.ReadLines(filePath))
                {
                    var match = Regex.Match(line, @"\[(.*?)\](.*)");
                    if (!match.Success) continue;

                    // 使用 ParseLogTime 来解析时间戳
                    var timestamp = match.Groups[1].Value;
                    var logItem = new LogItem();
                    var contents = new Dictionary<string, string>
                    {
                        { "message", match.Groups[2].Value.Trim() }
                    };

                    var timestampParts = timestamp.Split(new[] { ':', '-' }, StringSplitOptions.None);
                    if (timestampParts.Length == 6)
                    {
                        string milliseconds = timestampParts[5].PadLeft(3, '0');  // 确保毫秒是三位数
                        contents["milliseconds"] = milliseconds;
                    }

                    logItem.Time = ParseLogTime(timestamp);

                    foreach (var kv in contents)
                    {
                        logItem.PushBack(kv.Key, kv.Value);
                    }

                    logs.Add(logItem);
                }
            }

            // 新增 autotestlog 的解析逻辑
            else if (logStore == "autotestlog")
            {
                // 从文件名中提取时间戳
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var timestampMatch = Regex.Match(fileName, @"AutoTestResult_(\d{8}-\d{6})");
                if (timestampMatch.Success)
                {
                    var timestamp = timestampMatch.Groups[1].Value;

                    var reader = new StreamReader(filePath);
                    var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));

                    // 读取 CSV 数据记录
                    var records = csv.GetRecords<dynamic>();
                    foreach (var record in records)
                    {
                        var logItem = new LogItem();
                        var contents = new Dictionary<string, string>();

                        // 遍历每个字段，确保字段的值非空并且清理列头
                        foreach (var property in (IDictionary<string, object>)record)
                        {
                            // 清洗 header 和内容
                            var cleanKey = CleanHeader(property.Key);  // 清洗 header
                            var cleanValue = property.Value?.ToString()?.Replace(" ", "");  // 去掉值中的所有空格

                            // 只保留非空的内容
                            if (!string.IsNullOrEmpty(cleanValue))
                            {
                                contents[cleanKey] = cleanValue;
                            }
                        }

                        // 使用文件名中的时间戳作为日志时间
                        logItem.Time = ParseLogTime(timestamp);

                        // 将日志内容推入 LogItem
                        foreach (var kv in contents)
                        {
                            logItem.PushBack(kv.Key, kv.Value);
                        }

                        logs.Add(logItem);
                    }
                }
            }

            return logs;
        }

        private static string CleanHeader(string header)
        {
            if (string.IsNullOrEmpty(header)) return header;

            // 如果字段名以数字开头，添加下划线
            if (char.IsDigit(header[0]))
            {
                header = "_" + header;
            }

            // 替换所有非字母数字字符为下划线
            return Regex.Replace(header, @"[^a-zA-Z0-9_]", "_").ToLowerInvariant();
        }

        private static uint ParseLogTime(string timestamp)
        {
            // 去除方括号和首尾空白
            string cleanedTimestamp = timestamp.Trim().TrimStart('[').TrimEnd(']');

            // 增加一个格式，允许时、分、秒为单数字
            var standardFormats = new[]
            {
                "yyyy-MM-dd HH:mm:ss",   // 处理 2025-01-21 13:24:44
                "yyyy-M-d HH:mm:ss",     // 处理 2025-1-21 13:24:44（月份/日期为单数字，但时、分、秒要求两位）
                "yyyy-M-d H:m:s",        // 处理 2025-1-21 13:26:7 和 2025-1-21 14:0:8（时、分、秒也允许为单数字）
                "MM-dd-HH:mm:ss:fff",     // 处理不带年份的格式（低优先级）
                "yyyyMMdd-HHmmss"
            };

            if (DateTimeOffset.TryParseExact(
                cleanedTimestamp,
                standardFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var standardTime))
            {
                return (uint)standardTime.ToUnixTimeSeconds();
            }

            // 处理自定义格式 [02-09-23:44:24:837] 和 [02-10-00-54-17:178]
            string[] parts = cleanedTimestamp.Split(new[] { '-', ':', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 6 && cleanedTimestamp.Contains("-") && cleanedTimestamp.Contains(":"))
            {
                // 假设年份为当前年，组合成完整时间字符串
                string year = DateTime.UtcNow.Year.ToString();
                string month = parts[0].PadLeft(2, '0');
                string day = parts[1].PadLeft(2, '0');
                string hour = parts[2].PadLeft(2, '0');
                string minute = parts[3].PadLeft(2, '0');
                string second = parts[4].PadLeft(2, '0');

                // 格式化为 yyyy-MM-dd HH:mm:ss
                string fullTimestamp = $"{year}-{month}-{day} {hour}:{minute}:{second}";

                if (DateTimeOffset.TryParseExact(
                    fullTimestamp,
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var customTime))
                {
                    return (uint)customTime.ToUnixTimeSeconds();
                }
            }

            // 解析失败，返回当前 UTC 时间戳
            return (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// 获取计算机的关键配置信息并返回为字典形式，供 LogTags 使用。
        /// 只读取一次并缓存结果。
        /// </summary>
        private static IDictionary<string, string> GetMachineConfiguration()
        {
            var logTags = new Dictionary<string, string>();


            /* TODO: 这里后续添加Dispatcher版本、BIOS信息、Preload版本、机器型号、chid、CPUGPU等信息*/
            // 获取操作系统信息
            var os = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem").Get();
            foreach (ManagementObject obj in os)
            {
                logTags["OS"] = obj["Caption"]?.ToString(); // 操作系统名称
                logTags["OSVersion"] = obj["Version"]?.ToString(); // 操作系统版本
            }

            // 获取内存信息
            var memory = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory").Get();
            foreach (ManagementObject obj in memory)
            {
                logTags["MemorySize"] = (Convert.ToInt64(obj["Capacity"]) / 1024 / 1024 / 1024).ToString() + " GB"; // 内存大小
            }

            // 获取处理器信息
            var processor = new ManagementObjectSearcher("SELECT * FROM Win32_Processor").Get();
            foreach (ManagementObject obj in processor)
            {
                logTags["Processor"] = obj["Name"]?.ToString(); // 处理器名称
            }

            // 获取机器名称
            logTags["MachineName"] = Environment.MachineName;

            return logTags;
        }
        public static void SetConsoleColor(ConsoleColor color)
        {
            Console.ForegroundColor = color;
        }
    }
}
