using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace TorrentFileCopier
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new TorrentFileCopier()
                //new TorrentFileCopier(args)       //If need/want startup paramaters
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
