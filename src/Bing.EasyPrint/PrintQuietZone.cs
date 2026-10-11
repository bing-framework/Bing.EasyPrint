using System;
using System.Collections.Generic;

namespace Bing.EasyPrint;

/// <summary>码图占用区四周保留的空白，单位为旋转前的逻辑打印点。</summary>
/// <remarks>
/// 静区向目标协议条码占用区外扩展，不改变原生命令的数据。ESC/POS Raster Code 49
/// 的占用区包含固有左右静区和顶/底边界分隔条，其坐标指向完整符号外框。
/// </remarks>
public sealed class PrintQuietZone
{
    /// <summary>获取旋转前左侧静区点数。</summary>
    public int Left { get; }
    /// <summary>获取旋转前上侧静区点数。</summary>
    public int Top { get; }
    /// <summary>获取旋转前右侧静区点数。</summary>
    public int Right { get; }
    /// <summary>获取旋转前下侧静区点数。</summary>
    public int Bottom { get; }

    /// <summary>创建四边静区；各边必须为非负整数打印点。</summary>
    public PrintQuietZone(int left = 0, int top = 0, int right = 0, int bottom = 0)
    {
        if (left < 0 || top < 0 || right < 0 || bottom < 0)
            throw new ArgumentOutOfRangeException(nameof(left), "静区必须为非负整数打印点。");
        Left = left; Top = top; Right = right; Bottom = bottom;
    }

    /// <summary>创建四周相同的静区；零表示显式不保留静区。</summary>
    public static PrintQuietZone Uniform(int dots) => new PrintQuietZone(dots, dots, dots, dots);

    /// <summary>判断四边是否均为零；显式零值不添加静区。</summary>
    public bool IsEmpty() => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;

    /// <summary>按顺时针旋转换算各边空白。</summary>
    public PrintQuietZone Rotate(RotationAngle rotation)
    {
        switch (rotation)
        {
            case RotationAngle.None: return this;
            case RotationAngle.Rotate90: return new PrintQuietZone(Bottom, Left, Top, Right);
            case RotationAngle.Rotate180: return new PrintQuietZone(Right, Bottom, Left, Top);
            case RotationAngle.Rotate270: return new PrintQuietZone(Top, Right, Bottom, Left);
            default: throw new ArgumentOutOfRangeException(nameof(rotation));
        }
    }

    internal IReadOnlyList<QuietZoneArea> Areas(long x, long y, long width, long height)
    {
        var areas = new List<QuietZoneArea>(4);
        if (Left > 0) areas.Add(new QuietZoneArea(x - Left, y - Top, Left, height + Top + Bottom));
        if (Right > 0) areas.Add(new QuietZoneArea(x + width, y - Top, Right, height + Top + Bottom));
        if (Top > 0) areas.Add(new QuietZoneArea(x, y - Top, width, Top));
        if (Bottom > 0) areas.Add(new QuietZoneArea(x, y + height, width, Bottom));
        return areas;
    }
}

internal readonly struct QuietZoneArea
{
    internal long X { get; }
    internal long Y { get; }
    internal long Width { get; }
    internal long Height { get; }
    internal long Right => X + Width;
    internal long Bottom => Y + Height;
    internal QuietZoneArea(long x, long y, long width, long height)
    { X = x; Y = y; Width = width; Height = height; }
    internal bool Contains(long x, long y) => x >= X && x < Right && y >= Y && y < Bottom;
    internal bool Intersects(QuietZoneArea other) => Width > 0 && Height > 0 && other.Width > 0 && other.Height > 0 &&
        X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
}
