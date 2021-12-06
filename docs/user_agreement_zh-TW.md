---
title: ULogViewer
---

# 使用協議
- 版本：1.0。
- 更新時間：2021/12/6。

這是 ```ULogViewer``` 的使用協議，您應該要在使用 ```ULogViewer``` 之前詳細閱讀本協議。
使用協議可能會在未來有所更新，您可以在 ```ULogViewer``` 網站中查看。
當您開始使用 ```ULogViewer``` 表示您同意本使用協議。

## 適用範圍
```ULogViewer``` 為開放原始碼專案，以下所指 ```ULogViewer``` **僅包括** 與下列頁面所提供之可執行檔或壓縮檔內容完全相同之版本：
* [ULogViewer 網站](https://carina-studio.github.io/ULogViewer/)
* [GitHub 上之 ULogViewer 專案頁面及各版本釋出頁面](https://github.com/carina-studio/ULogViewer)

本使用協議適用於您使用 ```ULogViewer``` 時。

## 檔案存取
所有 ```ULogViewer``` 所需之檔案皆存放於 ```ULogViewer``` 目錄內（若您有安裝 ```.NET``` 則亦包含 ```.NET 執行期間``` 之目錄）。當執行 ```ULogViewer``` 且未載入任何日誌時不需要額外的檔案存取。

### 日誌載入時之檔案存取
* 包含原始日誌內容之檔案將以 ```讀取``` 模式開啟。
* 與日誌檔案位於相同目錄之 ```*.ulvmark``` 檔案將以 ```讀取``` 模式開啟。

### 日誌檢視時檔案存取
* 與日誌檔案位於相同目錄之 ```*.ulvmark``` 檔案將以 ```讀寫``` 模式開啟。

### 日誌儲存時之檔案存取
* 寫入日誌內容之檔案將以 ```讀寫``` 模式開啟。
* 與日誌檔案位於相同目錄之 ```*.ulvmark``` 檔案將以 ```讀寫``` 模式開啟。

### 自我升級時之檔案存取
* 下載的升級檔案及應用程式備份將存放於系統之 ```暫存``` 目錄內。

其他由 ```ULogViewer``` 執行檔以外的檔案存取不受本協議之約束。

## 網路存取
```ULogViewer``` 將會在下列狀況存取網路：

### 透過網路載入日誌
如果日誌來源為下列之一則必須存取網路：
* ```HTTP/HTTPS```
* ```TCP 伺服器```
* ```UDP 伺服器```
* ```檔案``` 且指定之檔案不位於本機。

### 檢查應用程式更新
```ULogViewer``` 會定期從 ```ULogViewer``` 網站下載資訊清單以檢查是否有新的應用程式更新。

### 自我更新
以下 4 種資料需要在更新 ```ULogViewer``` 時下載：
* 自動更新程式之資訊清單以選取適合您的自動更新程式。
* ```ULogViewer``` 之資訊清單以選取適合您的升級封裝。
* 自動更新程式封裝。
* ```ULogViewer``` 升級封裝。

其他由 ```ULogViewer``` 執行檔以外的網路存取不受本協議之約束。

## 執行外部命令
當日誌來源為 ```標準輸出 (stdout)``` 時將執行外部命令。您可以在編輯日誌類型之 ```資料來源``` 時在 ```資料來源參數``` 對話方塊中檢視完整的指令及參數列表。

請注意，我們 **不保證** 執行外部指令後的結果，這完全依賴於外部指令及執行檔之行為。這部分必須由您自行確認。

## 變更您的電腦
除了檔案存取，```ULogViewer``` **不會** 變更您電腦的設定。

請注意，我們 **不保證** 執行外部指令後您的電腦不會被變更，這部分需要您自行注意，特別是當 ```ULogViewer``` 在 Windows 上以系統管理員身分執行時。

## 授權及著作權
```ULogViewer``` 是 ```Carina Studio``` 在 [MIT](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE) 授權之下的開放原始碼專案。除了應用程式圖示外，所有圖示皆在 [MIT](https://en.wikipedia.org/wiki/MIT_License) 或 [CC 4.0](https://en.wikipedia.org/wiki/Creative_Commons_license) 授權下使用。您可以在 [MahApps.Metro.IconPacks](https://github.com/MahApps/MahApps.Metro.IconPacks) 了解更多圖示相關資訊與授權。

應用程式圖示由 [Freepik](https://www.freepik.com/) 提供並發布於 [Flaticon](https://www.flaticon.com/)。

載入至 ```ULogViewer``` 或由 ```ULogViewer``` 儲存之日誌的授權與著作權不受本協議之約束。您必須自行注意及負責日誌的授權與著作權。

## 聯絡我們
如果您對於本使用協議有任何疑問，可以至 [GitHub](https://github.com/carina-studio/ULogViewer/issues) 提出或寄信至 [carina.software.studio@gmail.com](mailto:carina.software.studio@gmail.com)。


<br/>📔[回到首頁](index.md)