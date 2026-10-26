# GS1 Digital Link 与结构化 AI

模板可把一组 GS1 应用标识符（AI）编码为现有 GS1 Elements，或生成供普通 QR / DataMatrix 使用的 GS1 Digital Link URI。AI 规则来自固定 GS1 Syntax Dictionary 提交 `d1702ffb2fa5060f8460f7d581b01e377deaa1d6`，共 536 个定义；URI 语法依据 [GS1 Digital Link URI Syntax 1.7.0](https://ref.gs1.org/standards/digital-link/uri-syntax/1.7.0/)。实现只解析和构造 URI，不访问 resolver。

## URI 行为

Digital Link 主键只接受未压缩 GTIN AI `01`。构建 API 可接收 8、12、13 或 14 位 GTIN，并左侧补零至 14 位后校验校验位；URI 中的 GTIN 必须已是 14 位。压缩形式和其他主键不支持。解析兼容接收一个尾随斜线（该形式不符合规范 ABNF，但规范建议 resolver 容忍）；构建和 Normalize 输出不带尾随斜线。

路径限定符按固定字典的 `dlpkey` 分支校验顺序和组合，包括 `22,10,21|235`；构建器按目录顺序输出限定符。GS1 数据属性放在 query 中并逐项校验。Digital Link Syntax 不支持的 AI（包括 `03`、`8014`、`8200`）会被目录标记为 Unsupported，并只在 Digital Link 模式拒绝；Elements 模式仍按固定字典语法校验。

非 GS1 query 参数使用有序键值列表，不使用字典，因此保留重复键、顺序和值。query 可用 `&` 或 `;` 分隔；扩展参数值可含额外 `=`，解码只进行一次，百分号转义必须完整且 UTF-8 合法，原始 `+` 按字面加号处理。未知全数字键和保留键 `linkType`、`context` 不会作为扩展参数放行。扩展参数若无法表达为 GS1 Elements，反向转换会明确失败。

`BaseUri` 必须是显式 HTTPS 基址；调用方的域名和 `/01` 前路径前缀会保留。`Fragment` 独立保留。Normalize 只规范化当前 URI，不选择或宣称一个 canonical resolver URL，因此不会把自有域名改成 `id.gs1.org`。所有操作都是本地字符串处理，不发起网络请求。

## 模板结构

`BgField.Gs1Input` 与原有 `Name`、`Value`、`ValueFunc` 互斥。省略 `Gs1Input` 时旧绑定与默认输出不变。每个 AI 项按顺序提供静态 `Value` 或字段路径 `Name`，恰好一个；`Format` 可显式格式化该项，避免对整条 URI 再次套用字段格式。

```json
{
  "Type": 3,
  "UnitWidth": 4,
  "Gs1": false,
  "Field": {
    "Gs1Input": {
      "Mode": 1,
      "BaseUri": "https://id.gs1.org",
      "Fragment": "product-label",
      "Items": [
        { "Ai": "01", "Name": "Product.Gtin" },
        { "Ai": "10", "Value": "LOT-A", "Placement": 2 }
      ],
      "QueryParameters": [
        { "Key": "source", "Value": "print" },
        { "Key": "source", "Value": "batch-7" }
      ]
    }
  }
}
```

`Mode` 为 `0`（Elements）或 `1`（DigitalLink）；`Placement` 为 `0`（Auto）、`1`（Qualifier）或 `2`（DataAttribute）。Digital Link 的 `01` 必须恰有一项且使用 Auto。QR / DataMatrix 的 Elements 模式要求 `Gs1=true`；Digital Link 要求普通模式 `Gs1=false`。DataMatrix 只支持 DataMatrix 格式；PDF417 和 Composite 拒绝 Digital Link，Composite 只接受 Elements。支持的 GS1 一维条码只接受 Elements。其他元素若包含 `Gs1Input` 会明确报错。

Elements 模式使用已有括号 AI 解析器和编码器，不引入第二套编码路径。绑定沿用模板当前行、重复项、变量和组件参数的字段上下文。校验请求省略数据时，依赖重复项、条件或组件参数的字段返回待验证；静态 GS1 配置仍会检查。请求提供数据后，重复路径或字段路径不存在会返回无效诊断。实际渲染仍会报告缺失绑定。req/ex 关联检查只有在 `ValidateAssociations=true` 时开启。

## Host 与编辑器

DesignerHost 提供三个特性协商接口：

| 方法与路径 | 用途 |
| --- | --- |
| `GET /api/designer/gs1-catalog` | 返回固定目录、角色、约束、分支、关联规则和字典提交 |
| `POST /api/designer/gs1-validate` | 对模板和当前数据运行服务端权威校验 |
| `POST /api/designer/gs1-convert` | 在括号 Elements 与 Digital Link 之间转换 |

编辑器要求 `gs1-ai-catalog`、`gs1-structured-input` 和 `gs1-digital-link` 三项特性。旧 Host 缺任一特性时会禁用编辑器并显示契约错误。目录只从 Host 加载；界面可搜索 AI、查看长度/组件/分隔符/分支说明，添加静态值或字段绑定，排序/删除 AI，并保留 query 参数顺序和重复键。绑定数据变化后会去抖并取消过期校验。

文本转换可接收括号串或 Digital Link。转换成功才写回结构化来源；失败会保留模板当前配置并显示 Host 诊断。把 Digital Link 粘贴到转换器时，默认保留来源 URI 的主机、路径前缀、AI 位置、重复 query 参数和 fragment；只有勾选“应用当前设置覆盖来源 URI”后，才使用编辑器中现有的基址、扩展 query 参数和 fragment。包含非 GS1 query 参数的 Digital Link 转为 Elements 时会拒绝，避免静默丢数据。设计器保存项目或模板 JSON 时保留模式、AI 顺序、静态/动态值、基址、fragment、query 参数和关联开关。GS1 Digital Link QR 以普通 URI 模式编码，不使用 GS1/FNC1 模式。

## 案例与验证

[C# 示例](../samples/Templates/Gs1DigitalLinkSamples.cs)和[JSON 模板](../samples/Templates/gs1-digital-link-template.json)使用同一结构数据，运行样例时比较两者生成的 CPCL 字节。执行：

```powershell
dotnet run --project samples/Templates/Templates.csproj -- artifacts/template-samples
```

浏览器端的 AI 编辑、字段绑定、Digital Link 参数顺序和转换失败保留由 `web/Bing.EasyPrint.Designer/e2e/gs1AiEditor.spec.ts` 覆盖。
