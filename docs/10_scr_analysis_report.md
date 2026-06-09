# 10.scr 逆向分析报告

| 项目 | 内容 |
|------|------|
| 文件名 | 10.scr |
| 文件大小 | 1,630,208 bytes（1.55 MB） |
| 分析日期 | 2026-06-08 |
| 制作工具 | Photo Screensaver Maker V3.6.2（Aone Software，2004年） |

---

## 目录

1. [文件基本信息](#1-文件基本信息)
2. [PE 结构分析](#2-pe-结构分析)
3. [技术路线与框架](#3-技术路线与框架)
4. [图片存储机制](#4-图片存储机制)
5. [配置存储机制](#5-配置存储机制)
6. [过渡效果清单](#6-过渡效果清单)
7. [配置参数结构体](#7-配置参数结构体)
8. [运行时行为分析](#8-运行时行为分析)
9. [提取的图片](#9-提取的图片)
10. [对 PicScreenSaver 的参考价值](#10-对-picscreensaver-的参考价值)

---

## 1. 文件基本信息

| 属性 | 值 |
|------|-----|
| 文件格式 | PE32 可执行文件（Windows GUI 程序） |
| 目标架构 | x86（32位），Intel 80386 |
| 编译器 | Microsoft Visual C++ 6.0（1998年版） |
| 链接器版本 | 6.0 |
| 编译时间戳 | 2005-06-14 06:46:28 |
| 运行时框架 | 原生 Win32 API（非 .NET） |
| GUI 子系统 | Windows GUI（无控制台） |
| ImageBase | 0x400000 |
| 入口点 | 0x1E801 |
| 映像大小 | 1,608 KB |

**版本信息字段：**

```
CompanyName:     Aone Software
FileDescription: Screensaver make with Photo Screensaver Maker V3.6.2
FileVersion:     3.6.2.1
LegalCopyright:  Copyright (C) 2004
ProductName:     Screensaver make with Photo Screensaver Maker V3.6.2
ProductVersion:  3.6.2.1
```

---

## 2. PE 结构分析

### 2.1 节区（Sections）

| 节区名 | 虚拟地址 | 原始大小 | 用途 | 属性 |
|--------|---------|---------|------|------|
| `.text` | 0x1000 | 229,376 B（224 KB） | 代码段（程序逻辑） | 可读可执行 |
| `.rdata` | 0x39000 | 40,960 B（40 KB） | 只读数据（字符串常量、导入表） | 只读 |
| `.data` | 0x43000 | 24,576 B（24 KB） | 可读写数据（全局变量） | 可读可写 |
| `.rsrc` | 0x4D000 | **1,331,200 B（1,300 KB）** | 资源节（图片 + 配置） | 只读 |

> 资源节（.rsrc）占文件总体积的 **81.7%**，绝大部分是嵌入的图片数据。

### 2.2 数据目录

| 目录 | 偏移 | 大小 | 说明 |
|------|------|------|------|
| Import Table | 0x40C30 | 200 B | 导入的系统 DLL 函数表 |
| Resource Table | 0x4D000 | 1,329,368 B | 全部资源数据 |
| IAT | 0x39000 | 1,260 B | 导入地址表 |
| CLR Header | — | 0 | **无**，确认非 .NET 程序 |

---

## 3. 技术路线与框架

### 3.1 核心技术选型

```
语言：      C++（Microsoft Visual C++ 6.0 编译）
框架：      纯原生 Win32 API，无 MFC，无 .NET
图形库：    CxImage 5.51（开源 C++ 图像处理库）
图形渲染：  Windows GDI（BitBlt、StretchBlt、AlphaBlend）
音频：      Windows MCI（Media Control Interface）via WINMM.dll
时间控制：  timeGetTime()（高精度毫秒计时器）
多显示器：  EnumDisplayMonitors / MonitorFromPoint
```

### 3.2 依赖的系统 DLL

| DLL | 主要用途 |
|-----|---------|
| `KERNEL32.dll` | 内存管理、线程、文件系统（129+ 函数） |
| `USER32.dll` | 窗口管理、消息循环、输入处理（97+ 函数） |
| `GDI32.dll` | 图形绘制（BitBlt、StretchBlt 等，25+ 函数） |
| `WINMM.dll` | 背景音乐播放（mciSendCommandA、timeGetTime） |
| `ADVAPI32.dll` | 注册表读写（RegOpenKeyExA、RegSetValueExA 等） |
| `SHELL32.dll` | 打开超链接（ShellExecuteA） |
| `comdlg32.dll` | 颜色选择对话框（ChooseColorA） |
| `COMCTL32.dll` | 图标列表控件（ImageList） |
| `WINSPOOL.DRV` | 打印机相关（推测用于导出功能） |

### 3.3 图像处理库：CxImage 5.51

这是本文件最关键的技术细节之一。代码中明确找到字符串：

```
@CxImage 5.51
```

CxImage 是一个开源 C++ 图像处理库（SourceForge），支持 JPEG、PNG、BMP、GIF 等格式的编解码，是 2000 年代 Win32 屏保工具常用的图片处理方案。

**推断的图片处理流程：**

```
原始图片（JPEG/BMP/PNG）
    ↓ CxImage 解码
DIB（设备无关位图，内存中）
    ↓ StretchBlt / AlphaBlend（GDI）
屏幕输出（过渡动画帧）
```

### 3.4 过渡动画渲染方式

基于 GDI API 分析，过渡动画通过以下方式实现：

- **位块传输**：`BitBlt`（直接复制像素块）
- **拉伸/缩放**：`StretchBlt`（含插值缩放，SetStretchBltMode 控制质量）
- **透明混合**：`AlphaBlend`（淡入淡出效果）
- **裁剪控制**：`IntersectClipRect`（实现擦除/遮罩类效果）
- **定时驱动**：`SetTimer` + `WM_TIMER` 消息循环控制帧率

---

## 4. 图片存储机制

### 4.1 存储位置

图片存储在 PE 资源节（.rsrc）的**自定义资源类型 5000** 下：

| 资源 ID | 文件大小 | 图片分辨率 | 格式 | 说明 |
|---------|---------|-----------|------|------|
| `5000/0` | 660,774 B（645 KB） | 3840×2160（4K） | JPEG | 第 1 张图片 |
| `5000/1` | 435,108 B（424 KB） | 3840×2160（4K） | JPEG | 第 2 张图片 |

### 4.2 存储格式

图片以**原始 JPEG 字节流**直接写入资源节，无任何加密或二次压缩：

```
资源节偏移 → 标准 JPEG 文件头（FF D8 FF E0 ...）→ JPEG 数据体 → FF D9
```

验证：

```
Image 0: FF D8 FF E0 00 10 4A 46 49 46 ...（标准 JFIF 头）
Image 1: FF D8 FF E0 00 10 4A 46 49 46 ...（标准 JFIF 头）
```

两张图片均为 4K（3840×2160）分辨率，直接以原始分辨率嵌入，**未做任何降采样处理**，这也解释了为何 .scr 文件体积较大（图片部分合计 1.05 MB，已是 JPEG 压缩后的结果）。

### 4.3 图片数量存储方式

推测图片数量和索引存储于**自定义资源类型 5300**（配置块）中，运行时通过递增 ID（0, 1, 2...）遍历 `5000/N` 资源来加载全部图片。

---

## 5. 配置存储机制

### 5.1 配置资源（类型 5300）

自定义资源类型 `5300/0`（744 字节）存储屏保的元数据和路径信息，以**混合格式**（ASCII 十六进制字符串 + 二进制结构体 + 空字符分隔字符串）编码：

从中解析出的可读字段：

```
制作工具路径：  ...Photo Screensaver Maker\...
制作者邮箱：    power@sgcc.com.cn
制作时间戳：    2026-05-27 16:05:53
屏保标题：      十条禁令（GBK编码中文）
制作机器路径：  C:\Users\Administrator\Desktop\
门户网址：      http://portal.cq.sgcc.com.cn/
屏保说明文字：  Describe here your screensaver!（默认占位文本）
默认字体：      MS Sans Serif
```

> **注意**：配置中包含制作者的完整桌面路径和邮箱地址，说明该工具在生成 .scr 时会将制作环境信息嵌入文件，存在**隐私泄露风险**。

### 5.2 运行时配置（INI 文件）

运行中的可调参数通过 **INI 文件**持久化（节名 `SCRSAVE`），而非注册表：

```ini
[SCRSAVE]
szEffect=Slide Center
nPicturePause=5000
nEffectMilliSecond=1000
bMovePicture=0
bAutoAdjustSize=1
bShufflePic=1
bMuteMusic=0
nAudioPause=0
bShuffleAudio=0
bKeyboardEvent=1
bMouseRightClick=1
bMouseLeftClick=1
bMouseMove=1
crBgColor=0x000000
```

安装后同时写入注册表：

```
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Screen Savers\
```

---

## 6. 过渡效果清单

从 `.text` 段字符串表中提取到完整的过渡效果名称列表，共 **42 种**：

### 滑动 / 推移类（9 种）

| 效果名 | 描述 |
|--------|------|
| `Slide Center` | 从中心向外滑动展开 |
| `Slide Middle` | 从中线水平展开 |
| `Slide From Corner` | 从角落滑入 |
| `Slide Bevel` | 斜角方向滑入 |
| `Slide Bevel Back` | 斜角方向反向滑入 |
| `Move Horizontal` | 水平平移 |
| `Move Vertical` | 垂直平移 |
| `Move To Center` | 向中心收拢 |
| `Move To Middle` | 向中线收拢 |

### 移动类（4 种）

| 效果名 | 描述 |
|--------|------|
| `Move Cross` | 十字方向移动 |
| `Move From Corner` | 从角落移出 |
| `Random Lines Horizontal` | 随机水平线条 |
| `Rdndom Lines Vertical` | 随机垂直线条（原文有拼写错误） |

### 百叶窗 / 格栅类（6 种）

| 效果名 | 描述 |
|--------|------|
| `Blinds Horizontal` | 水平百叶窗 |
| `Blinds Vertical` | 垂直百叶窗 |
| `Grid` | 网格擦除 |
| `Sliding Jaws Horizontal` | 水平颌骨夹入 |
| `Sliding Jaws Vertical` | 垂直颌骨夹入 |
| `Mosaic` | 马赛克溶解 |

### 滚动类（2 种）

| 效果名 | 描述 |
|--------|------|
| `Roll Horizontal` | 水平卷轴展开 |
| `Roll Vertical` | 垂直卷轴展开 |

### 形状展开类（12 种）

| 效果名 | 描述 |
|--------|------|
| `Cross In` | 十字形向内收缩 |
| `Cross Out` | 十字形向外展开 |
| `Square In` | 矩形向内收缩 |
| `Square Out` | 矩形向外展开 |
| `Veil Horizontal` | 水平纱帘 |
| `Veil Vertical` | 垂直纱帘 |
| `Round In` | 圆形向内收缩 |
| `Round Out` | 圆形向外展开 |
| `Arris In` | 菱形向内收缩 |
| `Arris Out` | 菱形向外展开 |
| `Star In` | 星形向内收缩 |
| `Star Out` | 星形向外展开 |

### 旋转 / 扫描类（7 种）

| 效果名 | 描述 |
|--------|------|
| `Clock` | 顺时针扫描 |
| `Clock Back` | 逆时针扫描 |
| `Double Clock` | 双向时钟扫描 |
| `Wheel` | 轮辐扫描 |
| `Wheel Back` | 反向轮辐扫描 |
| `Fan` | 扇形展开 |
| `Fan Back` | 扇形反向展开 |

### 其他（2 种）

| 效果名 | 描述 |
|--------|------|
| `Coalition` | 合并过渡 |
| `Stretch` | 拉伸变形 |

> **实现方式**：所有效果均基于 GDI `BitBlt` / `StretchBlt` / `AlphaBlend` 逐帧绘制，无 GPU 加速，依赖 CPU 计算像素。这与现代 WPF/DirectX 方案形成鲜明对比。

---

## 7. 配置参数结构体

从 `.data` / `.rdata` 段字符串逆向重建的运行时配置结构体：

```cpp
// 推断的屏保配置结构体（C++ 伪代码）
struct ScreensaverConfig {
    // 过渡效果
    char        szEffect[64];           // 效果名称字符串，如 "Slide Center"
    int         nEffectMilliSecond;     // 过渡动画时长（毫秒）
    
    // 图片播放
    int         nPicturePause;          // 每张图片停留时长（毫秒）
    BOOL        bMovePicture;           // 是否启用 Ken Burns 平移缩放效果
    BOOL        bAutoAdjustSize;        // 是否自动适应屏幕尺寸
    BOOL        bShufflePic;            // 是否随机打乱图片顺序
    
    // 音频
    BOOL        bMuteMusic;             // 是否静音背景音乐
    int         nAudioPause;            // 音频暂停时长
    BOOL        bShuffleAudio;          // 是否随机打乱音乐顺序
    
    // 退出触发条件
    BOOL        bKeyboardEvent;         // 键盘按键退出
    BOOL        bMouseRightClick;       // 鼠标右键退出
    BOOL        bMouseLeftClick;        // 鼠标左键退出
    BOOL        bMouseMove;             // 鼠标移动退出
    
    // 外观
    COLORREF    crBgColor;              // 背景颜色（图片外区域）
};
```

---

## 8. 运行时行为分析

### 8.1 命令行参数处理

标准 Windows 屏保协议，通过 `GetCommandLine()` 解析：

| 参数 | 行为 |
|------|------|
| `/s` 或 无参数 | 全屏运行屏保 |
| `/c` | 打开设置对话框（Effects / Settings / General 三标签页） |
| `/p HWND` | 在控制面板预览窗口内渲染 |

设置对话框包含三个 Tab：
- **Effects**：选择过渡效果、速度
- **Settings**：图片停留时长、随机播放、背景色
- **Gerneral**：（原文拼写错误）通用选项

### 8.2 多显示器支持

使用 `EnumDisplayMonitors` 枚举所有显示器，`MonitorFromPoint` 检测当前活动显示器，支持多屏环境。

### 8.3 安装机制

```
检测 SCRNSAVE.EXE 注册表项
    ↓
写入 system.ini [boot] 节
    ↓
写入注册表 HKCU\...\Screen Savers
    ↓
弹窗提示："Do you want to set %s as your default Screensaver?"
```

### 8.4 时间控制

使用 `timeGetTime()`（精度 ~1ms）而非 `GetTickCount()`（精度 ~15ms），保证动画帧率计算的准确性。

### 8.5 密码保护

支持 Windows 屏保密码（调用 `PwdChangePasswordA` via `MPR.DLL`），但该特性在 Windows Vista 之后已被系统废弃。

---

## 9. 提取的图片

从 .scr 文件资源节成功提取两张嵌入图片：

| 文件 | 分辨率 | 原始大小 | 格式 |
|------|--------|---------|------|
| `extracted_img_0.jpg` | 3840×2160（4K） | 645 KB | JPEG |
| `extracted_img_1.jpg` | 3840×2160（4K） | 424 KB | JPEG |

两张图片均以标准 JPEG 格式直接存储，无加密，可用任何图片查看器打开。

---

## 10. 对 PicScreenSaver 的参考价值

### 10.1 设计印证

此次分析印证了我们 PicScreenSaver 项目技术方案的合理性：

| 对比项 | 10.scr（旧方案）| PicScreenSaver（新方案）|
|--------|--------------|----------------------|
| 技术框架 | Win32 C++ GDI | WPF .NET FX 4.8 |
| 图片格式 | JPEG 原始存储 | JPEG（同，已优化） |
| 图片存储 | PE 资源节 type 5000 | PE 资源节 SSIMAGE |
| 配置存储 | PE 资源节 type 5300 + INI | PE 资源节 SSCONFIG（JSON） |
| 动画渲染 | GDI BitBlt（CPU） | WPF Storyboard（GPU） |
| 多显示器 | EnumDisplayMonitors | WPF 原生支持 |
| 图片分辨率 | 原图直接嵌入（4K未压缩降采样）| 强制降至 1080P（体积优化）|

### 10.2 可借鉴的设计

**资源 ID 命名规则**：旧方案用 `5000/0`, `5000/1`... 按索引递增存储图片，简洁有效。PicScreenSaver 可采用同样方式：`SSIMAGE/1`, `SSIMAGE/2`...

**配置参数设计**：旧方案的 14 个配置参数覆盖全面，其中 `bMovePicture`（Ken Burns 效果）、`bAutoAdjustSize`（自适应尺寸）是值得考虑加入 Phase 4 的功能。

**退出机制**：旧方案将鼠标左键、右键、移动分别作为独立开关，PicScreenSaver 目前简化为统一处理，这是合理的简化。

### 10.3 旧方案的缺陷（PicScreenSaver 已改进）

| 缺陷 | 旧方案表现 | PicScreenSaver 改进 |
|------|-----------|-------------------|
| 图片未降采样 | 4K 图直接嵌入，体积大 | 强制降至 1080P |
| 动画无 GPU 加速 | GDI 纯 CPU 渲染，老电脑卡顿 | WPF DirectX 硬件加速 |
| 配置泄露制作环境 | 嵌入桌面路径、邮箱地址 | 仅存必要参数 |
| INI 文件配置 | 依赖外部文件，移动后丢失配置 | 配置完全内嵌于 .scr |
| 单一效果选择 | 每次只能选一种效果 | 多选 + 随机轮换 |
| 编译器过旧 | MSVC 6.0（1998），兼容性受限 | .NET FX 4.8，现代 API |

---

*10.scr 逆向分析报告 · 2026-06-08*
*分析工具：pefile、Pillow、Python 3 字符串提取*
