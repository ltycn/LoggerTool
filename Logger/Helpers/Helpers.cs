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
            var formats = new[]
            {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-M-d HH:mm:ss"
            };

            if (DateTimeOffset.TryParseExact(timestamp, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                return (uint)time.ToUnixTimeSeconds();
            }

            if (Regex.IsMatch(timestamp, @"^\d{2}-\d{2}-\d{2}-\d{2}-\d{2}:\d{1,3}$"))
            {
                string[] parts = timestamp.Split(new[] { '-', ':', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                string fullTimestamp = $"{DateTime.UtcNow.Year}-{parts[0].PadLeft(2, '0')}-{parts[1].PadLeft(2, '0')} {parts[2].PadLeft(2, '0')}:{parts[3].PadLeft(2, '0')}:{parts[4].PadLeft(2, '0')} {parts[5].PadLeft(3, '0')}";

                if (DateTimeOffset.TryParseExact(fullTimestamp, "yyyy-MM-dd HH:mm:ss fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
                {
                    return (uint)time.ToUnixTimeSeconds();
                }

                string adjustedTimestamp = fullTimestamp.Substring(0, fullTimestamp.Length - 1) + "00";

                if (DateTimeOffset.TryParseExact(adjustedTimestamp, "yyyy-MM-dd HH:mm:ss fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
                {
                    return (uint)time.ToUnixTimeSeconds();
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
