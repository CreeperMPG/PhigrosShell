# AGENTS.md — PhigrosShell

基于命令的交互式命令行 Shell，提供虚拟文件系统（VFS）来管理 Phigros 存档。本文件是**中性项目说明**。

## 定位

```
PhigrosShell ──→ 引用 CreeperMPG.PhiKits.Save
```

| 项目 | 类型 | 目标框架 | 产出 |
|---|---|---|---|
| CreeperMPG.PhiKits.Save | 类库 | `net6.0;net10.0` | `.dll` 引用库 |
| **PhigrosShell**（本仓库） | 控制台应用 | `net6.0` | **`phishell.exe`**（CLI） |

> 三者是**各自独立的 git 仓库**，同级放在 `source/repos/Phigros/` 下。GUI 版本（PhiShellStudio）**不引用**本仓库。
> `CreeperMPG.PhiKits.Save` 是 `PhigrosArchive` 的**重写版**，本仓库已于 2026-09 切换到它；
> `PhigrosArchive` 仍留在磁盘上，但**本仓库不再引用**。
> 工程引用路径有两层同名目录：`..\..\CreeperMPG.PhiKits.Save\CreeperMPG.PhiKits.Save\src\CreeperMPG.PhiKits.Save\CreeperMPG.PhiKits.Save.csproj`。

## 核心概念：VFS

**VFS 通过识别类中的「属性」（注意不是字段）**，并处理 `IDictionary` / `IEnumerable` 的特殊形式，形成目录结构进行管理。

这条是理解整个项目的关键：存档对象 → 反射其属性 → 映射成虚拟目录树 → 用命令（`cd`/`ls`/`print`/`touch`/`modify`/`remove`）操作。加新的可遍历结构时，**用属性而不是字段**。

### 投影层：数据与目录结构是分开的

库的对象形状（`SavePackage` 把五个条目平铺、`SaveFileInfo` 与 `SaveFile` 分离）并不等于好用的目录树，
所以 `Mapping/` 里做了一层**只读投影**：

```
ShellPlayerRoot.SaveFiles : List<ShellSlotRoot>     ← 属性，VFS 可遍历
        └─ ShellSlotRoot.Slot : ShellSaveSlot        ← 字段，VFS 看不见
                ├─ SaveInfoObject?  Info             ← 云端元信息 + 摘要
                └─ SavePackage?     File             ← 下载后才非 null
```

要点：

- `ShellSaveSlot` 的成员基本都是**字段**（`Info` / `File` / `SlotIndex`），故意让 VFS 穿不透；
  暴露给 VFS 的只有 `ShellSlotRoot` 上那几个计算属性。
- 属性是**实时委托**（`=> Slot?.File?...`），所以 `save fetch` 之前那几个条目显示 `(null)`，fetch 之后同一路径下就出来了。
- 槽位列表按云端 `SaveUpdateTime` **从晚到早**排序，`save` 命令的槽位号就是排出来的序号；
  槽位增删后必须 `Shell.RebuildPlayerRoot()`，否则 VFS 里挂的还是旧的 `ShellSaveSlot`。
- `ShellSlotRoot.GameRecord` 直接返回 `GameRecord.Records` 字典——**把 `Records` 这一层拿掉了**，
  路径是 `/SaveFiles/0/GameRecord/<曲目 ID>/IN/Score` 而不是 `/.../GameRecord/Records/...`。

### VDirectory 的读写模型

**路径遍历只有一份实现**（`VDirectory.Resolve`），返回「容器 + 定位方式」：
目标由 `Dictionary` / `List` / `Property` 三者之一描述，读与写都建立在它之上。
加新容器类型只需改 `Resolve` 的分支和写操作的分派——**别再各写一份遍历**（历史上
`ResolvePath` / `ListEntries` / `Get` / `Exists` / `IsDisallowToModify` 各写了一遍，必漏）。

| 操作 | 命令 | 行为 |
|---|---|---|
| `Set` | `modify` | 改**已存在**的目标：属性赋值 / 覆写字典项 / 替换列表元素 |
| `Create` | `touch` | 让它**存在**：字典新建键、列表**末尾追加**、把值为 `null` 的属性实例化 |
| `Remove` | `rm` | 字典与列表**真删除**；普通属性**置 null**（需白名单） |

三个写操作都返回 **`WriteResult` 枚举**（不是 `bool`）：命令层据此给出**准确**的提示。
只返回 bool 时只能拿"路径存不存在"去猜失败原因，曾把"类型没开白名单"报成"已存在"。

- ⚠️ **`touch` 判的是「值」不是「属性存在与否」**：值为 `null` 的属性**不算已存在**，
  那正是要实例化的对象；只有**存在且非 null** 才返回 `WriteResult.AlreadyExists`。
- 类型转换统一走 `TryConvert`：**`InvariantCulture`**（中文区域下 `"3.14"` 否则解析失败）、
  支持 `enum` / `bool` / 数字，失败返回 `WriteResult.BadValue`（**不抛异常**）。
- `ls` 的 `|rX|` 会**继承**父节点的保护：受保护节点下的条目全部标只读。
  另外注意槽位层（`ShellSlotRoot`）是**只读投影**（`=>` 表达式体没有 setter），
  所以那里全是 `|rX|` 是**正常的**——真正可写的是再往里一层的 `SongDifficultySet` 难度属性。

### 写操作白名单（默认全禁，按需显式开）

`VDirectoryTypeRegistry` 的 `CanInstantiate` / `CanResetToNull` **默认都是 false**——
写操作的"忘了配"不该变成"能乱来"。

| 开关 | 含义 | 谁开了 |
|---|---|---|
| `CanInstantiate` | `touch` 时允许 `new` 一个默认实例 | `LevelRecord`、`SongDifficultySet<>` |
| `CanResetToNull` | `rm` 时允许置 `null` | `LevelRecord`、`SongDifficultySet<>` |

> ⚠️ **`CanResetToNull` 检查的是「被置 null 的那个值的类型」**，不是容器的类型。
> `SongDifficultySet<T>.EZ` 的类型是 `T`（`LevelRecord`），所以要挂在 `LevelRecord` 上——
> 挂在 `SongDifficultySet<>` 上不会生效。（实测踩过。）

> 🚨 **`Register` 是同类型「累加」而不是「覆盖」**——必须如此！
> 曾把 `LevelRecord` 注册两次（一次白名单、一次预览），覆盖语义让第二次把
> `CanInstantiate` / `CanResetToNull` **清成 false**；`SaveInfoObject` 同理，
> **`DisallowModify` 被预览注册冲掉，只读保护直接失效**。
> 修法：`Register` 复用已存在的 `VDirectoryTypeInfo` 再 `configure`。
> 现在每个类型仍**只注册一次**（所有行为写在一起），但别再依赖"顺序"这种东西。
>
> 📌 这类 bug 很隐蔽：**单次注册测不出来**，编译也测不出来。写类型注册时务必确认
> 同一个类型没有被注册两遍。

- **值类型不受 `CanInstantiate` 限制**（`new int()` 是 0，没副作用）；引用类型必须显式开。
- `rm` 对**值类型属性**一律拒绝（没法置 null）。

### 只读保护

`DisallowModify` 挂在**路径上任一环节**的类型上，命中即整条路径不可写：

| 类型 | 为什么 |
|---|---|
| `SaveInfoObject` | 云端元信息；本地改了没意义，下次刷新就被覆盖 |

> 五个 `SaveEntry`（`PhigrosProgress` / `User` / `Settings` / `Record` / `Key`）
> **都是可写数据，刻意不设保护**——这是存档修改器的本分。

### 预览

`PreviewGenerator` 按类型注册；字典/列表里的元素也会走它（`RenderValue`），
所以没重载 `ToString` 的类型不会露出完整类型名。文本经 `Sanitize`（去控制字符）
与 `TruncatePreview`（**按显示宽度**截断，全角算 2）处理。

> ⚠️ `VDirectoryTypeRegistry` 支持**开放泛型**注册（`Register(typeof(SongDifficultySet<>), ...)`），
> 一条规则覆盖全部封闭形式。`GetInfo` 先精确匹配，再剥泛型定义，最后沿基类链。
> 但**泛型参数不参与匹配**：挡 `LevelRecord` 有效（它是叶子），
> 别指望挡 `SongDifficultySet<>` 能连带挡住里面的值。

> ℹ️ `SongDifficultySet<T>` 带索引器（`this[int]`），反射里是叫 `Item` 的属性。
> `VDirectory` 的 `EnumerableProperties` 会**跳过索引器**——否则 `GetValue(obj)` 少了下标参数，
> `ls` 会抛 `TargetParameterCountException`。

## 存档操作命令

`save <action> <slot> [args]`，槽位号见上一节：

| action | 行为 |
|---|---|
| `fetch` | 下载并解包该槽位存档 |
| `export <path>` | 把当前存档写成 `.save` 文件 |
| `check` | 校验：游戏规则、定数表、以及"云摘要 vs 当前文件" |
| `p3b27` / `phibest [n]` | P3 + B27 分析并给出推分所需准确率 |
| `upload` | **替换式**上传：先新建云槽位，成功后再删掉旧槽位 |
| `syncsummary` | 用当前存档重算摘要，写回本地的云摘要对象 |
| `delete` | 彻底删除槽位（云端记录 + 文件） |

### ⚠️ 摘要的 RKS 必须带定数提供者

`SavePackage.GenerateSummary()` **无参重载算的是占位值**，只有 `GenerateSummary(provider)` 才是真 RKS。
`upload` 与 `syncsummary` 因此在 `difficulty.tsv` 未加载时**直接拒绝执行**，
否则会把占位分数写进云端摘要。

### ⚠️ 上传是"新建 + 删旧"，不是就地更新

新库的 `PlayerObject.UploadSave` 只会**新建**槽位（旧的 PUT 接口已废弃）。
Shell 承担"替换"语义：**先新建、后删旧**——反过来一旦新建失败就把云端存档白删了。
删旧失败只提示、不中断（最坏是云端多留一个旧槽位，用 `save delete` 收拾）。

## 依赖

- `CreeperMPG.PhiKits.Save`（项目引用）— 存档格式、云 API、TapTap 登录
- `FluentConsole` v0.8.3 — 彩色控制台输出
- `SixLabors.ImageSharp` v3.1.11 — 图片处理（二维码）
- `ZXing.Net` + `ZXing.Net.Bindings.ImageSharp` — 二维码生成

## 目录结构

```
PhigrosShell/
├── AppConfig.cs              ← 应用配置
├── Shell.cs                  ← Shell 主循环 + VFS 根的重建
├── CommandBase.cs            ← 命令基类
├── CommandManager.cs         ← 命令管理器
├── Check/                    ← 存档校验（不依赖控制台）
│   ├── SaveChecker.cs        ← 校验规则
│   └── SaveDataIssue.cs      ← IssueSeverity / IssueType / SaveDataIssue
├── Commands/                 ← 所有命令实现
│   ├── AboutCommand.cs / AliasCommand.cs / ClearCommand.cs
│   ├── ConfigCommand.cs / ExitCommand.cs / HelpCommand.cs / PauseCommand.cs
│   ├── Phigros/              ← Phigros 相关命令
│   │   ├── DownloadPhiInfoCommand.cs
│   │   ├── LoginCommand.cs / LogoutCommand.cs
│   │   ├── RefreshSessionTokenCommand.cs
│   │   └── SaveCommand.cs / WhoAmICommand.cs
│   └── VFileSystem/          ← 虚拟文件系统命令
│       ├── CdCommand.cs / ClearAllCommand.cs / LsCommand.cs
│       ├── ModifyCommand.cs / PrintCommand.cs / RemoveCommand.cs / TouchCommand.cs
├── Mapping/                  ← VFS 投影层
│   ├── ShellPlayerRoot.cs / ShellSaveSlot.cs
│   ├── ShellSession.cs / ShellSlotRoot.cs
├── PhiInfo/                  ← 谱面信息（info.tsv）
│   └── InfoTSV.cs
├── Services/
│   ├── LocalizationService.cs  ← 界面文案（key → string）
│   └── ChangelogService.cs     ← 更新日志（结构化的版本记录）
├── Utils/
│   ├── ConsoleUtils.cs / LoadUtils.cs / QRUtils.cs / TsvUtils.cs
│   ├── PathUtils.cs          ← VFS 路径解析（相对路径 / . / ..）
│   └── RankingScoreAdvisor.cs ← P3B27 推分数学（纯函数）
└── VFS/                      ← 虚拟文件系统实现
    ├── VDirectory.cs / VDirectoryTypeRegistry.cs
    ├── WriteResult.cs        ← 写操作结果枚举（modify/touch/rm 共用）
    ├── VEntry.cs / VEntryType.cs
```

## 本地化

`Resources/lang/en.json`、`Resources/lang/zh-cn.json` —— 新增用户可见文本时**两边都要加**。

存档校验的文案键按约定命名：`IssueType` 枚举名加上 `Warn` / `Info` 前缀
（见 `SaveDataIssue.LocalizationKey`），所以**新增一个 IssueType 就要同时补两份语言文件**。

### 更新日志单独存放（`Resources/changelog/`）

```jsonc
{
  "entries": [
    { "version": "1.3.0", "date": "2026-09-24 00:02",
      "changes": [ "…", "…" ] }
  ]
}
```

**为什么不放进 `lang/`**：那里是扁平的 `key → string` 表，表达不了"版本 → 一串条目"
（硬塞只能写成 `"UpdateLog.1.2.0.3"`，版本结构在键名里重复）。**changelog 是内容，
lang 是文案**，生命周期不同，分开才不打架。

- **数组顺序就是显示顺序**（约定**从新到旧**）；`date` 是独立字段，不再靠"第一行是日期"。
- `date` **不随语言变化**（是数据不是文案），所以两份文件里日期完全相同，方便比对。
- 新增版本：在**两份** `changelog/*.json` 的 `entries` **头部**各加一项，不用动代码。
- ⚠️ 发版时**三处版本号要一起改**：`Program.Version`、`changelog` 首项、README。

## 构建

```bash
dotnet build
# 产出 phishell.exe
```

### ⚠️ 受限环境里先关掉 MSBuild server

在某些沙箱 / 受限环境下，.NET 10 的 MSBuild server 会死掉，
只报 `MSB5021 终止 csc`，**把真实的编译错误完全掩盖**：

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
$env:MSBUILDDISABLENODEREUSE='1'
dotnet build -m:1 -nodeReuse:false -p:UseSharedCompilation=false
```

### ⚠️ 没有真控制台就跑不起来

`Shell.InitShell` 一上来就调 `Console.WindowWidth` / `Console.CursorLeft`，
**输出被重定向时（无控制台句柄）会抛 `IOException: 句柄无效`**。
所以这个程序没法在 CI 里用管道喂命令——要验证得开真终端。

### ⚠️ 只构建本工程（依赖库在另一个仓库）

本仓库引用的 `CreeperMPG.PhiKits.Save` 是**同级但在工作区之外**的独立仓库。
受限环境里 `dotnet build` 会连带重建它、写它的 `obj`/`bin` 而被拒绝（`MSB3491`/`MSB3021`）。

**✅ 正确做法：申请覆盖该路径的权限**（会话沙箱的 `workspace-write` 覆盖不到工作区之外的仓库，
要 `danger-full-access`）。批准一次就能跑完整的解决方案构建。

**临时替代**（不方便提权时）：依赖库已经构建过的话，只构建本工程即可——
这仍然会跑完整的 target 图，**包含** `GenerateResource`：

```powershell
dotnet build PhigrosShell/PhigrosShell.csproj `
    -p:BuildProjectReferences=false -p:ResolveProjectReferences=false
```

### 🚨 绝不要用 `-t:Compile` 或 `-p:OutDir` 绕开问题

- **`-t:Compile` 只跑编译，不含 `GenerateResource`** → 产出的 `phishell.dll`
  **没有嵌入的语言资源** → 运行时 `LocalizationService` 两个 json 都加载不到，
  **回落到 `LoadDefaults()`（全英文）**。曾因此误报"本地化炸了"。
- **`-p:OutDir` / `-p:BaseIntermediateOutputPath`** 会被**子项目继承**，
  导致 `AssemblyInfo` 重复生成（`CS0579`），还会**清掉依赖库的 `bin`**。

> **"编译通过"不等于"产物正确"**。验证构建就跑**完整 build**；
> 缺权限就**申请**（一次弹窗），**不要用构建参数绕**——绕出来的产物不能用。

