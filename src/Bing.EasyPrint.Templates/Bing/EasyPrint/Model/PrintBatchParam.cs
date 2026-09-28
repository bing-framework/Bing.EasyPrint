using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 封装按多组业务数据批量执行模板打印所需的输入。
    /// </summary>
    /// <typeparam name="T">
    /// 批量打印所使用的模板类型。
    /// </typeparam>
    public class PrintBatchParam<T>
    {
        /// <summary>
        /// 获取或设置本批次使用的模板对象。
        /// </summary>
        /// <remarks>
        /// 集合中的所有业务数据共用此模板。
        /// </remarks>
        public T Template{ get; set; }

        /// <summary>
        /// 获取或设置逐条传入模板的业务数据。
        /// </summary>
        /// <remarks>
        /// 每项对应一条打印记录；旧版字段绑定不支持将字段值解析为集合、映射或嵌套对象。
        /// </remarks>
        public List<object> Params { get; set; }
    }
}
