using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace TorrentFileCopier
{
    public partial class TorrentFileCopier : ServiceBase
    {
        #region Constants and Member Variables

        private bool bLogToFile = false;
        private bool bLogToSystem = true;
        private string strLogFile = "C:\\Temp\\TFC-LogFile.txt";
        private int intEventId = 1;
        private System.Timers.Timer objTimer = null;

        #endregion Constants and Member Variables

        #region Constructors

        public TorrentFileCopier()
        {
            InitializeComponent();

            if (!bLogToFile && !bLogToSystem)
            {
                bLogToSystem = true;
            }

            if (!bLogToSystem)
            {
                CreateEventLog();
            }
        }

        #endregion Constructors

        #region Service Actions

        protected override void OnStart(string[] args)
        {
            //add an entry to the event log when the service starts
            WriteAnEntry("TorrentFileCopier Started");

            //If we want to use a timer instead of an event handler that triggers on file update/create/etc.
            CreateTimer();
        }

        protected override void OnStop()
        {
            //add an entry to the event log when the service stops
            WriteAnEntry("TorrentFileCopier Stopped");

            //One sample had .Enable the other had .Stop.
            //objTimer.Enabled = false;
            objTimer.Stop();
        }

        //OnPause, OnContinue, and OnShutdown are other service actions that can be overridden

        #endregion Service Actions

        #region Methods

        private void CreateEventLog()
        {
            eventLog1 = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists("TorrentFileCopier"))
            {
                System.Diagnostics.EventLog.CreateEventSource("TorrentFileCopier", "TFC Log");
            }

            //the event log source by which the application is registered on the computer
            eventLog1.Source = "TorrentFileCopier";
            //The name of the Log File any entries will be under
            eventLog1.Log = "TFC Log";
        }

        private void CreateTimer()
        {
            // Set up a timer to trigger every minute.  
            objTimer = new System.Timers.Timer();
            objTimer.Interval = 60000;     // 60 seconds  
            objTimer.Elapsed += new System.Timers.ElapsedEventHandler(this.Timer_Tick);

            //One sample had .Enable the other had .Start.
            //objTimer.Enabled = true;
            objTimer.Start();
        }

        private void LogError(string inMessage)
        {
            WriteAnEntry(inMessage);
        }

        private void LogError(Exception ex)
        {
            WriteAnEntry(ex.Source.ToString() + "; " + ex.Message.ToString().Trim());
        }

        private void Timer_Tick(object sender, System.Timers.ElapsedEventArgs args)
        {
            WriteAnEntry("TorrentFileCopier timer event triggered (" + (objTimer.Interval / 1000).ToString() + " sec)");

            // TODO: Insert monitoring activities here.  
        }

        private void WriteAnEntry(string inMessage)
        {
            if (bLogToFile)
            {
                //Log to File
                //LogError(inMessage);

                StreamWriter objSW = null;
                try
                {
                    objSW = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + strLogFile, true);
                    objSW.WriteLine(DateTime.Now.ToString() + ": " + inMessage);
                    objSW.Flush();
                    objSW.Close();
                }
                catch
                {
                }
            }

            if (bLogToSystem)
            {
                //Log to system Event Logs
                eventLog1.WriteEntry(inMessage, EventLogEntryType.Information, intEventId++);
            }
        }

        #endregion Methods
    }
}
