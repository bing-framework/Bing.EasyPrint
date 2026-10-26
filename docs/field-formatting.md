# 字段格式化

`BgField.Format` 是模板字段的可持久化显示格式；未配置时完全沿用旧绑定输出。格式只描述数据，不执行脚本、表达式或任意代码。

## 配置

```json
{
  "Name": "Amount",
  "Format": {
    "Kind": "Currency",
    "Pattern": "C2",
    "Culture": "en-US",
    "NullText": "N/A",
    "PadLength": 12,
    "PadCharacter": " ",
    "Prefix": "USD ",
    "Suffix": ""
  }
}
```

| 字段 | 说明 |
| --- | --- |
| `Kind` | `Text`、`Number`、`Currency`、`Date`、`Enum`，省略为 `Text`。 |
| `Pattern` | .NET 数字、货币或日期输出格式。`Number`/`Currency`/`Date` 必须提供。 |
| `InputFormat` | 日期输入格式；省略时接受明确的 ISO 日期、ISO 日期时间或带 offset 的 ISO 时间。显式格式必须同时包含年、月、日。 |
| `Culture` | 显式文化名称；省略使用 `InvariantCulture`。 |
| `NullText` | 实际 `null` 的替代文本；空字符串不视为 null。 |
| `PadLength` | 格式化/映射后的最小字符数；按总长度补齐。 |
| `PadCharacter` | 单个补齐字符，默认 `0`。 |
| `Prefix`/`Suffix` | 补齐后的前后缀。 |
| `Map` | `Enum` 的原始值到显示文本映射，必须是 JSON 对象。 |

处理顺序固定为：

```text
取值
→ 实际 null 处理
→ 类型转换 / 枚举映射
→ 格式化
→ 补齐
→ 前后缀
```

数字使用 `decimal` 语义；字符串先按显式文化解析，再格式化。Host 为格式化字段保留十进制和 ISO 日期原文，同时为无 `Format` 字段保留旧 `JToken` 日期/浮点语义。Raw 数字保留原始词法并延迟转换：只有实际绑定到 `Number`/`Currency` 等需要 decimal 的字段时才检查溢出；未使用的大数不会破坏旧数据路径。前端只把会丢失精度的 JSON 数字表示为带原始词法的内部数字包装，序列化时仍输出 JSON 数字；普通数字仍是 number，带引号的区域数字字符串不会被当成 JSON 数字。非法 JSON 数字词法严格拒绝。标准数字格式的大小写等价，长度预检包含符号、精度和已支持格式；短结果在小预算下仍可输出，大精度输出会提前拒绝。日期不会读取当前时间，也不会进行隐式本地时区换算。日期字符串带 offset 时保留 offset；日期输入必须包含完整年、月、日，`InputFormat` 也不能缺少年月日；格式包含 `K`/`z` 时输入必须实际带 offset。日期输出使用显式 `Pattern` 和 `Culture`。`PadCharacter=0` 且文本带正负号时，符号保留在最前，例如 `-12.5`、`PadLength=8`、`Pattern="0.00"` 得到 `-0012.50`。

未配置 `Format` 时仍完全走旧绑定路径，包括旧 JSON 日期显示和原有异常类型；配置了格式但 Pattern、精度、日期输入或长度非法时，Host 返回带字段路径的 400。格式化前按实际值计算可信长度上界并预留空间，不会先写出半截文本。

## 错误

以下情况在编码前失败，并携带元素路径和字段路径：

- 缺失字段路径；
- 数字/日期转换失败或溢出；
- 枚举值没有映射；
- `Pattern`/`Map` 缺失或超出配置长度；
- 补齐或格式化输出超过当前文本预算。

不会把原值直接输出以掩盖转换错误。`ValueFunc` 仍只适用于内存模板，且不能保存为 JSON。

## Host 与设计器

Host 响应特性包含 `field-format`。预览和预览包的布局元素同时返回 `text` 与 `displayText`，两者都是服务端按格式生成的权威显示值；画布使用 `displayText`。前端只编辑配置，不复制金额/日期转换算法。

旧 Host 未声明 `field-format` 时，客户端请求会明确失败；项目文件仍保留 `Format`，不会静默打印未格式化值。

## 示例

- `samples/Templates/field-format-template.json`
- `samples/Templates/FieldFormatSamples.cs`
- `tests/Bing.EasyPrint.Tests/FieldFormatTests.cs`
- `web/Bing.EasyPrint.Designer/e2e/fieldBatch.spec.ts`
- Host 定向测试：`tests/DesignerHost.Tests/FieldFormatHostTests.cs`
