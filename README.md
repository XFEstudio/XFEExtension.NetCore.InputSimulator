# XFE各类拓展.NetCore.InputSimulator

## 简介

InputSimulator是一个简单的模拟键盘输入的包，其中包含了模拟键盘按键输入和鼠标输入

## 用法

### 模拟键盘输入

```csharp
using XFE各类拓展.NetCore.InputSimulator;

InputSimulator.PressKey('X');//按下X按键
InputSimulator.InputKeys("XFE");//顺序按下 X、F、E 按键
```

### 模拟鼠标移动

```csharp
using XFE各类拓展.NetCore.InputSimulator;

InputSimulator.Move(100, 100);//使得鼠标从当前位置相对移动 100 x 100 个像素（相对于鼠标指针的当前位置）
InputSimulator.LocateTo(1900, 202);//使得鼠标移动至绝对坐标 1900,202 的位置处（相对于整个屏幕的位置）
InputSimulator.LocateTo(new Point(0, 0));//使得鼠标移动至绝对坐标（0, 0）的位置处（相对于整个屏幕的位置）
InputSimulator.LocateTo(ScreenPosition.TopLeft);//使得鼠标移动至屏幕的左上角，即（0, 0）的位置处（相对于整个屏幕的位置）
```

### 模拟鼠标点击

```csharp
using XFE各类拓展.NetCore.InputSimulator;

InputSimulator.MouseClick(MouseButton.Left);//模拟鼠标左键点击
InputSimulator.MouseWhellRoll(-800);//模拟鼠标滚轮下滑800像素
```

### 获取信息

```csharp
using XFE各类拓展.NetCore.InputSimulator;

var screenSize = GetScreenSize();//获取屏幕的宽和高（像素）
var mousePoint = GetMousePosition();//获取鼠标的位置
var percentage = GetMousePointRelatively();//获取鼠标相对于屏幕的比值（范围是0-1的double型百分比）
```