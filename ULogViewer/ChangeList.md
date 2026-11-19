# Changes in ULogViewer 2026.3
 ---

## New Features
+ Added built-in log profiles:
    + `glog/klog Log Files`
    + `Go slog/logrus Log Files`
    + `Hermes Agent Log`
    + `Hermes Agent Log Files`
    + `LM Studio Server Log`
    + `LM Studio Server Log Files`
    + `Log4j/Logback Log Files`
    + `Loguru Log Files`
    + `Monolog Log Files`
    + `Nginx Error Log Files`
    + `NLog (UDP)`
    + `NLog Log Files`
    + `Ollama Server Log`
    + `Ollama Server Log Files`
    + `Ollama Service Log`
    + `OpenClaw Log Files`
    + `Ruby Logger Log Files`
    + `Rust env_logger/tracing Log Files`
    + `Serilog Log Files`
    + `spdlog Log Files`
    + `Spring Boot Log Files`
+ Added more log profile icons.
+ Added more log property names.
+ Added `Allow Data Across Multiple Datagrams` log data source option.

## Improvement
+ Added suggested log profiles to empty tabs for quick selection.
+ Renamed `Apache Access Log Files` built-in log profile to `Apache/Nginx Access Log Files`.

## Behavior Changes
+ 

## Bug Fixing
+ Fixed an issue that timestamps of logs read by the `Apple Devices Log` log profile cannot be parsed.
+ Fixed an issue that logs in the format of Apache 2.4 cannot be read by the `Apache Error Log Files` log profile.
+ Minor bug fixing.