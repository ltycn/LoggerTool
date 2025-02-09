using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipes;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// SDK 命名空间
using Aliyun.Api.LOG.Data;
using Aliyun.Api.LOG.Request;
using Aliyun.Api.LOG.Response;
using Aliyun.Api.LOG;
using System.Linq;

namespace Logger
{
    public class StreamMode
    {
        private readonly string _endpoint;
        private LogClient _client;
        private readonly ConcurrentQueue<(string logStore, LogItem log)> LogQueue = new ConcurrentQueue<(string, LogItem)>();
        private readonly CancellationTokenSource Cts = new CancellationTokenSource();


        public StreamMode(string endpoint)
        {
            _endpoint = endpoint;

            Cts = new CancellationTokenSource();
            LogQueue = new ConcurrentQueue<(string logStore, LogItem log)>();
        }

        private void OnProcessExit(object sender, EventArgs e)
        {
            Console.ResetColor();
        }

        public void Start()
        {
            SetConsoleColor(ConsoleColor.Cyan);
            Console.WriteLine("[StreamMode] Starting...");

            _client = new LogClient(_endpoint, Program.AccessKeyId, Program.AccessKeySecret);
            _client.ConnectionTimeout = _client.ReadWriteTimeout = 10000;

            // 启动后台任务处理日志队列
            Task.Run(() => ProcessLogQueue(Cts.Token));

            // 异步启动所有命名管道服务
            var tasks = Program.PipeToLogStore.Keys.Select(pipeName => Task.Run(() => StartNamedPipeServer(pipeName))).ToArray();
            Task.WhenAll(tasks).Wait();

            Console.ResetColor();
        }

        private void StartNamedPipeServer(string pipeName)
        {
            while (true)
            {
                try
                {
                    using (var pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.In, 10, PipeTransmissionMode.Message, PipeOptions.Asynchronous))
                    {
                        pipeServer.WaitForConnection();
                        SetConsoleColor(ConsoleColor.Green);
                        Console.WriteLine($"[{pipeName}] Connected: Waiting for data...");

                        using (var reader = new StreamReader(pipeServer, Encoding.UTF8))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                EnqueueLog(pipeName, line);
                            }
                        }

                        SetConsoleColor(ConsoleColor.Yellow);
                        Console.WriteLine($"[{pipeName}] Disconnected: Connection closed.");
                    }
                }
                catch (Exception ex)
                {
                    SetConsoleColor(ConsoleColor.Red);
                    Console.WriteLine($"[{pipeName}] Error: {ex.Message}\n{ex.StackTrace}");
                }

                Thread.Sleep(1000);
            }
        }

        private void EnqueueLog(string pipeName, string message)
        {
            if (Program.PipeToLogStore.TryGetValue(pipeName, out var logStore))
            {
                var contents = new Dictionary<string, string>();
                long timestamp = 0;

                // 按逗号分割消息，拆分为键值对
                var pairs = message.Split(',', (char)StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs)
                {
                    var kv = pair.Split('=', (char)2);
                    if (kv.Length == 2)
                    {
                        var key = kv[0].Trim();
                        var value = kv[1].Trim();
                        if (key.Equals("Timestamp", StringComparison.OrdinalIgnoreCase) && long.TryParse(value, out long ts))
                        {
                            timestamp = ts;
                        }
                        else
                        {
                            contents[key] = value;
                        }
                    }
                }

                DateTimeOffset logTime = timestamp > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(timestamp)
                    : DateTimeOffset.UtcNow;

                // 创建 LogItem 对象
                var logItem = new LogItem();
                logItem.Time = (uint)(int)logTime.ToUnixTimeSeconds();
                foreach (var kv in contents)
                {
                    logItem.PushBack(kv.Key, kv.Value);
                }

                // 入队时将 logStore 与 LogItem 一起保存
                LogQueue.Enqueue((logStore, logItem));
            }
        }

        private void ProcessLogQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (LogQueue.IsEmpty)
                {
                    Thread.Sleep(10000); // 如果队列为空，则等待 10 秒
                    continue;
                }

                // 按 logStore 分组，构造一个字典：日志库 -> List<LogItem>
                var logGroups = new Dictionary<string, List<LogItem>>();

                while (LogQueue.TryDequeue(out var logEntry))
                {
                    if (!logGroups.TryGetValue(logEntry.logStore, out var list))
                    {
                        list = new List<LogItem>();
                        logGroups[logEntry.logStore] = list;
                    }
                    list.Add(logEntry.log);
                }

                // 对每个日志库的日志进行上传
                foreach (var entry in logGroups)
                {
                    var putLogsReq = new PutLogsRequest
                    {
                        Project = Program.Project,
                        Logstore = entry.Key,
                        Topic = "StreamMode",
                        LogItems = entry.Value
                    };

                    // 同步上传日志
                    PutLogsResponse response = _client.PutLogs(putLogsReq);
                    SetConsoleColor(ConsoleColor.Blue);
                    Console.WriteLine($"[{entry.Key}] Uploaded: {entry.Value.Count} logs successfully. HTTP Status: {response}");
                }
            }
        }

        private void SetConsoleColor(ConsoleColor color)
        {
            Console.ForegroundColor = color;
        }
    }
}
