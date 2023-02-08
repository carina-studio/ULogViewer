# ULogViewer 使用者協議
 ---
+ 版本：2.0
+ 更新時間：2023/2/8

這是 ULogViewer 的使用者協議，您應該要在使用 ULogViewer 之前詳細閱讀本協議。 使用者協議可能會在未來有所更新，您可以在 ULogViewer 網站中查看。 當您開始使用 ULogViewer 表示您同意本使用者協議。


## 適用範圍
ULogViewer 為基於開放原始碼專案之軟體，以下所指 ULogViewer **僅包括** 與下列頁面所提供之可執行檔或壓縮檔內容完全相同之版本：

+ [ULogViewer 網站](https://carinastudio.azurewebsites.net/ULogViewer/)
+ [GitHub 上之 ULogViewer 專案頁面及各版本釋出頁面](https://github.com/carina-studio/ULogViewer)

本使用者協議適用於您使用 ULogViewer 3.0 及下一份使用者協議所指定之版本之間 (但不包括) 的所有版本。


## 偵錯模式
ULogViewer 包含預設關閉的內建偵錯模式，您可以透過 **「關於 ULogViewer > 以偵錯模式重新啟動」** 啟用偵錯模式。


## 外部相依性

### Android SDK
若要使用 **「Android 裝置日誌」** 及 **「Android 裝置事件日誌」** 日誌類型，您必須先安裝 [Android SDK or Android Studio](https://developer.android.com/studio)。

### Azure 命令列介面 (CLI)
若要使用 **「Azure 命令列介面 (CLI)」**、**「MySQL 資料庫」** 及 **「SQL Server 資料庫」** 資料來源之完整功能，您必須先安裝 [Azure 命令列介面 (CLI)](https://docs.microsoft.com/cli/azure/)。

### Git
若要使用 **「Git 提交紀錄」** 及 **「Git 提交紀錄 (精簡)」** 日誌類型，您必須先安裝 [Git](https://git-scm.com/)。

### Trace 轉換工具
若要在 **macOS/Linux** 上使用 **「Android 裝置系統追蹤」** 日誌類型，您必須先安裝 [Trace 轉換工具](https://perfetto.dev/docs/quickstart/traceconv)。

### Xcode 命令列工具
若要在 **macOS** 上使用 **「Apple 裝置模擬器日誌」** 及 **「特定 Apple 裝置模擬器日誌」** 日誌類型，您必須先安裝 [Xcode 命令列工具](https://developer.apple.com/xcode/)。若您透過安裝 Xcode 以安裝 Xcode 命令列工具，您需要將 **「Xcode > Settings > Locations > Command Line Tools」** 設定為 **「Xcode」** 來啟用。


## 檔案存取
除了系統檔案之外，所有 ULogViewer 所需之檔案皆存放於 ULogViewer 目錄內。當執行 ULogViewer 且未載入/匯入/儲存/匯出資料至/從 ULogViewer 時不需要額外的檔案存取，除了下列之外：

+ 讀取 **/proc/meminfo** 以在 Linux 上取得記憶體資訊。
+ 讀取 **/etc/paths** 以在 macOS 上取得全域路徑列表。
+ 讀/寫系統之暫存目錄以存放執行期間所需資源。
+ 其餘由 .NET 或第三方程式庫之必要檔案存取。

### 日誌載入
+ 包含原始日誌內容之檔案將以 **讀取** 模式開啟。
+ 與日誌檔案位於相同目錄之 \*.ulvmark 檔案將以 **讀取** 模式開啟。

### 日誌檢視
+ 與日誌檔案位於相同目錄之 \*.ulvmark 檔案將以 **讀寫** 模式開啟。

### 日誌儲存
+ 寫入日誌內容之檔案將以 **讀寫** 模式開啟。
+ 與日誌檔案位於相同目錄之 \*.ulvmark 檔案將以 **讀寫** 模式開啟。

### 匯入日誌類型
+ 日誌類型之 \*.json 檔案將以 **讀取** 模式開啟。

### 匯入已定義之文字篩選
+ 已定義文字篩選之 \*.json 檔案將以 **讀取** 模式開啟。

### 匯入日誌分析規則集
+ 日誌分析規則集之 \*.json 檔案將以 **讀取** 模式開啟。

### 匯入日誌分析腳本
+ 日誌分析腳本之 \*.json 檔案將以 **讀取** 模式開啟。

### 匯入日誌資料來源腳本
+ 日誌資料來源腳本之 \*.json 檔案將以 **讀取** 模式開啟。

### 匯出日誌類型
+ 包含匯出日誌類型之 \*.json 檔案將以 **讀寫** 模式開啟。

### 匯出已定義之文字篩選
+ 包含匯出已定義文字篩選之 \*.json 檔案將以 **讀寫** 模式開啟。

### 匯出日誌分析規則集
+ 包含匯出日誌分析規則集之 \*.json 檔案將以 **讀寫** 模式開啟。

### 匯出日誌分析腳本
+ 包含匯出日誌分析腳本之 \*.json 檔案將以 **讀寫** 模式開啟。

### 匯出日誌資料來源腳本
+ 包含匯出日誌資料來源腳本之 \*.json 檔案將以 **讀寫** 模式開啟。

### 自我升級
+ 下載的升級檔案及應用程式備份將存放於系統之暫存目錄內。

其他由 ULogViewer 執行檔以外的檔案存取不受本協議之約束。


## 網路存取
ULogViewer 將會在下列狀況存取網路：

### 透過網路載入日誌
如果日誌來源為下列之一則必須存取網路：

+ **Azure 命令列介面 (CLI)**。
+ **HTTP/HTTPS**。
+ **MySQL 資料庫**。
+ **SQL Server 資料庫**。
+ **TCP 伺服器**。
+ **UDP 伺服器**。
+ **檔案** 且指定之檔案不位於本機。
+ 會存取網路之 **日誌資料來源腳本**。

### 網路連線測試
ULogViewer 會連線至下列伺服器以確認網路連線狀態：

+ [Cloudflare](https://www.cloudflare.com/)
+ [Google DNS](https://dns.google/)
+ [OpenDNS](https://www.opendns.com/)

ULogViewer 會連線至下列伺服器以確認裝置的公開 [IP 位址](https://zh.wikipedia.org/wiki/IP%E5%9C%B0%E5%9D%80)：

+ [https://ipv4.icanhazip.com](https://ipv4.icanhazip.com/)
+ [http://checkip.dyndns.org](http://checkip.dyndns.org/)

### 啟用 ULogViewer 專業版
ULogViewer 會在下列情況與 [Carina Studio](https://carinastudio.azurewebsites.net/) 伺服器連線：

+ 啟用 ULogViewer 專業版。
+ 當您完成啟用 ULogViewer 專業版且使用 ULogViewer 時。

### 檢查應用程式更新
ULogViewer 會定期從 ULogViewer 網站下載資訊清單以檢查是否有新的應用程式更新。

### 自我更新
以下 4 種資料需要在更新 ULogViewer 時下載：

+ 自動更新程式之資訊清單以選取適合您的自動更新程式。
+ ULogViewer 之資訊清單以選取適合您的升級封裝。
+ 自動更新程式封裝。
+ ULogViewer 升級封裝。

其他由 ULogViewer 執行檔以外的網路存取不受本協議之約束。


## 執行外部命令
在執行 ULogViewer 時有些必要情況需要執行外部命令：

+ 執行 **dotnet** 以確認在裝置上安裝的 .NET 版本。
+ 執行 **explorer** 以在 Windows 上開啟檔案總管。
+ 執行 **open** macOS 上開啟 Finder。
+ 執行 **defaults** 以確認在 macOS 上的系統語系與佈景設定。
+ 執行 **nautilus** 或 **xdg-open** 以在 Linux 上開啟檔案管理器。
+ 執行 **cmd** 以在必要時更新 Windows 上的 PATH 環境變數。
+ 執行 **osascript** 以在必要時更新 macOS 上的 /etc/paths。

除了上述必要情況外，當日誌來源為 **「Azure 命令列介面 (CLI)」** 或 **「標準輸出 (stdout)」** 時將執行外部命令。您可以在編輯日誌類型之 **「資料來源」** 時在 **「資料來源參數」** 對話方塊中檢視完整的指令及參數列表。

請注意，我們 **不保證** 執行外部指令後的結果，這完全依賴於外部指令及執行檔之行為。這部分必須由您自行確認。


## 變更您的電腦
除了檔案存取以及下列的情況，ULogViewer **不會** 變更您電腦的設定。

請注意，我們 **不保證** 執行外部指令後您的電腦不會被變更，這部分需要您自行注意，特別是當 ULogViewer 在 Windows 上以系統管理員身分執行時。

### 在 Windows 上編輯 PATH 環境變數

#### 新增路徑
新增之路徑會被設定至 **使用者** 的 PATH 環境變數。

#### 移除路徑
若移除之路徑定義於 **電腦** 的 PATH 環境變數，ULogViewer 將會以 **系統管理員** 權限啟動 cmd 指令以更新 PATH 環境變數。

### 在 macOS 上編輯 /etc/paths
ULogViewer 將會以 **管理員** 權限啟動 **osascript** 指令以更新 /etc/paths 檔案。

### 執行腳本
在 ULogViewer 中執行之腳本可以存取 .NET 之功能，包含檔案存取、網路存取及變更電腦等。因此執行腳本可能會變更甚至損壞您的電腦。您在執行腳本之前必須仔細檢查其內容。


## 授權及著作權
ULogViewer 是 Carina Studio 在 [MIT](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE) 授權之下的開放原始碼專案。除了應用程式圖示外，所有圖示皆在 [MIT](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE) 或 [CC 4.0](https://en.wikipedia.org/wiki/Creative_Commons_license) 授權下使用。您可以在 [MahApps.Metro.IconPacks](https://github.com/MahApps/MahApps.Metro.IconPacks) 了解更多圖示相關資訊與授權。

應用程式圖示修改自 [Freepik](https://www.freepik.com/) 提供並發布於 [Flaticon](https://www.flaticon.com/) 之圖示。

內建字型 **「Roboto」** 及 **「Roboto Mono」** 在 [Apache License 2.0](http://www.apache.org/licenses/LICENSE-2.0) 授權下使用及發佈，**「IBM Plex Mono」** 及 **「Source Code Pro」** 在 [Open Font License](https://scripts.sil.org/cms/scripts/page.php?site_id=nrsi&id=OFL) 授權下使用及發佈。

載入至 ULogViewer 或由 ULogViewer 儲存之日誌的授權與著作權不受本協議之約束。您必須自行注意及負責日誌的授權與著作權。


## 聯絡我們
如果您對於本使用者協議有任何疑問，可以至 [GitHub](https://github.com/carina-studio/ULogViewer/issues) 提出或寄信至 [carina.software.studio@gmail.com](mailto:carina.software.studio@gmail.com)。