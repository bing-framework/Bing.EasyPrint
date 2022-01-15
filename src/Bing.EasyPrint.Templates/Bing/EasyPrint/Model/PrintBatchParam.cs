using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 打印批量参数
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    public class PrintBatchParam<T>
    {
        /// <summary>
        /// 模板
        /// </summary>
        public T Template{ get; set; }

        /// <summary>
        /// 打印主数据。注：属性值不支持List,Map,类对象
        /// </summary>
        public List<object> Params { get; set; }
    }
}
