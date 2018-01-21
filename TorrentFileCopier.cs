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
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TorrentFileCopier
{
    //Has info on how to add/create an installer project to the solution, and is one of the 4 URL's I used as reference in building this program.
    //https://www.codeproject.com/Articles/3990/Simple-Windows-Service-Sample

    public partial class TorrentFileCopier : ServiceBase
    {
        #region Constants and Member Variables

        private string strVerNum = "1.2.0";
        private System.Timers.Timer objTimer = null;
        private int intTimerDelay = 3600;      //in sec, how often the Timer task runs. (3600 = 1 hour)
        private System.Diagnostics.EventLog objEventLog = null;
        private int intEventId = 1;

        private bool bLogToSystem = false;
        private bool bLogToFile = true;
        private string strLogPath = "E:\\Videos\\";
        private string strLogFile = "TFC-LogFile.txt";

        private FileSystemWatcher objFSW = null;
        private string strSourceDir = "D:\\Torrents\\Completed\\";
        private string strDestinationDir = "E:\\Videos\\Shows\\";
        private string[] strFileTypes = new string[]{".avi", ".mkv", ".mp4"};
        private string strLastFileChecked = "";

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

            //We use a timer to double check the source dir every so often, just to make sure.
            CreateTimer();

            CreateFileWatcher();
        }

        protected override void OnStop()
        {
            //add an entry to the event log when the service stops
            WriteAnEntry("TorrentFileCopier Stopped" + System.Environment.NewLine);

            //Stop the Timer (One sample had .Enable the other had .Stop).
            if (objTimer != null)
            {
                objTimer.Enabled = false;
                objTimer.Stop();
            }

            //Stop watching
            if (objFSW != null)
            {
                objFSW.EnableRaisingEvents = false;
                objFSW.Dispose();
            }
        }

        //OnPause, OnContinue, and OnShutdown are other service actions that can be overridden

        #endregion Service Actions

        #region Methods

        private void CheckFileAndCopy(string strFullFileName)
        {
            string strFileName = strFullFileName;
            string strFileType = "dir";

            if (strFileName.Contains("\\"))
            {
                strFileName = strFileName.Substring(strFileName.LastIndexOf("\\") + 1);
            }
            if (strFileName.Contains("."))
            {
                strFileType = strFileName.Substring(strFileName.LastIndexOf("."));
            }

            //Copy file if it is a matching type we want
            if (strFileTypes.Contains(strFileType))
            {
                bool IsInUse = false;

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

                                    do
                                    {
                                        IsInUse = true;
                                        System.Threading.Thread.Sleep(intSleepTime);

                                        intCount++;
                                        if (intCount >= intWaitMax)
                                        {
                                            WriteAnEntry("File '" + strFileName + "' was inuse for " + ((intSleepTime / 1000) * intWaitMax) + " seconds.");
                                            break;
                                        }
                                        IsInUse = false;
                                    } while (IsFileReady(strFullFileName) == false);
                                }

                                //Copy file to strDestinationDir
                                if (!IsInUse)
                                {
                                    //http://www.bearnakedcode.com/2016/09/multi-threaded-asynchronous-file-copy.html
                                    //https://www.codeproject.com/Questions/1063313/How-to-copy-files-asynchronously-async-await-Sourc
                                    try
                                    {
                                        File.Copy(strFullFileName, strSubFolder + "\\" + strFileName);

                                        WriteAnEntry("Copied file '" + strFileName + "' to '" + strSubFolder + "\\'.");
                                    }
                                    catch
                                    {
                                        WriteAnEntry("File '" + strFileName + "' could not be copied, is it in use or does it exist already? ");
                                    }
                                }

                                break;      //No need to check the other SubDirectories.
                            }
                        }
                    }

                    strLastFileChecked = strFileName;
                }
                catch (Exception ex)
                {
                    WriteAnEntry("Error copying file: " + ex.Message);
                }
            }
        }

        private void CheckForUpdates()
        {
            //Check the Source dir for files, including sub Dirs.
            List<string> objFileList = new List<string>();
            objFileList = Directory.GetFiles(strSourceDir, "*.*", SearchOption.AllDirectories).ToList();

            foreach (string strFile in objFileList)
            {
                if (strFile != null)
                {
                    CheckFileAndCopy(strFile);
                }
            }
        }

        private void CreateEventLog()
        {
            string strEventSourceName = "TorrentFileCopier";
            string strLogName = "TFC Logs";

            objEventLog = new System.Diagnostics.EventLog();

            //Sample from the Designer:
            //this.eventLog1.Log = "Application";
            //this.eventLog1.Source = "TorrentFileCopier";

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
            objFSW.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.Attributes;

            //Add event handlers.
            objFSW.Changed += new FileSystemEventHandler(FSW_Change);
            objFSW.Created += new FileSystemEventHandler(FSW_Change);
            //objFSW.Deleted += new FileSystemEventHandler(FSW_Change);
            objFSW.Renamed += new RenamedEventHandler(FSW_Change);

            //Begin watching.
            objFSW.EnableRaisingEvents = true;

            WriteAnEntry("Watching dir '" + objFSW.Path.ToString() + "' for changes to '" + objFSW.Filter.ToString() + "' files.");
        }

        private void CreateTimer()
        {
            // Set up a timer to trigger every minute.  
            objTimer = new System.Timers.Timer();
            //objTimer.Interval = 60000;     // 60 seconds
            objTimer.Interval = (intTimerDelay * 1000);     //To convert desired sec to the required msec
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

            if ((e.ChangeType == WatcherChangeTypes.Deleted) || (strLastFileChecked == strFileName))
            {
                //We don't care about files that get Deleted.
                //
                //Multiple Events fire when a file is copied, we only need to handle copying the file once.
                return;
            }

            WriteAnEntry("File '" + strFileName + "' was '" + e.ChangeType.ToString() + "'.");

            //Copy file if it is a matching type we want
            CheckFileAndCopy(strFullFileName);
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
            WriteAnEntry("\t TorrentFileCopier timer event triggered (" + (objTimer.Interval / 1000).ToString() + " sec)");

            CheckForUpdates();
        }

        private void WriteAnEntry(string inMessage)
        {
            //Log to File
            if (bLogToFile)
            {
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

            //Log to system Event Log
            if (bLogToSystem)
            {
                try
                {
                    objEventLog.WriteEntry(inMessage, EventLogEntryType.Information, intEventId++);
                    //Maybe we try using "eventLog1"


                    ////http://www.itprotoday.com/microsoft-visual-studio/how-build-folder-watcher-service-c
                    //string message = inMessage;
                    //string eventSource = "Torrent File Copier";
                    //DateTime dt = new DateTime();
                    //dt = System.DateTime.UtcNow;
                    //message = dt.ToLocalTime() + ": " + message;
                    //EventLog.WriteEntry(eventSource, message);
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
