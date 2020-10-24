using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL
{
    /// <summary>
    /// 图片组件
    /// </summary>
    internal class ImageComponent : ComponentMetadata
    {
        /// <summary>
        /// 旋转角度
        /// </summary>
        public int Rotate { get; set; }

        /// <summary>
        /// 图片起始x坐标
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// 图片起始y坐标
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// 图片数据
        /// </summary>
        public byte[] Data { get; private set; }

        /// <summary>
        /// 宽度
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// 高度
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// 初始化一个<see cref="ImageComponent"/>类型的实例
        /// </summary>
        private ImageComponent() { }

        /// <summary>
        /// 构建
        /// </summary>
        /// <param name="command">打印命令</param>
        public override void Build(CPCLPrintCommand command)
        {
            var byteCountW = (Width + 7) / 8;
            var data = ToHex(Data);
            var cmd = Helper.GetImageRotateCommand(Rotate);
            command.Writer.WriteLine($"{cmd} {byteCountW} {Height} {X} {Y} {data}");
        }

        /// <summary>
        /// 初始化位置
        /// </summary>
        /// <param name="x">图片起始x坐标</param>
        /// <param name="y">图片起始y坐标</param>
        public ImageComponent InitPosition(int x, int y)
        {
            X = x;
            Y = y;
            return this;
        }

        /// <summary>
        /// 通过文件路径创建图片组件
        /// </summary>
        /// <param name="fileUrl">文件地址</param>
        public static ImageComponent CreateFromFile(string fileUrl)
        {
            using (var image = Image.FromFile(fileUrl))
            {
                return new ImageComponent
                {
                    Width = image.Width,
                    Height = image.Height,
                    Data = GetImageData(image)
                };
            }
        }

        /// <summary>
        /// 通过流创建图片组件
        /// </summary>
        /// <param name="stream">流</param>
        public static ImageComponent CreateFromStream(Stream stream)
        {
            using (var image = Image.FromStream(stream))
            {
                return new ImageComponent
                {
                    Width = image.Width,
                    Height = image.Height,
                    Data = GetImageData(image)
                };
            }
        }

        /// <summary>
        /// 通过字节数组创建图片组件
        /// </summary>
        /// <param name="bytes">字节数组</param>
        public static ImageComponent CreateFromBytes(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
                return CreateFromStream(ms);
        }

        /// <summary>
        /// 通过图片创建图片组件
        /// </summary>
        /// <param name="image">图片</param>
        public static ImageComponent CreateFromImage(Image image)
        {
            return new ImageComponent
            {
                Width = image.Width,
                Height = image.Height,
                Data = GetImageData(image)
            };
        }

        /// <summary>
        /// 获取图片数据
        /// </summary>
        /// <param name="image">图片</param>
        private static byte[] GetImageData(Image image)
        {
            Bitmap srcBmp = new Bitmap(image);

            var width = srcBmp.Width;
            var height = srcBmp.Height;
            var rowRealBytesCount = width % 8 > 0 ? width / 8 + 1 : width / 8;
            var rowSize = ((width + 31) >> 5) << 2;

            Bitmap dstBmp = null;
            var dstStream = new MemoryStream();
            byte[] result = null;

            try
            {
                dstBmp = srcBmp.Clone(new Rectangle(0, 0, width, height), PixelFormat.Format1bppIndexed);
                dstBmp.Save(dstStream, ImageFormat.Bmp);
                var dstBuffer = dstStream.ToArray();

                var bfOffBits = BitConverter.ToInt32(dstBuffer, 10);
                result = new byte[height * rowRealBytesCount];

                // 读取时需要反向读取每行字节实现上下翻转的效果，打印机打印顺序需要这样读取。
                for (var i = 0; i < height; i++)
                    Array.Copy(dstBuffer, bfOffBits + (srcBmp.Height - 1 - i) * rowSize, result, i * rowRealBytesCount, rowRealBytesCount);
                // 结果数据处理
                for (var i = 0; i < result.Length; i++)
                    result[i] ^= 0xFF;
            }
            finally
            {
                dstStream?.Dispose();
                srcBmp?.Dispose();
                dstBmp?.Dispose();
            }
            return result;
        }

        /// <summary>
        /// 转换为16进制
        /// </summary>
        /// <param name="data">数</param>
        private static string ToHex(byte[] data) => BitConverter.ToString(data).Replace("-", string.Empty);
    }
}
