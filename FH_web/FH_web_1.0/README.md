# Fishing Heaven Official Website

《钓鱼天国》游戏官网占位工程。

## 当前规则

- 网站从空目录重新编写，不继承旧版页面 DOM。
- 同一时刻只显示一个 `.page`，避免素材与旧页面互相重叠。
- 换页使用独立 `page-transition` 遮罩，遮罩覆盖后才切换页面。
- 所有未完成视觉内容只用简单矩形/方块占位，不画假鱼、假角色或假场景。
- 左上角只使用一次游戏图标。
- 加载界面使用透明哥布林图片；保留左右翻转、上下弹跳和轻微旋转；没有地面阴影。
- 加载进度是内联原生 JavaScript，即使主站脚本报错也不会永久卡在 0%。

## 页面

- INDEX
- PLAY
- FEVER
- WORLD
- MEDIA
- MORE

## Developer Agent

设置中的 Agent 仅为 UI 占位：

- `/fh-status`
- `/fh-verify`
- `/fh-apply`

公开网页不会直接修改本地游戏工程。

## 预览

可直接打开 `index.html`。也可以运行 `preview.bat` 使用本地 HTTP 服务器。


## 按键弹性

所有主要交互控件使用原生 Web Animations API：

- Hover：轻微上浮
- Press：压缩到约 `0.94`
- Release：`1.07 → 0.985 → 1.025 → 1` 多段回弹
- 设置里关闭动态效果后会同步关闭弹性动画

不依赖外部 Motion/CDN。
