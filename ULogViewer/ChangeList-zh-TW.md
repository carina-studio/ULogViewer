# ULogViewer 2026.3 中有什麼改變
 ---

## 新功能
+ 新增內建日誌類型：
    + `glog/klog 日誌檔案`
    + `Go slog/logrus 日誌檔案`
    + `Hermes Agent 日誌`
    + `Hermes Agent 日誌檔案`
    + `LM Studio 伺服器日誌`
    + `LM Studio 伺服器日誌檔案`
    + `Log4j/Logback 日誌檔案`
    + `Loguru 日誌檔案`
    + `Monolog 日誌檔案`
    + `Nginx 伺服器錯誤日誌檔案`
    + `NLog (UDP)`
    + `NLog 日誌檔案`
    + `Ollama 伺服器日誌`
    + `Ollama 伺服器日誌檔案`
    + `Ollama 服務日誌`
    + `OpenClaw 日誌檔案`
    + `Ruby Logger 日誌檔案`
    + `Rust env_logger/tracing 日誌檔案`
    + `Serilog 日誌檔案`
    + `spdlog 日誌檔案`
    + `Spring Boot 日誌檔案`
+ 新增更多日誌類型圖示。
+ 新增更多日誌屬性名稱。
+ 新增 `允許資料橫跨多個封包` 日誌資料來源選項。

## 改善
+ 於空白分頁中顯示建議的日誌類型以提供快速選取。
+ 將 `Apache 伺服器存取日誌檔案` 內建日誌類型更名為 `Apache/Nginx 伺服器存取日誌檔案`。

## 行為變更
+ 

## 錯誤修正
+ 修正 `Apple 裝置日誌` 日誌類型無法解析日誌時間戳的問題。
+ 修正 `Apache 伺服器錯誤日誌檔案` 日誌類型無法讀取 Apache 2.4 格式日誌的問題。
+ 其他錯誤修正。