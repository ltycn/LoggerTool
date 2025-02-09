using Aliyun.Api.LOG.Data;
using Aliyun.Api.LOG.Request;
using Aliyun.Api.LOG.Response;
using Aliyun.Api.LOG;
using CsvHelper.Configuration;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;

namespace Logger
{
    public static class Helpers
    {
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

        public static string CleanHeader(string header)
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

        public static uint ParseLogTime(string timestamp)
        {
            // 先定义标准格式（带年份）进行解析
            var formats = new[]
            {
                "yyyy-MM-dd HH:mm:ss",  // 完整格式
                "yyyy-M-d HH:mm:ss"     // 宽松格式
            };

            // 尝试解析标准格式的时间戳
            if (DateTimeOffset.TryParseExact(timestamp, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                return (uint)time.ToUnixTimeSeconds();
            }

            // 如果是 mm-dd-HH-mm-ss:fff 格式，没有年份
            if (Regex.IsMatch(timestamp, @"^\d{2}-\d{2}-\d{2}-\d{2}-\d{2}:\d{3}$"))
            {
                // 补充当前年份到时间戳，并将时间戳格式化为标准格式
                string fullTimestamp = $"{DateTime.UtcNow.Year}-{timestamp.Replace('-', ' ').Replace(':', ' ')}";

                // 使用 TryParseExact 解析完整时间戳
                if (DateTimeOffset.TryParseExact(fullTimestamp, "yyyy MM HH mm ss fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
                {
                    return (uint)time.ToUnixTimeSeconds();
                }
            }

            // 解析失败，返回当前 UTC 时间戳
            return (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public static void UploadLogs(LogClient client, string logStore, List<LogItem> logs)
        {
            if (logs.Count == 0)
            {
                Console.WriteLine("No logs to upload.");
                return;
            }

            const int maxRetries = 5; // 最大重试次数
            const int retryDelayMilliseconds = 2000; // 重试延迟时间（毫秒）

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // 创建 PutLogsRequest 请求对象
                    var putLogsReqError = new PutLogsRequest
                    {
                        Project = Program.Project,
                        Logstore = logStore,
                        Source = Environment.MachineName,
                        Topic = "FileMode",
                        LogItems = logs
                    };

                    // 同步上传日志
                    PutLogsResponse putLogRespError = client.PutLogs(putLogsReqError);
                    SetConsoleColor(ConsoleColor.Green);
                    Console.WriteLine($"[{logStore}] Logs uploaded successfully.");
                    Console.ResetColor();
                    return; // 上传成功，退出方法
                }
                catch (Exception ex)
                {
                    // 上传失败时重试
                    Console.WriteLine($"Log upload attempt {attempt} failed: {ex.Message}");

                    if (attempt == maxRetries)
                    {
                        Console.WriteLine("Max retries reached. Log upload failed.");
                        throw;
                    }

                    Console.WriteLine($"Retrying in {retryDelayMilliseconds / 1000} seconds...");
                    Thread.Sleep(retryDelayMilliseconds);
                }
            }
        }

        private static void SetConsoleColor(ConsoleColor color)
        {
            Console.ForegroundColor = color;
        }
    }
}
