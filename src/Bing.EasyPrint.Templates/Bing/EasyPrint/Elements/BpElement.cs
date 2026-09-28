using Bing.EasyPrint.Model;

namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 定义模板可渲染元素共用的数据绑定、坐标和边距。
    /// </summary>
    public class BpElement
    {
        /// <summary>
        /// 获取或设置控制元素是否参与渲染的条件。
        /// </summary>
        public BpCondition Condition { get; set; }
        /// <summary>
        /// 获取或设置用于识别元素种类的类型编码。
        /// </summary>
        /// <remarks>1=文字；2=图片；3=二维码；4=条形码；5=线段；6=组合；7=矩形；8=椭圆；9=图标；10=二维点阵码</remarks>
        public int Type { get; set; }

        /// <summary>
        /// 获取或设置元素使用的字段、静态文本或资源键。
        /// </summary>
        public BgField Field { get; set; }

        /// <summary>
        /// 获取或设置元素布局区域四周的外边距。
        /// </summary>
        public BpMargin Margin { get; set; }

        /// <summary>
        /// 获取或设置元素相对于所在单元格或组件原点的起始坐标，单位为打印点。
        /// </summary>
        public BpCoordinate StartCoordinate { get; set; }

        /// <summary>
        /// 获取或设置可选的元素布局终点坐标，单位为打印点。
        /// </summary>
        public BpCoordinate EndCoordinate { get; set; }
    }
}
