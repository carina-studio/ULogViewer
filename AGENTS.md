# AGENTS.md

This file provides guidance to AI coding agents when working with code in this repository.

## Coding Style

### Naming

| Element | Convention | Example |
|---|---|---|
| Public properties | PascalCase | `AllLogCount`, `IsActivated` |
| Private fields (instance and static) | camelCase; instance fields always qualified with `this.` | `this.logs`, `latestPanelSizeKey`, `defaultTimestampCultureInfo` |
| Internal / protected / public fields | PascalCase | `TimestampAsc` |
| Static `ObservableProperty` fields | PascalCase + `Prop` suffix | `AllLogCountProp` |
| Private/helper methods | PascalCase | `ChangeState()`, `OnDataSourcePropertyChanged()` |
| Public methods | PascalCase | `ClearLogs()`, `Pause()` |
| Async methods | Must end with `Async` | `OnPrepareShuttingDownAsync()` |
| Constants | PascalCase | `DefaultSidePanelSize` |
| Parameters & local variables | camelCase | `cancellationToken`, `logReader` |
| Interfaces | `I` prefix + PascalCase | `ILogDataSource`, `IDisplayableLogProcessor` |
| Event handlers | `On` prefix | `OnDataSourcePropertyChanged` |

**All methods use PascalCase regardless of accessibility**, per the standard .NET naming convention — a private or internal helper method is named exactly like a public one (`ChangeState()`, not `changeState()`). camelCase is reserved for private fields, parameters, and local variables; there is no case in which a method is camelCase. Some existing types still use camelCase for their private methods — that is legacy, not a convention to match, and new methods in those types are PascalCase too.

**Field casing follows accessibility, not lifetime** — a field is camelCase when it is `private` (instance or static alike) and PascalCase when it is `internal`, `protected`, or `public`. When adding a field to an existing type, follow the convention already used in that type.

### Formatting & Structure

- **File-scoped namespaces** — use `namespace Foo.Bar;` (not block-scoped).
- **`using` directives outside** the namespace declaration. Always import the correct namespace when using a new type; after any code modification, audit every edited file and remove `using` directives that are no longer referenced.
- **Allman-style braces** — opening brace on its own line for types and methods; single-statement bodies may omit braces, but only when that single statement fits on one line. If the inner statement spans multiple lines (e.g. the outer of a stacked `using` whose inner `using` has a multi-line block), the outer statement must use braces.
- **`try`/`catch`/`finally` blocks** always use full braces even when the body is a single statement or empty.
- **`this.` prefix** on all instance member accesses (fields and properties). It does **not** apply to primary-constructor parameters, which are accessed directly by name.
- **Static members are accessed through the type that declares them**, never through a derived type that merely inherits them. `CurrentOrNull` is declared on `CarinaStudio.Application`, which `App` inherits it from, so write `Application.CurrentOrNull` — **not** `App.CurrentOrNull`. Both spellings bind to the identical member, so this is purely about showing the reader where the member actually lives, and about not implying that the derived type adds something it does not. `using CarinaStudio;` resolves the bare `Application` name; qualify it in a file that also imports `Avalonia`, where `Avalonia.Application` would collide.
- **Primary constructors** preferred over explicit constructors when the body would only assign fields.
- **Expression-bodied members** for concise single-expression properties and methods.
- **Assignments are dedicated statements** — never combine an assignment with a value read in the same expression. Do not consume the result of an assignment (`=`, `??=`, `++`, `--`, etc.) as a sub-expression (method argument, condition, initializer, return value, expression-bodied member, etc.). Assign on its own line first, then read the variable/field on the following line. This also rules out returning an assignment: a lazily-initialized property must use a block getter that assigns on one line and returns on the next (`get { field ??= …; return field; }`), **not** an expression body that consumes the assignment (`=> field ??= …`).
- **Enum members** are listed consecutively with no blank line between them, even when each carries an XML doc comment.
- **Blank lines between members** — two blank lines between members of a top-level type; one blank line between members of an inner (nested) type.

### Fields & Properties

- Access instance fields with `this.` consistently — never omit it.
- Mark thread-shared fields `volatile`; use `Interlocked.*` for atomic updates.
- Use `[ThreadSafe]` / `[UsedOnBackgroundThread]` / `[CalledOnBackgroundThread]` attributes to document thread semantics.
- Register reactive properties with `ObservableProperty.Register<TOwner, T>(nameof(...))` rather than implementing `INotifyPropertyChanged` manually. Coercion logic goes in the registration call.
- `ObservableProperty<T>` fields are named `XxxProp` (e.g. `AllLogCountProp`), **never** `XxxProperty`, regardless of visibility. Reason: Avalonia 12 compiled bindings resolve any public static field named `<Member>Property` on the bound type as an `AvaloniaProperty` and fail compilation when it is not one; the `Prop` suffix is applied to all visibilities for consistency. `AvaloniaProperty` fields on controls keep the standard `XxxProperty` suffix — that convention is required by Avalonia and does not conflict. The existing `XxxProperty`-suffixed `ObservableProperty` fields are renamed as part of the Avalonia 12 upgrade; use `Prop` in all new code.
- **Time units** — milliseconds are the default. Bare `Timeout` / `Delay` / `Interval` names (e.g. `StopListeningTimeout = 3000`, `LogsTimeInfoReportingInterval = 500`) are always milliseconds; do not append `Ms`. Use a unit suffix only when the value is **not** in milliseconds (`SomethingSeconds`, `SomethingMicroseconds`, `SomethingTicks`).
- When a property needs custom accessor logic (validation, change notification, etc.) but `ObservableProperty.Register` is not in use, prefer the C# `field` keyword over a manually-declared backing field. Example:
  ```csharp
  public int Count
  {
      get;
      private set
      {
          if (field == value)
              return;
          field = value;
          this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
      }
  }
  ```

### Types & Nullability

- Nullable reference types are fully enabled — annotate everything.
- Prefer **`var`** for local variables when the type is inferable from the right-hand side; fall back to an explicit type only when the inferred type would be unclear to a reader of the line in isolation (e.g. when the right-hand side is a method call whose return type isn't evident from its name).
- **Omit explicit generic type arguments** when the compiler infers them from the arguments — `settings.SetValue(SettingKeys.IgnoreCaseOfLogTextFilter, true)`, not `settings.SetValue<bool>(...)`.
- Use **`is not null` / `is null`** pattern matching instead of `!= null` / `== null` in all new code — this holds inside compound boolean guards (`… && x is not null`) and when null-checking an `out` variable from a `Try…` method (e.g. `TryGetEntryData(out var data) && data is not null`), not just standalone `if` conditions. Reserve `==` / `!=` for non-null comparisons such as reference or value equality (e.g. `logProfile == LogProfiles.Empty`).
- Use **null-conditional** (`?.`) and **null-coalescing** (`??`) operators for safe access and defaults.
- Use `.AsNonNull()` (framework extension) to assert non-null instead of `!`.
- Use `.Let(it => ...)` for safe chained operations on nullable values.
- Use `.Also(it => ...)` for fluent object initialization.
- Null-coalesce events: `this.SomeEvent?.Invoke(this, e)`.
- **Never pass `default` as an argument** — always use an explicit value (e.g. `CancellationToken.None`, `TimeSpan.Zero`).

### Async

- Business logic async methods return `Task` / `Task<T>` — never `async void` (UI event handlers are the sole exception).
- Pass `CancellationToken` through the full call chain. Never swallow `OperationCanceledException`.
- To check for cancellation, call `token.ThrowIfCancellationRequested()` rather than manually testing `token.IsCancellationRequested` and throwing `TaskCanceledException` / `OperationCanceledException`. This applies to **all new code**, including the cancellation check that immediately follows an `await Task.Run(...)` block — write `token.ThrowIfCancellationRequested();` there, even though some existing code still uses the older manual `if (token.IsCancellationRequested) throw new TaskCanceledException();` form (do not mirror that legacy pattern into new code).
- When calling an async method, **always use the overload that accepts a `CancellationToken`** if one exists. Pass the available token if you have one; otherwise pass `CancellationToken.None` explicitly. Examples: `Task.Run(work, token)`, `task.WaitAsync(timeout, CancellationToken.None)`, `stream.ReadAsync(buffer, token)`.
- In event-handler lambdas, name the sender parameter (`(sender, e) => ...`) instead of discarding it (`(_, e) => ...`) when the body fire-and-forgets an async call (`_ = SomeAsync(...)`). The `_ = ...` pattern silently discards the returned `Task` (and any exception it would surface); keeping `sender` named flags the handler as stateful for the reader and gives a debugger something to inspect when the fire-and-forget faults.

### Collections

- `ImmutableList<T>` / `ImmutableHashSet<T>` for snapshot/read-only data.
- `ObservableList<T>` for mutable collections that the UI binds to; expose them as `ReadOnlyObservableList<T>` publicly.
- Prefer the `.IsEmpty()` / `.IsNotEmpty()` / `.IsNullOrEmpty()` extension methods over `.Count == 0` / `.Count > 0` / `.Length == 0` / `.Length > 0`, and over emptiness pattern matching (e.g. `x is null or []`, `x is { Count: > 0 }`, `x is null || x.Count == 0`). They cover every `ICollection<T>` / `IReadOnlyCollection<T>` — lists, sets, dictionaries, queues, stacks, and arrays alike — so the same three names apply whatever the concrete collection type is. Reserve `.Count` / `.Length` comparisons for non-zero thresholds (e.g. `Count >= maxCount`).
- The nullable-accepting overloads (`IsNotEmpty` / `IsNullOrEmpty`) are annotated with `[NotNullWhen]`, so a successful check propagates non-null state to a later dereference — no `.AsNonNull()` assertion is needed after the guard. If an overload is ever found not to propagate it, that is a bug to fix in AppBase, not a reason to avoid these methods or to work around them at the call site.
- **Collection expressions** — prefer C# collection expressions (`[]`, `[ element ]`, `[ ..source ]`) over `new()`, `new List<T>()`, `new T[]{ ... }`, `Array.Empty<T>()`, or spread-copy constructors when creating or initializing collections, whenever no constructor arguments (e.g. initial capacity, custom comparer) are required — including untargeted contexts such as `object`-typed parameters, where C# 14's natural type (`List<T>`) applies. They target arrays, spans, and interfaces such as `IList<T>` / `IReadOnlyList<T>`, so use them for collection-typed return values, fields, and locals too. Non-empty collection expressions use a single space after `[` and before `]` (`[ A, B, C ]`); empty ones stay `[]`.

### Patterns

- **Early returns** for guard clauses (dispose checks, cancellation, already-done checks) at the top of methods.
- **`switch` expressions** for multi-way type or value dispatch.
- **Unused lambda parameters** use the ignored identifier `_` (e.g. `(_, e) => ...`, `async _ => ...`) instead of a named parameter the body never reads. Exception: keep the sender parameter named in an event-handler lambda whose body fire-and-forgets an async call (see the Async section). Caveat: a single `_` parameter is a real identifier, so a body containing `out _` / `_ = ...` discards will conflict (CS1503 or silent capture) — restructure the body to avoid the discard (e.g. assert on `GetProperty(...).ValueKind` instead of `TryGetProperty(..., out _)`) rather than naming the parameter.
- **`.Let()` / `.Also()` / `.Use()`** for functional-style chaining and initialization:
  ```csharp
  var set = new HashSet<string>().Also(s =>
  {
      s.Add("foo");
      this.extras?.Let(s.AddAll);
  });
  ```
- **`.Setup()` for `IDisposable` initialization** — when creating an `IDisposable` and setting its properties immediately, do not use object-initializer syntax (`new Foo { Prop = value }`): if the initializer throws, the instance is never disposed. Use the `.Setup(it => ...)` extension instead, which guarantees `Dispose()` is called when the setup action throws:
  ```csharp
  using var reader = new LogReader(null, source).Setup(it =>
  {
      it.IsContinuousReading = true;
  });
  ```
- **Extension members (C# 14 / .NET 10+)** — when extending a type, prefer an extension property inside an `extension(T value)` block over a `GetX()`-style extension method, whenever the accessor is a pure, side-effect-free projection that reads naturally as a property. Use it so call sites read `type.IsStacked` instead of `type.GetIsStacked()`:
  ```csharp
  static class LogChartTypeExtensions
  {
      extension(LogChartType type)
      {
          /// <summary>
          /// Check whether the chart type stacks its series or not.
          /// </summary>
          public bool IsStacked => type switch
          {
              LogChartType.ValueStackedAreas or LogChartType.ValueStackedAreasWithDataPoints or LogChartType.ValueStackedBars => true,
              LogChartType.ValueLines or LogChartType.ValueAreas or LogChartType.ValueBars => false,
              _ => throw new NotImplementedException(),
          };
      }
  }
  ```

### File Organization

`extension` blocks (C# 14 extension members) are placed **first** in the containing class, before all other members; they are not sorted with the members below. Members inside an `extension` block are ordered alphabetically.

The remaining members inside a class are ordered as follows:

1. **Public constants** — no section comment; each member has its own `///` XML doc.
2. **Public static fields** — no section comment; each member has its own `///` XML doc.
3. **Inner types** — alphabetically ordered; each type has its own dedicated comment.
4. **Constants** (private/internal) — under a `// Constants.` section comment.
5. **Static fields** (private/internal) — under a `// Static fields.` section comment.
6. **Private fields** — under a `// Fields.` section comment.
7. **Static initializer** — under a `// Static initializer.` section comment.
8. **Constructors** — under a `// Constructor(s).` section comment.
9. **Non-private fields, properties, and methods** — alphabetically ordered by member name. Each member is preceded by:
   - a `///` XML doc comment for public members, OR
   - a single-line `//` comment describing the member, for private/internal members.

The descriptive `//` comment summarizes *what the member is*; it does not need to restate the member name. Example from `Session.cs`:

```csharp
// Attach to given component.
void AttachToComponent(SessionComponent component) { ... }


// Bump LogsVersion to signal that pinned log IDs may have been invalidated.
void BumpLogsVersion() => ...


/// <summary>
/// Calculate duration between given logs.
/// </summary>
public static TimeSpan? CalculateDurationBetweenLogs(...)
```

### XAML / Avalonia

- **Compiled vs. reflection bindings — an Avalonia 12 rule.** This repo is still on Avalonia 11 (`AvaloniaVersion` in `Directory.Build.props`), where compiled bindings are opt-in; the rule below takes effect with the Avalonia 12 upgrade and does not govern today's XAML. On Avalonia 12: prefer compiled bindings as much as possible. `x:CompileBindings="True"` becomes redundant (compiled is the default) and is omitted. `x:CompileBindings="False"` is **not allowed** — it switches compiled bindings off for an entire scope, taking every binding in that scope down with the one that needed it; when a reflection binding genuinely is needed, opt that **single** binding out with the dedicated `{ReflectionBinding …}` markup extension instead. Carina Studio's AppSuiteBase framework is already on Avalonia 12 and states this as an active rule — see the `AGENTS.md` of the [AppSuiteBase repository](https://github.com/carina-studio/AppSuiteBase).
- Use `{DynamicResource Brush/...}` for theme-sensitive values; `{StaticResource Double/...}` for fixed values.
- String resources via markup extension: `{asXaml:StringResource SessionView.SomeKey}`.
- Namespace aliases follow the pattern `xmlns:asXaml="using:CarinaStudio.AppSuite.Xaml"`, `xmlns:appViewModels="using:CarinaStudio.ULogViewer.ViewModels"`.
- Resource names use slash-separated paths: `Image/Icon.Details.Outline`, `Brush/SessionView.StatusBar.Background`.
- To combine multiple bindings with boolean AND / OR, prefer the `{asXaml:AndBindings …}` / `{asXaml:OrBindings …}` markup extensions (comma-separated child `{Binding …}` entries) over a `MultiBinding` with `{x:Static aConverters:BoolConverters.And}` / `.Or`. The markup-extension form is more concise and is the established pattern across Carina Studio apps.
- **`asControls:DialogItem` sizing** — use the default item size only when the item contains a **ComboBox** input or has a **description** (`asControls:DialogElement.TextRole="DescriptionBelowLabel"`); for every other item (plain TextBox/IntegerTextBox, ToggleSwitch, etc. with just a label) set `ItemSize="Small"`.

### Comments

- XML doc (`/// <summary>`) on all public types and members.
- `<summary>` always uses the three-line form — opening tag, body, closing tag — even when the body is a single sentence:
  ```csharp
  /// <summary>
  /// Short sentence describing the member.
  /// </summary>
  ```
- Other XML doc tags (`<remarks>`, `<param>`, `<returns>`, etc.) collapse to a single line when their content fits on one — open tag, body, and close tag all together:
  ```csharp
  /// <summary>
  /// Short sentence.
  /// </summary>
  /// <remarks>Extra context that the summary should not absorb.</remarks>
  /// <param name="value">The value to set.</param>
  /// <returns>True if the operation succeeded.</returns>
  ```
- If a longer explanation is genuinely needed (subtle invariants, cross-cutting behavior, etc.), put it in a separate `<remarks>` tag — do not pad the `<summary>`.
- For a member that overrides a base member or implements an interface/abstract member (including explicit interface implementations), use `/// <inheritdoc/>` (self-closing) rather than a `//` comment or a restated `<summary>`. Members that are not overrides/implementations — private helpers, constructors — keep the usual `//` comment or `<summary>`.
- Inline section comments inside method bodies are **lowercase**, no trailing period, on their own line before the code: `// cancel reading logs`, `// update memory usage`
- Inside **any** code block — method body, `case` block, `if`/`else`, `for`/`while`/`foreach`, `try`/`catch`/`finally`, lambda body — group related statements into logical blocks separated by a single blank line, and give each block its own comment — **including the leading and trailing blocks** (e.g. a final `return new { ... }` separated from the preceding code by a blank line still needs its own comment). Comments after the leading one are preceded by a single blank line. Exception: an enclosing block that contains only a **single** logical block does not need an inline comment.
- When splitting an existing logical block into multiple blocks (during a refactor or edit), audit the result: if the original block had a leading comment, the new blocks each need their own; if the original had none (single-block exception applied), the new blocks now require one each.
- Comments above private/internal members use sentence case with a trailing period: `// Called when property of application changed.`
- No end-of-line comments.

### Logging

- **Log levels** —
  - `Trace`: high-frequency per-event detail that would flood production logs (e.g. per-log-line or per-chunk progress). `Debug` is visible in production builds, so anything that can fire faster than once per reading session should be `Trace`, not `Debug`.
  - `Debug`: bounded-frequency diagnostic events (per-reader lifecycle, data-source open/close, profile-selection outcome).
  - `Information`: subsystem lifecycle and operator-visible state transitions (`Reading logs started …`).
  - `Warning`: unusual but non-fatal situations (unreadable log file, malformed log line, fallback taken).
  - `Error`: unexpected exceptions or operations that genuinely failed.
- **Message text** —
  - Use **sentence case** — capitalize the first word (`"Read: source closed before completion"`, not `"read: source closed …"`).
  - For dispatch / request outcomes, use the format **`Subject: {target} [<outcome>]`** — the bracketed token is a machine-friendly result code. Examples:
    - `Read: /var/log/system.log [ok]`
    - `Read: /var/log/missing.log [file_not_found]`
    - `Read: syslog [internal_error]`
  - Use **lowercase** result codes inside the brackets (`[ok]`, `[file_not_found]`, `[internal_error]`).
  - When a message carries one or more named state values, list them as `name: {value}` separated by commas, after the descriptive prefix or outcome bracket. The **name** is written as plain English words separated by spaces, not as the C# identifier — `max aging days: {maxAgeDays}`, not `max_age_days: {maxAgeDays}` or `maxAgeDays: {maxAgeDays}`. The placeholder inside `{...}` is the structured-logging key and follows normal C# naming (camelCase). Examples:
    - `Read: /var/log/system.log [ok], count: {count}, duration: {ms}`
    - `Read: log profile mismatch, detected: {detected}, requested: {requested}`
- **Logger names** — for classes with per-instance identifiers (readers, writers, data sources), construct a logger named `<TypeName>-<Id>` via `app.LoggerFactory.CreateLogger($"{nameof(MyType)}-{this.Id}")`. The id then appears in NLog's `${logger:shortName=true}` prefix, so log messages don't need to embed it.

---

## Localization & String Resources

UI strings live in `ULogViewer/Strings/` as Avalonia `.axaml` resource dictionaries. All keys are prefixed `String/` (e.g. `String/SessionView.AddLogFiles`); preserve this prefix in every file.

- `Default.axaml` — base English (en-US). All keys live here.
- `ja-JP.axaml` — Japanese.
- `zh-TW.axaml`, `zh-CN.axaml` — Traditional / Simplified Chinese.
- Every non-default file only contains entries that differ from `Default.axaml`; the resource system falls back to the default for missing keys. Entries are kept in the same order as `Default.axaml`, under the same section comments.
- `Default-OSX.axaml`, `Default-Linux.axaml`, `ja-JP-OSX.axaml`, `ja-JP-Linux.axaml`, `zh-TW-OSX.axaml`, `zh-TW-Linux.axaml`, `zh-CN-OSX.axaml`, `zh-CN-Linux.axaml` — platform overrides for keystrokes (`⌘` vs `Ctrl`) and OS-specific labels (Finder / File Manager / File Explorer). Only override what's platform-specific.

### English style

- **Title case** for strings without trailing period (item titles, option labels, button text). AP-style: lowercase short prepositions/conjunctions (`a/an/the/of/for/in/on/at/to/by/per/as/and/or/if/after/when/during`); capitalize 4+ letter prepositions when at the start (`Before`, `Between`, `With`) and all verbs including `Is`/`Are`.
- **Sentence case** for strings ending with `.` or `?` (descriptions, error/status messages, hints).
- Articles in titles: keep when natural (`Scroll to the Latest Log after Reloading Logs`).
- Avoid `Max` in long option labels — prefer `Maximum`.

### Chinese conventions

- `zh-TW.axaml` uses Taiwan terms: 檔案, 資訊, 資料, 介面, 日誌, 篩選, 載入, 設定, 影像, etc.
- `zh-CN.axaml` uses Mainland terms: 文件, 信息, 数据, 界面, 日志, 筛选/过滤, 加载, 设置, 图像, etc. **Watch for Taiwan-leaning leftovers** in zh-CN — common ones to convert:
  - General vocabulary: 资讯→信息, 资料→数据 (when meaning "data"), 介面→界面, 回应→响应, 网路→网络, 数位→数字, 套用→应用, 储存→保存, 开启→打开 (when meaning "open file/dialog"; `开启` is fine for "enable"), 取得→获取, 透过→通过, 效能→性能, 载入→加载, 设定→设置 (as a UI label), 选取→选择/选中, 撷取→抓取/截取, 拖曳→拖动, 影像→图像, 字型→字体, 色彩→颜色.
  - Compound terms: 资料库→数据库, 资料来源→数据源, 资料点→数据点, 资料夹→文件夹, 剪贴簿→剪贴板, 状态列→状态栏, 文件总管→Windows 资源管理器.
  - HTTP/network: `User Agent` → 用户代理 (not 用户媒介); device communication uses 连接/通信, not 联系.
- **Watch for Traditional characters mixed inside Simplified strings** — e.g. `設定` (Traditional 設) inside an otherwise-Simplified string, or a description that switches script mid-sentence. These are silent bugs that pass spell-check.
- For "PNG" the `G` is *Graphics* — translate as 图形 / 圖形, not 图像 / 影像.
- For "keyboard shortcut", zh-TW uses 快速鍵 (the standard Taiwan vendor term, per Microsoft / Apple Taiwan), **not** 快捷鍵 (the Mainland-origin form); zh-CN uses 快捷键.
- Tab UI term: 标签页 (Mainland) / 分頁 (Taiwan).
- For "double-tap", zh-TW uses 點兩下 (**not** 雙擊); zh-CN uses 双击.
- For "frame", zh-TW uses 畫格 (**not** 畫面 or 影格); zh-CN uses 帧.
- zh-TW phrasing preferences: prefer 不包含 over 不具備, and 而不是 over 而非. zh-CN keeps 不具备 / 而非.
- For "set" (a value), zh-TW uses 設定, zh-CN uses 设置. A description explaining a special value uses the declarative pattern 「設定為 X 表示…」 / 「设置为 X 表示…」 — state what the value means, not a conditional 「若…則設為 X」.
- For English entries phrased as "Added support for X" (typical in `ChangeList*.md` and similar notes), translate as `支援 X` (zh-TW) / `支持 X` (zh-CN), not the literal `新增 X 的支援 / 新增 X 的支持`. **Exception — when X is a bare noun short enough that `支援 X` reads too terse** (a language, format, or file-type name, e.g. 日文 / CLEF), use `新增 X 支援` (zh-TW) / `新增 X 支持` (zh-CN) instead; there is still no `的`. Example: "Added support for Japanese language." → `新增日文支援。` (zh-TW) / `新增日语支持。` (zh-CN).
- For "fix" wording, **zh-CN uses 修复 everywhere, including the section header** (`修复…的问题`, the `## 错误修复` header, `其他错误修复`); **zh-TW keeps 修正** (`修正…的問題`, `## 錯誤修正`).
- Description strings end with the full-width period 。 — no trailing space before `</sys:String>`.

### Japanese conventions

Three reference sources settle wording, in this order of precedence:

1. **The file itself.** When `ja-JP.axaml` already renders a term, match it — consistency inside the shipped UI outranks any external style guide. This is why the long-vowel mark is not applied uniformly: the file uses サーバー and ユーザー (with ー) but フィルタ, コンピュータ and フォルダ (without), and new strings follow the term already present rather than a blanket rule.
2. **AppSuiteBase's own `Core/Strings/ja-JP.axaml`** — see the [AppSuiteBase repository](https://github.com/carina-studio/AppSuiteBase) — for anything the framework names: 利用規約 (User Agreement), プライバシーポリシー, アプリケーションオプション, オプション, デバッグモードで再起動, 外部依存関係. Documents under `ULogViewer/Resources/` must use these exact terms.
3. **macOS's Japanese locale tables** (the `.loctable` files under `/System/Library/Frameworks` and `/System/Library/PrivateFrameworks`, readable with `plistlib`) as the tie-breaker for everything else. They are the only sizeable Japanese glossary available locally.

- "Ignore case" is 大文字小文字を区別しない (matching the existing `RegexEditorDialog.IgnoreCase` string), not Apple's 大文字/小文字を無視.
- "Later" as a dismissive button is あとで, not 後で.
- Other settled terms: 上書き (overwrite), 構成 (configuration), スニペット (snippet), パターン (pattern), マスク (mask), 名前付きグループ (named group), 正規表現 (regular expression), 組み込み (built-in), カスタム (custom), 履歴 (history), クリップボード (clipboard), 連携 (integration).
- A session is タブ when it means a ULogViewer tab (`空のタブ`, `このタブの…`); use セッション only when the English genuinely means something other than a tab.
- Put a space between Latin and Japanese runs: `IP アドレス`, `Pro バージョン`.
- Descriptions use polite です/ます form and end with the full-width period 。 Titles and button labels are bare noun or verb phrases with no period.

### Keys

- Keys are stable identifiers and must match exactly across all language and platform-override files. A typo like `…ImageHint` vs `…Image` causes silent fallback to English.

### Retrieving strings in code

- To get a string resource as an `IObservable` (e.g. for `MessageDialog.Message` or bindings set in code), prefer `Application.GetObservableString("Some.Key")` over `Control.GetResourceObservable("String/Some.Key")`. Note `GetObservableString` takes the **bare** key (`"SessionView.SomeKey"`) — the `String/` prefix is added internally.

---

## Change Lists

`ULogViewer/ChangeList.md`, `ULogViewer/ChangeList-ja-JP.md`, `ULogViewer/ChangeList-zh-TW.md`, and `ULogViewer/ChangeList-zh-CN.md` describe the changes shipping in the next version.

- **Key names and key combinations must be wrapped in single-backtick inline code** — e.g. `` `⌘Q` ``, `` `Ctrl+Q` ``, `` `⌘←` ``, `` `Ctrl+Shift+F` ``. Use single backticks, not triple. This rule applies in every locale variant; the inline code wrapping is identical across English, ja-JP, zh-TW, and zh-CN.
- **When an entry lists a shortcut for more than one platform, order the platforms Windows/Linux first, then macOS** — e.g. `Ctrl+V` on Windows/Linux, `⌘V` on macOS.
- English entries use past tense (`Added`, `Improved`, `Prevented`, `Fixed`).
- Each new bullet must be mirrored in all four locale files; do not update one without updating the others.
- **Keep the existing order of entries** — when adding a bullet, append it (or place it beside a directly related entry) without resorting or regrouping the entries already present in a section. Do not reorder existing items by importance.

---

## Code Review Checklist

### Correctness
- Logic is correct for all paths, including edge cases (empty collections, null values, zero counts).
- Multi-step operations that must be atomic are protected by a lock or semaphore across all steps, not just individual operations.
- State mutations under a lock do not leak mutable references that can be read or written outside the lock.
- `async`/`await` is used correctly — no fire-and-forget unless intentional; no `.Result` or `.Wait()` blocking on async code.
- `CancellationToken` is propagated through all async calls; `OperationCanceledException` is not swallowed.
- `IDisposable` resources are disposed in all paths, including error paths.

### Thread Safety
- Shared mutable fields accessed from multiple threads are protected consistently.
- No TOCTOU (time-of-check/time-of-use) races — check and act happen under the same lock or synchronization primitive.
- Background-thread methods are marked `[CalledOnBackgroundThread]`; UI-thread calls are dispatched via `SynchronizationContext` or guarded with `CheckAccess()`.

### Error Handling
- Exceptions are not silently swallowed — at minimum log the error.
- Expected failure paths (network down, file missing) are logged at `Warning`; unexpected exceptions at `Error`.
- Best-effort operations (e.g. cleanup) catch and log per-item rather than aborting the entire operation.

### Style
- All coding style rules above are followed (naming, formatting, nullability, patterns).
- Unused `using` directives removed; correct namespaces imported for any new types introduced.
- Static members are accessed through their declaring type, not through a derived type (`Application.CurrentOrNull`, not `App.CurrentOrNull`).
- `default` is not passed as an argument — explicit values used instead.
- Inline section comments (inside methods) are lowercase with no trailing period; each logical block has its own comment preceded by a blank line, including the leading and trailing blocks; member-level comments use sentence case with a trailing period.
- **Member ordering** is correct: public constants → public static fields → inner types → constants → static fields → private fields → static initializer → constructor(s) → all remaining members sorted alphabetically by name. Verify after adding, renaming, or moving any member.
