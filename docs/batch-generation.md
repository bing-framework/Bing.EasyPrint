# 批量生成

JSONL 的解析和非对象错误使用源文件物理行号定位（包括空白行，支持 LF/CRLF）；跳过空行后，成功记录的 `index` 仍按有效记录从 0 连续编号，用于稳定的结果目录及 manifest 对应。

批量生成面向一次最多 1000 条独立记录，使用无持久化队列的 HTTP 分块接口。客户端每块最多 25 条，按顺序提交；服务端不保存跨请求状态。

## 输入与 UI

设计器“批量”面板支持 UTF-8：

- CSV：默认保留字符串和前导零；支持 BOM、引号转义、嵌入换行、逗号和分号/制表符分隔；重复表头、空表头、列数不一致、引号未闭合明确失败。含明确字段分隔符的全空记录按原始序号保留；纯空物理行跳过。
- JSON 对象数组：顶层必须是对象数组；标量、数组或 null 条目拒绝；高精度数字保持原值。
- JSONL：每个非空行必须是独立 JSON 对象。

CSV 可逐列编辑字段映射，映射目标使用点号路径（例如 `Product.Code`）。映射不转换类型；字段格式在服务端绑定后执行。记录序号保持原始顺序。字段路径中的 `__proto__`、`prototype`、`constructor` 被拒绝，客户端和服务端都不写入对象原型。

UI 提供记录选择、份数字段、逐条真实 Host 预览、进度、取消和失败筛选。份数字段可选；未选择时使用输出设置的份数。份数字段只接受严格整数，`1.6`、布尔值、空值和溢出均明确失败。批量接口明确不接受旧 `job` 参数，避免份数与任务策略静默相乘。

一次运行会完整复制模板、协议配置、资源和记录数据为独立快照；取消会使该次运行 ID 失效，旧请求的迟到响应不能恢复结果或触发下载。运行期间禁用批量面板控件，模板或配置变化不会混入后续分块。

## 限制

| 项目 | 限制 |
| --- | --- |
| 单块记录 | 25 |
| 整批记录 | 1000 |
| 实际物理页 | 1000（含份数展开） |
| 累计结果 | 32 MiB |
| 单条资源 | 继续遵守模板/Host 单条限制 |

`remainingBytes` 统一计量“协议原始输出 + UTF-8 `manifest.json` + `errors.csv`”；响应头与 `manifest.json.remainingBytes` 始终使用同一数值。服务端在生成元数据前计算实际 UTF-8 字节数和 JSON 元数据长度；JSON 计数 writer 的初始和扩容数组按生命周期预留/释放，CSV 直接写入预分配字节数组，不再构造额外 StringBuilder/UTF-16 副本；达到页数或字节限制时明确终止，不提高预算、不返回截断 ZIP。最终 ZIP 大小另行校验。

客户端 `mergeBatchArchives` 的 `maxBytes` 是同一 32 MiB 产品限制下的**输出与显式工作缓冲联合门禁**：已持有的输入 ZIP、解压临时数组、解码后的 manifest 模型、页面字节、最终 manifest/errors UTF-8 字节、以及 JSZip/pako Deflate state 的 267,160 字节、64 KiB 输出块、累计压缩输出块和最终 Blob 拼接副本都计入。合并前分别无大结果分配地计算 JSON/CSV 的 UTF-16 字符串长度、UTF-8 输出长度和 CSV 构造副本，再生成文本；不使用 `generateAsync` 的隐式 accumulate/concat，而用 `generateInternalStream` 有界收集输出。最终结果还单独检查 `byteLength + metadata` 和压缩后 ZIP 大小。由于工作缓冲也受限，接近 32 MiB 的输出可能先命中工作空间门禁；这不是提高或放宽产品结果上限。

输入解压不使用 `zipObject.async` 的 `internalStream(...).accumulate`。页面和 manifest 通过 `internalStream('uint8array')` 直接写入按声明未压缩大小预分配的目标数组，不再保留解压分块后额外 concat 一份完整副本；每个条目在分配前计入目标数组、pako Inflate state（12,768 字节）、64 KiB 输出块、仍存活的输入 ZIP/arrayBuffer 和已有累计工作字节。manifest 额外计入目标字节、UTF-16 解码字符串和 JSON 模型上界；多条记录按顺序累计。取消或解压失败会丢弃目标数组，不发布 ZIP。

独立边界探针明确区分负载类型：31.5 MiB 高压缩率原始内容在默认 32 MiB 下成功，但其 ZIP 仅约 33 KB，不能称为近 32 MiB ZIP 成功；31.99/32 MiB 高压缩率负载由工作空间门禁拒绝；31 MiB 不可压缩随机内容生成的输入 ZIP 约 32.5 MiB，在默认 32 MiB 下由输入/解压工作空间门禁拒绝。1 MiB 单页在 1.5 MiB 工作额度下成功，捕获到的 >=1 MiB Uint8Array 分配只有一个预分配目标数组，不再出现 async accumulate 的 concat 副本。输出上限仍保持 32 MiB，未通过伪造 `Blob.size` 放宽。

## HTTP 契约

```json
{
  "template": { "Version": 2, "Width": 240, "Height": 80, "Rows": [] },
  "data": {},
  "records": [
    { "index": 0, "data": { "Name": "Tea", "Copies": 1 } },
    { "index": 1, "data": { "Name": "Coffee", "Copies": 2 } }
  ],
  "recordOffset": 0,
  "totalRecords": 2,
  "copiesField": "Copies",
  "remainingPages": 1000,
  "remainingBytes": 33554432
}
```

- `records` 中 `data` 必须是对象；`index` 为稳定的原始记录序号，必须唯一且在 0–999 内。
- `copies` 可与记录级 `copies` 或 `copiesField` 二选一；大于 1 的基础 `copies` 与 `copiesField` 冲突。基础 `copies` 是整次请求的不可变默认值，不会成为下一条记录的累计值；记录级份数只作用于当前记录。
- `recordOffset`/`totalRecords` 用于块级边界校验；`remainingPages`/`remainingBytes` 是**整批剩余额度**，服务端校验范围并在响应头返回实际剩余值。`X-EasyPrint-Batch-Remaining-Bytes` 与 `manifest.json.remainingBytes` 使用同一“原始输出 + 元数据 UTF-8 字节”口径；`X-EasyPrint-Batch-Bytes` 只报告协议原始输出。
- 协议、资源、份数、格式、模板和与单条请求相同的输出字段都在同一请求中传入；`protocolId`/`protocolConfig` 可继续使用 O4 注册协议。
- 旧 `job` 请求返回显式错误；请使用 `copies` 或 `copiesField`。

响应为 `application/zip`，并返回以下响应头：

```text
X-EasyPrint-Batch-Records
X-EasyPrint-Batch-Pages
X-EasyPrint-Batch-Bytes
X-EasyPrint-Batch-Failed
X-EasyPrint-Batch-Remaining-Pages
X-EasyPrint-Batch-Remaining-Bytes
```

ZIP 结构：

```text
manifest.json
errors.csv
records/000000/page-0001.cpcl
records/000000/page-0001-002.cpcl
records/000000/finish-0001.demo-protocol
records/000001/page-0001.cpcl
```

记录目录使用稳定六位序号。每页保留原有 segment 顺序；独立收尾控制文件使用独立条目，不能拼入页面文件。PageEnd 文件与页面输出按实际物理副本顺序写入 `manifest.json.files`。

`manifest.json` 的 `status` 为：

- `completed`：没有失败；
- `partial`：至少一条成功、至少一条失败；
- `all_failed`：全部失败；只有错误报告，不表示生成成功。

`errors.csv` 包含原始记录序号、错误码、阶段、字段路径、元素路径和消息；单元格按 CSV 引用规则处理，并以单引号前缀转义公式起始字符。

## 客户端行为

客户端按 25 条分块提交，累计服务端返回的剩余页/字节额度，再用 JSZip 合并分块 ZIP、重建 `manifest.json` 和 `errors.csv`，最后才提供下载。

取消会中止在途请求、清空结果并不自动下载。内部异常、取消或整批资源超限终止任务，沿异常链识别，不会把包装后的取消/资源错误降级为记录错误；输入、绑定、编码错误按记录汇总。部分成功显示“部分完成”，全部失败下载错误报告。

旧 Host 未声明 `batch-export` 时，批量请求明确失败。

## 示例与验证

- `samples/Templates/field-format-template.json`
- `samples/Templates/batch-records.json`、`batch-records.csv`
- `samples/BatchConsumer`：独立业务调用方示例，按 25 条累计响应头、合并 chunk ZIP 并重建 manifest/errors。
- `web/Bing.EasyPrint.Designer/e2e/fieldBatch.spec.ts`
- `tests/DesignerHost.Tests/BatchHostTests.cs`
- `web/Bing.EasyPrint.Designer/src/batch.test.ts`

独立运行：

```powershell
dotnet run --project samples/BatchConsumer -- `
  http://127.0.0.1:5187 `
  samples/Templates/field-format-template.json `
  samples/Templates/batch-records.json `
  artifacts/business-batch.zip
```

当前只导出已有协议页面/ZIP；PDF、SVG 和持久化任务队列不在 F2 范围。
