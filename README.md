# XFEExtension.NetCore.InputSimulator

[![NuGet Version](https://img.shields.io/nuget/v/XFEExtension.NetCore.InputSimulator.svg)](https://www.nuget.org/packages/XFEExtension.NetCore.InputSimulator/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/XFEExtension.NetCore.InputSimulator.svg)](https://www.nuget.org/packages/XFEExtension.NetCore.InputSimulator/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)

**English** | [简体中文](README.zh-CN.md)

## Introduction

XFEExtension.NetCore.InputSimulator is a lightweight library for simulating user input on Windows. It supports keyboard key simulation, mouse control, and clipboard read/write operations.

## Installation

Install via NuGet Package Manager:

```
Install-Package XFEExtension.NetCore.InputSimulator
```

Or via .NET CLI:

```
dotnet add package XFEExtension.NetCore.InputSimulator
```

## Usage

### Keyboard Simulation

```csharp
using XFEExtension.NetCore.InputSimulator;

InputSimulator.KeyDown('A');           // Hold down the A key
InputSimulator.KeyUp('A');             // Release the A key
InputSimulator.PressKey('X');          // Press and release the X key
InputSimulator.InputKeys("XFE");       // Press X, F, E keys in sequence

// Async: hold a key for the specified duration (ms) then release
await InputSimulator.PressKeyAsync('A', holdTime: 500);

// Async: press a sequence of keys with per-key hold time and inter-key delay
await InputSimulator.InputKeysAsync("XFE", holdTime: 100, delay: 50);
```

### Mouse Movement

```csharp
using XFEExtension.NetCore.InputSimulator;

// Relative movement: move the mouse 100 pixels in both X and Y from its current position
InputSimulator.MouseMove(100, 100);

// Absolute positioning
InputSimulator.LocateTo(1900, 202);
InputSimulator.LocateTo(new Point(0, 0));

// Move to a predefined screen position (TopLeft / Top / TopRight / Left / Center / Right / BottomLeft / Bottom / BottomRight)
InputSimulator.LocateTo(ScreenPosition.TopLeft);
InputSimulator.LocateTo(ScreenPosition.Center);
```

### Mouse Click

```csharp
using XFEExtension.NetCore.InputSimulator;

InputSimulator.MouseClick(MouseButton.Left);    // Simulate a left mouse button click
InputSimulator.MouseClick(MouseButton.Right);   // Simulate a right mouse button click
InputSimulator.MouseClick(MouseButton.Middle);  // Simulate a middle mouse button click

// Async: hold the mouse button for the specified duration (ms) then release
await InputSimulator.MouseClickAsync(MouseButton.Left, holdTime: 1000);

// Simulate mouse wheel scroll (negative = scroll down, positive = scroll up)
InputSimulator.MouseWhellRoll(-800);
```

### System Information

```csharp
using XFEExtension.NetCore.InputSimulator;

Point screenSize = InputSimulator.GetScreenSize();                    // Screen resolution (width x height)
Point mousePos   = InputSimulator.GetMousePosition();                 // Current mouse cursor position
(double X, double Y) rel = InputSimulator.GetMousePointRelatively(); // Mouse position as a ratio of screen size (0.0–1.0)
bool isDown = InputSimulator.GetMouseDown(MouseButton.Left);          // Check whether the left mouse button is held down
double scale = InputSimulator.GetScalingFactorForWindow(hWnd);        // DPI scaling factor for a given window handle
```

### Clipboard

```csharp
using XFEExtension.NetCore.InputSimulator;

// Write text to the clipboard
Clipboard.SetClipboardContent("Hello, XFE!");

// Read content from the clipboard
object? content = Clipboard.GetClipboardContent();

// Use a custom clipboard format (default is CF_UNICODETEXT)
Clipboard.SetClipboardContent("Hello", ClipboardFormat.CF_TEXT);
```

## API Reference

### `InputSimulator` Static Class

| Method | Description |
|--------|-------------|
| `KeyDown(char key)` | Hold down the specified key |
| `KeyUp(char key)` | Release the specified key |
| `PressKey(char key)` | Press and immediately release the specified key |
| `PressKeyAsync(char key, int holdTime)` | Hold the key for the given duration then release (async) |
| `InputKeys(string keys)` | Press each character in the string in sequence |
| `InputKeysAsync(string keys, int holdTime, int delay)` | Press each key with configurable hold time and delay (async) |
| `MouseMove(int x, int y)` | Move the mouse relative to its current position |
| `LocateTo(int x, int y)` | Move the mouse to an absolute screen coordinate |
| `LocateTo(Point point)` | Move the mouse to the specified `Point` |
| `LocateTo(ScreenPosition pos)` | Move the mouse to a predefined screen position |
| `MouseClick(MouseButton button)` | Simulate a mouse button click |
| `MouseClickAsync(MouseButton button, int holdTime)` | Hold a mouse button for the given duration then release (async) |
| `MouseWhellRoll(int length)` | Simulate mouse wheel scrolling |
| `GetMouseDown(MouseButton button)` | Check whether the specified mouse button is currently pressed |
| `GetScreenSize()` | Get the screen resolution |
| `GetMousePosition()` | Get the current mouse cursor position |
| `GetMousePointRelatively()` | Get the mouse position as a ratio of the screen dimensions |
| `GetScalingFactorForWindow(nint hWnd)` | Get the DPI scaling factor for the specified window |

### `Clipboard` Static Class

| Method | Description |
|--------|-------------|
| `SetClipboardContent(string text, uint format)` | Write text to the clipboard |
| `GetClipboardContent(uint format)` | Read content from the clipboard |

### `ScreenPosition` Enum

`TopLeft` · `Top` · `TopRight` · `Left` · `Center` · `Right` · `BottomLeft` · `Bottom` · `BottomRight`

### `MouseButton` Enum

`Left` · `Right` · `Middle`

## License

This project is licensed under the [MIT License](LICENSE).