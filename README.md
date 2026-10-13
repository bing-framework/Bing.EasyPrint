# Bing.EasyPrint

[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](https://mit-license.org/)

Bing.EasyPrint 是面向热敏打印机和部分针式打印场景的 .NET 打印指令生成库。当前代码支持 CPCL、ESC/POS、TSPL 和 ZPL，并提供协议命令、统一打印模型、版本化模板渲染以及浏览器模板设计器。

项目只生成协议字节，不负责打印机连接、驱动、设备发现、打印队列或状态查询。

## 文档

- [项目文档索引](docs/README.md)
- [核心打印与协议](docs/core-printing.md)
- [模板渲染指南](docs/templates-guide.md)
- [设计器与 Host](docs/designer-host.md)
- [开发与验证](docs/development.md)
- [待实现与待确认](docs/status.md)

## 目标框架

核心库和模板库面向 `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0` 和 `netstandard2.1`。DesignerHost 使用 .NET 8，浏览器设计器使用 Vue 3、TypeScript 和 Vite。

## 快速示例

```csharp
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Protocols;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var bytes = new EscPosPrintCommand(Encoding.GetEncoding("gbk"))
    .Initialize()
    .Text("订单完成")
    .Feed(2)
    .Build()
    .GetBytes();
```

完整的协议选择、模板渲染、设备 Profile 和资源限制说明见 `docs` 目录。输出字节投入生产前，应按目标打印机型号和固件完成实机验收。

## 开源地址与许可

- 源码：[bing-framework/EasyPrint](https://github.com/bing-framework/EasyPrint)
- NuGet：[Bing.EasyPrint](https://www.nuget.org/packages/Bing.EasyPrint)
- 许可：MIT
