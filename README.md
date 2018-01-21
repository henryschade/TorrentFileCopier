Torrent File Copier (aka TFC)

Is a windows service that monitors a specified directory (using FileSystemWatcher), and copies files to a specified directory, 
and into sub directories based on the file name, if the file does not exist already.

This is the first C# windows service I have done.  I used 4 different "how to" guides for guidance, and took the parts of each 
that I liked.

Right now (21 Jan 2018) it logs to a text file (specified in the code), but has the code to also log to the 
Event Viewer (not working 100% so turned off).  There are variables to turn each type of logging on and off.

There is Timer code, but currently not used, the routine that does the instantiating and starting is never called.  From the beginning 
I wanted/planned to make TFC use FileSystemWatcher, but one of the how to's used a timer, and I figured it would not hurt to play 
with one a bit while developing and testing.

My TODO's and wish lists for TFC are:
* Tray icon/access to configure the source and destination directories
* Get Event Log logging working
* Update torrent client labels of files copied (not sure if this can happen)
* Do the file copy in another thread or asynchronously
* Add an installer project to the solution
* Add UnRAR/UnZip capabilities
* When logging to a text file, monitor the file size, and clean up as needed

