namespace Bing.EasyPrint.CPCL
{
    /// <summary>
    /// CPCL 打印命令 - 基础指令
    /// </summary>
    // ReSharper disable once InconsistentNaming
    partial class CPCLPrintCommand
    {
        /// <summary>
        /// 初始化命令
        /// </summary>
        /// <param name="offset">标签横向偏移量</param>
        /// <param name="width">标签宽度</param>
        /// <param name="height">标签高度</param>
        /// <param name="qty">打印标签数量</param>
        public CPCLPrintCommand InitCommand(int offset, int width, int height, int qty)
        {
            WriteRawLine($"! {offset} 200 200 {height} {qty}");
            PageWidth(width);
            return this;
        }

        /// <summary>
        /// PRINT 命令
        /// </summary>
        /// <remarks>PRINT 命令作为整个命令集的结束命令，将会启动文件打印。在任何情况下（行式打印模式除外），这项命令都必 须是后一条命令。执行 PRINT 命令时，打印机将从控制会话中退出。确保使用回车和换行字符结束此项及所有命令。
        /// </remarks>
        public CPCLPrintCommand Print() => WriteRawLine("PRINT");

        /// <summary>
        /// ENCODING 命令
        /// </summary>
        /// <param name="encoding">编码。<br/>
        /// ASCII<br/>
        /// UTF-8<br/>
        /// GB18030
        /// </param>
        /// <remarks>ENCODING 控制命令可以指定要发送到打印机的数据的编码形式。</remarks>
        public CPCLPrintCommand Encoding(string encoding) => WriteRawLine($"ENCODING {encoding}");

        /// <summary>
        /// FORM 命令
        /// </summary>
        /// <remarks>FORM 命令可以指示打印机在一页打印结束后切换至下一页顶部。 </remarks>
        public CPCLPrintCommand Form() => WriteRawLine("FORM");

        /// <summary>
        /// JOURNAL 命令
        /// </summary>
        /// <remarks>默认情况下，如果在打印周期期间（LABEL 模式）发现明显标记（介质背面的黑色水平条），则打印机会检查介质对 齐情况是否正确。必要时，可以使用  JOURNAL 命令禁用自动校正功能。用户程序负责在 JOURNAL 模式下进行检 查并确保有纸。有关检查缺纸条件的详细信息，请参阅状态询问命令。</remarks>
        public CPCLPrintCommand Journal() => WriteRawLine("JOURNAL");
    }
}
