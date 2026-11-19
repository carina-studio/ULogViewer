# ULogViewer 2026.3 中有什么改变
 ---

## 新功能
+ 新增内建日志类型：
    + `glog/klog 日志文件`
    + `Go slog/logrus 日志文件`
    + `Hermes Agent 日志`
    + `Hermes Agent 日志文件`
    + `LM Studio 服务器日志`
    + `LM Studio 服务器日志文件`
    + `Log4j/Logback 日志文件`
    + `Loguru 日志文件`
    + `Monolog 日志文件`
    + `Nginx 服务器错误日志文件`
    + `NLog (UDP)`
    + `NLog 日志文件`
    + `Ollama 服务器日志`
    + `Ollama 服务器日志文件`
    + `Ollama 服务日志`
    + `Ruby Logger 日志文件`
    + `Rust env_logger/tracing 日志文件`
    + `Serilog 日志文件`
    + `spdlog 日志文件`
    + `Spring Boot 日志文件`
+ 新增更多日志类型图标。
+ 新增更多日志属性名称。
+ 新增 `允许数据横跨多个数据包` 日志数据源选项。

## 优化
+ 在空白标签页中显示建议的日志类型以便快速选择。
+ 将 `Apache 服务器存取日志文件` 内建日志类型更名为 `Apache/Nginx 服务器存取日志文件`。

## 行为变更
+ 

## 错误修复
+ 修复 `Apple 设备日志` 日志类型无法解析日志时间戳的问题。
+ 修复 `Apache 服务器错误日志文件` 日志类型无法读取 Apache 2.4 格式日志的问题。
+ 其他错误修复。