using Aliyun.Api.LOG;
using Aliyun.Api.LOG.Data;
using Aliyun.Api.LOG.Request;
using Aliyun.Api.LOG.Response;
using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

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
            const int batchSize = 10000; // 设定触发上传的阈值

            try
            {

                foreach (var log in Helpers.ParseLogFile(logStore, filePath))
                {
                    logs.Add(log);
                    if (logs.Count >= batchSize)
                    {
                        Console.WriteLine($"[{logStore}] Uploading {logs.Count} logs...");
                        Helpers.UploadLogs(_client, logStore, logs);
                        logs.Clear();
                    }
                }

                // 上传剩余的日志
                if (logs.Count > 0)
                {
                    Console.WriteLine($"[{logStore}] Uploading remaining {logs.Count} logs...");
                    Helpers.UploadLogs(_client, logStore, logs);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during log processing: {ex.Message}");
            }

        }
    }
}
