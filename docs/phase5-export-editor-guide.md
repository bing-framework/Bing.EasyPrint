# Phase 5 导出与编辑状态指南

本指南说明设计器页面 PDF、码图 SVG、批量可选导出，以及 DesignerProject v2 编辑状态的保存边界。可直接使用 [Phase 5 案例](../samples/Phase5/README.md)。

## Host 能力协商

Host 在响应中返回 X-EasyPrint-Contract-Version 和 X-EasyPrint-Features。页面 PDF 和 SVG 分别由 page-pdf-export、barcode-svg-export 声明；批量接口由 batch-export 声明。直接调用导出端点时，建议发送 X-EasyPrint-Contract-Version: 1 与 X-EasyPrint-Required-Features 指定本次调用所需的能力。

前端会检查响应头中的契约版本和功能标记。旧 Host 缺少相应标记时，设计器会禁用该导出入口并保留项目配置；请升级 Host 后重试，不要把旧服务的 404 或错误响应当作导出文件。

端点：

- POST /api/designer/page-pdf：导出单页或全部打印页 PDF。
- POST /api/designer/element-svg：导出选定条码或码图元素 SVG。
- POST /api/designer/batch：按记录批量输出原有打印结果，并可选择附加 PDF/SVG 文件。

本指南使用 `export-template.json` 中的 400 × 400 dots 页面：标题坐标为 (20,20)，Code 128 坐标为 (20,80) 且 `NarrowModuleWidth=2`；Digital Link QR 坐标为 (20,180) 且 `UnitWidth=2`，与 Code 128 上下分行。SVG 示例选择第二个数组元素 Code 128；QR 是第三个元素。

## 页面 PDF

整页 PDF 仅支持内置 ESC/POS Raster 输出。请求必须同时指定 protocol 为 EscPos、escPosLayoutMode 为 Raster。其他协议、ESC/POS Native 和未声明准确整页共享光栅的自定义协议会被拒绝。Host 根据本次模板布局结果，经 Core 共享的单色绘制路径生成页面；不使用浏览器截图或另一套布局/字体算法。

页面尺寸按每轴光栅点数乘以 72 再除以对应 DPI 转成 PDF point；例如 400 × 400 dots、203 DPI 对应约 141.872 × 141.872 pt，X/Y 分别使用对应 DPI。模板空白边缘保留在整页内；非 8 倍数宽度的行字节补位不会改变页面点宽，元素旋转沿用共享绘制结果。多页按渲染顺序输出。

请求字段使用 Host API 命名：

~~~powershell
$template = Get-Content samples/Phase5/export-template.json -Raw | ConvertFrom-Json
$body = @{
  template = $template
  data = @{
    Code = "P5-20261026-A"
    DigitalLink = "https://id.gs1.org/01/09520123456788/10/LOT-2026"
  }
  protocol = "EscPos"
  escPosLayoutMode = "Raster"
  pageIndex = 0
  copies = 2
  escPosDevice = @{ dpi = 203 }
} | ConvertTo-Json -Depth 100

Invoke-WebRequest -Uri http://127.0.0.1:5187/api/designer/page-pdf -Method Post -ContentType application/json -Headers @{ "X-EasyPrint-Contract-Version" = "1"; "X-EasyPrint-Required-Features" = "page-pdf-export" } -Body $body -OutFile page.pdf
~~~

示例不带 job：省略 pageIndex 时按渲染顺序选择全部页；提供 pageIndex 时从 0 开始选择一个模板页，越界会返回错误。copies 为 2，因此示例页会输出两份。若使用 job，pageIndex 对应来源模板页，PDF 保留该来源页在 job 输出中的页序与份数。请求取消、资源或输出超限、渲染失败时，不会交付部分 PDF。

## 码图 SVG

SVG 端点只接受可由共享软件编码器生成的码图元素，包括已支持的一维条码、QR、DataMatrix、PDF417 和 GS1/Composite 码图；具体制式及组合受当前软件编码能力约束。字体、普通图片及不支持的软件编码能力会明确拒绝。生成内容是矢量路径/矩形几何，不是嵌入位图；viewBox 使用码图 dot 尺寸，width/height 按 X/Y DPI 以 mm 表达物理尺寸，静区和旋转边界沿用共享符号。输出不包含设备 HRI（Human Readable Interpretation）。如需显示码值，请另加绑定同一数据字段的文字元素。

先以相同模板和数据调用 POST /api/designer/preview，再从 pages[pageIndex].elements 读取要导出的元素索引及 sourcePath。索引是当前渲染页的零基索引，sourcePath 可一并传入以校验选择没有过期。以下示例对应案例模板中固定的第二个数组元素，即 Code 128；第三个元素才是 Digital Link QR：

~~~powershell
$template = Get-Content samples/Phase5/export-template.json -Raw | ConvertFrom-Json
$body = @{
  template = $template
  data = @{
    Code = "P5-20261026-A"
    DigitalLink = "https://id.gs1.org/01/09520123456788/10/LOT-2026"
  }
  protocol = "EscPos"
  escPosLayoutMode = "Raster"
  pageIndex = 0
  elementIndex = 1
  sourcePath = "Rows[0].Cells[0].Elements[1]"
} | ConvertTo-Json -Depth 100

Invoke-WebRequest -Uri http://127.0.0.1:5187/api/designer/element-svg -Method Post -ContentType application/json -Headers @{ "X-EasyPrint-Contract-Version" = "1"; "X-EasyPrint-Required-Features" = "barcode-svg-export" } -Body $body -OutFile barcode.svg
~~~

示例的 SVG 索引针对单页、无条件、无重复行的数据。若模板布局变化，应始终使用本次 preview 返回的 pageIndex、elementIndex、sourcePath，而不要缓存旧索引。服务检查 sourcePath 与索引指向的元素是否相同。

## 批量可选导出

批量请求默认不附加 PDF 或 SVG。只有显式提供非空 exportFormats 数组才会增加导出文件；可选值为 pdf、svg。省略或留空时，既有批量输出保持不变。批量 PDF 仍要求 ESC/POS Raster；SVG 仍要求所选条码/码图可软件编码。请求中选择格式时，前端会同时要求 batch-export 与对应的导出特性。

~~~powershell
$template = Get-Content samples/Phase5/export-template.json -Raw | ConvertFrom-Json
$body = @{
  template = $template
  protocol = "EscPos"
  escPosLayoutMode = "Raster"
  records = @(
    @{ index = 0; data = @{ Code = "P5-20261026-A"; DigitalLink = "https://id.gs1.org/01/09520123456788/10/LOT-2026" } },
    @{ index = 1; data = @{ Code = "P5-20261026-B"; DigitalLink = "https://id.gs1.org/01/09520123456788/10/LOT-2027" } }
  )
  recordOffset = 0
  totalRecords = 2
  remainingPages = 1000
  remainingBytes = 33554432
  exportFormats = @("pdf", "svg")
} | ConvertTo-Json -Depth 100

Invoke-WebRequest -Uri http://127.0.0.1:5187/api/designer/batch -Method Post -ContentType application/json -Headers @{ "X-EasyPrint-Contract-Version" = "1"; "X-EasyPrint-Required-Features" = "batch-export,page-pdf-export,barcode-svg-export" } -Body $body -OutFile batch.zip
~~~

每块最多 25 条记录，整批最多 1000 条、1000 页，累计结果预算最多 32 MiB。manifest 会列出输出格式及记录、页、元素、份数和相对文件路径。记录级错误遵循现有批量语义：混合成功/失败时归档状态为 partial，全失败时为 all_failed，并在 manifest.json 和 errors.csv 中报告。请求取消、资源或累计预算拒绝会中止归档，不返回因中断产生的部分 ZIP；客户端也不应在取消后自动下载已缓冲内容。

## DesignerProject v2 与锁

设计器项目是 envelope：format 为 Bing.EasyPrint.DesignerProject、version 为 2，包含 template、outputSettings 和 editorState。editorState.nodeStates 将当前源路径映射到稳定 id 和 locked 标记。路径用于定位当前模板节点；移动/重排会更新路径映射但保留节点 ID，复制会创建新 ID，编组/拆组和撤销/重做维护对应身份与锁，删除会回收节点映射。导入时必须为每个节点提供唯一 ID、有效路径及布尔 locked 值；重复 ID、悬空/缺失路径或错误值会被拒绝。旧版 v1 envelope 和裸模板仍可导入；缺少 editorState 时会为节点生成新身份。父节点锁对子节点生效，树视图仍可明确解锁。

保存项目 v2 会保留节点身份、锁和其他编辑状态。历史快照保留原始业务数据文本（包含无效文本）、批量原文、字段映射、所选记录、导出格式及选择/锁状态。撤销/重做恢复原文后重新解析派生数据和解析状态，并中止旧预览请求后安排新预览。每个已提交编辑动作作为一个撤销单元；历史最多保存 80 个快照，按 32 MiB 历史预算修剪较旧快照。大业务数据采用共享不可变快照，避免每次移动复制整批记录。

单独导出模板只包含打印所需的 template，不包含 editorState，也不会把编辑器 ID、锁或选择状态写进打印模板。案例中的 export-template.json 可作为 Host 模板请求体；project-v2-editor-state.json 是设计器保存/重载用的 v2 项目示例。
