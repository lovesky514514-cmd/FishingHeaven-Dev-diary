# CSharp_Upload

这里是 OpenCode 智能体的 **唯一 C# 输入目录**。

正式输入文件固定命名为：

```text
FH_simple.cs
```

## 使用方式

更新完整 C# 版本后：

1. 不要改文件名。
2. 直接用新文件覆盖这里的 `FH_simple.cs`。
3. 在游戏工程根目录启动 OpenCode。
4. 执行：

```text
/fh-apply
```

智能体会将：

```text
CSharp_Upload/FH_simple.cs
```

安全部署到：

```text
Assets/Scripts/FishingHeavenDemo.cs
```

部署前会备份旧版本，部署后会比较 SHA256。

## 重要

`FH_simple.cs` 是输入源。

OpenCode **不允许直接修改它**，也不允许在部署后自行修改 `FishingHeavenDemo.cs`。

如果代码需要修复，应先由人工生成新的完整版本，再覆盖 `FH_simple.cs`。
