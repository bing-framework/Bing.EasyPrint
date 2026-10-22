# Bing.EasyPrint

[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](https://mit-license.org/)

Bing.EasyPrint 是面向热敏打印机和部分针式打印场景的 .NET 打印指令生成库。当前代码支持 CPCL、ESC/POS、TSPL 和 ZPL，并提供协议命令、统一打印模型、版本化模板渲染以及浏览器模板设计器。统一打印模型与模板现支持受限的 GS1 Composite：EAN-8/EAN-13/UPC-A/UPC-E 和 DataBar Omnidirectional/Truncated/Limited/单行 Expanded/Stacked/StackedOmnidirectional/ExpandedStacked + CC-A/CC-B，以及 GS1-128 + CC-A/CC-B/CC-C。此范围不代表完整 GS1 Composite 制式覆盖；EAN/UPC 附加码尚未实现；ExpandedStacked 当前使用固定两列布局。

项目只生成协议字节，不负责打印机连接、驱动、设备发现、打印队列或状态查询。

## 文档

- [项目文档索引](docs/README.md)
- [核心打印与协议](docs/core-printing.md)
- [模板渲染指南](docs/templates-guide.md)
- [Truncated / Stacked Omnidirectional 使用说明](docs/gs1-composite-databar-completion.md)
- [EAN/UPC Composite 使用说明](docs/gs1-composite-ean-upc.md)
- [ExpandedStacked 普通条码与 Composite 使用说明](docs/gs1-composite-expanded-stacked.md)
- [GS1-128 + CC-A/CC-B Composite 使用说明](docs/gs1-composite-gs1-128-ab.md)
- [GS1-128 + CC-A/CC-B Composite 验收记录](docs/gs1-128-ab-verification-2026-10-22.md)
- [GS1-128 + CC-C Composite 使用说明](docs/gs1-composite-cc-c.md)
- [单行 DataBar Expanded Composite 使用说明](docs/gs1-composite-expanded.md)
- [DataBar Stacked Composite 使用说明](docs/gs1-composite-stacked.md)
- [设计器与 Host](docs/designer-host.md)
- [开发与验证](docs/development.md)
- [2026-10-22 工程修复与验收](docs/review-fixes-2026-10-22.md)
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
