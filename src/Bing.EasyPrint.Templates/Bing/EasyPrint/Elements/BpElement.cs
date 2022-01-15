using Bing.EasyPrint.Model;

namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 最小显示元素
    /// </summary>
    public class BpElement
    {
        /// <summary>
        /// 类型
        /// </summary>
        /// <remarks>1=文字；2=图片；3=二维码；4=条形码</remarks>
        public int Type { get; set; }

        /// <summary>
        /// 显示的属性信息
        /// </summary>
        public BgField Field { get; set; }

        /// <summary>
        /// 元素显示间距
        /// </summary>
        public BpMargin Margin { get; set; }

        /// <summary>
        /// 左上角起始坐标
        /// </summary>
        public BpCoordinate StartCoordinate { get; set; }

        /// <summary>
        /// 右下角终止坐标
        /// </summary>
        public BpCoordinate EndCoordinate { get; set; }
    }
}
