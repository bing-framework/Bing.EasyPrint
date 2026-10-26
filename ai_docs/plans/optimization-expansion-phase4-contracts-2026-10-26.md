# 第四阶段 F3/F4 跨层契约与验收

日期：2026-10-26。前置F2已由主代理收口，总体6/10；本阶段实现原计划GTIN Digital Link和可视化AI编辑器，不进入F5/F6。初始代码/测试执行器为OpenCode/deepseek-v4.1-flash；因重复API拒绝，按用户2026-10-26授权改由Codex/gpt-6-luna/max续作。主代理负责架构与独立复核，luna_worker负责窄证据核对。最多三轮集成修正，连续两轮无进展停止；连接失败不计为已完成的集成修正。

## 固定来源与兼容

以 [GS1 URI Syntax 1.7.0](https://ref.gs1.org/standards/digital-link/uri-syntax/1.7.0/) 为规范依据，测试保存来源章节/向量。主代理通过官方搜索索引核实版本和关键约束；直接页面读取曾超时，执行方需读取完整规范后实现，不能仅凭搜索摘要宣称覆盖全部规范。

AI定义沿用仓库已固定的Syntax Dictionary提交d1702ffb2fa5060f8460f7d581b01e377deaa1d6；不更新字典、不复制一份前端字典。现有内部 `Gs1SyntaxDictionary.AllDefinitions`、`Gs1SyntaxAiDefinition.Attributes/Components/RequirementRules/ExclusionRules` 是复用点；公开只读DTO，不把内部可变集合直接暴露给调用者。

增量API保持生产五TFM、旧签名/枚举值/HTTP路由/模板默认行为及产品版本。公共API中文XML注释；API基线不自动覆盖。旧括号解析器及抛异常入口继续兼容。

## Core：URI与元数据

- 独立构建、解析、规范化API及只读数据结果；只接受未压缩GTIN主键01，输出14位GTIN并校验校验位；输入补零策略明确，不丢前导零。其他主键和压缩形式明确诊断。
- 构建显式HTTPS基址，解析纯本地，不访问resolver或任何URL。基址前缀、路径、转义和query处理按规范实现；不得依赖System.Uri自动解码后的路径去猜分隔符。
- 限定符分支来自固定字典 `dlpkey=22,10,21|235`：每分支可省略可选项但顺序有效，235不得与前分支混合；不能硬编码只支持22/10/21。输入顺序非法给具体诊断，构建时如何按声明顺序输出须有一致契约。
- query中GS1属性严格校验；未知全数字key不能当非GS1参数放行。非GS1参数用有序键值项保存，保留重复键和值，不用Dictionary静默覆盖；解码一次、合法百分号/UTF8、加号语义依规范。fragment及基址前缀保留或明确拒绝，不静默丢失。
- 本产品Normalize保持调用者域名和非GS1参数语义，不冒充规范中会选择id.gs1.org的canonical URI；不因规范化丢业务信息。测试规范化幂等、parse/build语义往返。
- 逐字段及URI结构始终验证；完整req/ex仅在现有显式关联校验开启时执行。核心新增结构化诊断带AI、类别、关联/冲突项、项索引，旧异常入口仍抛原契约类型。
- 元数据目录包含AI、标题、长度、字符组件、分隔要求、版本/commit、关联规则与Digital Link角色/分支；由实际固定字典生成，涵盖展开后的全部AI。

## 模板与绑定

为共享字段新增可选结构化GS1来源（建议 `BgField.Gs1Input` 及独立模型，最终名称在实施报告列明）；每项为AI + 静态字符串或字段路径，恰选一个，允许明确空字符串后交字典诊断。不得复用数字类型表示GTIN。结构化来源与旧Name/Value/ValueFunc互斥；缺省属性不改变旧JSON/绑定行为。

结构化来源区分Elements与DigitalLink输出。Elements生成现有括号元素串并进入已有GS1编码路径，不另写编码器。DigitalLink结果只进入普通QR或DataMatrix路径，要求Gs1=false；PDF417/Composite明确拒绝DigitalLink。文本等不相关元素若不支持结构化来源必须明确拒绝，不能忽略配置。Elements与各元素原有GS1能力和主/次数据角色保持一致。

绑定复用现有路径解析及批量/重复节点上下文；AI项使用原始绑定值并保持数字词法/日期行为的既有约定，必要格式使用明确配置，不能让F1在整条URI上静默二次转换。绑定失败携带元素/字段/AI路径，限制和取消沿现有渲染预算。预览、渲染、PNG、批量采用同一来源解析，不各自实现一套。

## Host与设计器

- 新增版本化目录和验证/转换端点，建议GET `/api/designer/gs1-catalog`、POST `/api/designer/gs1-validate`、POST `/api/designer/gs1-convert`；沿现有执行/结构错误/预算机制，不增加外部传输。特性建议 `gs1-ai-catalog`、`gs1-structured-input`、`gs1-digital-link`，旧端点仍可用。
- 验证使用真实绑定数据与服务端权威规则。没有动态数据时返回待数据验证，静态配置可先诊断；渲染时缺字段仍报错，不能把待验证作为合法结果。
- AI编辑器提供搜索/说明/约束、静态或字段绑定、添加/排序/删除；以Host目录为唯一来源。支持结构化、括号串、Digital Link转换，失败保留原输入并定位具体项。转换包含非GS1参数时保留；目标格式无法表达时明确失败，不静默删除。
- 绑定数据变化重新验证，保留既有去抖/取消/旧请求失效行为。旧Host缺特性时禁用并明确原因，保存未知配置，不降级成普通字符串渲染。
- 项目v2/模板JSON往返保持来源模式、AI顺序、静态/绑定值、基址、扩展query和关联设置；读取旧v1/裸模板继续兼容。设计器渲染后的二维码须能独立解码，证明普通URI模式与FNC1不同。

## 验收与文档

先最小测试，再受影响项目，阶段收口一次完整门禁：

1. 官方固定向量及固定字典分支：GTIN校验、01/22/10/21、01/235、混合分支拒绝、重复AI、错误顺序、query属性/扩展参数、转义与往返；覆盖所有元数据条目，验证只读集合不能修改内部字典。
2. 模板静态/动态绑定、缺失字段、取消/限制、混合输入拒绝、DigitalLink的QR/DataMatrix普通编码与PDF417/Composite拒绝、旧输出回归；req/ex开关与结构化诊断。
3. 真实Host目录/验证/转换、浏览器AI搜索编辑、绑定待验证到有效/失败、保存重载、转换失败原文保留、旧Host降级、批量至少一个有效/无效混合记录。
4. Core/Templates五框架严格XML构建、真实net6/netcoreapp3.1及net10核心测试、Host、前端类型/生产构建/Vitest和完整Chromium（workers1/retries0）、模板及候选消费。先L1后阶段门禁，首败独立保存，不因单次失败盲重跑全量。
5. C#/JSON/浏览器案例，README受影响导航、开发手册、模板/设计器指南、GS1与Digital Link契约说明。新结果独立记录、历史不改写。

证据根 `artifacts/optimization-expansion-20261025/phase4/`；执行方报告 `docs/optimization-expansion-phase4-2026-10-26.md`。不发布/提交/推送、不改版本，不顺带增加压缩URI、resolver服务或其他主键。
