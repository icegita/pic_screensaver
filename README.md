# PicScreenSaver v1.0.0

个性化 Windows 屏保制作工具。选择图片、配置过渡效果、一键生成 `.scr` 屏保文件。

## 项目结构

```
PicScreenSaver/
├── PicScreenSaver.Maker/        # 制作者工具 — 可视化编辑屏保项目
│   ├── MainWindow.xaml(.cs)     # 主界面：图片管理、效果选择、设置
│   ├── Models/
│   │   ├── ImageItem.cs         # 图片项（缩略图、压缩数据）
│   │   ├── ProjectFile.cs       # .ssproj 项目文件序列化
│   │   └── ScreensaverConfig.cs # 嵌入 .scr 的配置
│   ├── Services/
│   │   ├── ImageProcessor.cs    # 图片压缩、缩略图生成
│   │   └── PackageBuilder.cs    # Win32 资源注入，打包 .scr
│   └── Themes/                  # 浅色/深色主题
│
├── PicScreenSaver.Runtime/      # 屏保运行时 — 实际的 .scr
│   ├── Program.cs               # 入口：/s /c /p 命令行解析
│   ├── ScreensaverWindow.xaml   # 全屏播放窗口
│   ├── ConfigDialog.xaml        # 运行时设置页（浅色主题）
│   ├── Engine/
│   │   ├── SlideEngine.cs       # 轮播引擎（顺序/随机 + 预加载）
│   │   ├── TransitionManager.cs # 过渡效果管理器
│   │   └── Transitions/         # 30 种过渡效果实现
│   ├── ResourceLoader.cs        # 从 .scr 读取嵌入资源
│   └── ScreensaverConfig.cs     # 运行时配置反序列化
│
└── img/                         # 素材
    ├── icon.png                 # Maker 图标
    ├── sys.png                  # 设置页图标
    ├── pic1.jpg / pic2.jpg      # 预览示例图
```

## 功能特性

### Maker（制作者）
- **图片管理**：添加/删除/拖拽排序/移动，实时预览 + 压缩信息
- **30 种过渡效果**：淡变、滑动、缩放、擦除、推拉、旋转、百叶窗、形状揭示、棋盘、扇形擦除
  - 每种效果均有实时动画预览
- **参数设置**：展示时长(1-60s)、过渡时长(0.3-5s)、压缩质量、顺序/随机播放
- **长按加速度**：加减号长按 1.5s 后加速，步进自动切为整数
- **主题切换**：浅色/深色主题
- **生成 .scr**：将运行时 + 配置 + 图片打包为屏保文件，支持一键安装到系统

### Runtime（屏保运行时）
- **命令行支持**：`/s`(屏保)、`/c`(设置)、`/p`(预览)
- **设置页**：浅色主题，展示时长/过渡时长/播放顺序/效果选择 + 过渡动画实时预览
- **全屏轮播**：支持随机/顺序播放，图片预加载，30 种过渡效果随机切换
- **自动退出**：鼠标移动/按键触发退出，500ms 防误触

## 技术栈

- **语言**：C# (.NET Framework 4.8)
- **UI 框架**：WPF
- **打包方式**：Win32 Resource API（`BeginUpdateResource`/`UpdateResource`）
- **依赖**：Newtonsoft.Json、PeNet（PE 解析，当前未激活）

## 构建

```bash
# 编译 Maker + Runtime
dotnet build PicScreenSaver.Maker/PicScreenSaver.Maker.csproj

# 运行 Runtime 设置页
PicScreenSaver.Runtime/bin/Debug/net48/PicScreenSaver.Runtime.exe /c
```

## 字体

全局中文字体使用 **Noto Sans SC**，等宽数字使用 **Consolas**。
