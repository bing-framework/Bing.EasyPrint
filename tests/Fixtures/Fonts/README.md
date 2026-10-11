# 固定测试字体

这些未修改的字体取自 [DejaVu 官方 2.37 发行包](https://github.com/dejavu-fonts/dejavu-fonts/releases/tag/version_2_37)：

- `DejaVuSansMono.ttf`：常规字形，340,712 字节。
- `DejaVuSansMono-Bold.ttf`：同族真实粗体，331,992 字节。
- `DejaVuSans.ttf`：整段替代字体，757,076 字节。

中文测试另使用未修改的 `NotoSansCJKsc-Regular.otf`（16,437,364 字节），取自 [Noto CJK 官方仓库](https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf)。本次取得的官方文件 blob 为 `dc15562470b4f842321894787a0d066879ccff8b`，资源已保存在仓库，测试运行时不下载。原始 SIL Open Font License 1.1 保存在 `Noto-LICENSE`；此资源仅提供常规样式，不合成中文粗体。

下载来源为该发行的 `dejavu-fonts-ttf-2.37.zip`，保留归档中的原始 `LICENSE`。许可包含 Bitstream Vera 与 Arev 声明，DejaVu 修改属于公有领域；分发字体时保留版权与许可文本，参见 [官方许可说明](https://dejavu-fonts.github.io/License.html)。这些字体仅用于仓库测试，不随 Core/Templates NuGet 包发布。

C# 测试项目在构建时将本目录链接并复制到输出目录 `Fixtures/Fonts`；显式固定字体测试无需安装系统字体。E2E 的固定字体场景从本目录直接读取，按现有 `npm run test:e2e` 流程执行。固定资源缺失会明确失败，不扫描本机字库，也不下载替代字体。完整套件的系统字体入口要求见下文。

DejaVu 测试资源版本标记为 `dejavu-2.37`；中文资源标记为 `noto-cjk-fixture-2026-10`。Regular/Bold 资源检查、缺字拒绝、整段替代、上传、保存重载及输出字节一致性使用同一套 DejaVu 资源。Mono 与 Sans 的覆盖差异用于验证真实替代行为；中文绘字保留原中英混排样本并使用 Noto 资源。这些资源不替代实际设备字库或复杂文字验收。

普通字形、像素边界、静区回滚与语义字体测试显式传入固定目录。生产代码未显式配置固定字体时仍使用系统字体；专门验证该兼容入口的测试标记为 `FontEnvironment=System`，完整套件还包括未配置字体的消费者集成场景。因此完整测试环境仍应准备能覆盖样本的系统常规、粗体及中文字库，不能仅因复制 fixture 就宣称默认系统字体入口跨机器确定。无字库的隔离环境可先执行固定目录精准测试；完整验收必须执行默认入口和消费者场景，不通过跳过代替。
