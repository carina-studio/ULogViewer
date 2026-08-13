# ULogViewer 利用規約
 ---
+ バージョン: 2.8
+ 更新日: 2026/8/13

これは ULogViewer の利用規約であり、ULogViewer をご使用になる前にお読みいただく必要があります。利用規約は今後更新される場合があり、ULogViewer の Web サイトでご確認いただけます。ULogViewer の使用を開始した時点で、本利用規約に同意したものとみなされます。


## 適用範囲
ULogViewer は、Carina Studio のオープンソースプロジェクトです。以降で述べる ULogViewer は、下記のページで提供される実行可能ファイルまたは圧縮ファイルとまったく同一のもの **のみ** を指します。

+ [ULogViewer の Web サイト](https://carinastudio.net/ULogViewer/)
+ [GitHub 上の ULogViewer プロジェクトページおよびリリースページ](https://github.com/carina-studio/ULogViewer)

ULogViewer をソースコードからビルドした場合、そのビルドのご使用は本利用規約ではなく [MIT](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE) ライセンスのみに従います。

本利用規約は、ULogViewer 2026.2 から、次回の利用規約の更新で指定されるバージョンまでのすべてのバージョンに適用されます。


## デバッグモード
ULogViewer には、既定で無効になっているデバッグモードが組み込まれています。デバッグモードは **「ULogViewer について > デバッグモードで再起動」** から有効にできます。


## 外部依存関係

### Android SDK Platform Tools
**「Android デバイスログ」**、**「Android デバイス イベントログ」**、**「Android デバイスシステムトレース」**、**「Android システムメモリモニター」**、**「Android プロセスメモリモニター」**、**「特定 Android デバイスイベントログ」**、**「特定 Android デバイスログ」**、**「特定 Android デバイスシステムトレース」** の各ログプロファイルを使用するには、あらかじめお使いのデバイスに [Android SDK Platform Tools](https://developer.android.com/tools/releases/platform-tools) または [Android Studio](https://developer.android.com/studio) をインストールする必要があります。

### Azure コマンドラインインターフェイス (CLI)
**「Azure CLI」**、**「MySQL データベース」**、**「SQL Server データベース」** の各データソースの全機能を使用するには、あらかじめお使いのデバイスに [Azure CLI](https://docs.microsoft.com/cli/azure/) をインストールする必要があります。

### Git
**「Git ログ」** および **「Git ログ (簡易)」** のログプロファイルを使用するには、あらかじめお使いのデバイスに [Git](https://git-scm.com/) をインストールする必要があります。

### libimobiledevice
**「Apple デバイスログ」** および **「特定 Apple デバイスログ」** のログプロファイルを使用するには、あらかじめお使いのデバイスに [libimobiledevice](https://libimobiledevice.org/) をインストールする必要があります。

+ [Windows をご使用の場合](https://github.com/iFred09/libimobiledevice-windows)
+ [macOS をご使用の場合](https://formulae.brew.sh/formula/libimobiledevice)
+ [Linux をご使用の場合](https://command-not-found.com/idevicesyslog)

### Trace Conversion Tool
**macOS/Linux** 上で **「Android デバイスシステムトレース」** および **「特定 Android デバイスシステムトレース」** の組み込みログプロファイルを使用するには、あらかじめお使いのデバイスに [Trace Conversion Tool](https://perfetto.dev/docs/quickstart/traceconv) をインストールする必要があります。

### Xcode 用コマンドラインツール
**macOS** 上で **「Apple デバイスシミュレータログ」** および **「特定 Apple デバイスシミュレータログ」** の組み込みログプロファイルを使用するには、[Xcode 用コマンドラインツール](https://developer.apple.com/xcode/) をインストールする必要があります。Xcode 用コマンドラインツールを Xcode と一緒にインストールした場合は、**「Xcode > Settings > Locations > Command Line Tools」** を **「Xcode」** に設定して有効にする必要があります。


## ファイルアクセス
システムファイルを除き、ULogViewer に必要なすべてのファイルは ULogViewer ディレクトリ内に配置されます。**macOS** では、アプリケーション署名の要件により、アプリケーションデータはアプリケーションバンドル内ではなく **Application Support** ディレクトリ (`~/Library/Application Support/CarinaStudio/ULogViewer/`) に保存されます。**Windows** および **Linux** では、アプリケーションデータはアプリケーションディレクトリ自体に保存されます。ULogViewer へのデータの読み込み/インポート/保存/エクスポートを行わずに ULogViewer を実行する場合、以下を除いてその他のファイルアクセスは必要ありません。

+ Linux で物理メモリの情報を取得するために **/proc/meminfo** を読み取ります。
+ macOS でグローバルパスを取得するために **/etc/paths** を読み取ります。
+ 実行時のリソースを配置するためにシステムの一時ディレクトリを読み書きします。
+ .NET またはサードパーティライブラリによるその他の必要なファイルアクセス。

### ログの読み込み
+ Raw ログを含むファイルは **読み取り** モードで開かれます。
+ ログファイルと同じ場所にある \*.ulvmark ファイルは **読み取り** モードで開かれます。

### ログの表示
+ ログファイルと同じ場所にある \*.ulvmark ファイルは **読み書き** モードで開かれます。

### ログの保存
+ Raw ログの書き込み先となるファイルは **読み書き** モードで開かれます。
+ ログファイルと同じ場所にある \*.ulvmark ファイルは **読み書き** モードで開かれます。

### ログプロファイルのインポート
+ ログプロファイルの \*.json ファイルは **読み取り** モードで開かれます。

### 定義済みテキストフィルターのインポート
+ 定義済みテキストフィルターの \*.json ファイルは **読み取り** モードで開かれます。

### ログ分析ルールセットのインポート
+ ログ分析ルールセットの \*.json ファイルは **読み取り** モードで開かれます。

### ログ分析スクリプトのインポート
+ ログ分析スクリプトの \*.json ファイルは **読み取り** モードで開かれます。

### ログデータソーススクリプトのインポート
+ ログデータソーススクリプトの \*.json ファイルは **読み取り** モードで開かれます。

### ログプロファイルのエクスポート
+ エクスポートされるログプロファイルの \*.json ファイルは **読み書き** モードで開かれます。

### 定義済みテキストフィルターのエクスポート
+ エクスポートされる定義済みテキストフィルターの \*.json ファイルは **読み書き** モードで開かれます。

### ログ分析ルールセットのエクスポート
+ エクスポートされるログ分析ルールセットの \*.json ファイルは **読み書き** モードで開かれます。

### ログ分析スクリプトのエクスポート
+ エクスポートされるログ分析スクリプトの \*.json ファイルは **読み書き** モードで開かれます。

### ログデータソーススクリプトのエクスポート
+ エクスポートされるログデータソーススクリプトの \*.json ファイルは **読み書き** モードで開かれます。

### アプリケーションログのエクスポート
+ アプリケーションログを含む \*.zip ファイルは **読み書き** モードで開かれます。

ULogViewer の実行可能ファイル以外によるファイルアクセスは、本利用規約の対象外です。


## ネットワークアクセス
ULogViewer は以下の場合にネットワークにアクセスします。

### ネットワーク経由でのログの読み込み
ログのソースが以下のいずれかである場合、ネットワークアクセスが必要です。

+ **Azure CLI**。
+ **HTTP/HTTPS**。
+ **MySQL データベース**。
+ **SQL Server データベース**。
+ **TCP サーバー**。
+ **UDP サーバー**。
+ ローカルマシンの外部にあるファイルを指定した **ファイル**。
+ ネットワークにアクセスする **ログデータソーススクリプト**。

### ネットワーク接続の確認
ULogViewer はネットワーク接続を確認するために以下のサーバーと通信します。

+ [Cloudflare](https://www.cloudflare.com/)
+ [Google DNS](https://dns.google/)
+ [OpenDNS](https://www.opendns.com/)

ULogViewer はデバイスのパブリック [IP アドレス](https://ja.wikipedia.org/wiki/IP%E3%82%A2%E3%83%89%E3%83%AC%E3%82%B9) を確認するために以下のサーバーと通信します。

+ [https://ipv4.icanhazip.com](https://ipv4.icanhazip.com/)
+ [http://checkip.dyndns.org](http://checkip.dyndns.org/)

### メモリスナップショットの取得
[dotMemory](https://www.jetbrains.com/dotmemory/) は、Carina Studio がメモリ使用量の分析に使用している主要なツールです。デバッグモードで初めてメモリスナップショットの取得を開始すると、[dotMemory](https://www.jetbrains.com/dotmemory/) に必要なすべてのファイルが ULogViewer ディレクトリ内にダウンロードされます。

ULogViewer の実行可能ファイル以外によるネットワークアクセスは、本利用規約の対象外です。

## 外部コマンドの実行
ULogViewer の実行時には、いくつかの必要な外部コマンドの実行があります。

+ デバイスにインストールされている .NET のバージョンを確認するために **dotnet** を実行します。
+ Windows でエクスプローラーを開くために **explorer** を実行します。
+ macOS で Finder を開くために **open** を実行します。
+ macOS でシステムの言語とテーマモードを確認するために **defaults** を実行します。
+ Linux でファイルマネージャーを開くために **nautilus** または **xdg-open** を実行します。
+ 必要に応じて、Windows で PATH 環境変数を更新するために **cmd** を実行します。
+ 必要に応じて、macOS で /etc/paths を更新するために **osascript** を実行します。
+ Linux でシステムのテーマモードを確認するために **gsettings** を実行します。

上記の必要な場合を除き、外部コマンドの実行はログのソースが **「Azure CLI」** または **「標準出力 (stdout)」** の場合に発生します。コマンドと引数の一覧は、ログプロファイルの **「データソース」** を編集する際の **「データソースオプション」** ダイアログで確認できます。

なお、当社は外部コマンドの実行結果を **保証しません** 。結果は外部コマンドおよび実行可能ファイルの動作に依存するため、お客様ご自身でご確認ください。


## お使いのコンピュータの変更
ファイルアクセスおよび以下の場合を除き、ULogViewer がお使いのコンピュータの設定を変更することは **ありません** 。

なお、当社は外部コマンドの実行後にお使いのコンピュータが変更されないことを **保証しません** 。特に Windows で ULogViewer を管理者として実行する場合は、お客様ご自身でご注意ください。

### Windows での PATH 環境変数の編集

#### パスの追加
追加されるパスはすべて、**ユーザー** スコープの PATH 環境変数に設定されます。

#### パスの削除
削除するパスが **マシン** スコープの PATH 環境変数に登録されていた場合、ULogViewer は PATH 環境変数を更新するために **管理者** 権限で cmd コマンドを実行します。

### macOS での /etc/paths の編集
ULogViewer は /etc/paths ファイルを更新するために **管理者** 権限で **osascript** コマンドを実行します。

### スクリプトの実行
ULogViewer で実行されるスクリプトは、ファイルアクセス、ネットワークアクセス、コンピュータの変更などを含む .NET の機能を利用できます。そのため、スクリプトの実行によってお使いのコンピュータが変更されたり、損害を受けたりする可能性があります。実行する前にスクリプトの内容を十分にご確認ください。


## 免責事項
ULogViewer は、明示または黙示を問わずいかなる保証もなく **「現状のまま」** 提供されます。これには、商品性、特定目的への適合性、および権利非侵害の保証が含まれますが、これらに限られません。Carina Studio は、ULogViewer がお客様の要件を満たすこと、またはその動作が中断せず、エラーがないことを保証しません。

適用法令が許容する最大限の範囲において、Carina Studio は、ULogViewer の使用または使用不能に起因または関連して生じるいかなる直接的、間接的、付随的、特別、懲罰的、または結果的損害 (データの喪失、利益の喪失、業務の中断を含みますが、これらに限られません) についても、たとえそのような損害の可能性を知らされていた場合であっても、一切の責任を負いません。


## ライセンスと著作権
ULogViewer は、[MIT](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE) ライセンスの下で提供される Carina Studio のオープンソースプロジェクトです。アプリケーションアイコンを除くすべてのアイコンは、[MIT](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE)、[CC 4.0](https://en.wikipedia.org/wiki/Creative_Commons_license)、または [Universal Multimedia License Agreement for Icons8](https://intercom.help/icons8-7fb7577e8170/en/articles/5534926-universal-multimedia-licensing-agreement-for-icons8) ライセンスの下で配布されています。アイコンとそのライセンスの詳細については、[MahApps.Metro.IconPacks](https://github.com/MahApps/MahApps.Metro.IconPacks)、[SVG Repo](https://www.svgrepo.com/)、[Icons8](https://icons8.com/)、[Google Fonts Icons](https://fonts.google.com/icons)、[Phosphor Icons](https://phosphoricons.com/)、[Tabler Icons](https://tabler.io/icons) をご参照ください。

組み込みフォントの **「Roboto」** および **「Roboto Mono」** は [Apache License 2.0](http://www.apache.org/licenses/LICENSE-2.0) の下で、**「IBM Plex Mono」**、**「Noto Sans SC」**、**「Noto Sans TC」**、**「Source Code Pro」** は [Open Font License](https://scripts.sil.org/cms/scripts/page.php?site_id=nrsi&id=OFL) の下で配布されています。

ULogViewer に読み込まれたログ、または ULogViewer によって保存されたログのライセンスおよび著作権は、本利用規約の対象外です。ログのライセンスおよび著作権については、お客様ご自身でご確認ください。


## お問い合わせ
本利用規約についてご不明な点がある場合は、[GitHub](https://github.com/carina-studio/ULogViewer/issues) で issue を作成するか、[support@carinastudio.net](mailto:support@carinastudio.net) までメールをお送りください。
