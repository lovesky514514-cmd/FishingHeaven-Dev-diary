# Fishing Heaven OpenCode Agent

用于《钓鱼天国》项目的 OpenCode 辅助开发工作流。

这个仓库主要解决一个问题：

> 让 OpenCode 负责工程操作、代码部署、编译检查和错误定位，但不允许它擅自修改核心 C# 代码。

目前项目使用团结引擎开发，核心游戏脚本由外部确认版本后统一放入 `CSharp_Upload/FH_simple.cs`，再由 OpenCode 部署到实际工程中。

2026.08.14 添加了 clickhere.bat 和 Check&Repair.pdf 

> clickhere.bat 用于一键启动该项目。
> Check&Repair.pdf 用于查错和修复，是一份帮助文档。

# 若只希望下载该智能体项目:

> 请下载 FHOA.zip 

---

## 这个仓库是做什么的

在之前的开发过程中，OpenCode 如果直接参与修改游戏代码，很容易出现：

- 自动重写已经确认的逻辑
- 修改类名或文件结构
- 自动修复后引入新的问题
- 同时存在多份不同版本脚本
- 旧工程和新工程混用
- 为了辅助操作额外启动 Web 页面或本地服务
- 很难判断现在运行的到底是哪一版代码

所以这个仓库把工作分成两部分：

**C# 代码修改**

由开发者确认后提供新的 `FH_simple.cs`。

**OpenCode**

只负责把已经确认的代码安全地放进工程，并检查它能不能正常运行。

---

## 当前工作方式

核心入口固定为：

```text
CSharp_Upload/FH_simple.cs
```

无论当前内部版本是：

```text
FH 1.2
FH 1.3
FH 1.4
...
```

上传文件的名字都保持：

```text
FH_simple.cs
```

版本信息直接写在代码内部。

例如：

```csharp
// 【1.2修改】修改内容：修复 AudioListener 缺失导致游戏无声音。
```

下一次修改：

```csharp
// 【1.3修改】修改内容：……
```

---

## 目录结构

```text
fishing-heaven-opencode-agent/

├─ .opencode/
│  ├─ agents/
│  └─ commands/
│
├─ CSharp_Upload/
│  ├─ FH_simple.cs
│  └─ README.md
│
├─ FH_Agent/
│  └─ scripts/
│
├─ docs/
│
├─ AGENTS.md
├─ README.md
├─ CHANGELOG.md
├─ VERSION
├─ .gitignore
└─ .gitattributes
```

### `CSharp_Upload`

保存当前准备部署到游戏工程中的 C# 文件。

目前统一入口：

```text
CSharp_Upload/FH_simple.cs
```

使用方式详见 CSharp_Upload 文件夹中的 README.md 文档

### `.opencode`

保存项目使用的 OpenCode Agent 和 Commands。

这里定义 OpenCode 在当前项目中允许做什么，以及禁止做什么。

### `FH_Agent/scripts`

保存工程部署、备份、检查等辅助脚本。

### `docs`

保存工作流和其他开发说明。

---

# OpenCode 的职责

OpenCode 在这个仓库中主要负责：

1. 读取新的 `FH_simple.cs`
2. 检查目标工程是否存在
3. 备份当前工程里的旧脚本
4. 计算源文件 SHA256
5. 将 `FH_simple.cs` 原样部署到游戏工程
6. 再次计算目标文件 SHA256
7. 确认部署前后内容完全一致
8. 检查团结引擎编译结果
9. 检查场景和组件是否正常
10. 报告具体错误

它不负责决定游戏代码应该怎么修改。

---

# OpenCode 不应该做什么

默认情况下，不希望 OpenCode：

```text
自行修改 FH_simple.cs
自动修复 C#
重构游戏逻辑
格式化整个游戏脚本
删除版本修改注释
改变 MonoBehaviour 类名
创建重复的主游戏脚本
自行重新制作场景
创建新的游戏工程
启动 Web Dashboard
创建 .fishdev
启动 Node 本地网页
为了“修复问题”直接重写玩法代码
```

如果编译失败，优先报告错误，而不是直接修改代码。

例如：

```text
ERROR CODE:
CSxxxx

FILE:
Assets/Scripts/FishingHeavenDemo.cs

LINE:
xxx

COLUMN:
xx

MESSAGE:
...
```

然后由开发者决定下一步修改方式。

---

# 使用方法

将新的 C# 文件覆盖到：

```text
CSharp_Upload/FH_simple.cs
```

然后在项目目录中启动 OpenCode。

根据当前仓库提供的 Command 执行部署。

OpenCode 应先备份旧代码，然后将：

```text
CSharp_Upload/FH_simple.cs
```

部署为游戏工程中的：

```text
Assets/Scripts/FishingHeavenDemo.cs
```

注意：

虽然上传文件叫：

```text
FH_simple.cs
```

但实际团结工程中的文件仍然保持：

```text
FishingHeavenDemo.cs
```

因为当前主类为：

```csharp
public class FishingHeavenDemo : MonoBehaviour
```

---

# 开发流程

```text
发现问题
↓
确认问题原因
↓
修改核心 C#
↓
添加版本修改注释
↓
保存为 FH_simple.cs
↓
交给 OpenCode
↓
备份当前工程版本
↓
SHA256 校验
↓
部署
↓
团结引擎重新编译
↓
进入 Play Mode 测试
↓
出现问题则只返回日志
↓
继续下一版本
```

核心原则是：

> OpenCode 操作工程，C# 修改保持可控。

---

# 版本注释

代码中的修改尽量保留明确版本标记。

例如：

```csharp
// 【1.2修改】修改内容：修复运行时没有 AudioListener 导致的无声音问题。
```

或者：

```csharp
// 【1.3修改】修改内容：调整 Fever 阶段鱼群生成逻辑。
```

旧版本注释不需要删除。

这样可以直接在一个文件里搜索：

```text
【1.1修改】
【1.2修改】
【1.3修改】
```

---

# 引擎

当前项目使用：

```text
团结引擎
Tuanjie Engine
```

当前 C# 仍使用兼容的：

```csharp
using UnityEngine;
```

这不代表项目使用 Unity Editor。

---
