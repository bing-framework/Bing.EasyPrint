# GS1 完整关联校验与 GS1 QR / DataMatrix

本页说明固定字典的 `req`/`ex` 关联校验，以及 GS1 QR Code、GS1 DataMatrix 的模板、Host 和设计器能力。历史 FNC1/PNG 报告保留原日期和原结果，本页记录 2026-10-23 新阶段。

## 1. 关联规则

固定字典为 `d1702ffb2fa5060f8460f7d581b01e377deaa1d6`。解析遵循字典第 66–85 行：

- 逗号分隔可选备选；加号表示同一组内必须同时存在。
- `n` 匹配一个数字位置，例如 `35nn`、`394n`。
- 一个 AI 有多个 `req` 属性时，每个 `req` 都必须满足；不能只满足其中一个。
- `ex` 只排斥其他 AI；即使模式也匹配自身，也不排斥自身。
- 关联规则作用于显式给出的完整数据集合，不会自动合并整页互不相关的条码。

核心入口：

```csharp
using Bing.EasyPrint.Protocols;

Gs1AssociationValidator.Validate("(01)12345678901231(10)LOT-A");
Gs1AssociationValidator.ValidateCarriers(new[] { "(01)12345678901231", "(10)LOT-A" });
```

`Validate` 处理单个载体；`ValidateCarriers` 才会合并多个载体。两者都会先执行逐字段语法/Linter 校验，再执行关联校验。括号式和规范化输入在逐字段语法上等价：每个载体先独立解析，合并后才执行 `req`/`ex`；因此 `4300` 与 `00`、`254` 与 `414` 可以分散在不同载体。字段边界按字典的预定义长度属性处理，固定长度但没有 `*` 的 AI 在后续元素前仍需要 FNC1/ASCII 29。失败信息包含具体 AI、缺失关系或冲突的 `ex` 原文。

模板和 Host 默认保持旧的逐字段语法行为。只有显式设置 `TemplateRenderOptions.ValidateGs1Associations = true` 或 Host 请求 `validateGs1Associations: true` 才启用完整关联校验。严格开关只控制关联校验；新 GS1 二维模式始终执行逐字段语法、校验位、分隔符和非 AI 数据校验。设计器在输出设置中提供“严格 GS1 关联”开关；设置会随项目保存/导入，旧 Host 未协商时功能不可用。

Composite 必须先分别保证主数据、次数据语法正确，再合并主次 AI 判断。EAN/UPC/DataBar 的原始 GTIN 主码贡献隐式 `(01)`，因此只有 `(20)12` 或 `(10)LOT` 的次码不能单独调用严格校验；通过 Composite 合并后可以满足主标识。独立条码不会被页面扫描自动合并。

`(37)` 的字典规则同时要求 `(00)` 以及 `(02)` 或 `(8026)`，所以包含 `(02)...(37)` 的严格示例还需要 `(00)` 才能通过；这不是把独立条码自动合并，而是同一显式数据集合内的字典关联。

Composite 的严格模板路径会把合并后的作用域传到实际测量、绘制和协议文档副本：主次数据先分别做纯逐字段语法检查，再合并执行 `req`/`ex`，因此 `4300` 与 `00`、`254` 与 `414` 分散在主次载体时可以输出。独立 `PrintGs1CompositeElement` 默认仍保留原有逐载体检查，不因该路径改变旧入口行为。

## 2. GS1 二维码

现有 `BpQrCodeElement` 和 `BpMatrixCodeElement` 新增 `Gs1` 显式选项，默认 `false`/缺省保持普通码：

| 模板元素 | `Gs1=true` | `Gs1` 缺省/false |
| --- | --- | --- |
| Type 3 QR | GS1 QR Code，首位 FNC1，`]Q3` | 普通 QR Code |
| Type 10 DataMatrix | GS1 DataMatrix，首位 FNC1，`]d2` | 普通 DataMatrix |
| Type 10 PDF417 | 明确拒绝 | 普通 PDF417 |

共享编码器使用 ZXing.Net 0.16.11 的 `GS1_FORMAT`；GS1 DataMatrix 同时启用紧凑编码。`GS1_FORMAT` 会在二维码中写 FNC1 first-position 模式头，但不会在数据前额外插入 ASCII 29；payload 是去除展示括号后的规范化 AI 串，字段分隔符使用 ASCII 29。字母数字模式下的字面 `%` 按 GS1 规则编码为 `%%`（解码回 `%`），`%%` 编码为 `%%%%`；含 ASCII 29 或非字母数字字符时使用字节模式，保留原始 `%` 和分隔符。编码结果不是“在普通文本前拼接 ASCII 29”的假 GS1 模式，独立 ZXing 解码可返回 `]Q3`/`]d2`、无额外开头 GS 和正确 payload。GS1 QR 的 `UnitWidth` 仍遵循 1–32 的现有契约；旋转 90/270 时按旋转后的实际占位检查单元格宽度，符号自身尺寸和静区限制仍先校验。

GS1 二维码通过 Host 图片路径输出，适用于：

- CPCL
- ESC/POS Raster（`GS v 0`）
- TSPL（`BITMAP`）
- ZPL（`^GFA`）

ESC/POS Native 明确拒绝 GS1 QR/DataMatrix；普通 QR/DataMatrix 的原路径不受影响。GS1 二维码不宣称设备原生 GS1 二维命令。严格关联开关对 GS1 QR 和 DataMatrix 作用一致，错误会带模板元素路径；同一模板中的相同 GS1 QR 实例复用完整编码参数的会话缓存，不改变纠错级别或静区。

### Host 契约

新增特性：

- `gs1-two-dimensional`
- `gs1-association-validation`

能力响应新增：

```json
{
  "gs1TwoDimensional": {
    "qr": true,
    "dataMatrix": true,
    "native": false,
    "associationValidation": true
  }
}
```

`/api/designer/element-png`、`/api/designer/page-png` 和 `/render` 复用同一共享编码结果。旧 Host 缺少 `gs1-two-dimensional` 时，设计器不会把 `Gs1` 参数静默降级为普通码；选项禁用并要求更新 Host。普通模式请求不要求该特性，保持旧客户端兼容。

### 设计器

二维码和二维点阵属性面板提供 `GS1 QR Code` / `GS1 DataMatrix` 复选框：

- 只在当前 Host/协议的 GS1 二维能力可用时启用；Native 和旧 Host 禁用并说明原因。
- 切换二维格式为 PDF417 会清除 `Gs1`，避免生成普通 PDF417。
- 项目 JSON、模板包、撤销重做和保存/导入保留该布尔值。
- 输出设置提供“严格 GS1 关联”开关；错误显示具体 AI 和缺失/冲突关系。

## 3. 示例

- [GS1 二维模板 JSON](../samples/Templates/gs1-two-dimensional.json)
- [C# 四协议和 JSON 往返示例](../samples/Templates/Gs1TwoDimensionalSamples.cs)

```powershell
dotnet run --project samples/Templates/Templates.csproj -c Release -- artifacts/gs1-associations-2d-20261023/samples
```

示例同时生成 `gs1-two-dimensional-strict.cpcl`，并断言缺少 `(37)` 的 GS1 DataMatrix 在严格关联下失败。

## 4. 边界

- 不实现 Digital Link、PDF/SVG、批量数据导入、富文本、传输层或协议插件。
- 不做实机打印/扫码验收；设备原生 GS1 二维、固件字体和物理尺寸仍需目标机型验证。
- GS1 二维图片路径使用共享编码器，不代表目标设备会原生解释 GS1 标志。
- 关联校验只覆盖固定字典的 `req`/`ex` 语法关系；业务对象身份、跨页组包和实际扫描结果仍由调用方负责。
