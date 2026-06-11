# PicScreenSaver

## 技术设计文档 v1.4

|项目  |内容                           |
|----|-----------------------------|
|文档版本|v1.4                         |
|技术栈 |C# / WPF / .NET Framework 4.8|
|目标平台|Windows 7 SP1 / 8.1 / 10 / 11|
|输出格式|.SCR（Windows 标准屏保）           |
|文档日期|2026-06-04                   |

-----

## 目录

1. [项目概述](#1-项目概述)
1. [系统架构](#2-系统架构)
1. [功能模块设计](#3-功能模块设计)
1. [Windows 屏保协议](#4-windows-屏保协议)
1. [关键技术实现](#5-关键技术实现)
1. [依赖与开发环境](#6-依赖与开发环境)
1. [开发阶段规划](#7-开发阶段规划)
1. [配置文件格式](#8-配置文件格式)
1. [风险与注意事项](#9-风险与注意事项)

-----

## 1. 项目概述

### 1.1 目标

构建一款轻量、免费的 Windows 图片屏保制作工具。用户导入图片、配置参数后，一键生成标准 `.SCR` 文件，在目标机器上无需安装任何依赖即可运行。

### 1.2 设计原则

|原则              |说明                                         |
|----------------|-------------------------------------------|
|**零依赖运行**       |生成的 .scr 在任何目标机器上双击即用，无需安装运行库              |
|**全版本兼容**       |Windows 7 SP1 / 8.1 / 10 / 11，32位 / 64位 全覆盖|
|**轻量输出**        |图片统一降至 1080P + JPEG 压缩，控制 .scr 文件体积        |
|**原生 GPU 加速**   |WPF Storyboard 动画自动利用 DirectX 硬件加速         |
|**Windows 规范兼容**|完整响应 `/s` `/c` `/p` 命令行参数                  |

### 1.3 运行时框架选择：.NET Framework 4.8

.NET Framework 4.8 是 Windows 操作系统的内置组件：

- **Windows 10 / 11**：出厂预装，无需任何操作
- **Windows 8.1**：通过 Windows Update 自动获取
- **Windows 7 SP1**：通过 Windows Update 自动获取

目标机器几乎不需要主动安装。极端情况（Win7 长期未联网更新）下，运行时启动检测到版本不足，弹窗提示用户前往微软官网下载 .NET Framework 4.8 离线安装包（66MB）。

所有 20 种过渡动画使用的 WPF API（`Storyboard`、`DoubleAnimation`、`TranslateTransform`、`ScaleTransform`、`PlaneProjection`、`ClipRect`）在 .NET Framework 4.8 上完全支持。

-----

## 2. 系统架构

### 2.1 整体架构

项目由两个独立可执行程序组成，通过「模板嵌入 + PE 资源注入」机制协作：

```
┌──────────────────────────────────────────────────────┐
│           制作器  PicScreenSaver.exe           │
│  运行于：制作者的 Win10+ 电脑                          │
│                                                      │
│  ┌────────────┐  ┌───────────────┐  ┌─────────────┐ │
│  │  WPF GUI   │  │ ImageProcessor │  │ Package     │ │
│  │  主界面     │  │ 降采样+JPEG压缩 │  │ Builder     │ │
│  │  参数配置   │  │               │  │ PE资源注入   │ │
│  └────────────┘  └───────────────┘  └─────────────┘ │
│                                                      │
│  内嵌资源：PicScreenSaver.Runtime.exe（运行时模板）        │
└───────────────────────┬──────────────────────────────┘
                        │ 构建输出
                        ▼
┌──────────────────────────────────────────────────────┐
│                   MySaver.scr                        │
│  运行于：Win7 SP1 及以上的任意目标机器                 │
│                                                      │
│  ┌──────────────────────────────────────────────┐   │
│  │      PicScreenSaver.Runtime（.NET FX 4.8 WPF）    │   │
│  │  命令行解析 → 幻灯片引擎 → 20种过渡动画        │   │
│  └──────────────────────────────────────────────┘   │
│  ┌─────────────────┐  ┌──────────────────────────┐  │
│  │  config.json    │  │  JPEG 图片 × N           │  │
│  │  （PE 资源节）   │  │  （PE 资源节，1080P压缩） │  │
│  └─────────────────┘  └──────────────────────────┘  │
└──────────────────────────────────────────────────────┘
```

### 2.2 构建流程

|步骤|操作   |输入                            |输出            |
|--|-----|------------------------------|--------------|
|① |图片预处理|原始图片（JPG/PNG/BMP/TIFF）        |1080P JPEG 字节流|
|② |生成配置 |UI 中所有参数                      |config.json   |
|③ |克隆运行时|内嵌的 PicScreenSaver.Runtime.exe|临时可执行副本       |
|④ |资源注入 |JPEG 图片 × N + config.json     |含资源节的 PE 文件   |
|⑤ |重命名输出|临时 PE 文件                      |OutputName.scr|

### 2.3 目录结构

```
PicScreenSaver/
├── PicScreenSaver.Maker/
│   ├── PicScreenSaver.Maker.csproj
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / .cs
│   ├── Views/
│   │   ├── ImageListPanel.xaml          # 图片列表（拖拽排序）
│   │   ├── EffectSelectorPanel.xaml     # 效果复选框分组面板
│   │   └── PreviewWindow.xaml           # 实时预览窗口
│   ├── Models/
│   │   ├── ScreensaverConfig.cs
│   │   └── ImageItem.cs                 # 含缩略图、原始尺寸、压缩后体积
│   ├── Services/
│   │   ├── ImageProcessor.cs            # 降采样 + JPEG 压缩
│   │   ├── PackageBuilder.cs            # PE 资源注入，输出 .scr
│   │   └── PreviewService.cs
│   └── Resources/
│       └── PicScreenSaver.Runtime.exe       # 编译后内嵌的运行时模板
│
└── PicScreenSaver.Runtime/
    ├── PicScreenSaver.Runtime.csproj
    ├── Program.cs                       # 入口 + 命令行参数解析
    ├── ScreensaverWindow.xaml / .cs     # 全屏主窗口
    ├── Engine/
    │   ├── SlideEngine.cs               # 幻灯片调度状态机
    │   ├── TransitionManager.cs         # 效果轮调度器
    │   └── Transitions/
    │       ├── ITransition.cs           # 统一接口
    │       ├── FadeTransition.cs
    │       ├── SlideTransition.cs
    │       ├── ZoomTransition.cs
    │       ├── WipeTransition.cs
    │       └── SpecialTransition.cs
    ├── ResourceLoader.cs                # PE 自身资源读取
    └── ConfigDialog.xaml / .cs          # /c 参数对应的信息对话框
```

-----

## 3. 功能模块设计

### 3.1 制作器主界面

界面采用 Tab 结构，分为三个页面。标题栏与「生成 .scr」按钮全局固定，三个 Tab 风格统一。

#### 公共框架（所有 Tab 共享）

```
+--------------------------------------------------------------+
|  o  o  o          PicScreenSaver              [生成 .scr ↑]  |
+------------+------------+----------------------------------   |
| [图片导入]  |   [效果]   |   [设置]                           |
+------------+------------+------------------------------------+
```

-----

#### Tab 1 · 图片导入

```
+--------------------------------------------------------------+
| [+ 添加]  [↑ 上移]  [↓ 下移]  [x 移除]   7 张图片 · 点击预览  |
+------------------------------------------+-------------------+
|  图片网格（4列，支持拖拽调整顺序）            |  预览              |
|                                           |  +---------------+|
|  +------+ +------+ +------+ +------+      |  |               ||
|  |  01  | |  02  | |  03  | |  04  |      |  | [选中图大图]   ||
|  |  山  | |  树  | |  海  | |  星  |      |  |               ||
|  | fname| | fname| | fname| | fname|      |  +---------------+|
|  | 680KB| | 412KB| | 892KB| | 2.1M |      |                   |
|  +------+ +------+ +------+ +------+      |  文件名 mount.jpg  |
|                                           |  原始   3840x2160  |
|  +------+ +------+ +------+ +------+      |  原大小 5.2 MB     |
|  |  05  | |  06  | |  07  | |  +   |      |  压缩后 680 KB     |
|  |  麦  | |  浪  | |  花  | |拖入或|      |  压缩率 87%        |
|  | fname| | fname| | fname| |点击  |      |  ─────────────── |
|  | 340KB| | 756KB| | 521KB| |添加  |      |  播放顺序          |
|  +------+ +------+ +------+ +------+      |  [顺序] [随机 √]  |
+------------------------------------------+-------------------+
|  7 张图片  |  预计体积 ~4.2 MB  |  压缩质量 75%                 |
+--------------------------------------------------------------+
```

-----

#### Tab 2 · 效果

```
+--------------------------------------------------------------+
|  选择效果                          [全选]  [清空]             |
+-------------------------------+------------------------------+
|  淡变                          |                             |
|  [√] Fade    [√] FadeBlack    |                             |
|  [ ] FadeWhite  [√] CrossFade |    [ 效果预览大图 ]           |
|  ────────────────────────     |                             |
|  滑动                          |      ▶ 点击播放预览          |
|  [√] SlideLeft  [ ] SlideRight|                             |
|  [ ] SlideUp    [ ] SlideDown |  ───────────────────────── |
|  ────────────────────────     |  Fade                       |
|  缩放                          |  旧图渐隐，新图渐现           |
|  [√] ZoomIn    [ ] ZoomOut    |                             |
|  [ ] ZoomInFade [ ] ZoomOutF  |              已选 6 种       |
|  ────────────────────────     |                             |
|  擦除                          |                             |
|  [ ] WipeLeft  [ ] WipeRight  |                             |
|  [ ] WipeUp    [ ] WipeDown   |                             |
|  ────────────────────────     |                             |
|  特效                          |                             |
|  [ ] FlipH    [ ] FlipV       |                             |
|  [√] PushLeft  [ ] PushUp     |                             |
+-------------------------------+------------------------------+
```

-----

#### Tab 3 · 设置

```
+--------------------------------------------------------------+
|  播放时长                        输出                         |
|  +─────────────────────────+   +──────────────────────────+  |
|  | 每张展示时长  [- 5.0 +] 秒|   | 屏保名称 [MyScreensaver].scr|  |
|  | 过渡动画时长  [- 1.2 +] 秒|   | 输出路径 [C:\Users\...\][浏览]| |
|  +─────────────────────────+   | 生成后安装  [否] [是 √]    |  |
|                                 +──────────────────────────+  |
|  图片压缩                        文件体积预估                  |
|  +─────────────────────────+   +──────────────────────────+  |
|  | JPEG 压缩质量             |   |  4.2 MB                  |  |
|  | 50% ────────●──── 90%   |   |  ████░░░░░░░░░░░░        |  |
|  |             75%          |   |  图片（7张）  ~4.0 MB     |  |
|  +─────────────────────────+   |  运行时壳    ~200 KB      |  |
|                                 |  配置文件    ~1 KB        |  |
|                                 +──────────────────────────+  |
+--------------------------------------------------------------+
```

### 3.2 参数说明

|参数    |类型  |默认值             |范围       |说明       |
|------|----|----------------|---------|---------|
|每张展示时长|数字输入|5.0 秒           |1 ~ 60 秒 |图片静止展示的时间|
|过渡动画时长|数字输入|1.2 秒           |0.3 ~ 5 秒|切换动画播放时长 |
|播放顺序  |单选  |随机              |顺序 / 随机  |图片播放顺序   |
|过渡效果  |复选框组|Fade + SlideLeft|20 种多选   |随机排列后轮流循环|
|压缩质量  |滑块  |75%             |50% ~ 90%|JPEG 编码质量|
|输出分辨率 |单选  |1080P           |原图 / 2K / 1080P|图片降采样上限|


> **动画速度曲线**：固定为 `Linear`（匀速），无需用户选择，保持简洁。
> 
> **图片分辨率**：用户可选输出分辨率（原图/2K/1080P），默认 1080P。超出设定上限时等比缩小，未超出保持原尺寸。

### 3.3 图片压缩处理

#### 压缩策略

所有图片经过两步处理后以 JPEG 格式嵌入 .scr，兼容所有 Windows 版本：

```
第一步：分辨率降采样（由用户选择）
  原图：不缩放，保持原始分辨率
  2K：宽度 > 2560 或 高度 > 1440
      → 等比缩小至 2560×1440 以内
      → 未超出：保持原分辨率
  1080P（默认）：宽度 > 1920 或 高度 > 1080
      → 等比缩小至 1920×1080 以内
      → 未超出：保持原分辨率

第二步：JPEG 编码
  调用 Windows 内置 WIC API（System.Windows.Media.Imaging）
  质量值由用户设置（默认 75，范围 50~90）
  编码完成后将字节流注入 PE 资源节
```

#### JPEG 质量值参考

|质量值        |视觉效果        |典型压缩比   |适用场景       |
|-----------|------------|--------|-----------|
|50%        |有轻微块状感，远看无影响|~15:1   |极度压缩，图片数量多 |
|60%        |细看有轻微压缩痕迹   |~10:1   |兼顾体积与质量    |
|**75%（默认）**|**肉眼几乎无差异** |**~7:1**|**推荐大多数场景**|
|90%        |接近无损        |~3:1    |画质优先       |


> 对于屏保这类全屏但观看距离较远的场景，**75% 是画质与体积的最优平衡点**，相比保存为 PNG 体积减少约 85%，视觉上无可感知差异。

#### 体积预估参考

|图片数量      |质量 50%|质量 75%（默认）|质量 90%|
|----------|------|----------|------|
|2 张 1MB原图 |~0.3MB|**~0.5MB**|~0.9MB|
|10 张 1MB原图|~1.5MB|~2.5MB    |~4.5MB|
|10 张 4K原图 |~8MB  |~18MB     |~35MB |
|20 张 4K原图 |~16MB |~36MB     |~70MB |
|30 张 4K原图 |~24MB |~54MB     |~105MB|


> 以上均为图片部分体积，加上运行时壳（~200KB）即为最终 .scr 文件大小。体积几乎完全由图片内容决定。

### 3.4 过渡效果模块（20 种）

动画引擎基于 WPF 原生 `Storyboard` + `DoubleAnimation`，速度曲线固定为 `Linear`，DirectX GPU 加速自动启用。

**效果轮机制**：多选时将已选效果做一次随机排列，按顺序播放，一轮结束后重新随机排列，保证每种效果出现频率均等，不连续重复。

#### 淡变类（4 种）

|#|效果 ID      |视觉描述          |WPF 实现               |
|-|-----------|--------------|---------------------|
|1|`Fade`     |旧图渐隐，新图渐现     |`Opacity` Animation  |
|2|`FadeBlack`|旧图淡至黑场，新图从黑场淡入|双段 `Opacity` + 黑色背景层 |
|3|`FadeWhite`|旧图淡至白场，新图从白场淡入|双段 `Opacity` + 白色背景层 |
|4|`CrossFade`|新旧图叠加交叉淡变     |双层 Image 同步 `Opacity`|

#### 滑动类（4 种）

|#|效果 ID       |视觉描述        |WPF 实现                              |
|-|------------|------------|------------------------------------|
|5|`SlideLeft` |旧图静止，新图从右侧滑入|`TranslateTransform.X`：`+Width → 0` |
|6|`SlideRight`|旧图静止，新图从左侧滑入|`TranslateTransform.X`：`-Width → 0` |
|7|`SlideUp`   |旧图静止，新图从下方滑入|`TranslateTransform.Y`：`+Height → 0`|
|8|`SlideDown` |旧图静止，新图从上方滑入|`TranslateTransform.Y`：`-Height → 0`|

#### 缩放类（4 种）

|# |效果 ID        |视觉描述               |WPF 实现                       |
|--|-------------|-------------------|-----------------------------|
|9 |`ZoomIn`     |画面从 95% 缓慢放大至 100% |`ScaleTransform` `0.95 → 1.0`|
|10|`ZoomOut`    |画面从 105% 缓慢缩小至 100%|`ScaleTransform` `1.05 → 1.0`|
|11|`ZoomInFade` |放大同时淡入             |`ScaleTransform` + `Opacity` |
|12|`ZoomOutFade`|缩小同时淡出             |`ScaleTransform` + `Opacity` |

#### 擦除类（4 种）

|# |效果 ID      |视觉描述           |WPF 实现                                |
|--|-----------|---------------|--------------------------------------|
|13|`WipeLeft` |遮罩从左向右展开，逐渐露出新图|`RectangleGeometry.Rect` Width 动画     |
|14|`WipeRight`|遮罩从右向左展开，逐渐露出新图|`RectangleGeometry.Rect` X + Width 动画 |
|15|`WipeUp`   |遮罩从上向下展开，逐渐露出新图|`RectangleGeometry.Rect` Height 动画    |
|16|`WipeDown` |遮罩从下向上展开，逐渐露出新图|`RectangleGeometry.Rect` Y + Height 动画|

#### 特效类（4 种）

|# |效果 ID           |视觉描述          |WPF 实现                                           |
|--|----------------|--------------|-------------------------------------------------|
|17|`FlipHorizontal`|以 Y 轴为中心水平翻转切换|`PlaneProjection.RotationY` `0 → 90`，换图，`-90 → 0`|
|18|`FlipVertical`  |以 X 轴为中心垂直翻转切换|`PlaneProjection.RotationX` `0 → 90`，换图，`-90 → 0`|
|19|`PushLeft`      |新旧图同步向左平移（翻页感）|旧图 `X: 0 → -Width`，新图 `X: +Width → 0`            |
|20|`PushUp`        |新旧图同步向上平移（翻页感）|旧图 `Y: 0 → -Height`，新图 `Y: +Height → 0`          |

-----

## 4. Windows 屏保协议

### 4.1 SCR 文件机制

`.scr` 文件是将扩展名改为 `.scr` 的普通 Windows PE 可执行文件。控制面板扫描 `%SystemRoot%\System32\*.scr` 并列出所有屏保。运行时编译为 `.exe` 后重命名扩展名即可被系统识别，无需额外适配。

### 4.2 命令行参数

|参数       |触发场景       |运行时行为            |
|---------|-----------|-----------------|
|`/s`     |系统激活屏保     |全屏运行幻灯片，监听任意输入后退出|
|`/c`     |用户点击「设置」   |弹出信息对话框（屏保名称、版本） |
|`/p HWND`|控制面板预览缩略图  |在指定句柄窗口内渲染，不全屏   |
|无参数      |用户直接双击 .scr|等同于 `/s`         |

### 4.3 退出机制

|触发条件     |判定逻辑                               |
|---------|-----------------------------------|
|鼠标移动     |累计位移 > 5 像素（启动后 500ms 内忽略，过滤初始定位噪声）|
|鼠标点击 / 滚轮|任意鼠标事件立即退出                         |
|键盘按键     |任意按键立即退出                           |
|多显示器     |全部显示器同时全屏，任意显示器上的输入触发退出            |

-----

## 5. 关键技术实现

### 5.1 PE 资源注入

将 JPEG 图片和 config.json 注入到运行时 PE 文件的 Resource 节，运行时通过 Windows API 从自身读取，实现单文件零依赖分发。

**制作器侧（PackageBuilder.cs）**

```csharp
var runtimeBytes = File.ReadAllBytes("PicScreenSaver.Runtime.exe");
using var editor = new PEResourceEditor(runtimeBytes);

// 注入配置
editor.AddResource("SSCONFIG", 1, Encoding.UTF8.GetBytes(configJson));

// 注入 JPEG 图片
for (int i = 0; i < images.Count; i++)
    editor.AddResource("SSIMAGE", i + 1, images[i].JpegBytes);

File.WriteAllBytes(outputPath, editor.ToBytes());
```

**运行时侧（ResourceLoader.cs）**

```csharp
var hModule = GetModuleHandle(null);

// 读取配置
var configJson = ReadResourceAsString(hModule,
    FindResource(hModule, "SSCONFIG", "SSCONFIG"));

// 读取指定索引图片
public byte[] GetImageBytes(int index)
{
    var hRes = FindResource(hModule, "SSIMAGE", index);
    return ReadResourceAsBytes(hModule, hRes);
}
```

### 5.2 幻灯片调度引擎

#### 状态机

```
Idle ──启动──▶ Displaying ──倒计时结束──▶ Transitioning
                   ▲                            │
                   └──────── 动画完成 ───────────┘

Displaying：
  展示当前图片
  Timer = 展示时长 - 过渡时长
  到期后：预加载下一张 → 触发切换

Transitioning：
  从效果轮取下一个效果
  执行 Storyboard（时长 = 过渡动画时长）
  Completed 回调 → 切换到 Displaying
```

#### 效果轮算法

```csharp
private Queue<TransitionEffect> _queue = new Queue<TransitionEffect>();
private List<TransitionEffect> _selected;

void Refill()
{
    foreach (var e in _selected.OrderBy(_ => Guid.NewGuid()))
        _queue.Enqueue(e);
}

public TransitionEffect Next()
{
    if (_queue.Count == 0) Refill();
    return _queue.Dequeue();
}
```

#### 图片内存管理

运行时同时在内存中保持最多 3 张解码图片（当前帧、下一帧预加载、上一帧缓存），其余按需从 PE 资源读取并解码，避免大量图片撑爆内存。

```csharp
// 在 Displaying 阶段后台预加载下一张，消除切换卡顿
private void PreloadNext(int nextIndex)
{
    Task.Run(() => {
        var bytes = ResourceLoader.GetImageBytes(nextIndex);
        _nextBitmap = DecodeToBitmap(bytes); // 解码在后台线程完成
    });
}
```

### 5.3 过渡动画实现示例

以 `PushLeft` 为例（新旧图双图联动）：

```csharp
public class PushLeftTransition : ITransition
{
    public Storyboard Build(FrameworkElement outgoing,
                            FrameworkElement incoming,
                            double duration)
    {
        double w = SystemParameters.PrimaryScreenWidth;
        var sb = new Storyboard();

        // 旧图向左移出
        outgoing.RenderTransform = new TranslateTransform(0, 0);
        var moveOut = new DoubleAnimation(0, -w, TimeSpan.FromSeconds(duration));
        Storyboard.SetTarget(moveOut, outgoing);
        Storyboard.SetTargetProperty(moveOut,
            new PropertyPath("RenderTransform.X"));

        // 新图从右侧推入
        incoming.RenderTransform = new TranslateTransform(w, 0);
        var moveIn = new DoubleAnimation(w, 0, TimeSpan.FromSeconds(duration));
        Storyboard.SetTarget(moveIn, incoming);
        Storyboard.SetTargetProperty(moveIn,
            new PropertyPath("RenderTransform.X"));

        sb.Children.Add(moveOut);
        sb.Children.Add(moveIn);
        return sb;
    }
}
```

-----

## 6. 依赖与开发环境

### 6.1 NuGet 依赖

|项目 |包名                            |版本  |用途                                                    |
|---|------------------------------|----|------------------------------------------------------|
|制作器|`PeNet`                       |3.x |PE 文件资源节读写（资源注入）                                      |
|制作器|`Newtonsoft.Json`             |13.x|配置 JSON 序列化                                           |
|制作器|`Microsoft.Xaml.Behaviors.Wpf`|1.x |拖拽排序等 MVVM 行为绑定                                       |
|运行时|无                             |—   |零第三方依赖，config.json 用 `System.Runtime.Serialization` 解析|


> 图片处理（降采样 + JPEG 编码）直接调用 Windows 内置 WIC API，通过 `System.Windows.Media.Imaging` 访问，无需第三方图形库。

### 6.2 开发环境

|项目  |要求                                                 |
|----|---------------------------------------------------|
|操作系统|Windows 10 / 11（开发机）                               |
|IDE |Visual Studio 2022（17.8+），含 .NET Framework 4.8 开发工具|
|目标框架|.NET Framework 4.8                                 |
|目标架构|AnyCPU（同时兼容 32位 / 64位 系统）                          |

### 6.3 运行时发布配置

.NET Framework 4.8 的核心 WPF DLL（`PresentationCore`、`PresentationFramework`、`WindowsBase` 等）是系统内置组件，只要 .NET FX 4.8 安装完毕这些文件就存在于系统中，**无需打包进 .scr**。运行时不引入任何第三方 NuGet 包，config.json 解析使用 .NET FX 内置的 `System.Runtime.Serialization`，最终壳体积约 **200KB**。

```xml
<!-- PicScreenSaver.Runtime.csproj -->
<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
<PlatformTarget>AnyCPU</PlatformTarget>
<!-- 无需 Costura.Fody，系统 WPF DLL 不需要合并 -->
```

**.scr 文件体积构成：**

```
运行时壳（纯 EXE）   ~200KB
图片（JPEG 压缩后）  由图片数量和质量决定
config.json         ~1KB
─────────────────────────────
体积几乎完全由图片决定，壳的存在感可忽略不计
```

-----

## 7. 开发阶段规划

### Phase 1：核心引擎（3 ~ 4 周）

**交付物**：可以在目标机器上运行的 .scr 文件（命令行手动构建）

- [ ] `PicScreenSaver.Runtime` 项目骨架（.NET Framework 4.8 WPF）
- [ ] 命令行参数解析（`/s` `/c` `/p`）
- [ ] `ResourceLoader`：PE 自身资源读取（JPEG 图片 + config.json）
- [ ] `SlideEngine`：状态机 + DispatcherTimer 调度
- [ ] `TransitionManager`：效果轮算法（随机排列循环）
- [ ] 20 种过渡动画实现（统一 `ITransition` 接口）
- [ ] 多显示器全屏 + 退出机制
- [ ] 将 PicScreenSaver.Runtime 编译后嵌入 PicScreenSaver.Maker Resources 文件夹

### Phase 2：制作器基础（2 ~ 3 周）

**交付物**：图形界面可以生成合法 .scr 文件

- [ ] `PicScreenSaver` 项目骨架（.NET Framework 4.8 WPF）
- [ ] `ImageProcessor`：WIC 调用 + 1080P 降采样 + JPEG 编码
- [ ] `PackageBuilder`：PE 资源注入（PeNet）+ 输出 .scr
- [ ] 主界面 UI（图片列表 + 参数面板 + 效果复选框组）
- [ ] 状态栏实时体积预估

### Phase 3：体验完善（1 ~ 2 周）

**交付物**：达到可用产品标准

- [ ] 图片列表拖拽排序
- [ ] 内置实时预览窗口
- [ ] 一键安装为当前屏保（调用 `SystemParametersInfo`）
- [ ] 输入校验与错误提示（至少选择 1 种效果、至少导入 1 张图片等）

### Phase 4：可选增强

- [ ] 每张图片独立设置展示时长
- [ ] 项目文件保存 / 读取（`.ssproj`）
- [ ] 图片文字水印叠加

-----

## 8. 配置文件格式（config.json）

以 UTF-8 字符串形式注入 PE 资源节，资源名 `SSCONFIG`，资源 ID `1`。

```json
{
  "version": "1.4",
  "displayDuration": 5.0,
  "transitionDuration": 1.2,
  "shuffleImages": true,
  "selectedEffects": [
    "Fade", "SlideLeft", "ZoomInFade", "PushLeft"
  ],
  "imageCount": 12,
  "createdBy": "PicScreenSaver v1.0",
  "createdAt": "2026-06-04T10:30:00Z"
}
```

|字段                  |类型      |说明            |
|--------------------|--------|--------------|
|`version`           |string  |配置格式版本，供向后兼容判断|
|`displayDuration`   |float   |每张图静止展示时长（秒）  |
|`transitionDuration`|float   |过渡动画时长（秒）     |
|`shuffleImages`     |bool    |是否随机排列图片顺序    |
|`selectedEffects`   |string[]|已选过渡效果 ID 列表  |
|`imageCount`        |int     |嵌入图片总数        |
|`createdBy`         |string  |制作器标识         |
|`createdAt`         |string  |生成时间（ISO 8601）|

-----

## 9. 风险与注意事项

|风险项                     |等级|应对策略                                                                                   |
|------------------------|--|---------------------------------------------------------------------------------------|
|**PE 资源注入兼容性**          |中 |使用 PeNet 库；覆盖测试 Win7 x86/x64、Win10 x64、Win11 x64；备选方案：将资源打包为 ZIP 追加至 PE 末尾（更简单，但非标准资源节）|
|**杀毒软件误报**              |中 |PE 修改操作可能触发启发式检测；申请代码签名证书（~$70/年）可显著降低误报率；发布时附 VirusTotal 扫描链接                         |
|**Win7 未更新 .NET FX 4.8**|低 |运行时启动时检测框架版本，低于 4.8 则弹窗提示用户前往微软官网下载离线安装包                                               |
|**大量图片内存占用**            |低 |运行时内存中同时最多保留 3 张解码图片，其余按需读取解码                                                          |
|**Flip 效果在老显卡上的表现**     |低 |`PlaneProjection` 在部分老旧集显上可能有轻微锯齿；可在配置中增加降级标志，检测到异常时自动以 `CrossFade` 替代                 |

-----

*PicScreenSaver · 技术设计文档 v1.4 · 2026-06-04*
*C# · WPF · .NET Framework 4.8 · Windows 7 SP1 及以上*