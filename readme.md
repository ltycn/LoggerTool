# Platform Logger Tool

## 1. 项目概述

Platform Logger Tool 是一个适用于 Windows 平台的日志采集与上传工具，基于 C# 开发。它支持 **Named Pipe（命名管道）** 方式采集日志，并通过 **阿里云日志服务（Aliyun Logger Tool）** 进行存储和分析。

该工具支持 **实时日志上传** 和 **日志文件批量上传** 两种模式，可满足不同场景需求。

## 2. 核心功能

- **实时日志采集**：监听多个 Named Pipe 并行收集日志。
- **日志解析与存储**：自动解析日志格式并转换为 JSON。
- **异步批量上传**：使用阿里云日志服务 SDK，保证高并发处理能力。
- **日志上传模式**：
  - **实时流模式（Stream Mode）**：适用于高频日志处理，日志采集后即时上传。
  - **文件上传模式（File Mode）**：支持上传历史日志文件（CSV 或文本格式）。

## 3. 使用方式

### **3.1 实时日志上传模式（Stream Mode）**

**适用场景**：
- 适用于实时监控、故障检测等需要快速响应的日志场景。
- 确保日志数据低延迟上传，便于后续分析。

**使用方式**：
```plaintext
Logger.exe /stream
```
启动后，程序自动监听 Named Pipe 并将接收到的日志上传至阿里云。

### **3.2 文件上传模式（File Mode）**

**适用场景**：
- 适用于批量上传历史日志，或离线日志分析。
- 适合 CSV 或文本格式的日志文件。

**使用方式**：
```plaintext
Logger.exe /filemode /logstore <filepath>
```
示例：
```plaintext
Logger.exe /filemode /dispatcherlog C:\logs\dispatcher.csv
```
程序会自动解析日志文件并批量上传至阿里云日志服务。

## 4. 数据格式要求

### **4.1 受支持的Named Pipe 与 LogStore**

| Named Pipe              | LogStore         |
|------------------------|-----------------|
| CpuInfoPipe            | cpuinfolog      |
| DispatcherLogPipe      | dispatcherlog   |
| DispatcherCSVPipe      | dispatchercsv   |
| mlscenariocsvPipe      | mlscenariocsv   |

### **4.2 Named Pipe 数据格式**

- 采用**键值对格式**，各字段使用 `,` 分隔，每条日志数据以 `\n` 结尾。
- 必须包含 `Timestamp` 字段，格式为 Unix 时间戳。
- 示例数据：

```plaintext
CpuUsage=25.58,AvailableMemoryMB=0.003,DiskReadWriteBytes=0.000,ThreadCount=9347,Timestamp=1738826595\n
LogLevel=INFO,Message=Dispatcher started successfully,Timestamp=1738826600\n
```

### **4.3 文件格式要求**

- CSV 文件：需包含表头，字段间用逗号 `,` 分隔。
- 文本日志：每行代表一条日志记录，必须包含 `Timestamp` 字段。

## 5. 预期效果

- **高效日志采集**：支持高并发数据处理，保证日志稳定上传。
- **低延迟数据传输**：实时模式确保日志快速到达云端，支持秒级监控。
- **灵活适配业务需求**：提供实时和批量两种模式，适应不同业务场景。