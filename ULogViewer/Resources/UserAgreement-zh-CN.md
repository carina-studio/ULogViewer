# ULogViewer 用户协议
 ---
+ 版本：2.6
+ 更新时间：2026/5/4

这是 ULogViewer 的用户协议，您应该要在使用 ULogViewer 之前详细阅读本协议。 用户协议可能会在未来有所更新，您可以在 ULogViewer 网站中查看。 当您开始使用 ULogViewer 表示您同意本用户协议。


## 适用范围
ULogViewer 为 Carina Studio 之开放源代码项目，以下所指 ULogViewer **仅包括** 与下列页面所提供之可执行文件或压缩包内容完全相同之版本：

+ [ULogViewer 网站](https://carinastudio.azurewebsites.net/ULogViewer/)
+ [GitHub 上之 ULogViewer 项目页面及各版本发布页面](https://github.com/carina-studio/ULogViewer)

若您通过源代码自行构建 ULogViewer，您使用该构建之版本仅受 [MIT](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE) 授权之约束，不受本用户协议之约束。

本用户协议适用于您使用 ULogViewer 2026.0 及下一份用户协议所指定之版本之间 (但不包括) 的所有版本。


## 调试模式
ULogViewer 包含预设关闭的内建调试模式，您可以透过 **「关于 ULogViewer > 以调试模式重新启动」** 启用调试模式。


## 外部依赖

### Android SDK 平台工具
若要使用 **「Android 设备日志」** 、 **「Android 设备事件日志」** 、 **「Android 设备系统追踪」** 、 **「Android 系统内存监控」** 、 **「Android 进程内存监控」** 、 **「特定 Android 设备事件日志」** 、 **「特定 Android 设备日志」** 及 **「特定 Android 设备系统追踪」** 日志类型，您必须先安装 [Android SDK 平台工具](https://developer.android.com/tools/releases/platform-tools) 或 [Android Studio](https://developer.android.com/studio)。

### Azure 命令行界面 (CLI)
若要使用 **「Azure 命令行界面 (CLI)」**、**「MySQL 数据库」** 及 **「SQL Server 数据库」** 数据源之完整功能，您必须先安装 [Azure 命令行界面 (CLI)](https://docs.microsoft.com/cli/azure/)。

### Git
若要使用 **「Git 提交记录」** 及 **「Git 提交记录 (精简)」** 日志类型，您必须先安装 [Git](https://git-scm.com/)。

### libimobiledevice
若要使用 **「Apple 设备日志」** 及 **「特定 Apple 设备日志」** 日志类型，您必须先安装 [libimobiledevice](https://libimobiledevice.org/)。

+ [Windows 用户](https://github.com/iFred09/libimobiledevice-windows)
+ [macOS 用户](https://formulae.brew.sh/formula/libimobiledevice)
+ [Linux 用户](https://command-not-found.com/idevicesyslog)

### Trace 转换工具
若要在 **macOS/Linux** 上使用 **「Android 设备系统追踪」** 及 **「特定 Android 设备系统追踪」** 日志类型，您必须先安装 [Trace 转换工具](https://perfetto.dev/docs/quickstart/traceconv)。

### Xcode 命令行工具
若要在 **macOS** 上使用 **「Apple 设备模拟器日志」** 及 **「特定 Apple 设备模拟器日志」** 日志类型，您必须先安装 [Xcode 命令行工具](https://developer.apple.com/xcode/)。若您透过安装 Xcode 以安装 Xcode 命令行工具，您需要将 **「Xcode > Settings > Locations > Command Line Tools」** 设定为 **「Xcode」** 来启用。


## 文件访问
除了系统文件之外，所有 ULogViewer 所需之文件皆存放于 ULogViewer 目录内。在 **macOS** 上，由于应用程序签名之要求，应用程序数据将存放于 **Application Support** 目录（`~/Library/Application Support/CarinaStudio/ULogViewer/`）而非应用程序包内。在 **Windows** 及 **Linux** 上，应用程序数据存放于应用程序目录本身。当执行 ULogViewer 且未加载/导入/保存/导出数据至/从 ULogViewer 时不需要额外的文件访问，除了下列之外：

+ 读取 **/proc/meminfo** 以在 Linux 上获取内存信息。
+ 读取 **/etc/paths** 以在 macOS 上获取全局路径列表。
+ 读/写系统之临时目录以存放运行期间所需资源。
+ 其余由 .NET 或第三方程序库之必要文件访问。

### 日志加载
+ 包含原始日志内容之文件将以 **读取** 模式打开。
+ 与日志文件位于相同目录之 \*.ulvmark 文件将以 **读取** 模式打开。

### 日志查看
+ 与日志文件位于相同目录之 \*.ulvmark 文件将以 **读写** 模式打开。

### 日志保存
+ 写入日志内容之文件将以 **读写** 模式打开。
+ 与日志文件位于相同目录之 \*.ulvmark 文件将以 **读写** 模式打开。

### 导入日志类型
+ 日志类型之 \*.json 文件将以 **读取** 模式打开。

### 导入预定义文字筛选
+ 预定义文字筛选之 \*.json 文件将以 **读取** 模式打开。

### 导入日志分析规则集
+ 日志分析规则集之 \*.json 文件将以 **读取** 模式打开。

### 导入日志分析脚本
+ 日志分析脚本之 \*.json 文件将以 **读取** 模式打开。

### 导入日志数据源脚本
+ 日志数据源脚本之 \*.json 文件将以 **读取** 模式打开。

### 导出日志类型
+ 包含导出日志类型之 \*.json 文件将以 **读写** 模式打开。

### 导出预定义文字筛选
+ 包含导出预定义文字筛选之 \*.json 文件将以 **读写** 模式打开。

### 导出日志分析规则集
+ 包含导出日志分析规则集之 \*.json 文件将以 **读写** 模式打开。

### 导出日志分析脚本
+ 包含导出日志分析脚本之 \*.json 文件将以 **读写** 模式打开。

### 导出日志数据源脚本
+ 包含导出日志数据源脚本之 \*.json 文件将以 **读写** 模式打开。

### 自我升级
+ 下载的升级文件及应用程序备份将存放于系统之临时目录内。

### 导出应用程序日志
+ 包含应用程序日志的 \*.zip 文件将以 **读写** 模式打开。

其他由 ULogViewer 可执行文件以外的文件访问不受本协议之约束。


## 网络访问
ULogViewer 将会在下列状况访问网络：

### 通过网络加载日志
如果日志来源为下列之一则必须访问网络：

+ **Azure 命令行界面 (CLI)**。
+ **HTTP/HTTPS**。
+ **MySQL 数据库**。
+ **SQL Server 数据库**。
+ **TCP 服务器**。
+ **UDP 服务器**。
+ **文件** 且指定之文件不位于本机。
+ 会访问网络之 **日志数据源脚本**。

### 网络连接测试
ULogViewer 会连接至下列服务器以确认网络连接状态：

+ [Cloudflare](https://www.cloudflare.com/)
+ [Google DNS](https://dns.google/)
+ [OpenDNS](https://www.opendns.com/)

ULogViewer 会连接至下列服务器以确认设备的公开 [IP 地址](https://zh.wikipedia.org/wiki/IP%E5%9C%B0%E5%9D%80)：

+ [https://ipv4.icanhazip.com](https://ipv4.icanhazip.com/)
+ [http://checkip.dyndns.org](http://checkip.dyndns.org/)

### 检查应用程序更新
ULogViewer 会定期从 [GitHub](https://github.com/carina-studio/ULogViewer) 下载清单以检查是否有新的应用程序更新。

### 自我更新
以下 4 种数据需要在更新 ULogViewer 时下载：

+ 自动更新程序之清单以选取适合您的自动更新程序。
+ ULogViewer 之清单以选取适合您的升级安装包。
+ 自动更新程序安装包。
+ ULogViewer 升级安装包。

### 捕获内存快照
[dotMemory](https://www.jetbrains.com/dotmemory/) 是 Carina Studio 用以分析内存使用状况的主要工具。当您第一次在调试模式中捕获内存快照时，所有 [dotMemory](https://www.jetbrains.com/dotmemory/) 所需的文件将下载至 ULogViewer 目录中。

其他由 ULogViewer 可执行文件以外的网络访问不受本协议之约束。


## 执行外部命令
在执行 ULogViewer 时有些必要情况需要执行外部命令：

+ 执行 **dotnet** 以确认在设备上安装的 .NET 版本。
+ 执行 **explorer** 以在 Windows 上打开文件资源管理器。
+ 执行 **open** 以在 macOS 上打开 Finder。
+ 执行 **defaults** 以确认在 macOS 上的系统语言与主题设置。
+ 执行 **nautilus** 或 **xdg-open** 以在 Linux 上打开文件管理器。
+ 执行 **cmd** 以在必要时更新 Windows 上的 PATH 环境变量。
+ 执行 **osascript** 以在必要时更新 macOS 上的 /etc/paths。
+ 执行 **gsettings** 以确认在 Linux 上的系统主题设置。

除了上述必要情况外，当日志来源为 **「Azure 命令行界面 (CLI)」** 或 **「标准输出 (stdout)」** 时将执行外部命令。您可以在编辑日志类型之 **「数据源」** 时在 **「数据源参数」** 对话框中查看完整的命令及参数列表。

请注意，我们 **不保证** 执行外部命令后的结果，这完全依赖于外部命令及可执行文件之行为。这部分必须由您自行确认。


## 修改您的电脑
除了文件访问以及下列的情况，ULogViewer **不会** 更改您电脑的设置。

请注意，我们 **不保证** 执行外部命令后您的电脑不会被更改，这部分需要您自行注意，特别是当 ULogViewer 在 Windows 上以管理员身份运行时。

### 在 Windows 上编辑 PATH 环境变量

#### 添加路径
添加之路径会被设定至 **用户** 的 PATH 环境变量。

#### 删除路径
若删除之路径定义于 **计算机** 的 PATH 环境变量，ULogViewer 将会以 **管理员** 权限启动 cmd 命令以更新 PATH 环境变量。

### 在 macOS 上编辑 /etc/paths
ULogViewer 将会以 **管理员** 权限启动 **osascript** 命令以更新 /etc/paths 文件。

### 执行脚本
在 ULogViewer 中执行之脚本可以访问 .NET 之功能，包含文件访问、网络访问及修改电脑等。因此执行脚本可能会修改甚至损坏您的电脑。您在执行脚本之前必须仔细检查其内容。


## 免责声明
ULogViewer 系以 **「现状」** 提供，不附带任何明示或暗示之保证，包括但不限于适销性、特定用途适用性及不侵权之保证。Carina Studio 不保证 ULogViewer 能符合您的需求，亦不保证其运行不会中断或不发生错误。

在适用法律允许之最大范围内，Carina Studio 对于因使用或无法使用 ULogViewer 而产生之任何直接、间接、偶发、特殊、惩罚性或衍生性损害（包括但不限于数据丢失、利润损失或业务中断），概不承担任何责任，即使已被告知可能发生此类损害亦然。


## 授权及版权
ULogViewer 是 Carina Studio 在 [MIT](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE) 授权之下的开放源代码项目。除了应用程序图标外，所有图标皆在 [MIT](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE)、[CC 4.0](https://en.wikipedia.org/wiki/Creative_Commons_license) 或 [Universal Multimedia License Agreement for Icons8](https://intercom.help/icons8-7fb7577e8170/en/articles/5534926-universal-multimedia-licensing-agreement-for-icons8) 授权下使用。您可以在 [MahApps.Metro.IconPacks](https://github.com/MahApps/MahApps.Metro.IconPacks)、[SVG Repo](https://www.svgrepo.com/)、[Icons8](https://icons8.com/)、[Google Fonts Icons](https://fonts.google.com/icons)、[Phosphor Icons](https://phosphoricons.com/) 及 [Tabler Icons](https://tabler.io/icons) 了解更多图标相关信息与授权。

内建字体 **「Roboto」** 及 **「Roboto Mono」** 在 [Apache License 2.0](http://www.apache.org/licenses/LICENSE-2.0) 授权下使用及发布，**「IBM Plex Mono」** 、 **「Noto Sans SC」** 、 **「Noto Sans TC」** 及 **「Source Code Pro」** 在 [Open Font License](https://scripts.sil.org/cms/scripts/page.php?site_id=nrsi&id=OFL) 授权下使用及发布。

加载至 ULogViewer 或由 ULogViewer 保存之日志的授权与版权不受本协议之约束。您必须自行注意及负责日志的授权与版权。


## 联系我们
如果您对于本用户协议有任何疑问，可以至 [GitHub](https://github.com/carina-studio/ULogViewer/issues) 提出或发送邮件至 [carina.software.studio@gmail.com](mailto:carina.software.studio@gmail.com)。
