using System;
using System.IO;
using System.Linq;
using SixLabors.Fonts;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Printing;
using System.Threading;

namespace Bing.EasyPrint.Protocols;

/// <summary>调用方提供的固定字体资源；字体字节在构造时复制，不读取系统字体或外部路径。</summary>
public sealed class RasterFontResource
{
    private readonly byte[] _regularData;
    private readonly byte[] _boldData;
    private readonly FontFamily _family;
    /// <summary>模板使用的资源别名，与字体文件内部的字体族名称无关。</summary>
    public string Name { get; }
    /// <summary>调用方声明的资源版本；用于记录来源，不自动验证厂商版本。</summary>
    public string Version { get; }
    /// <summary>是否提供了经过验证的粗体资源。</summary>
    public bool HasBold => _boldData != null;
    /// <summary>常规与粗体原始字体数据的总字节数，不创建副本。</summary>
    public long ByteLength => _regularData.LongLength + (_boldData?.LongLength ?? 0);
    /// <summary>获取常规字体字节的独立副本。</summary>
    public byte[] RegularData => BudgetedBuffer.Copy(_regularData, "Font.RegularSnapshot", CancellationToken.None);
    /// <summary>获取粗体字体字节的独立副本；未提供时返回 null。</summary>
    public byte[] BoldData => _boldData == null ? null : BudgetedBuffer.Copy(_boldData, "Font.BoldSnapshot", CancellationToken.None);

    /// <summary>加载单个常规 TTF/OTF 字体及可选的同族粗体；不接受隐式合成样式。</summary>
    public RasterFontResource(string name, string version, byte[] regularData, byte[] boldData = null)
        : this(name, version, regularData, boldData, CancellationToken.None)
    {
    }

    /// <summary>使用取消令牌加载单个常规 TTF/OTF 字体及可选的同族粗体。</summary>
    public RasterFontResource(string name, string version, byte[] regularData, byte[] boldData,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("字体资源名称不能为空。", nameof(name));
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("字体资源版本不能为空。", nameof(version));
        if (regularData == null || regularData.Length == 0) throw new ArgumentException("常规字体数据不能为空。", nameof(regularData));
        if (boldData != null && boldData.Length == 0) throw new ArgumentException("粗体字体数据不能为空。", nameof(boldData));
        var totalDataBytes = checked(regularData.LongLength + (boldData?.LongLength ?? 0L));
        if (totalDataBytes > RasterFontLimits.MaxTotalDataBytes)
            throw new ArgumentException($"单个字体资源数据不能超过 {RasterFontLimits.MaxTotalDataBytes} 字节。", nameof(regularData));

        cancellationToken.ThrowIfCancellationRequested();
        Name = name;
        Version = version;
        var operation = PrintResourceScope.BeginTemporary();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _regularData = BudgetedBuffer.Copy(regularData, "Font.RegularData", cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _boldData = boldData == null
                ? null
                : BudgetedBuffer.Copy(boldData, "Font.BoldData", cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var collection = new FontCollection();
            cancellationToken.ThrowIfCancellationRequested();
            using (var stream = new MemoryStream(_regularData, false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                _family = collection.Add(stream);
                cancellationToken.ThrowIfCancellationRequested();
            }

            cancellationToken.ThrowIfCancellationRequested();
            var regularStyles = _family.GetAvailableStyles();
            cancellationToken.ThrowIfCancellationRequested();
            if (!regularStyles.Contains(FontStyle.Regular))
                throw new ArgumentException("常规资源必须包含 Regular 样式。", nameof(regularData));

            if (_boldData != null)
            {
                FontFamily boldFamily;
                cancellationToken.ThrowIfCancellationRequested();
                using (var stream = new MemoryStream(_boldData, false))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    boldFamily = collection.Add(stream);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                cancellationToken.ThrowIfCancellationRequested();
                var boldFamilyName = boldFamily.Name;
                cancellationToken.ThrowIfCancellationRequested();
                var boldStyles = boldFamily.GetAvailableStyles();
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.Equals(boldFamilyName, _family.Name, StringComparison.Ordinal) ||
                    !boldStyles.Contains(FontStyle.Bold))
                    throw new ArgumentException("粗体资源必须为同一字体族的 Bold 样式。", nameof(boldData));
            }

            cancellationToken.ThrowIfCancellationRequested();
            operation?.CommitReservations();
        }
        catch (PrintResourceLimitException) { throw; }
        catch (ArgumentException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            throw new ArgumentException($"字体资源 {name} 无法读取为单个 TTF/OTF 字体。", nameof(regularData), error);
        }
        finally
        {
            operation?.Dispose();
        }
    }

    internal bool SupportsStyle(bool bold) => !bold || HasBold;
    internal FontFamily Family => _family;
}
