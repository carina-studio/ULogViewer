﻿# ULogViewer 3.0 中有什麼改變
 ---

## 新功能
+ **正規表示式** 、 **日期與時間格式** 及 **時間間距格式** 的格式化文字色彩標示。
+ 以色彩標示日誌中符合文字篩選的文字內容。
+ 以色彩標示與選取日誌之 PID 及 TID 相同之 PID 及 TID。
+ 支援使用 **「標準輸出 (stdout)」** 日誌資料來源時以指定的命令列殼層 (Command-Line Shell) 執行指令。
+ 支援在 **「建立新的分頁」** 按鈕點一下右鍵以直接選取日誌類型，並設定至新的分頁。
+ 在 **「選取/變更日誌類型」** 按鈕旁新增選單按鈕以快速選取日誌類型。
+ 支援新增開始自或結束至另一個動作時距分析規則的新規則。
+ 支援使用指定的日誌屬性值轉換成為日誌等級。
+ 支援設定不同顏色的日誌類型、日誌分析規則集以及日誌分析腳本圖示。
+ 新增內建日誌類型：
    + Apache 伺服器存取日誌檔案
    + Apache 伺服器錯誤日誌檔案
    + Apple 裝置日誌
    + Apple 裝置模擬器日誌

+ 新增內建日誌類型範本：
    + 特定 Apple 裝置日誌
    + 特定 Apple 裝置模擬器日誌

+ 新增內建字型：
    + IBM Plex Mono
    + Roboto
    + Roboto Mono
    + Source Code Pro

+ 新增 **「使用緊密版面配置」** 設定以提供較小螢幕之裝置使用。
+ 支援使用 **Python 3.4** 作為腳本語言。
+ 新增 **「數量」** 及 **「位元組大小」** 屬性至日誌分析結果。
+ 自動產生選取日誌分析結果之 **「時距」** 、 **「數量」** 及 **「位元組大小」** 的統計結果。
+ 在 **macOS** 上新增 **Homebrew** 目錄為預設路徑以搜尋指令。

## 改善
+ 允許編輯內建日誌類型並自動建立為新的日誌類型。
+ 在 **Linux** 上自動選取適合的使用者介面縮放比例。
+ 輸入日期與時間格式時顯示結果範例。
+ 改善工具列項目排版。
+ 更多的日誌類型圖示。
+ 改善文字格式編輯的使用者體驗。
+ 支援在 **macOS** 上顯示進度於 Dock 圖示。
+ 改善日誌篩選的效能與記憶體使用。
+ 更新腳本的內部執行流程。
+ 其他使用者介面及體驗改善。

## 行為變更
+ 在 **macOS** 上改為使用 **⌘** 按鍵進行項目多選，而非 **Ctrl** 按鍵。
+ 同步在 **macOS** 上的應用程式啟用與背景行為。

## 錯誤修正
+ 其他錯誤修正。