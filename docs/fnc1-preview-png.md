# FNC1 规则、官方对照、真实码图预览与 PNG 导出

本页记录 2026-10-23 新阶段四项能力的当前契约。历史验收报告保留其原日期和原结果；本轮对 FNC1 规则的修正和新增预览/导出能力以本页及[本轮验收报告](fnc1-preview-png-verification-2026-10-23.md)为准。

## A. FNC1 分隔规则修正

固定字典 `d1702ffb2fa5060f8460f7d581b01e377deaa1d6` 第 41 行规定：`*` 表示“预定义长度、无需 FNC1”；没有 `*` 的 AI 即使字段总长度固定，在后续还有元素时也必须插入 FNC1。

因此：

- `00`、`01`、`02`、`03`、`11`–`17`、`20`、`3100`–`3165` 等带 `*` 的 AI 不插入分隔；
- `7001`、`402`、`4307`、`4325`、`8111`、`8005`、`7040`、`8008` 等固定长度但没有 `*` 的 AI，后跟另一元素时插入 ASCII 29；
- 最后一个元素后不追加尾随 FNC1；
- 可变长度 AI 仍按字段边界插入 FNC1。

示例：

```text
输入：(01)12345678901231(7001)1234567890123(10)LOT
输出：011234567890123170011234567890123<GS>10LOT
```

该规则此前按“总长度是否可变”判断，导致固定但非预定义 AI 漏分隔。该旧判断已被推翻；旧测试期望同步修正，未通过放宽生产规则迁就旧断言。

## B. 官方 GS1 独立对照

离线测试固定以下来源：

- GS1 Syntax Dictionary 提交 `d1702ffb2fa5060f8460f7d581b01e377deaa1d6`（Apache-2.0），SHA-256 `8d01d6596c77dbbfbea25939b2ff4558854d36a4d528dc5195f692a4539e8921`。
- GS1 Syntax Engine 提交 `9ae7fae0d43a731b6e22005d482f81482e092499`（Apache-2.0），其 `src/c-lib/aitable.inc`、JavaScript 测试和 linter 单元测试作为独立参考。
- 该 Engine 提交内置字典为 Release `2025-01-30`；与固定字典仅注释行不同，AI 定义一致。

离线夹具覆盖：

- 全部 536 个 AI 的官方 `NO_FNC1` / `DO_FNC1` 标志逐项比对（`tests/Bing.EasyPrint.Tests/Fixtures/Gs1OfficialFnc1Flags.cs`）；
- 官方 linter 向量：`csum`、`csumalpha`、日期/时间、ISO 3166/4217/5218、IBAN、件数、包装类型、优惠券、百分比编码、可选组件；
- 官方 Syntax Engine JavaScript 测试的括号元素串规范化结果；
- 合法/非法字段、最后元素无分隔、多元素、未知 AI；
- GS1-128、Expanded 与 Composite 可达输出回归；
- 原始单元素括号兼容路径单独验证，不伪装成完整字典严格校验。

更新方法：替换固定字典和 Engine 提交，更新 `Gs1SyntaxDictionary.DictionaryCommit` / `DictionarySha256`、官方 FNC1 夹具、测试报告和 ThirdPartyNotices；若新版本引入新 Linter，必须先实现并让加载器拒绝未知 Linter。全量 `req`/`ex` 跨 AI 业务关系不在本轮。

## C. 真实码图预览

Host 新增两个加法路由，旧 `/preview` 几何 JSON 语义保持不变：

| 路由 | 响应 | 用途 |
| --- | --- | --- |
| `POST /api/designer/element-png` | `image/png` | 单个元素真实软件位图 |
| `POST /api/designer/page-png` | 单页 `image/png`；多页 `application/zip`，每页一个 PNG | ESC/POS Raster 完整页面 |

请求沿用现有 Designer 请求字段，并新增可选 `pageIndex`、`elementIndex`、`sourcePath`。元素坐标按预览页元素顺序解析；`sourcePath` 若提供必须匹配，重复元素用索引区分。

能力响应新增：

```json
{
  "rasterPreview": {
    "softwareBitmap": true,
    "elementPng": true,
    "elementTypes": [0, 1, 2, 3, 4, 5, 7, 8, 9, 10, 11],
    "pagePng": true,
    "pagePngReason": null,
    "dpiX": 203,
    "dpiY": 203
  }
}
```

- `elementTypes` 是当前协议/布局可准确生成软件位图的元素类型。ESC/POS Raster 可覆盖文本、1D、QR、DataMatrix/PDF417、GS1 Composite、图片、基础形状和边框；其他协议只覆盖边框、QR、二维点阵、Composite、图片和基础形状（`0,2,3,5,7,8,9,10,11`），不宣称原生文字、Native HRI 或 1D 宽条/旋转语义。
- 元素选择按布局元素与绘制 primitive 的 ownership 映射解析。一个矩形会映射其四条 `PrintLineElement`，椭圆会映射全部分段，边框、重复实例和分页不会按 primitive 裸索引漂移；没有绘制的空文本/空组返回与其布局尺寸一致的空白图。
- `pagePng` 仅 ESC/POS Raster 为 `true`；其他协议返回 `false` 和明确原因，不输出假完整页。
- 新特性头：`raster-element-png`、`raster-page-png`。旧 Host 缺少特性时仍返回旧几何预览，新增下载按钮禁用，不发起图片请求。
- 画布复用 Host 编码结果，不建立浏览器第二套条码编码器；PNG 像素按整数模块缩放，关闭图像平滑。预览请求取消时中止旧图片请求，旧响应不覆盖新内容。
- 设备字体文本拒绝生成像素一致的软件位图；页面导出会明确失败，不会用主机字体冒充设备字体。CPCL 旋转/宽条、TSPL 显式宽条和 Native HRI 等无法无歧义转换为 Raster 的组合明确返回 `UnsupportedCapability`，前端保持布局示意并说明原因。

## D. PNG 导出

- 元素 PNG：`image/png`，尺寸等于该元素实际编码/绘制尺寸，包含静区、旋转、HRI 和图片内容。
- ESC/POS Raster 页面 PNG：单页直接 `image/png`，多页为每页 PNG 的 ZIP（`page-001.png` 等）。
- 页面像素与页面打印点一一对应；PNG `pHYs` 按输出横纵 DPI 写入（如 203 DPI -> 7992 pixels/metre）。
- 其他协议不提供完整页面 PNG；只允许下载本身可准确编码的元素码图。原生字体/固件专属完整页不使用 DOM 截图或占位图代替。
- 下载只生成软件位图，不连接或发送打印机；沿用现有 Host 准入、资源预算、取消和异常清理。raster 快照、源图数据、RGBA、PNG 和 ZIP 都计入当前资源作用域，不持久化保存；超限在输出前结构化失败。

## 验证入口

- 官方 FNC1 全表：`dotnet test tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj -f net6.0 --filter FullyQualifiedName~Gs1SyntaxDictionaryTests`
- Host PNG 与 ZXing 解码：`dotnet test tests/DesignerHost.Tests/DesignerHost.Tests.csproj -f net10.0 --filter FullyQualifiedName~RasterPreviewHostTests`
- 前端单元：`npm test`
- 浏览器真实 Host：`npx playwright test e2e/rasterPreviewPng.spec.ts --workers=1 --retries=0`
- 完整门禁和原始证据见[本轮验收报告](fnc1-preview-png-verification-2026-10-23.md)。
