namespace Bing.EasyPrint.CPCL
{
    /// <summary>
    /// CPCL 打印命令 - 高级指令
    /// </summary>
    // ReSharper disable once InconsistentNaming
    partial class CPCLPrintCommand
    {
        /// <summary>
        /// CONTRAST 命令
        /// </summary>
        /// <param name="level">对比度级别。<br />
        /// 0 - 默认值<br />
        /// 1 - 中<br />
        /// 2 - 暗<br />
        /// 3 - 非常暗
        /// </param>
        /// <remarks>CONTRAST 命令用于指定整个标签的打印黑度。亮的打印输出为对比度级别 0。暗的对比度级别为 3。打印 机在开机时的默认对比度级别为 0。必须为每个标签文件指定对比度级别。</remarks>
        public CPCLPrintCommand Contrast(int level) => WriteRawLine($"CONTRAST {level}");

        /// <summary>
        /// TONE 命令
        /// </summary>
        /// <param name="level">色调级别。选择介于 -99 到 200 之间的值</param>
        /// <remarks>TONE 命令可用于替代 CONTRAST 命令来指定所有标签的打印黑度。亮的打印输出为色调级别 -99。暗的 色调级别为 200。打印机在开机时的默认色调级别为 0。色调级别设置在更改前对所有打印任务保持有效。TONE 和 CONTRAST 命令不能彼此组合使用。 </remarks>
        public CPCLPrintCommand Tone(int level) => WriteRawLine($"TONE {level}");

        /// <summary>
        /// 对齐命令 - 居中对齐
        /// </summary>
        /// <param name="end">对齐的结束点。如果未输入参数，则对于横向打印，对齐命令将使用打印头的宽度；而对于纵向打印，对 齐命令将使用零（页头）。 </param>
        /// <remarks>使用对齐命令可以控制字段的对齐方式。默认情况下，打印机将左对齐所有字段。对齐命令将对所有后续字段保持 有效，直至指定了其他对齐命令。</remarks>
        public CPCLPrintCommand Center(int? end = null) => WriteRawLine($"CENTER{(end == null ? string.Empty : $" {end}")}");

        /// <summary>
        /// 对齐命令 - 左对齐
        /// </summary>
        /// <remarks>使用对齐命令可以控制字段的对齐方式。默认情况下，打印机将左对齐所有字段。对齐命令将对所有后续字段保持 有效，直至指定了其他对齐命令。</remarks>
        public CPCLPrintCommand Left() => WriteRawLine("LEFT");

        /// <summary>
        /// 对齐命令 - 右对齐
        /// </summary>
        /// <param name="end">对齐的结束点。如果未输入参数，则对于横向打印，对齐命令将使用打印头的宽度；而对于纵向打印，对 齐命令将使用零（页头）。 </param>
        /// <remarks>使用对齐命令可以控制字段的对齐方式。默认情况下，打印机将左对齐所有字段。对齐命令将对所有后续字段保持 有效，直至指定了其他对齐命令。</remarks>
        public CPCLPrintCommand Right(int? end = null) => WriteRawLine($"RIGHT{(end == null ? string.Empty : $" {end}")}");

        /// <summary>
        /// PAGE-WIDTH 命令
        /// </summary>
        /// <param name="width">页面的单位宽度</param>
        /// <remarks>打印机假定页面宽度为打印机的完整宽度。打印会话的大高度由页面宽度和可用打印内存决定。如果页面宽度小 于打印机的完整宽度，则用户可以通过指定页面宽度来增加大页面高度。</remarks>
        public CPCLPrintCommand PageWidth(int width) => WriteRawLine($"PAGE-WIDTH {width}");

        /// <summary>
        /// PACE 命令
        /// </summary>
        /// <remarks>此命令可以与批量打印一起使用。在激活 PACE 后，用户必须按下打印机的 FEED（送纸）键才能打印其他标签， 直至完成批次数量。默认情况下，开机时定步功能处于禁用状态。</remarks>
        public CPCLPrintCommand Pace() => WriteRawLine("PACE");

        /// <summary>
        /// AUTO-PACE 命令
        /// </summary>
        /// <remarks>此命令可用于指示配备了标签存在传感器的打印机延迟打印，直至取走了之前打印的标签。</remarks>
        public CPCLPrintCommand PaceAuto() => WriteRawLine("AUTO-PACE");

        /// <summary>
        /// NO-PACE 命令
        /// </summary>
        /// <remarks>如果打印机已处于 PACE 或 AUTO-PACE 模式，则此命令可取消 PACE 和 AUTO-PACE 模式。打印机在开机时默 认为 NO-PACE 模式。</remarks>
        public CPCLPrintCommand PaceNo() => WriteRawLine("NO-PACE");

        /// <summary>
        /// WAIT 命令
        /// </summary>
        /// <param name="delayTime">延迟时间。以 1/8 秒为单位</param>
        /// <remarks>此命令用于在打印一个标签后引入一段延迟。</remarks>
        public CPCLPrintCommand Wait(int delayTime) => WriteRawLine($"WAIT {delayTime}");

        /// <summary>
        /// REWIND 命令（开启）
        /// </summary>
        /// <remarks>此命令用于打开或关闭回卷（或卷纸）电机。打印机在开机时默认为 REWIND-ON。如果打印机没有配备电机驱动 的回卷机制，则将忽略 REWIND 命令。 </remarks>
        public CPCLPrintCommand RewindOn() => WriteRawLine("REWIND-ON");

        /// <summary>
        /// REWIND 命令（关闭）
        /// </summary>
        /// <remarks>此命令用于打开或关闭回卷（或卷纸）电机。打印机在开机时默认为 REWIND-ON。如果打印机没有配备电机驱动 的回卷机制，则将忽略 REWIND 命令。 </remarks>
        public CPCLPrintCommand RewindOff() => WriteRawLine("REWIND-OFF");

        /// <summary>
        /// TENSION 命令（打印标签之前）
        /// </summary>
        /// <param name="length">回卷电机用于收紧衬纸张力的单位长度。当张力调整到位后，回卷电机将滑动（不会为下一个打印周期 用尽所有调整余地）。</param>
        /// <remarks>TENSION 命令用于在打印标签之前和/或之后，通过按预先指定的长度运行回卷电机来调整衬纸张力。对于配备 电机驱动的回卷机制的打印机，此调整操作可以改善剥离器的性能。如果打印机没有配备电机驱动的回卷机制，则将 忽略 TENSION 命令。 </remarks>
        public CPCLPrintCommand TensionPre(int length) => WriteRawLine($"PRE-TENSION {length}");

        /// <summary>
        /// TENSION 命令（打印标签之后）
        /// </summary>
        /// <param name="length">回卷电机用于收紧衬纸张力的单位长度。当张力调整到位后，回卷电机将滑动（不会为下一个打印周期 用尽所有调整余地）。</param>
        /// <remarks>TENSION 命令用于在打印标签之前和/或之后，通过按预先指定的长度运行回卷电机来调整衬纸张力。对于配备 电机驱动的回卷机制的打印机，此调整操作可以改善剥离器的性能。如果打印机没有配备电机驱动的回卷机制，则将 忽略 TENSION 命令。 </remarks>
        public CPCLPrintCommand TensionPost(int length) => WriteRawLine($"POST-TENSION {length}");

        /// <summary>
        /// SPEED 命令
        /// </summary>
        /// <param name="level">电机的最高速度级别。一个介于 0 到 5 之间的数字，0 表示低速度。</param>
        /// <remarks>此命令用于设置电机的高速度级别。每一款打印机型号都设置了低和高极限速度。SPEED 命令可以在 0 到 5 的范围内选择速度级别，0 表示低速度。为每一款打印机型号设置的高速度仅可在理想条件下达到。电池或供 电电压、材料厚度、打印黑度、是否使用贴标机、是否使用剥离器以及标签长度等诸多因素均会影响大极限打印速度。</remarks>
        public CPCLPrintCommand Speed(int level) => WriteRawLine($"SPEED {level}");

        /// <summary>
        /// SETSP 命令
        /// </summary>
        /// <param name="spacing">字符间的单位尺寸。间距的默认值为零。请注意，此命令受单位命令设置的影响。 </param>
        /// <remarks>SETSP 命令用于更改文本字符之间的间距。 </remarks>
        public CPCLPrintCommand SetSp(int spacing) => WriteRawLine($"SETSP {spacing}");

        /// <summary>
        /// UNDERLINE 命令（开启）
        /// </summary>
        /// <remarks>UNDERLINE 命令用于给文本加下划线。仅当所使用的字体支持下划线时，此命令才会起作用。如果所使用的字体不支 持 UNDERLINE，则将忽略此命令。</remarks>
        public CPCLPrintCommand UnderlineOn() => WriteRawLine("UNDERLINE ON");

        /// <summary>
        /// UNDERLINE 命令（关闭）
        /// </summary>
        /// <remarks>UNDERLINE 命令用于给文本加下划线。仅当所使用的字体支持下划线时，此命令才会起作用。如果所使用的字体不支 持 UNDERLINE，则将忽略此命令。</remarks>
        public CPCLPrintCommand UnderlineOff() => WriteRawLine("UNDERLINE OFF");

        /// <summary>
        /// ON-OUT-OF-PAPER 命令
        /// </summary>
        /// <param name="action">遇到错误操作。<br />
        /// PURGE：当遇到打印错误时，在尝试指定的次数之后丢弃标签。<br />
        /// WAIT：当遇到打印错误时不丢弃标签。在此模式下，打印机将等待更正错误，更正后才会执行下一打印尝试
        /// </param>
        /// <param name="retries">指定打印机应尝试打印标签的次数。</param>
        /// <remarks>可以发出此命令来指示打印机在打印标签期间遇到错误（例如，纸张用完）时要采取的操作。</remarks>
        public CPCLPrintCommand OnOutOfPaper(string action, int retries) => WriteRawLine($"ON-OUT-OF-PAPER {action} {retries}");

        /// <summary>
        /// ON-FEED 命令
        /// </summary>
        /// <param name="action">遇到换页符(0x0c)或FEED(送纸)键操作。<br />
        /// IGNORE：当按下 FEED（送纸）键或收到换页符时，不采取任何操作。<br />
        /// FEED：当按下 FEED（送纸）键或收到换页符时，切换至下一页顶部。<br />
        /// REPRINT：当按下 FEED（送纸）键或收到换页符时，重新打印上一标签。
        /// </param>
        /// <remarks>当按下 FEED（送纸）键或打印机收到换页符 (0x0c) 时，打印机可配置为忽略、换页或重新打印上一标签。</remarks>
        public CPCLPrintCommand OnFeed(string action) => WriteRawLine($"ON-FEED {action}");

        /// <summary>
        /// PREFEED 命令
        /// </summary>
        /// <param name="length">打印机在打印之前将介质向前移动的单位长度</param>
        /// <remarks>PREFEED 命令指示打印机在打印之前将介质向前移动指定长度。</remarks>
        public CPCLPrintCommand PreFeed(int length) => WriteRawLine($"PREFEED {length}");

        /// <summary>
        /// POSTFEED 命令
        /// </summary>
        /// <param name="length">打印机在打印之后将介质向前移动的单位长度</param>
        /// <remarks>POSTFEED 命令指示打印机在打印之后将介质向前移动指定长度。</remarks>
        public CPCLPrintCommand PostFeed(int length) => WriteRawLine($"POSTFEED {length}");

        /// <summary>
        /// PRESEND-AT 命令
        /// </summary>
        /// <param name="length">打印之后介质向前移动以及打印下一标签之前介质回退的单位长度，以点行为单位</param>
        /// <param name="delay">打印标签之后打印机在向前移动介质前等待的时间间隔。以 1/8 秒为单位递增。延迟“1”等价于 1/8 秒。延迟“4”等价于 1/2 秒，以此类推。</param>
        /// <remarks>PRESENT-AT 命令可用于将介质定位到打印机的撕纸杆处或者操作人员可以轻松取走打印后的标签的位置。发出 PRESENT-AT 命令后，打印机将打印标签，然后在延迟一段时间后，将介质向前移动指定的距离。在开始新的打印作 业之前，打印机会将介质回退相同的距离。 “delay”参数用于在执行批量打印作业时，避免不必要的向前/回退操作。PRESENT-AT 命令可在标签文件中发 出，也可在实用工具命令会话(!UTILITIES…PRINT) 中发出。</remarks>
        public CPCLPrintCommand PreSentAt(int length, int delay) => WriteRawLine($"PRESENT-AT {length} {delay}");

        /// <summary>
        /// COUNTRY 命令
        /// </summary>
        /// <param name="name">国家/地区名称</param>
        /// <remarks>使用 COUNTRY 控制命令可以针对指定的国家/地区替代适当的字符集。</remarks>
        public CPCLPrintCommand Country(string name) => WriteRawLine($"COUNTRY {name}");

        /// <summary>
        /// DEFINE-FORMAT 命令
        /// </summary>
        /// <param name="name">格式文件名称。格式文件名可由不超过 8 个的字母或数字组成，格式文件扩展名可由不超过 3 个的字母或数字组成。格式文件名 或扩展名中的所有小写字母将转换为大写字母。</param>
        /// <param name="data">数据</param>
        /// <remarks>DEFINE-FORMAT 和 USE-FORMAT 命令分别用于标识格式和数据。 使用格式文件可以避免为打印的每个标签重新发送相同的格式信息。通过使用预加载的格式，只需向打印机发送 变量数据（例如描述、价格等）<br />
        /// 使用“\\”（双反斜杠）作为数据的占位符。 
        /// </remarks>
        public CPCLPrintCommand DefineFormat(string name, params string[] data)
        {
            WriteRawLine($"!DF {name}");
            foreach (var item in data)
                WriteRawLine(item);
            return this;
        }

        /// <summary>
        /// USE-FORMAT 命令
        /// </summary>
        /// <param name="name">格式文件名称。格式文件名可由不超过 8 个的字母或数字组成，格式文件扩展名可由不超过 3 个的字母或数字组成。格式文件名 或扩展名中的所有小写字母将转换为大写字母。</param>
        /// <param name="data">变量数据</param>
        /// <remarks>USE-FORMAT（或 UF）命令指示打印机使用指定的格式文件。   将使用该格式文件以及 USE-FORMAT 命令后面 提供的数据创建标签。在访问指定的格式文件后，打印机将使用所提供的数据替代“\\”分隔符，从而生成所需的标签。</remarks>
        public CPCLPrintCommand UseFormat(string name, params object[] data)
        {
            WriteRawLine($"!UF {name}");
            foreach (var item in data)
                WriteRawLine(item.ToString());
            return this;
        }

        /// <summary>
        /// BEEP 命令
        /// </summary>
        /// <param name="length">蜂鸣持续时间，以 1/8 秒为单位递增指定。 </param>
        /// <remarks>此命令用于指示打印机让蜂鸣器发出给定时间长度的声音。未配备蜂鸣器的打印机将忽略此命令。</remarks>
        public CPCLPrintCommand Beep(int length) => WriteRawLine($"BEEP {length}");

        /// <summary>
        /// CUT 命令
        /// </summary>
        /// <remarks>在配备有切纸器的打印机上，使用此命令可在打印标签之后裁切标签。</remarks>
        public CPCLPrintCommand Cut() => WriteRawLine("CUT");

        /// <summary>
        /// PARTIAL-CUT 命令
        /// </summary>
        /// <remarks>在配备有切纸器的打印机上，使用此命令可在打印标签之后裁切标签，保留部分标签不进行裁切以便可以轻松撕 开标签剩余部分。</remarks>
        public CPCLPrintCommand PartialCut() => WriteRawLine("PARTIAL-CUT");

        /// <summary>
        /// CUT-AT 命令
        /// </summary>
        /// <param name="length">在执行裁切或部分裁切操作之后，纸张应回退的单位长度。</param>
        /// <remarks>在配备有切纸器的打印机上，此命令与 CUT 或 PARTIAL-CUT 命令结合使用。此命令可指示打印机按指定长度回 退纸张。未配备切纸器的打印机将忽略此命令。</remarks>
        public CPCLPrintCommand CutAt(int length) => WriteRawLine($"CUT-AT {length}");
    }
}
