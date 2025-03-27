using Aliyun.Api.LOG;
using Aliyun.Api.LOG.Data;
using Aliyun.Api.LOG.Request;
using Aliyun.Api.LOG.Response;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Logger
{
    public class FileMode
    {
        private readonly string _endpoint;
        private readonly LogClient _client;

        public FileMode(string endpoint)
        {
            _endpoint = endpoint;
            _client = new LogClient(_endpoint, Program.AccessKeyId, Program.AccessKeySecret);
            _client.ConnectionTimeout = _client.ReadWriteTimeout = 10000;
        }

        public void Start(string logStore, string filePath)
        {
            var logs = new List<LogItem>();
            const int batchSize = 5000; // 设定触发上传的阈值 (行数)
            int totalUploaded = 0; // 用于记录总共上传的日志数量

            try
            {
                foreach (var log in Helpers.ParseLogFile(logStore, filePath))
                {
                    logs.Add(log);
                    if (logs.Count >= batchSize)
                    {
                        Console.WriteLine($"[{logStore}] Uploading {logs.Count} logs...");
                        UploadLogs(_client, logStore, logs);
                        totalUploaded += logs.Count; // 更新上传总量
                        Console.WriteLine($"[{logStore}] Total uploaded logs: {totalUploaded}");
                        logs.Clear();
                    }
                }

                // 上传剩余的日志
                if (logs.Count > 0)
                {
                    Console.WriteLine($"[{logStore}] Uploading remaining {logs.Count} logs...");
                    UploadLogs(_client, logStore, logs);
                    totalUploaded += logs.Count; // 更新上传总量
                    Console.WriteLine($"[{logStore}] Total uploaded logs: {totalUploaded}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during log processing: {ex.Message}");
            }

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
                        LogTags = Helpers.MachineConfiguration,
                        LogItems = logs
                    };

                    // 同步上传日志
                    PutLogsResponse putLogRespError = client.PutLogs(putLogsReqError);

                    Helpers.SetConsoleColor(ConsoleColor.Green);
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
    }
}