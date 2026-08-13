# Workflow

## 1. 修改代码

不要让 OpenCode 修改 C#。

把问题交给 ChatGPT/人工处理，获得新的完整脚本。

## 2. 覆盖固定输入

新代码固定放到：

```text
CSharp_Upload/FH_simple.cs
```

不要创建：

```text
FH_1.3.cs
FH_new.cs
FishingHeavenDemo_fixed.cs
final_final.cs
```

## 3. 部署

在 OpenCode 中：

```text
/fh-apply
```

## 4. 哈希

必须：

```text
SOURCE_SHA256 == DEST_SHA256
```

## 5. 编译失败

OpenCode 只报告：

```text
CODE
FILE
LINE
COLUMN
MESSAGE
```

不要让它自动修。

## 6. 下一版

ChatGPT 生成新代码后，再覆盖同一个：

```text
FH_simple.cs
```

版本历史依靠 Git 和代码内：

```text
【1.x修改】
```

进行追踪。
