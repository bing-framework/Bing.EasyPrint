using System;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class EscPosValidationTests
{
    [Fact]
    public void Initialize_without_columns_preserves_the_original_bytes()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .Initialize()
            .Build()
            .GetBytes();

        Assert.Equal(new byte[] { 0x1B, 0x40 }, bytes);
    }

    [Fact]
    public void Initialize_with_character_columns_rejects_an_unportable_setting_without_pollution()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Initialize();
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.Initialize(48));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void SetPrintAreaWidth_writes_the_standard_gs_w_command()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .Initialize()
            .SetPrintAreaWidth(480)
            .Build()
            .GetBytes();

        Assert.Equal(new byte[] { 0x1B, 0x40, 0x1D, 0x57, 0xE0, 0x01 }, bytes);
    }

    [Fact]
    public void SetPrintAreaWidth_rejects_values_outside_the_16_bit_field_without_pollution()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Initialize();
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.SetPrintAreaWidth(ushort.MaxValue + 1));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData("\0")]
    [InlineData("\u001B")]
    [InlineData("\u0007")]
    [InlineData("\t")]
    public void Text_rejects_control_characters_without_pollution(string value)
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Initialize();
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.Text(value));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void RasterImage_rejects_a_row_byte_count_above_the_16_bit_field_without_pollution()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster).Initialize();
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.RasterImage(ushort.MaxValue * 8 + 1, 1, new byte[0]));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void RasterImage_rejects_a_height_above_the_16_bit_field_without_pollution()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster).Initialize();
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.RasterImage(8, ushort.MaxValue + 1, new byte[0]));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void RasterImage_encodes_the_maximum_16_bit_height_without_truncation()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .Initialize()
            .RasterImage(8, ushort.MaxValue, new byte[ushort.MaxValue])
            .Build()
            .GetBytes();

        Assert.Equal(new byte[] { 0x1D, 0x76, 0x30, 0x00, 0x01, 0x00, 0xFF, 0xFF },
            bytes.Skip(2).Take(8).ToArray());
    }

    [Fact]
    public void RasterImage_rejects_a_payload_length_mismatch_without_pollution()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster).Initialize();
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.RasterImage(8, 2, new byte[] { 0x80 }));

        Assert.Equal(before, command.Build().GetBytes());
    }
}
