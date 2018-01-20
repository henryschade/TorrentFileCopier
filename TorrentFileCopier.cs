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

        private System.Timers.Timer objTimer = null;
        private int intTimerDelay = 30000;      //30 sec
        private System.Diagnostics.EventLog objEventLog = null;
        private int intEventId = 1;

        private bool bLogToSystem = false;
        private bool bLogToFile = true;
        private string strLogFile = "E:\\Videos\\TFC-LogFile.txt";

        private FileSystemWatcher objFSW = null;
        private string strSourceDir = "D:\\Torrents\\Completed";
        private string strDestinationDir = "E:\\Videos\\Shows";
        private string[] strFileTypes = new string[]{"avi", "mkv", "mp4"};

        #endregion Constants and Member Variables

        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        public TorrentFileCopier()
        {
            string[] strArgs = null;

            //Ideally read a config file into the above array at this point.

            new TorrentFileCopier(strArgs);
        }

        /// <summary>
        /// Constructor that takes arguments
        /// </summary>
        /// <param name="args">[LogToSystem], [(LogToFile), (full path to/of log file)]</param>
        public TorrentFileCopier(string[] args)
        {
            InitializeComponent();

            if (args != null)
            {
                if (args.Count() > 0)
                {
                    //LogToSystem
                    bLogToSystem = (args[0].ToLower() == "true");
                }
                if (args.Count() > 2)
                {
                    //LogToFile, and File path to log entries into.
                    bLogToFile = (args[1].ToLower() == "true");
                    strLogFile = args[2];
                }
            }

            if (!bLogToFile && !bLogToSystem)
            {
                bLogToSystem = true;
            }

            if (bLogToSystem)
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
            //CreateTimer();

            CreateFileWatcher();
        }

        protected override void OnStop()
        {
            //add an entry to the event log when the service stops
            WriteAnEntry("TorrentFileCopier Stopped" + System.Environment.NewLine);

            //One sample had .Enable the other had .Stop.
            //objTimer.Enabled = false;
            //objTimer.Stop();

            //Stop watching
            objFSW.EnableRaisingEvents = false;
            objFSW.Dispose();
        }

        //OnPause, OnContinue, and OnShutdown are other service actions that can be overridden

        #endregion Service Actions

        #region Methods

        private void CreateEventLog()
        {
            string strEventSourceName = "TorrentFileCopier";
            string strLogName = "TFC Logs";

            objEventLog = new System.Diagnostics.EventLog();

            this.ServiceName = "TorrentFileCopier";

            this.AutoLog = true;
            // These Flags set whether or not to handle that specific type of event. Set to true if you need it, false otherwise.
            this.CanHandlePowerEvent = true;
            this.CanHandleSessionChangeEvent = true;
            this.CanPauseAndContinue = true;
            this.CanShutdown = true;
            this.CanStop = true;

            //if (System.Diagnostics.EventLog.SourceExists(strEventSourceName))
            //{
            //    //Clean up the old/bad event source, if exists.  Requires a reboot after.
            //    System.Diagnostics.EventLog.DeleteEventSource(strEventSourceName);
            //}
            if (!System.Diagnostics.EventLog.SourceExists(strEventSourceName))
            {
                //Create a new event source
                System.Diagnostics.EventLog.CreateEventSource(strEventSourceName, strLogName);
            }

            //the event log source by which the application is registered on the computer
            objEventLog.Source = strEventSourceName;
            //The name of the Log File any entries will be under
            objEventLog.Log = strLogName;
        }

        public void CreateFileWatcher()
        {
            // Create a new FileSystemWatcher and set its properties.
            objFSW = new FileSystemWatcher();
            objFSW.Path = strSourceDir;
            objFSW.IncludeSubdirectories = true;

            // Only watch these file types.
            objFSW.Filter = "*.*";

            // Watch for changes in LastAccess and LastWrite times, and the renaming of files or directories.
            //objFSW.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            objFSW.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;

            // Add event handlers.
            objFSW.Changed += new FileSystemEventHandler(OnChanged);
            objFSW.Created += new FileSystemEventHandler(OnChanged);
            objFSW.Deleted += new FileSystemEventHandler(OnChanged);
            //objFSW.Renamed += new RenamedEventHandler(OnRenamed);

            // Begin watching.
            objFSW.EnableRaisingEvents = true;

            WriteAnEntry("Watching dir '" + objFSW.Path.ToString() + "' for changes to '" + objFSW.Filter.ToString() + "' files.");
        }

        private void CreateTimer()
        {
            // Set up a timer to trigger every minute.  
            objTimer = new System.Timers.Timer();
            //objTimer.Interval = 60000;     // 60 seconds
            objTimer.Interval = intTimerDelay;
            objTimer.Elapsed += new System.Timers.ElapsedEventHandler(this.Timer_Tick);

            //One sample had .Enable the other had .Start.
            //objTimer.Enabled = true;
            objTimer.Start();
        }

        public static bool IsFileReady(String inFilename)
        {
            // If the file can be opened for exclusive access it means that the file is no longer locked by another process.
            try
            {
                using (FileStream inputStream = File.Open(inFilename, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    if (inputStream.Length > 0)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }

                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            string strFullFileName = e.FullPath.ToString();
            string strFileName = e.Name.ToString();
            string strFileType = strFileName.Substring(strFileName.LastIndexOf(".") + 1);

            //Copy file if approperiate
            if (strFileTypes.Contains(strFileType))
            {
                //bool bFoundDir = false;

                WriteAnEntry("File '" + strFileName + "' was '" + e.ChangeType.ToString() + "'.");

                //Check if strDestinationDir has a folder that matches
                foreach (string strSubFolder in Directory.GetDirectories(strDestinationDir))
                {
                    if (strFullFileName.Contains(strSubFolder))
                    {
                        //bFoundDir = true;

                        if ((IsFileReady(strFullFileName) == false))
                        {
                            do
                            {
                                System.Threading.Thread.Sleep(5000);        //Wait 5 seconds
                            } while (IsFileReady(strFullFileName) == false);
                        }

                        //Copy file to strDestinationDir
                        //http://www.bearnakedcode.com/2016/09/multi-threaded-asynchronous-file-copy.html
                        //https://www.codeproject.com/Questions/1063313/How-to-copy-files-asynchronously-async-await-Sourc
                        try
                        {
                            File.Copy(strFullFileName, strDestinationDir + "\\" + strSubFolder + "\\" + strFileName);

                            WriteAnEntry("Copied file '" + strFileName + "' to '" + strDestinationDir + "\\" + strSubFolder + "'.");
                        }
                        catch
                        {
                            WriteAnEntry("File '" + strFileName + "' is in use? ");
                        }

                        break;
                    }
                }
            }
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
                StreamWriter objSW = null;
                try
                {
                    //objSW = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + strLogFile, true);
                    objSW = new StreamWriter(strLogFile, true);
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
                try
                {
                    //Log to system Event Logs
                    objEventLog.WriteEntry(inMessage, EventLogEntryType.Information, intEventId++);
                    //Maybe we try using "eventLog1"
                }
                catch (Exception ex)
                {
                    //Maybe we try using "eventLog1"
                    if (!bLogToFile)
                    {
                        bLogToFile = !bLogToFile;
                        bLogToSystem = !bLogToSystem;
                        WriteAnEntry("Error writing to Event Viewer: " + ex.Message + System.Environment.NewLine + "    " + inMessage);
                        bLogToFile = !bLogToFile;
                        bLogToSystem = !bLogToSystem;
                    }
                }
            }
        }

        #endregion Methods
    }
}
