using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonProject.Src
{
    public interface ILogger
    {
        void SendDebugLogAsync(string str);
        string SendLogWithException(string str, Exception ex);
        void SendLogAsync(string str);
        string GetLogFilePath();
        void DumpMemUsageIfHigherThenBefore();

    }
}
