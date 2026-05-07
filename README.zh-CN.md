# XFEExtension.NetCore.InputSimulator

[![NuGet 版本](https://img.shields.io/nuget/v/XFEExtension.NetCore.InputSimulator.svg)](https://www.nuget.org/packages/XFEExtension.NetCore.InputSimulator/)
[![NuGet 下载量](https://img.shields.io/nuget/dt/XFEExtension.NetCore.InputSimulator.svg)](https://www.nuget.org/packages/XFEExtension.NetCore.InputSimulator/)
[![许可证: MIT](https://img.shields.io/badge/许可证-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)

[English](README.md) | **简体中文**

## 简介

InputSimulator 是一个简单的模拟用户输入的包，支持模拟键盘按键输入、鼠标操作以及剪切板读写。

## 安装

通过 NuGet 包管理器安装：

```
Install-Package XFEExtension.NetCore.InputSimulator
```

或通过 .NET CLI：

```
dotnet add package XFEExtension.NetCore.InputSimulator
```

## 用法

### 模拟键盘输入

```csharp
using XFEExtension.NetCore.InputSimulator;

InputSimulator.KeyDown('A');           // 按住 A 键
InputSimulator.KeyUp('A');             // 松开 A 键
InputSimulator.PressKey('X');          // 按下并松开 X 键
InputSimulator.InputKeys("XFE");       // 顺序按下 X、F、E 键

// 异步方法：按住按键指定时间（毫秒）后松开
await InputSimulator.PressKeyAsync('A', holdTime: 500);

// 异步方法：以指定的按住时间和间隔延迟输入一组按键
await InputSimulator.InputKeysAsync("XFE", holdTime: 100, delay: 50);
```

### 模拟鼠标移动

```csharp
using XFEExtension.NetCore.InputSimulator;

// 相对移动：从当前位置偏移 100, 100 像素
InputSimulator.MouseMove(100, 100);

// 绝对定位
InputSimulator.LocateTo(1900, 202);
InputSimulator.LocateTo(new Point(0, 0));

// 定位到屏幕预设位置（TopLeft / Top / TopRight / Left / Center / Right / BottomLeft / Bottom / BottomRight）
InputSimulator.LocateTo(ScreenPosition.TopLeft);
InputSimulator.LocateTo(ScreenPosition.Center);
```

### 模拟鼠标点击

```csharp
using XFEExtension.NetCore.InputSimulator;

InputSimulator.MouseClick(MouseButton.Left);    // 模拟鼠标左键单击
InputSimulator.MouseClick(MouseButton.Right);   // 模拟鼠标右键单击
InputSimulator.MouseClick(MouseButton.Middle);  // 模拟鼠标中键单击

// 异步方法：按住鼠标按键指定时间（毫秒）后松开
await InputSimulator.MouseClickAsync(MouseButton.Left, holdTime: 1000);

// 模拟鼠标滚轮滚动（负值向下，正值向上）
InputSimulator.MouseWhellRoll(-800);
```

### 获取系统信息

```csharp
using XFEExtension.NetCore.InputSimulator;

Point screenSize = InputSimulator.GetScreenSize();               // 获取屏幕分辨率（宽 x 高）
Point mousePos  = InputSimulator.GetMousePosition();             // 获取鼠标当前坐标
(double X, double Y) rel = InputSimulator.GetMousePointRelatively(); // 获取鼠标位置占屏幕比例（0~1）
bool isDown = InputSimulator.GetMouseDown(MouseButton.Left);     // 检测鼠标左键是否按下
double scale = InputSimulator.GetScalingFactorForWindow(hWnd);   // 获取指定窗口的 DPI 缩放倍率
```

### 剪切板操作

```csharp
using XFEExtension.NetCore.InputSimulator;

// 写入文本到剪切板
Clipboard.SetClipboardContent("Hello, XFE!");

// 读取剪切板内容
object? content = Clipboard.GetClipboardContent();

// 使用自定义格式（默认为 CF_UNICODETEXT）
Clipboard.SetClipboardContent("Hello", ClipboardFormat.CF_TEXT);
```

## API 参考

### `InputSimulator` 静态类

| 方法 | 说明 |
|------|------|
| `KeyDown(char key)` | 按住指定按键 |
| `KeyUp(char key)` | 松开指定按键 |
| `PressKey(char key)` | 按下并立即松开指定按键 |
| `PressKeyAsync(char key, int holdTime)` | 按住指定按键一段时间后松开（异步） |
| `InputKeys(string keys)` | 依次按下字符串中的每个按键 |
| `InputKeysAsync(string keys, int holdTime, int delay)` | 依次按下每个按键，支持按住时长和间隔延迟（异步） |
| `MouseMove(int x, int y)` | 相对于当前位置移动鼠标 |
| `LocateTo(int x, int y)` | 将鼠标移至绝对坐标 |
| `LocateTo(Point point)` | 将鼠标移至指定 Point |
| `LocateTo(ScreenPosition pos)` | 将鼠标移至预设屏幕位置 |
| `MouseClick(MouseButton button)` | 模拟鼠标单击 |
| `MouseClickAsync(MouseButton button, int holdTime)` | 模拟鼠标长按后松开（异步） |
| `MouseWhellRoll(int length)` | 模拟鼠标滚轮滚动 |
| `GetMouseDown(MouseButton button)` | 检测指定鼠标按键是否被按下 |
| `GetScreenSize()` | 获取屏幕分辨率 |
| `GetMousePosition()` | 获取鼠标当前位置 |
| `GetMousePointRelatively()` | 获取鼠标相对屏幕的位置比例 |
| `GetScalingFactorForWindow(nint hWnd)` | 获取窗口 DPI 缩放倍率 |

### `Clipboard` 静态类

| 方法 | 说明 |
|------|------|
| `SetClipboardContent(string text, uint format)` | 将文本写入剪切板 |
| `GetClipboardContent(uint format)` | 从剪切板读取内容 |

### `ScreenPosition` 枚举

`TopLeft` · `Top` · `TopRight` · `Left` · `Center` · `Right` · `BottomLeft` · `Bottom` · `BottomRight`

### `MouseButton` 枚举

`Left` · `Right` · `Middle`

## 许可证

本项目基于 [MIT 许可证](LICENSE) 开源。
