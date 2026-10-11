using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 TSPL/ZPL 原生二维码旋转白名单的快照、命令和模板边界。</summary>
public sealed class LabelQrCodeRotationProfileTests
{
    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Profile_rotation_allowlist_is_snapshotted_and_exposed(PrintProtocol protocol)
    {
        var rotations = new[] { RotationAngle.Rotate90 };
        var command = CreateCommand(protocol, new[] { RotationAngle.Rotate90 }, rotations);
        var capabilities = protocol == PrintProtocol.Tspl
            ? ((TsplPrintCommand)command).Capabilities
            : ((ZplPrintCommand)command).Capabilities;

        rotations[0] = RotationAngle.Rotate270;

        Assert.Equal(new[] { RotationAngle.Rotate90 }, capabilities.SupportedNativeQrCodeRotations);
        Assert.True(capabilities.SupportsQrCodeRotationAngle(RotationAngle.None));
        Assert.True(capabilities.SupportsQrCodeRotationAngle(RotationAngle.Rotate90));
        Assert.False(capabilities.SupportsQrCodeRotationAngle(RotationAngle.Rotate180));
        Assert.False(capabilities.SupportsQrCodeRotationAngle(RotationAngle.Rotate270));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Direct_native_qr_command_rejects_angle_outside_profile(PrintProtocol protocol)
    {
        var command = CreateCommand(protocol, new[] { RotationAngle.Rotate90 });

        WriteQr(command, protocol, RotationAngle.None);
        WriteQr(command, protocol, RotationAngle.Rotate90);
        Assert.Throws<NotSupportedException>(() => WriteQr(command, protocol, RotationAngle.Rotate180));
        Assert.Throws<NotSupportedException>(() => WriteQr(command, protocol, RotationAngle.Rotate270));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Template_qr_capabilities_and_preflight_follow_profile_allowlist(PrintProtocol protocol)
    {
        var options = CreateOptions(protocol, new[] { RotationAngle.Rotate90 });
        var capabilities = TemplateProtocolCapabilities.Create(options);

        Assert.Equal(new[] { RotationAngle.None, RotationAngle.Rotate90 }, capabilities.QrCodeRotations);
        Assert.True(capabilities.SupportsQrCodeRotation(RotationAngle.Rotate90));
        Assert.False(capabilities.SupportsQrCodeRotation(RotationAngle.Rotate180));

        var qr = new BpQrCodeElement
        {
            UnitWidth = 3,
            Rotation = RotationAngle.Rotate180,
            Field = new BgField { Value = "QR" }
        };
        var template = new BpTemplate
        {
            Width = 100,
            Height = 100,
            Rows = new List<BpRow>
            {
                new() { Height = 100, Cells = new List<BpCell> { new() { Width = 100, Elements = new List<BpElement> { qr } } } }
            }
        };

        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(template, null, options: options));
        Assert.Contains("Rows[0].Cells[0].Elements[0]", error.SourcePath);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Empty_rotation_allowlist_keeps_only_unrotated_qr(PrintProtocol protocol)
    {
        var command = CreateCommand(protocol, Array.Empty<RotationAngle>());
        WriteQr(command, protocol, RotationAngle.None);
        Assert.Throws<NotSupportedException>(() => WriteQr(command, protocol, RotationAngle.Rotate90));

        var capabilities = TemplateProtocolCapabilities.Create(CreateOptions(protocol, Array.Empty<RotationAngle>()));
        Assert.Equal(new[] { RotationAngle.None }, capabilities.QrCodeRotations);
    }

    [Fact]
    public void Label_profile_rejects_none_and_duplicate_qr_rotations()
    {
        Assert.Throws<ArgumentException>(() => CreateCommand(PrintProtocol.Tspl, new[] { RotationAngle.None }));
        Assert.Throws<ArgumentException>(() => CreateCommand(PrintProtocol.Zpl,
            new[] { RotationAngle.Rotate90, RotationAngle.Rotate90 }));
    }

    private static object CreateCommand(PrintProtocol protocol, RotationAngle[] supported,
        RotationAngle[] source = null)
    {
        var profileRotations = source ?? supported;
        if (protocol == PrintProtocol.Tspl)
            return new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
            {
                Profile = new TsplPrinterProfile { SupportedNativeQrCodeRotations = profileRotations }
            });
        return new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile { SupportedNativeQrCodeRotations = profileRotations }
        });
    }

    private static void WriteQr(object command, PrintProtocol protocol, RotationAngle rotation)
    {
        if (protocol == PrintProtocol.Tspl)
            ((TsplPrintCommand)command).QrCode(0, 0, 0, 3, "A", (int)rotation, "QR");
        else
            ((ZplPrintCommand)command).QrCode(0, 0, "QR", 3, 'M', (int)rotation);
    }

    private static TemplateRenderOptions CreateOptions(PrintProtocol protocol, RotationAngle[] rotations)
        => protocol == PrintProtocol.Tspl
            ? new TemplateRenderOptions
            {
                Protocol = protocol,
                Encoding = Encoding.ASCII,
                TsplMedia = new TsplMediaOptions
                {
                    Profile = new TsplPrinterProfile { SupportedNativeQrCodeRotations = rotations }
                }
            }
            : new TemplateRenderOptions
            {
                Protocol = protocol,
                Encoding = Encoding.ASCII,
                ZplMedia = new ZplMediaOptions
                {
                    Profile = new ZplPrinterProfile { SupportedNativeQrCodeRotations = rotations }
                }
            };
}
