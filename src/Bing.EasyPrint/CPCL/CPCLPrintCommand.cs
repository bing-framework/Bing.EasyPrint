using System.Text;
using System;
using Bing.EasyPrint.Core;

// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 构建 CPCL 打印任务命令。
/// </summary>
// ReSharper disable once InconsistentNaming
public partial class CPCLPrintCommand : IPrintCommand<CPCLPrintCommand>, IPrintComponent<CPCLPrintCommand>,
    IRestrictedRawPrintCommand<CPCLPrintCommand>
{
    /// <summary>
    /// 获取当前构建过程的字节写入器。
    /// </summary>
    internal IBufferWriter Writer { get; private set; }

    /// <summary>
    /// 保存构建 CPCL 命令时使用的文本编码。
    /// </summary>
    private readonly Encoding _encoding;

    /// <summary>
    /// 保存包含 CPCL 边界命令的受限原始程序；该程序构建时必须保持原文顺序。
    /// </summary>
    private string _restrictedRawProgram;

    /// <summary>
    /// 保存受限原始程序的文本预算租约。
    /// </summary>
    private IDisposable _restrictedRawProgramLease;

    private CpclRestrictedFormatState _restrictedFormatState = new CpclRestrictedFormatState();

    /// <summary>
    /// 获取待构建的命令结构。
    /// </summary>
    internal PrintCommandStruct Content { get; }

    /// <summary>
    /// 获取当前打印任务的页面设置。
    /// </summary>
    public CPCLCommandInfo CommandInfo { get; internal set; }

    /// <summary>
    /// 初始化一个 <see cref="CPCLPrintCommand"/> 类型的实例。
    /// </summary>
    public CPCLPrintCommand() : this(System.Text.Encoding.GetEncoding("gbk")) { }

    /// <summary>
    /// 初始化一个 <see cref="CPCLPrintCommand"/> 类型的实例。
    /// </summary>
    /// <param name="encoding">命令文本所用编码。</param>
    public CPCLPrintCommand(Encoding encoding)
    {
        _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        Writer = new BufferWriter(_encoding);
        CommandInfo = new CPCLCommandInfo();
        Content = new PrintCommandStruct(EnsureRestrictedRawProgramCanBeMixed);
    }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <returns>当前打印命令实例。</returns>
    internal CPCLPrintCommand Init(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        _restrictedRawProgramLease?.Dispose();
        _restrictedRawProgramLease = null;
        _restrictedRawProgram = null;
        _restrictedFormatState = new CpclRestrictedFormatState();
        CommandInfo = new CPCLCommandInfo {Width = width, Height = height};
        Writer = new BufferWriter(_encoding);
        Content.Clear();
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand WriteRaw(string raw)
    {
        EnsureRestrictedRawProgramCanBeMixed();
        Content.AddRaw(raw);
        _restrictedFormatState.MarkUnknown();
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand WriteRawLine(string raw)
    {
        EnsureRestrictedRawProgramCanBeMixed();
        Content.AddRawLine(raw);
        TrackRestrictedFormatLine(raw);
        return this;
    }

    private void TrackRestrictedFormatLine(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;
        var fields = raw.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length == 0) return;
        var command = fields[0].ToUpperInvariant();
        try
        {
            switch (command)
            {
                case "SETMAG":
                    if (fields.Length != 3 || !int.TryParse(fields[1], out var horizontal) ||
                        !int.TryParse(fields[2], out var vertical) || horizontal < 0 || horizontal > 16 ||
                        vertical < 0 || vertical > 16)
                        _restrictedFormatState.MagnificationKnown = false;
                    else
                        _restrictedFormatState.SetMagnification(horizontal, vertical);
                    break;
                case "BT":
                    if (fields.Length == 2 && fields[1].Equals("OFF", StringComparison.OrdinalIgnoreCase))
                    {
                        _restrictedFormatState.SetBarcodeText(false, 0, 0);
                        break;
                    }
                    if (fields.Length != 4 || fields[2] != "0" || !int.TryParse(fields[3], out var offset) ||
                        offset < 0 || !TryGetCpclFontCell(fields[1], out var fontCell))
                        _restrictedFormatState.BarcodeTextKnown = false;
                    else
                        _restrictedFormatState.SetBarcodeText(true, fontCell, offset);
                    break;
                case "LEFT":
                    _restrictedFormatState.SetAlignment(CpclFieldAlignment.Left, null);
                    break;
                case "CENTER":
                case "RIGHT":
                    if (fields.Length > 2 || (fields.Length == 2 &&
                        (!int.TryParse(fields[1], out var end) || end < 0)))
                        _restrictedFormatState.AlignmentKnown = false;
                    else
                        _restrictedFormatState.SetAlignment(
                            command == "CENTER" ? CpclFieldAlignment.Center : CpclFieldAlignment.Right,
                            fields.Length == 2 ? int.Parse(fields[1]) : (int?)null);
                    break;
                case "SETSP":
                    if (fields.Length != 2 || !int.TryParse(fields[1], out var spacing) || spacing < 0 || spacing > 999)
                        _restrictedFormatState.SpacingKnown = false;
                    else
                    {
                        _restrictedFormatState.CharacterSpacing = spacing;
                        _restrictedFormatState.SpacingKnown = true;
                    }
                    break;
                case "R":
                    if (fields.Length != 2 || !int.TryParse(fields[1], out var rotation) ||
                        (rotation != 0 && rotation != 90 && rotation != 180 && rotation != 270))
                        _restrictedFormatState.RotationKnown = false;
                    else
                    {
                        CommandInfo.PagerRotate = rotation switch
                        {
                            0 => (int)PrintOrientation.None,
                            90 => (int)PrintOrientation.Left,
                            180 => (int)PrintOrientation.Down,
                            270 => (int)PrintOrientation.Right,
                            _ => CommandInfo.PagerRotate
                        };
                        _restrictedFormatState.RotationKnown = true;
                    }
                    break;
            }
        }
        catch (OverflowException)
        {
            _restrictedFormatState.MarkUnknown();
        }
    }

    private static bool TryGetCpclFontCell(string font, out int cell)
    {
        if (font == "24") { cell = 24; return true; }
        if (font == "0" || font == "55") { cell = 16; return true; }
        cell = 0;
        return false;
    }

    /// <inheritdoc />
    public IBufferWriter Build()
        => Build(System.Threading.CancellationToken.None);

    /// <summary>可取消构建命令；失败或取消时恢复前次 Writer。</summary>
    public IBufferWriter Build(System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var operation = Bing.EasyPrint.Printing.PrintResourceScope.BeginTemporary();
        var previousWriter = Writer;
        var candidate = new BufferWriter(_encoding, cancellationToken);
        Writer = candidate;
        try
        {
            if (_restrictedRawProgram != null)
            {
                if (Content.Begin != null || Content.PageWidth != null || Content.End != null ||
                    Content.Content.Count != 0)
                    throw new InvalidOperationException(
                        "包含完整 CPCL 受限 Raw 程序后不能再追加结构化命令。");
                candidate.Write(_restrictedRawProgram);
                cancellationToken.ThrowIfCancellationRequested();
                operation?.CommitReservations();
                return candidate;
            }

            Content.SafeInit(CommandInfo);
            Content.Build(this);
            cancellationToken.ThrowIfCancellationRequested();
            operation?.CommitReservations();
            return candidate;
        }
        catch
        {
            Writer = previousWriter;
            throw;
        }
    }

    /// <summary>
    /// 阻止包含完整原始程序的受限 Raw 与结构化记录静默拼接。
    /// </summary>
    private void EnsureRestrictedRawProgramCanBeMixed()
    {
        if (_restrictedRawProgram != null)
            throw new InvalidOperationException("包含 CPCL 边界命令的受限 Raw 批次不能继续追加其他命令。");
    }
}
