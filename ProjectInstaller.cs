using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Threading.Tasks;

namespace TorrentFileCopier
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }

        ////Saves startup parameters in the Registry (ImagePath key).
        ////    This would allow users to edit the StartUp parameters if desired.  A config file would be better IMO.
        //protected override void OnBeforeInstall(IDictionary savedState)
        //{
        //    string parameter = "true\" \"false\" \"C:\\Temp\\TFC-LogFile.txt";

        //    Context.Parameters["assemblypath"] = "\"" + Context.Parameters["assemblypath"] + "\" \"" + parameter + "\"";

        //    base.OnBeforeInstall(savedState);
        //}
    }
}
