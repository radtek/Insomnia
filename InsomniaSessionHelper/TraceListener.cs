using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia
{
    class TraceListener : System.Diagnostics.TraceListener
    {
        FileInfo logFile;

        internal TraceListener(FileInfo logFile)
        {
            this.logFile = logFile;
        }

        public override void Write(string message)
        {
            File.AppendAllText(logFile.FullName, message);
        }

        public override void WriteLine(string message)
        {
            File.AppendAllLines(logFile.FullName, new string[] { message });
        }
    }
}