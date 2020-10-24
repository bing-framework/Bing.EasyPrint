using System;
using System.Text.RegularExpressions;

namespace Bing.EasyPrint.CPCL
{
    /// <summary>
    /// 命令生成辅助操作
    /// </summary>
    internal static class Helper
    {
        /// <summary>
        /// 获取文本旋转命令
        /// </summary>
        /// <param name="rotate">旋转角度</param>
        public static string GetTextRotateCommand(int rotate)
        {
            switch (rotate)
            {
                case 0:
                    return "T";
                case 90:
                    return "T90";
                case 180:
                    return "T180";
                case 270:
                    return "T270";
                default:
                    return "T";
            }
        }

        /// <summary>
        /// 计算字体大小
        /// </summary>
        /// <param name="fontSize">字体大小</param>
        public static (int font, int size, int scale) ComputeFontSize(int fontSize)
        {
            switch (fontSize)
            {
                case 16:
                    return (55, 0, 1);
                case 24:
                    return (24, 0, 1);
                case 32:
                    return (55, 0, 2);
                case 48:
                    return (24, 0, 2);
                case 64:
                    return (55, 0, 4);
                case 72:
                    return (24, 0, 3);
                case 80:
                    return (55, 0, 5);
                case 96:
                    return (24, 0, 4);
                case 112:
                    return (55, 0, 7);
                case 120:
                    return (24, 0, 5);
                case 128:
                    return (55, 0, 8);
                case 144:
                    return (24, 0, 6);
                case 160:
                    return (55, 0, 10);
                case 168:
                    return (24, 0, 7);
                case 176:
                    return (55, 0, 11);
                case 192:
                    return (24, 0, 8);
                case 208:
                    return (55, 0, 13);
                case 216:
                    return (24, 0, 9);
                case 224:
                    return (55, 0, 14);
                case 240:
                    return (24, 0, 10);
                case 256:
                    return (55, 0, 16);
                case 264:
                    return (24, 0, 11);
                case 288:
                    return (24, 0, 12);
                case 312:
                    return (24, 0, 13);
                case 336:
                    return (24, 0, 14);
                case 360:
                    return (24, 0, 15);
                case 384:
                    return (24, 0, 16);
                default:
                    return (55, 0, 1);
            }
        }

        /// <summary>
        /// 转换样式
        /// </summary>
        /// <param name="style">样式</param>
        public static (bool bold, bool underline) ConvertStyle(int style)
        {
            switch (style)
            {
                case 1:
                case 3:
                    return (true, false);
                case 4:
                case 6:
                    return (false, true);
                case 5:
                case 7:
                    return (true, true);
                default:
                    return (false, false);
            }
        }

        /// <summary>
        /// 获取条码旋转命令
        /// </summary>
        /// <param name="rotate">旋转角度</param>
        public static string GetBarcodeRotateCommand(int rotate)
        {
            switch (rotate)
            {
                case 0:
                case 180:
                    return "B";
                case 90:
                case 270:
                    return "VB";
                default:
                    return "B";
            }
        }

        /// <summary>
        /// 获取条码起始坐标
        /// </summary>
        /// <param name="item">条码明细</param>
        public static (int x, int y) GetBarcodeCoordinate(Barcode1DComponent item) => GetBarcodeCoordinate(item.Rotate, item.X, item.Y, 0, item.Height);

        /// <summary>
        /// 获取条码起始坐标
        /// </summary>
        /// <param name="item">二维码明细</param>
        public static (int x, int y) GetBarcodeCoordinate(QRCodeComponent item) => GetBarcodeCoordinate(item.Rotate, item.X, item.Y, 0, 0);

        /// <summary>
        /// 获取条码起始坐标
        /// </summary>
        /// <param name="rotate">旋转角度</param>
        /// <param name="x">x轴起始坐标</param>
        /// <param name="y">y轴起始坐标</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        public static (int x, int y) GetBarcodeCoordinate(int rotate, int x, int y, int width, int height)
        {
            switch (rotate)
            {
                case 180:
                    x -= width;
                    y -= height;
                    break;
                case 270:
                    x -= height;
                    y += width;
                    break;
            }
            return (x, y);
        }

        /// <summary>
        /// 转换二维码纠错级别
        /// </summary>
        /// <param name="level">二维码纠错级别</param>
        public static string ConvertErrorLevel(int level)
        {
            switch (level)
            {
                case 0:
                    return "L";
                case 1:
                    return "M";
                case 2:
                    return "Q";
                default:
                    return "H";
            }
        }

        /// <summary>
        /// 是否中文字符串
        /// </summary>
        /// <param name="value">值</param>
        public static bool IsChinese(char value) => Regex.IsMatch(value.ToString(), "^[一-龥]$");

        /// <summary>
        /// 转换条码类型
        /// </summary>
        /// <param name="type">条码类型</param>
        public static string ConvertBarcodeType(BarcodeType type)
        {
            switch (type)
            {
                case BarcodeType.Code128:
                    return "128";
                case BarcodeType.Code39:
                    return "39";
                case BarcodeType.Code93:
                    return "93";
                case BarcodeType.Codabar:
                    return "CODABAR";
                case BarcodeType.Ean8:
                    return "EAN8";
                case BarcodeType.Ean13:
                    return "EAN13";
                case BarcodeType.UpcA:
                    return "UPCA";
                case BarcodeType.UpcE:
                    return "UPCE";
                case BarcodeType.I2OF5:
                    return "I2OF5";
                case BarcodeType.I2OF5C:
                    return "I2OF5C";
                case BarcodeType.I2OF5G:
                    return "I20F5G";
                case BarcodeType.UccEan128:
                    return "UCCEAN128";
                case BarcodeType.Msi:
                    return "MSI";
                case BarcodeType.Postnet:
                    return "POSTNET";
                case BarcodeType.Fim:
                    return "FIM";
            }
            throw new NotImplementedException($"未实现条码类型 {type.ToString()} ");
        }

        /// <summary>
        /// 获取图片旋转命令
        /// </summary>
        /// <param name="rotate">旋转角度</param>
        public static string GetImageRotateCommand(int rotate)
        {
            switch (rotate)
            {
                case 0:
                case 180:
                    return "EG";
                case 90:
                case 270:
                    return "VEG";
                default:
                    return "EG";
            }
        }
    }
}
