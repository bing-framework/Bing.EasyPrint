# 架构与模块

## 功能说明

Bing.EasyPrint 将打印内容逐层转换为协议字节。调用方可以直接使用协议命令，也可以先构造协议无关模型，或从模板和业务数据生成该模型。最终字节由应用自己的传输层发送到打印机。

```text
模板 JSON + 业务数据 + 资源
            |
            v
   Bing.EasyPrint.Templates
            |
            v
 PrintDocument / PrintPage / PrintElement
            |
            v
 CPCL / ESC-POS / TSPL / ZPL Provider
            |
            v
  PrintPageOutput / byte[]
```

## 模块与关键文件

### 核心协议库

- `src/Bing.EasyPrint/CPCL/`：兼容原有 CPCL 命令和组件 API。
- `src/Bing.EasyPrint/Protocols/EscPosPrintCommand.cs`：ESC/POS Native 与 Raster 命令。
- `src/Bing.EasyPrint/Protocols/TsplPrintCommand.cs`：TSPL 标签命令。
- `src/Bing.EasyPrint/Protocols/ZplPrintCommand.cs`：ZPL 标签命令。
- `src/Bing.EasyPrint/ProtocolCommandFactory.cs`：按 `PrintProtocol` 创建直接命令。
- `src/Bing.EasyPrint/IEasyPrint.cs` 与 `Extensions/Extensions.IEasyPrint.cs`：兼容的泛型及便捷创建入口。
- `src/Bing.EasyPrint/IRestrictedRawPrintCommand.cs`：受限 Raw 文本入口。

### 统一打印模型

- `src/Bing.EasyPrint/Printing/PrintDocument.cs`：文档、页面及文字、线、矩形、条码、二维码和图片元素。
- `src/Bing.EasyPrint/Printing/IPrintProtocolProvider.cs`：Provider 验证和渲染契约。
- `src/Bing.EasyPrint/Printing/PrintProtocolProviders.cs`：四协议 Provider 工厂与实现。
- `src/Bing.EasyPrint/Printing/PrintJobRenderer.cs`：多份、顺序、进纸和裁切编排。
- `src/Bing.EasyPrint/Printing/PrintResourceScope.cs`：可嵌套的工作资源预留。
- `src/Bing.EasyPrint/PrintCapabilities.cs`：协议与设备 Profile 的能力快照。

### 模板库

- `src/Bing.EasyPrint.Templates/Rendering/TemplateLoader.cs`：版本化 JSON 的加载、校验和保存。
- `src/Bing.EasyPrint.Templates/Rendering/TemplateRenderer.cs`：绑定、布局、分页和协议输出。
- `src/Bing.EasyPrint.Templates/Rendering/TemplateRenderOptions.cs`：协议、设备、资源和预算配置。
- `src/Bing.EasyPrint.Templates/Rendering/TemplateRenderResult.cs`：页面字节、布局轨迹和任务元数据。
- `src/Bing.EasyPrint.Templates/Labels/`：价签预设与数据适配。

### 设计器

- `tools/DesignerHost/Program.cs`：ASP.NET Core HTTP 入口。
- `tools/DesignerHost/DesignerRequestProcessor.cs`：模板请求、严格 DTO、能力、预览和导出处理。
- `tools/DesignerHost/DesignerRequestAdmission.cs`：并发、队列与工作内存准入。
- `web/Bing.EasyPrint.Designer/src/App.vue`：Vue 设计器主界面。
- `web/Bing.EasyPrint.Designer/src/api.ts`：Host 契约协商和请求封装。
- `web/Bing.EasyPrint.Designer/src/archive.ts`：模板包导入导出与路径校验。

## 核心实现逻辑

1. 输入在写入输出前完成结构、能力、数据和几何预检。
2. 统一模型由目标 Provider 转换为逐页协议输出；不支持的能力抛出带协议和元素路径的异常。
3. ESC/POS Native 使用顺序式设备命令；Raster 将坐标页面合成为单色位图。
4. 设备 Profile 收窄默认能力，并提供纸宽、DPI、字体、条码、图片和切刀参数。
5. 输出和临时对象受页数、字节、图片像素及工作内存预算约束，并支持取消。

## 依赖方向

`Bing.EasyPrint.Templates` 引用 `Bing.EasyPrint`；DesignerHost 引用 Templates；Vue 设计器只通过 HTTP 调用 Host。核心库不依赖 Host 或浏览器项目，也不包含打印传输实现。

公开入口的选择和错误类型见[公开 API 入口](public-api.md)，资源预算的层级关系见[资源限制与错误处理](resource-limits.md)。

## 已知限制

- 坐标和字体度量使用逻辑打印点；物理尺寸仍取决于设备 DPI、固件和字体。
- Profile 是调用方声明的设备契约，不自动探测设备能力。
- 真实打印、扫描、走纸和切刀结果需要外部设备验收。
