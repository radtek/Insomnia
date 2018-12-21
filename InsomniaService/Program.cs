using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace MadWizard.Insomnia
{
    static class Program
    {
        static void Main()
        {
            ServiceBase.Run(new ServiceBase[] { new InsomniaService() });
        }
    }
}
