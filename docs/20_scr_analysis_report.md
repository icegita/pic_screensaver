# 20.scr 逆向分析 + 改进建议报告

| 项目 | 内容 |
|------|------|
| 文件名 | 20.scr |
| 文件大小 | 1,101,824 bytes（1,076 KB） |
| 制作工具 | PicScreenSaver v1.0（iCeGiTa，2026年） |
| 运行时版本 | PicScreenSaver.Runtime 1.1.2.0 |
| 分析日期 | 2026-06-08 |

---

## 目录

1. [文件基本信息](#1-文件基本信息)
2. [PE 结构分析](#2-pe-结构分析)
3. [技术架构分析](#3-技术架构分析)
4. [资源存储分析](#4-资源存储分析)
5. [配置 Schema 分析](#5-配置-schema-分析)
6. [过渡效果实现分析](#6-过渡效果实现分析)
7. [运行时行为分析](#7-运行时行为分析)
8. [与 10.scr（旧方案）对比](#8-与-10scr旧方案对比)
9. [问题与改进建议](#9-问题与改进建议)

---

## 1. 文件基本信息

| 属性 | 值 |
|------|-----|
| 文件格式 | PE32（x86），.NET Mono/Assembly |
| 目标架构 | x86（32位，AnyCPU 编译） |
| .NET 框架 | .NET Framework 4.8 |
| 链接器版本 | 48.0（对应 .NET FX 4.8） |
| 编译模式 | **Debug**（未发布 Release 版本） |
| GUI 子系统 | Windows GUI（WPF） |
| 编译时间戳 | 2085-11-27（.NET 程序时间戳无意义，为随机值） |
| PDB 路径 | `E:\deepseek\pic_screensaver\PicScreenSaver.Runtime\obj\Debug\net48\PicScreenSaver.Runtime.pdb` |

**版本信息：**

```
ProductName:     PicScreenSaver.Runtime
FileVersion:     1.1.2.0
CompanyName:     iCeGiTa
Description:     PicScreenSaver Runtime 1.0.0 - 屏保播放引擎
Copyright:       Copyright © 2026 iCeGiTa. All rights reserved.
Assembly Version: 1.1.2.0
```

---

## 2. PE 结构分析

### 2.1 节区

| 节区 | 虚拟地址 | 原始大小 | 用途 |
|------|---------|---------|------|
| `.text` | 0x2000 | 514,560 B（502 KB） | .NET IL 代码 + 元数据 + 字符串堆 |
| `.rsrc` | 0x80000 | 586,240 B（572 KB） | 资源节（图片 + 配置 + 图标） |
| `.reloc` | 0x110000 | 512 B | 重定位表（.NET 程序极小） |

### 2.2 数据目录

| 目录 | 地址 | 大小 | 说明 |
|------|------|------|------|
| Import Table | 0x7F885 | 79 B | 仅 mscoree.dll 一项（.NET 标准） |
| Resource Table | 0x80000 | 586,216 B | 全部资源 |
| CLR Header | **0x2008** | 72 B | **确认为 .NET 程序**（CLR 入口头） |
| IAT | 0x2000 | 8 B | 仅 `_CorExeMain` 一个导入函数 |

### 2.3 导入表

```
mscoree.dll → _CorExeMain
```

这是标准 .NET 程序的最小化导入，所有功能通过 CLR 运行时提供，与旧方案（10.scr）导入数百个 Win32 API 形成鲜明对比。

---

## 3. 技术架构分析

### 3.1 技术选型

```
语言：        C#（.NET Framework 4.8）
UI 框架：     WPF（Windows Presentation Foundation）
动画引擎：    System.Windows.Media.Animation（WPF Storyboard）
图像处理：    System.Windows.Media.Imaging（WIC，Windows内置）
JSON 序列化： System.Runtime.Serialization.Json（.NET FX 内置）
图形效果：    System.Windows.Media.Effects（BlurEffect 等）
线程调度：    System.Windows.Threading.DispatcherTimer
```

### 3.2 完整命名空间结构

```
PicScreenSaver.Runtime
├── App                          # WPF 应用入口
├── ScreensaverWindow            # 主屏保全屏窗口
├── ConfigDialog                 # /c 参数对应的配置对话框
├── Engine
│   ├── (SlideEngine)            # 幻灯片调度状态机
│   ├── (TransitionManager)      # 效果轮管理器
│   └── Transitions              # 过渡动画命名空间
│       ├── ITransition          # 统一接口
│       ├── FadeTransition       # 淡变类
│       ├── SlideTransition      # 滑动类
│       ├── ZoomTransition       # 缩放类
│       ├── BlindsTransition     # 百叶窗类
│       ├── CheckerboardTransition # 棋盘类
│       ├── RadialWipeTransition # 径向擦除类
│       ├── RotateTransition     # 旋转类
│       ├── ShapeRevealTransition # 形状揭示类
│       └── SpecialTransition    # 特效类（Push 等）
```

### 3.3 状态机

从字符串提取确认两个运行时状态：

```
Idle ──加载完成──▶ Displaying ──Timer到期──▶ Transitioning
                       ▲                          │
                       └────── 动画完成回调 ────────┘
```

对应事件处理器：
- `DisplayTimer_Tick`：展示计时器到期触发切换
- `Window_Loaded`：初始化加载
- `ExitScreensaver`：统一退出逻辑

---

## 4. 资源存储分析

### 4.1 资源结构

```
PE 资源节（.rsrc）
├── SSCONFIG/SSCONFIG/0   ← 配置 JSON（251 bytes）
├── SSIMAGE/1/0           ← 第 1 张图片 JPEG（342 KB）
├── SSIMAGE/2/0           ← 第 2 张图片 JPEG（227 KB）
├── type=16/1/0           ← Version 信息（1,008 bytes）
└── type=24/1/0           ← 应用程序清单 Manifest（872 bytes）
```

### 4.2 图片存储

| 资源 ID | 大小 | 分辨率 | 格式 |
|---------|------|--------|------|
| SSIMAGE/1 | 350,943 B（342 KB） | **1920×1080** | JPEG |
| SSIMAGE/2 | 232,725 B（227 KB） | **1920×1080** | JPEG |

- 图片均已降采样至 1080P（相比原始 4K 节省了大量体积）
- 以标准 JPEG 字节流直接存储，无加密
- 命名方式：`SSIMAGE/N`（从 1 开始递增）✅ 符合技术文档设计

### 4.3 配置存储

配置以 **UTF-8 编码的 JSON** 存储于 `SSCONFIG` 资源，完整内容：

```json
{
  "version": "1.4",
  "title": "20",
  "author": null,
  "description": null,
  "displayDuration": 60.0,
  "transitionDuration": 1.2,
  "shuffleImages": true,
  "selectedEffects": ["Fade"],
  "imageCount": 2,
  "createdBy": "PicScreenSaver v1.0",
  "createdAt": "2026-06-08T01:59:29.4308163Z"
}
```

---

## 5. 配置 Schema 分析

从 .NET 元数据重建的 `ScreensaverConfig` 类：

```csharp
public class ScreensaverConfig
{
    public string   version             { get; set; }  // "1.4"
    public string   title               { get; set; }  // 屏保标题
    public string   author              { get; set; }  // 作者（可为 null）
    public string   description         { get; set; }  // 描述（可为 null）
    public double   displayDuration     { get; set; }  // 每张展示时长（秒）
    public double   transitionDuration  { get; set; }  // 过渡动画时长（秒）
    public bool     shuffleImages       { get; set; }  // 是否随机播放
    public string[] selectedEffects     { get; set; }  // 已选效果 ID 列表
    public int      imageCount          { get; set; }  // 图片总数
    public string   createdBy           { get; set; }  // 制作工具标识
    public string   createdAt           { get; set; }  // 创建时间（ISO 8601）
}
```

> **注意**：JSON 中的 `selectedEffects` 字段存储的是效果名称字符串（如 `"Fade"`），而代码中同时存在 `selectedEffectIds` 字段名，两者可能存在不一致，详见[问题 #3](#问题-3-selectedeffects-字段命名不一致)。

---

## 6. 过渡效果实现分析

### 6.1 已实现效果（从 Build* 方法提取）

共发现 **27 个** `Build*` 方法，实际过渡效果约 **24 种**：

| 分类 | 方法名 | 实现说明 |
|------|--------|---------|
| **淡变** | `BuildFade` | Opacity 动画 |
| | `BuildFadeBlack` | 双段 Opacity + 黑色背景 |
| | `BuildFadeWhite` | 双段 Opacity + 白色背景 |
| | `BuildFadeBlur` | Opacity + BlurEffect（**超出规划的额外效果**） |
| | `BuildCrossFade` | 双层叠加 Opacity |
| **滑动** | `BuildSlideLeft` | TranslateTransform.X |
| | `BuildSlideRight` | TranslateTransform.X |
| | `BuildSlideUp` | TranslateTransform.Y |
| | `BuildSlideDown` | TranslateTransform.Y |
| **缩放** | `BuildZoomIn` | ScaleTransform |
| | `BuildZoomOut` | ScaleTransform |
| | `BuildZoomInFade` | ScaleTransform + Opacity |
| | `BuildZoomOutFade` | ScaleTransform + Opacity |
| | `BuildCrossZoom` | 交叉缩放（**额外效果**） |
| **推移** | `BuildPushLeft` | 双 TranslateTransform.X |
| | `BuildPushRight` | 双 TranslateTransform.X |
| | `BuildPushUp` | 双 TranslateTransform.Y |
| | `BuildPushDown` | 双 TranslateTransform.Y |
| **擦除** | `BuildWipeLeft` | ClipRect 动画 |
| | `BuildWipeRight` | ClipRect 动画 |
| | `BuildWipeUp` | ClipRect 动画 |
| | `BuildWipeDown` | ClipRect 动画 |
| **特效** | `BuildBlinds` | BlindsGeometryAnimation（**额外效果**） |
| | `BuildCircleReveal` | DiamondRevealAnimation（**额外效果**） |
| | `BuildDiamondReveal` | DiamondRevealAnimation（**额外效果**） |
| | `BuildRotateCW` | RotateTransform（**额外效果**） |
| | `BuildRotateCCW` | RotateTransform 反向（**额外效果**） |

> **实际实现了 27 种效果**，超过技术文档规划的 20 种，包含百叶窗、圆形揭示、菱形揭示、旋转、棋盘、径向擦除等额外效果。

### 6.2 特殊实现类

- `BlindsGeometryAnimation`：自定义几何动画，用 PathGeometry 实现百叶窗切割
- `CheckerboardGeometryAnimation`：棋盘格几何动画
- `DiamondRevealAnimation`：菱形/圆形揭示动画
- `RadialWipeTransition`：径向扫描擦除
- `BlurEffect`（`System.Windows.Media.Effects`）：毛玻璃模糊，用于 `FadeBlur` 效果

---

## 7. 运行时行为分析

### 7.1 命令行参数处理

从事件处理器和方法名推断的 SCR 协议实现：

| 参数 | 处理方法 | 行为 |
|------|---------|------|
| `/s` | `RunScreensaver` | 调用 `SetupFullscreen`，全屏启动 |
| `/c` | `ConfigDialog` | 打开配置对话框（含效果复选框列表） |
| `/p HWND` | `hWndChild` → `hWndNewParent` | 嵌入控制面板预览窗口 |

### 7.2 退出机制

确认实现了完整的退出事件监听：

```csharp
Window_MouseMove   → 检测鼠标位移（_lastMousePos 记录初始位置）
Window_MouseDown   → 任意鼠标键退出
Window_MouseWheel  → 滚轮退出
Window_KeyDown     → 任意键盘按键退出
```

使用 `GetCursorPos`（P/Invoke Win32）获取真实鼠标坐标，而非 WPF 逻辑坐标，处理多显示器 DPI 缩放问题。

### 7.3 多显示器

发现 `_exitMonitor` 字段，表明实现了多显示器管理，每个显示器独立创建窗口，任意显示器上的输入触发统一退出。

### 7.4 屏保安装

`SetScreensaverRegistry` 方法：写入注册表将自身设为系统当前屏保，对应 UI 中「生成后安装」选项。

### 7.5 配置对话框

`ConfigDialog` 包含：
- `EffectsList`：效果复选框列表（`EffectCheckBox_Changed` 事件）
- `SelectedEffectName` / `SelectedEffectDesc`：选中效果的名称和描述
- `SelectedEffectCount`：已选效果数量显示
- `DisplayDurationSlider` + `DisplayDuration_ValueChanged`：展示时长滑块
- `TransitionDurationSlider` + `TransitionDuration_ValueChanged`：过渡时长滑块
- `EffectPreviewImageA` / `EffectPreviewImageB`：效果预览用的两张图层
- `PlayEffectPreview`：预览播放逻辑（发现 3 个 lambda：`b__0`, `b__1`, `b__2`）

### 7.6 资源内嵌图标

`.rsrc` 中发现 `PicScreenSaver.Runtime.Resources.sys.png`，说明运行时内嵌了系统图标资源。

---

## 8. 与 10.scr（旧方案）对比

### 8.1 体积对比

| 维度 | 10.scr（Win32 C++） | 20.scr（WPF .NET） | 变化 |
|------|-------------------|------------------|------|
| 总文件大小 | 1,592 KB | **1,076 KB** | **↓32.4%** |
| 图片部分 | 1,070 KB（4K 未压缩） | **569 KB**（1080P 压缩） | **↓46.8%** |
| 运行时壳 | ~521 KB（含全部逻辑） | ~506 KB | ↓2.9% |
| 图片张数 | 2 张（同图） | 2 张（同图） | — |

> 20.scr 体积缩小主要来自**图片降采样**（4K→1080P），运行时壳体积两者相近。

### 8.2 技术对比

| 对比项 | 10.scr（旧） | 20.scr（新） |
|--------|------------|------------|
| 语言 | C++ | C# |
| 框架 | 原生 Win32 GDI | WPF .NET FX 4.8 |
| 动画渲染 | CPU BitBlt 逐帧 | GPU DirectX（WPF Storyboard） |
| 图像处理 | CxImage 5.51（第三方） | WIC（Windows 内置） |
| 音频支持 | MCI（WINMM.dll） | 未实现 |
| 效果数量 | 42 种 | 27 种 |
| 图片格式 | JPEG（原图直接嵌入） | JPEG（1080P 降采样） |
| 配置格式 | INI 文件 + 资源节 | JSON（资源节内嵌） |
| 隐私安全 | 嵌入制作者路径/邮箱 | 仅必要字段 |
| 编译模式 | Release | **Debug（待优化）** |

---

## 9. 问题与改进建议

### 问题 #1：以 Debug 模式发布

**发现依据：**
```
PDB 路径: E:\deepseek\pic_screensaver\...\obj\Debug\net48\PicScreenSaver.Runtime.pdb
DebuggableAttribute 存在于程序集中
```

**影响：**
- 运行时性能显著下降（JIT 不做优化，代码未内联）
- 程序集体积偏大（含调试符号和额外元数据）
- 暴露完整源码路径（`E:\deepseek\pic_screensaver\...`）

**改进：** 发布时切换为 Release 模式：
```
Visual Studio → 右键项目 → 发布 → 配置选择 Release
或 CLI: dotnet publish -c Release
```
预计体积可减少 **15~25%**，运行性能提升明显。

---

### 问题 #2：运行时壳体积偏大（506 KB）

**发现依据：** `.text` 节区 514,560 bytes，远超技术文档预期的 ~200 KB。

**原因分析：**
- Debug 模式编译膨胀（见问题 #1）
- `System.Runtime.Serialization.Json` 引入了较多反射元数据
- WPF XAML 编译产物（`g.resources`）含内嵌 BAML

**改进建议：**
1. 切换 Release 模式（立竿见影）
2. 用轻量 JSON 解析替代 `DataContractJsonSerializer`：

```csharp
// 现在（重）：DataContractJsonSerializer 需要大量反射元数据
// 改为手动解析或用 System.Text.Json（.NET 5+ 内置，.NET FX 需 NuGet）

// 或者最简方案：直接用正则/Split 解析固定格式 JSON，零依赖
```

3. 启用 IL Trimming（.NET FX 4.8 不完全支持，可用 ILMerge + 手动裁剪）

---

### 问题 #3：`selectedEffects` 字段命名不一致

**发现依据：**
- JSON 中实际字段名为 `selectedEffects`（存放效果名称字符串数组）
- 代码元数据中同时存在 `selectedEffectIds`（暗示存放 ID）
- `GetTransitionsByIds` 方法名也暗示使用 ID 查找

**影响：** 若代码内部用 ID 匹配而配置存字符串名称，版本迭代时效果名称改动会导致存档失效。

**改进建议：** 统一使用枚举字符串 ID 作为唯一标识符，与显示名称分离：

```json
{
  "selectedEffects": ["Fade", "SlideLeft", "ZoomIn"]
}
```

```csharp
// 效果注册表 - ID 与显示名分离
static readonly Dictionary<string, string> EffectDisplayNames = new() {
    { "Fade",      "淡入淡出" },
    { "SlideLeft", "从右滑入" },
    // ...
};
```

---

### 问题 #4：config.json 中存在无用字段

**发现依据：** `title`、`author`、`description` 三个字段在当前版本均为 `null`。

**影响：** 少量冗余体积，更重要的是 UI 中没有对应的输入入口，功能残缺。

**改进建议二选一：**
- **保留并实现**：在制作器「设置」Tab 中增加「屏保名称」和「描述」输入框，`/c` 配置对话框展示这些信息
- **删除**：从 Schema 中移除，精简配置

---

### 问题 #5：效果数量超出规划但未在文档更新

**发现依据：** 实现了 27 种效果（含 `FadeBlur`、`Blinds`、`CircleReveal`、`DiamondReveal`、`RotateCW/CCW`、`CrossZoom`、`Checkerboard`、`RadialWipe`），超出技术文档规划的 20 种。

**改进建议：** 更新技术文档 v1.4 的效果清单，将实际实现的效果补全记录，同时在制作器 UI 的效果 Tab 中补充这些额外效果的复选框和预览。

---

### 问题 #6：`displayDuration` 默认值异常

**发现依据：** config.json 中 `displayDuration: 60.0`（60 秒），而技术文档规划默认值为 5 秒。

**影响：** 用户体验异常，每张图停留整整 1 分钟。

**改进：** 检查制作器 UI 的默认值设置，确保与文档一致（默认 5.0 秒）。

---

### 问题 #7：FadeBlur 效果在低端显卡上的兼容性

**发现依据：** `BuildFadeBlur` 使用 `System.Windows.Media.Effects.BlurEffect`，该效果需要像素着色器支持。

**影响：** 在极低端显卡（PS 2.0 以下）或 Remote Desktop 环境下，BlurEffect 会静默回退为无效果，用户看到的是直接切换。

**改进：**
```csharp
// 运行时检测像素着色器支持级别
if (RenderCapability.Tier == 0) {
    // 降级：用 CrossFade 替代 FadeBlur
}
```

---

### 综合优先级

| 优先级 | 问题 | 预期收益 |
|--------|------|---------|
| 🔴 立即 | #1 Debug → Release | 体积减少 15~25%，性能提升 |
| 🔴 立即 | #6 displayDuration 默认值 | 修复用户体验 bug |
| 🟡 近期 | #3 字段命名统一 | 防止版本兼容性问题 |
| 🟡 近期 | #5 文档与效果清单同步 | 文档与实现一致性 |
| 🟡 近期 | #4 null 字段处理决策 | 明确功能边界 |
| 🟢 后期 | #2 Runtime 体积优化 | 进一步轻量化 |
| 🟢 后期 | #7 BlurEffect 兼容检测 | 老显卡兼容性 |

---

## 附录：提取的图片

| 文件 | 分辨率 | 大小 | 说明 |
|------|--------|------|------|
| `new_extracted_img_1.jpg` | 1920×1080 | 342 KB | 第 1 张（已降采样至 1080P） |
| `new_extracted_img_2.jpg` | 1920×1080 | 227 KB | 第 2 张（已降采样至 1080P） |

---

*20.scr 逆向分析 + 改进建议报告 · 2026-06-08*
*分析工具：pefile · Pillow · Python 3*
