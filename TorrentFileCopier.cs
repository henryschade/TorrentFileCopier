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

        private string strVerNum = "1.0.5";
        private System.Timers.Timer objTimer = null;
        private int intTimerDelay = 30000;      //30 sec
        private System.Diagnostics.EventLog objEventLog = null;
        private int intEventId = 1;

        private bool bLogToSystem = false;
        private bool bLogToFile = true;
        private string strLogPath = "E:\\Videos\\";
        private string strLogFile = "TFC-LogFile.txt";

        private FileSystemWatcher objFSW = null;
        private string strSourceDir = "D:\\Torrents\\Completed\\";
        private string strDestinationDir = "E:\\Videos\\Shows2\\";
        //private string strDestinationDir = "\\\\ZETA\\Videos\\Shows\\";
        private string[] strFileTypes = new string[]{".avi", ".mkv", ".mp4"};
        private string strLastFileCopied = "";

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
        /// <param name="args">[(bool)LogToSystem], [(bool)[LogToFile], (string)[full path to/of log file]]</param>
        public TorrentFileCopier(string[] args)
        {
            InitializeComponent();

            strLogFile = strLogPath + strLogFile;

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
            WriteAnEntry("TorrentFileCopier " + strVerNum + " Started");

            //If we want to use a timer instead of a FileWatcher.
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

            ////Clean up old/bad event source, if exists.  Requires a reboot after.
            //if (System.Diagnostics.EventLog.SourceExists(strEventSourceName))
            //{
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
            //Create a new FileSystemWatcher and set its properties.
            objFSW = new FileSystemWatcher();
            objFSW.Path = strSourceDir;
            objFSW.IncludeSubdirectories = true;

            //Only watch these file types.
            objFSW.Filter = "*.*";

            //Watch for changes in files.
            objFSW.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.Size;

            //Add event handlers.
            objFSW.Changed += new FileSystemEventHandler(FSW_Change);
            objFSW.Created += new FileSystemEventHandler(FSW_Change);
            objFSW.Deleted += new FileSystemEventHandler(FSW_Change);
            //objFSW.Renamed += new RenamedEventHandler(FSW_Change);

            //Begin watching.
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

        private void FSW_Change(object sender, FileSystemEventArgs e)
        {
            string strFullFileName = "";
            string strFileName = "";
            string strFileType = "dir";

            strFullFileName = e.FullPath.ToString();
            strFileName = e.Name.ToString();
            if (strFileName.Contains("\\"))
            {
                strFileName = strFileName.Substring(strFileName.LastIndexOf("\\") + 1);
            }
            if (strFileName.Contains("."))
            {
                strFileType = strFileName.Substring(strFileName.LastIndexOf("."));
            }

            if ((e.ChangeType == WatcherChangeTypes.Deleted) || (strLastFileCopied == strFileName))
            {
                //We don't care about files that get Deleted.
                //
                //Multiple Events fire when a file is copied, we only need to handle copying the file once.
                return;
            }

            //Copy file if right type
            if (strFileTypes.Contains(strFileType))
            {
                bool IsInUse = false;

                WriteAnEntry("File '" + strFileName + "' was '" + e.ChangeType.ToString() + "'.");
                //"File 'Once.Upon.a.Time.S07E08.XviD-AFG\Once.Upon.a.Time.S07E08.XviD-AFG.avi' was 'Changed'."
                //"File 'Blindspot.S03E10.XviD-AFG\Blindspot.S03E10.XviD-AFG.avi' was 'Deleted'."

                //Check if strDestinationDir has a folder that matches the file name
                try
                {
                    foreach (string strSubFolder in Directory.GetDirectories(strDestinationDir))
                    {
                        //strSubFolder is the full path to the dir, so lets shorten it
                        string strSubFolderName = strSubFolder.Substring(strSubFolder.LastIndexOf("\\") + 1);

                        if (
                            (strFullFileName.Contains(strSubFolderName)) ||
                            (strFullFileName.ToLower().Contains(strSubFolderName.ToLower())) ||
                            (strFullFileName.ToLower().Contains(strSubFolderName.ToLower().Replace(" ", ".")))
                           )
                        {
                            //Check that not have the file already
                            if (File.Exists(strSubFolder + "\\" + strFileName))
                            {
                                //Already have the file
                                WriteAnEntry("There is already a copy of '" + strFileName + "' in '" + strSubFolder + "'.");

                                break;      //No need to check the other SubDirectories.
                            }
                            else
                            {
                                //Copy the File

                                int intSleepTime = 5000;    //5 seconds
                                int intWaitMax = 24;        //with a 5 sec sleep should be around 2 min
                                int intCount = 0;           //to make sure we don't wait indefinitly

                                //We are COPYING, do we care if the file is in use?  Best practice says maybe/probably?
                                if ((IsFileReady(strFullFileName) == false))
                                {
                                    WriteAnEntry("File '" + strFileName + "' is inuse, waiting....");
                                    IsInUse = true;

                                    do
                                    {
                                        System.Threading.Thread.Sleep(intSleepTime);

                                        intCount++;
                                        if (intCount >= intWaitMax)
                                        {
                                            WriteAnEntry("File '" + strFileName + "' was inuse for " + ((intSleepTime / 1000) * intWaitMax) + " seconds.");
                                            break;
                                        }
                                    } while (IsFileReady(strFullFileName) == false);
                                    IsInUse = false;
                                }

                                //Copy file to strDestinationDir
                                //http://www.bearnakedcode.com/2016/09/multi-threaded-asynchronous-file-copy.html
                                //https://www.codeproject.com/Questions/1063313/How-to-copy-files-asynchronously-async-await-Sourc
                                try
                                {
                                    File.Copy(strFullFileName, strSubFolder + "\\" + strFileName);
                                    strLastFileCopied = strFileName;

                                    WriteAnEntry("Copied file '" + strFileName + "' to '" + strSubFolder + "\\'.");
                                }
                                catch
                                {
                                    WriteAnEntry("File '" + strFileName + "' could not be copied, is it in use or does it exist already? ");
                                }

                                break;      //No need to check the other SubDirectories.
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteAnEntry("Error copying file: " + ex.Message);
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

                if ((strLogFile.Contains(":") == false) || (strLogFile.Contains("\\") == false))
                {
                    strLogFile = strLogPath + strLogFile;
                }

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
                    if (!bLogToFile)
                    {
                        bLogToFile = !bLogToFile;
                        bLogToSystem = !bLogToSystem;
                        WriteAnEntry("Error writing to Event Viewer: " + ex.Message + System.Environment.NewLine + "\t" + inMessage);
                        bLogToFile = !bLogToFile;
                        bLogToSystem = !bLogToSystem;
                    }
                }
            }
        }

        #endregion Methods
    }
}
