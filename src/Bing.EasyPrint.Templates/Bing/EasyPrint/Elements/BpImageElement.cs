namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 用于从受限资源提供器读取并输出图片的模板元素。
    /// </summary>
    public class BpImageElement : BpElement
    {
        /// <summary>
    /// 初始化一个 <see cref="BpImageElement"/> 类型的实例。
        /// </summary>
        public BpImageElement() { Type = 2; }
        /// <summary>
        /// 获取或设置图片布局宽度，单位为打印点；零表示按原始宽度计算。
        /// </summary>
        public int Width{ get; set; }

        /// <summary>
        /// 获取或设置图片布局高度，单位为打印点；零表示按比例计算。
        /// </summary>
        public int Height{ get; set; }

        /// <summary>
        /// 获取或设置是否使用压缩方式编码图片打印数据。
        /// </summary>
        public bool IsCompress { get; set; }
        /// <summary>
        /// 获取或设置图片的顺时针旋转角度。
        /// </summary>
        /// <remarks>
        /// 宽高表示旋转前尺寸；默认不旋转。
        /// </remarks>
        public RotationAngle Rotation { get; set; }

        /// <summary>
        /// 获取或设置图片源文件中的裁切区域。
        /// </summary>
        /// <remarks>
        /// 坐标和尺寸使用源图片像素；为空时使用完整源图，非空时先裁切再按 Width 和 Height 缩放。
        /// </remarks>
        public Bing.EasyPrint.Model.BpImageCrop Crop { get; set; }
    }
}
