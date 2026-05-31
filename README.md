# PhigrosShell

基于命令的交互式 Shell，通过**虚拟文件系统**管理 Phigros 存档数据。

输出文件：`phishell.exe`

## 快速开始

```bash
# 构建
dotnet publish -f net10.0 -c Release -r win-x64 --self-contained

# 运行
phishell.exe
```

## VFS 虚拟文件系统

VFS 将 Phigros 存档数据映射为目录结构，使用标准的文件系统命令操作：

```
/                          ← 根目录
├── SaveFiles/                 ← 存档槽位
│   ├── 0/
│   │   ├── Summary        ← 概要信息
│   │   ├── GameRecords       ← 游戏存档
│   │   └── Settings       ← 设置
│   └── 1/
│       └── ...
└── ...                ← 玩家信息
```

VFS 通过识别类属性（而非字段）构建目录树，自动处理 `IDictionary`、`IEnumerable` 等特殊类型。

## 可用命令

| 命令 | 说明 |
|------|------|
| `cd <path>` | 切换当前目录 |
| `ls [path]` | 列出目录内容 |
| `print <path>` | 查看节点内容 |
| `touch <path>` | 创建/修改节点 |
| `remove <path>` | 删除节点 |
| `modify <path>` | 交互式修改节点 |
| `clear` | 清屏 |
| `help` | 显示帮助 |
| `exit` | 退出 |

### Phigros 相关命令

| 命令 | 说明 |
|------|------|
| `login` | 使用 TapTap 扫码登录 |
| `logout` | 退出登录 |
| `whoami` | 查看当前用户 |
| `save` | 手动保存存档 |
| `refresh` | 刷新 Session Token |
| `download-phi-info` | 下载谱面定数信息 |

## 本地化

- `Resources/lang/en.json` — English
- `Resources/lang/zh-cn.json` — 简体中文

## NuGet 依赖

- `FluentConsole` — 彩色控制台输出
- `SixLabors.ImageSharp` — 图片处理（二维码生成）
- `ZXing.Net` + `ZXing.Net.Bindings.ImageSharp` — 二维码编解码