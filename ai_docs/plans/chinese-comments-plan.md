# `src/*` 中文 XML 注释补全计划

## 扫描范围与基线

按 `chinese-comments` 技能扫描两个类库。排除 `bin`、`obj`、`*.g.cs`、`*.generated.cs`、`*.Designer.cs`、EF Core Migration/ModelSnapshot、自动生成客户端、代理代码和第三方源码；不涉及测试、工具和设计器。语法树按声明计数，字段按变量计数，partial 类型按声明计数，不含隐式构造函数、访问器和本地函数。

| 项目 | 文件 | 声明 | 缺少摘要或继承注释 |
|---|---:|---:|---:|
| `Bing.EasyPrint` | 47 | 477 | 36 |
| `Bing.EasyPrint.Templates` | 41 | 381 | 197 |
| 合计 | 88 | 858 | 233 |

| 类别 | 总数 | 缺失 |
|---|---:|---:|
| 类、接口、结构体、枚举 | 102 | 13 |
| 构造函数 | 29 | 15 |
| 方法 | 330 | 99 |
| 属性 | 234 | 48 |
| 字段 | 61 | 58 |
| 枚举成员 | 102 | 0 |

方法缺失分布：public 25/243、internal 15/16、private 59/71；static 56/88，与访问级别统计重叠。100 处单行摘要需改三行；186 个已有摘要的属性缺少访问语义前缀；14 个已有构造函数摘要不符指定句式。20 个 override 均无 `inheritdoc`（12 个重复独立摘要、8 个缺注释）。已有注释中 7 项声明缺少部分参数标签、1 项缺少泛型参数标签、201 项方法缺少返回标签；另有 3 个空返回标签和 1 个空参数标签。问题统计有交叠，不能相加为独立问题总数。

## 分批实施

严格按顺序处理；同一文件可跨批次参与，但每批只改对应类别。

1. **接口、抽象类和公共契约**：核心项目根目录 `I*.cs`、`Bing/EasyPrint/IPrintValue*.cs`、`CPCL/Metadata`，模板项目 `Rendering`/`Labels` 公共入口。先明确单位、输入约束、返回对象、资源所有权和错误契约，作为实现成员的上游文档。
2. **DTO、Entity、Options 和枚举**：模板 `Model`、`Elements`、价签 DTO、结果与配置，核心 `Enums`、图片选项、命令信息。补默认值、单位、空值和版本语义；枚举成员复核准确性。当前没有独立 Entity 层，不新增类型。
3. **构造函数和属性文案**：两个项目全部显式构造函数与属性。构造函数统一为“初始化一个 `<see cref="当前类型"/>` 类型的实例。”；属性按可见访问器使用“获取……”“设置……”“获取或设置……”或“获取或初始化……”。受限 setter 说明放入 remarks。
4. **实现、重写与重载**：`DefaultEasyPrint`、`Core`、`CPCLPrintCommand*.cs`、`CPCL/Components`、`CPCL/Metadata` 和模板加载器内部实现。确认上游文档有效后使用 `<inheritdoc />`；重载核心用途一致，差异放在参数或 remarks。
5. **private、internal、static 方法**：模板 `Rendering`/`Labels`、核心 `CPCL` Helper/Validator/Binary、Components、Core 与 Extensions。描述分页、度量、绑定、图片和资源计数的真实语义；非 void/无结果 Task 方法补充有效返回说明。
6. **字段、常量、缓存键和配置键**：重点核对渲染会话、`Binding`、`QrMetrics`、`CPCLInputValidator`、`TemplatePage`、图片组件和缓冲区。说明作用范围、生命周期、键组成和容量；局部常量不强制 XML 注释。
7. **最终审计与构建**：检查所有入选声明、三行摘要、继承来源、参数/泛型/返回标签、XML/cref、重载一致性；验证两个项目五个目标框架，并确认 C# 非注释 token 未变。

## 批次状态（2026-09-28 本轮）

| 批次 | 状态 | 本轮处理范围 |
|---|---|---|
| 1. 接口、抽象类和公共契约 | 已完成本轮复核与修订 | 修正 `IPrintComponent<T>` 的页面单位、链式返回、文本区域零值约束、图片资源所有权及本地路径、份数和偏移边界等契约文案；实现成员继续继承接口文档。 |
| 2–7 | 本轮未执行 | 保留下方既有实施审计记录；本轮未重新确认其完成状态。 |

后续批次待核对项：`RawMetadata` 构造函数的命令文本与换行参数说明归入批次 3；`MetadataType` 枚举及成员的类别说明归入批次 2。本轮不修改这两处。

本轮修订 1 个接口及其 15 个方法的 XML 文档，新增 `<inheritdoc />` 0 处。核心项目五个目标框架的 Release 重建均通过，XML 文档诊断为 0；`git diff --check` 通过。本轮未修改 C# 非注释语句或成员签名，未运行真实打印。

## 已确认的文案与继承问题

- `IBufferWriter`、`IPrintComponent` 的 fluent 返回值和部分参数说明不完整；先完善接口，再让实现继承。
- `CPCLPrintCommand` 和组件 `Build` 重复接口或基类的摘要、参数，需改为有效的 `inheritdoc`。
- `TemplateRenderResult` 中资源提供器的流所有权约束、`TemplateRenderOptions.Encoding` 的编码注册条件应放入成员契约或 remarks；属性摘要保持简洁。
- `CPCLPrintCommand.Graphics` 的 `VCompressedGraphics` 方向文案需按生成的命令复核；`ImageComponent` 存在“数”等残缺参数文案。
- Graphics/Text 协议说明有重复、断词与空标签；方法摘要只保留核心用途，调用约束下沉到 remarks。
- 不能从代码确认的设备协议或业务规则作为待确认项记录，不编造约束。

## 风险与验证

只编辑注释和本计划，不改变业务逻辑、签名、可见性、默认值、依赖、版本、JSON 格式或目标框架。工作区已有大量改动，不能回退或归因于本任务。`common.props` 屏蔽 `CS1591`，常规构建不能证明注释完整；必须结合语法树审计。项目默认生成源码目录 XML，验证时重定向到 `artifacts/comment-audit`。注释任务复用已有功能测试和性能证据，不提交真实打印任务。

在仓库根目录运行以下 PowerShell；先构建核心项目，再构建模板项目：

```powershell
$auditRoot = Join-Path $PWD 'artifacts/comment-audit'
New-Item -ItemType Directory -Force -Path $auditRoot | Out-Null
$projects = @('Bing.EasyPrint', 'Bing.EasyPrint.Templates')
$frameworks = @('netstandard2.0', 'netstandard2.1', 'netcoreapp3.1', 'net6.0', 'net7.0')
foreach ($project in $projects) {
    foreach ($framework in $frameworks) {
        $xmlPath = Join-Path $auditRoot "$project-$framework.xml"
        dotnet build "src/$project/$project.csproj" -t:Rebuild -c Release -f $framework --no-restore `
            "-p:TargetFrameworks=$framework" -p:BuildProjectReferences=false `
            -p:UseSharedCompilation=false -p:NoWarn=NETSDK1138 `
            "-p:DocumentationFile=$xmlPath" `
            '-warnaserror:CS1570,CS1572,CS1573,CS1580,CS1581,CS1584,CS1591,CS1658,CS1710,CS1711,CS1712,CS1734,CS1735'
        if ($LASTEXITCODE -ne 0) { throw "$project / $framework 验证失败。" }
    }
}
git diff --check
```

验收：全部入选声明具备准确的独立注释或有效继承来源；独立摘要为三行；构造函数、属性、参数、泛型和返回文案符合技能规范；无空标签、无错误 cref、无 XML 诊断；同语义重载保持一致；五目标框架构建通过；实施前后 C# 非注释 token 一致。不自动提交、推送或发布。

## 实施审计记录（2026-09-28）

- 已补全两类库范围内的中文 XML 注释，接口实现和重写成员采用有效的 `<inheritdoc />`；修正构造函数、属性、返回值、参数、字段、协议方向及残缺文案。
- `artifacts/comment-audit/CommentAudit.csproj` 对 88 个文件、858 项声明执行语法树审计：缺少摘要或继承注释 0、单行摘要/构造函数/属性文案问题 0、缺失返回标签 0、未继承的 override 文档 0、参数及泛型参数标签不匹配 0。另查空 `<param>`/`<returns>`/`<typeparam>` 标签，结果为 0。
- 核心与模板项目的 netstandard2.0、netstandard2.1、netcoreapp3.1、net6.0、net7.0 共十次 Release 重建均通过，启用 XML 文档诊断升级为错误；各框架生成的 XML 保存在 `artifacts/comment-audit`。`git diff --check` 通过。
- 本机 MSBuild 直接解析引用项目的完整多目标框架列表会无诊断地失败；使用命令中的 `TargetFrameworks=$framework` 单框架覆盖后逐一验证成功。此参数仅用于构建验证，未修改项目目标框架。
- 额外执行的 net6.0 精准测试在恢复阶段后持续无结果，已停止；本轮注释工作不以该次运行作为通过证据。既有自动测试与性能证据不因注释变更重复生成，真实打印未执行。
- 初始工作区含大量未提交功能改动，实施前未保存完整的机器可比较 C# token 快照。因此无法声称对初始状态完成逐文件 token 哈希证明；已核对注释改动及组件恢复历史，未有意改变业务语句或公开签名。严格的实施前后 token 相等验收项记录为未独立验证。
- 未提交、推送或发布。
