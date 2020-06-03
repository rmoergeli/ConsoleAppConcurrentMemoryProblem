using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleAppConcurrentMemoryProblem.Helpers
{
    class BiggestFolder
    {
        private static bool queueing = true;
        private static bool queueprocessed = false;
        private static List<string> excludedDirectories = new List<string>();
        private static ConcurrentQueue<FileInfo> FileInfoQueue = new ConcurrentQueue<FileInfo>();
        private static ConcurrentDictionary<string, long> FolderInformationList = new ConcurrentDictionary<string, long>();
        private static object _locker = new object();
        private const int maxFolderDepth = 5;

        internal static void Execute()
        {
            Run().Wait();

            // free managed resources
            FolderInformationList.Clear();
            FolderInformationList = null;
            excludedDirectories.Clear();
            excludedDirectories = null;
            FileInfoQueue.Clear();
            FileInfoQueue = null;
            GC.Collect();
        }

        private static async Task Run()
        {
            await Task.Run(() =>
            {
                excludedDirectories = GetExcludedDirectories();

                BackgroundWorker _fetchFilesBackgroundWorker = new BackgroundWorker();
                _fetchFilesBackgroundWorker.DoWork += _fetchFilesBackgroundWorker_DoWork;
                _fetchFilesBackgroundWorker.RunWorkerAsync();

                BackgroundWorker _processQueueBackgroundWorker = new BackgroundWorker();
                _processQueueBackgroundWorker.DoWork += _processQueueBackgroundWorker_DoWork;
                _processQueueBackgroundWorker.RunWorkerAsync();

                while (!queueprocessed)
                {
                    Task.Delay(500);
                }

                List<FolderInformation> listgrouped = new List<FolderInformation>();

                listgrouped = FolderInformationList.
                    Select(o => new FolderInformation { FolderFullName = o.Key, FolderSize = o.Value }).
                    OrderByDescending(o => o.FolderSize).ThenBy(o => o.FolderFullName).
                    Where(o => o.FolderSize > (1024 * 1024)).Take(3).ToList();

                if (listgrouped.Count > 0)
                {
                    foreach (var item in listgrouped)
                    {
                        string _folderName = $"{item.FolderFullName} [{item.FolderSize / 1024 / 1024 / 1024} GB]|";
                        Console.WriteLine(_folderName.Remove(_folderName.Length - 1)); // Add biggest folder information to log files
                    }
                }

                listgrouped.Clear();
                listgrouped = null;
            });
        }

        private static void _fetchFilesBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            RunAsync().Wait();
#if DEBUG
            Console.WriteLine($">>>>>>>>>>>>>>>>>>>>>> Queueing finished");
#endif
            queueing = false;
        }

        private static async Task RunAsync()
        {
            List<FileInfo> di = new List<FileInfo>();

            IEnumerable<DirectoryInfo> subdi = new DirectoryInfo[] { new DirectoryInfo(@"C:\") };

            //await foreach (var files in LoadFilesAsync(subdi, "*", SearchOption.AllDirectories))
            //{
            //    foreach (FileInfo file in files)
            //    {
            //        if (DirectoryIsNotExcluded(file.FullName))
            //        {
            //            FileInfoQueue.Enqueue(file);
            //        }
            //    }
            //}

            SemaphoreSlim semaphoreSlim = new SemaphoreSlim(4, 4);
            await foreach (var files in LoadFilesAsync(subdi, "*", SearchOption.AllDirectories))
            {
                files.AsParallel().AsOrdered().ForAll(file =>
                {
                    try
                    {
                        semaphoreSlim.Wait();

                        if (DirectoryIsNotExcluded(file.FullName))
                        {
                            FileInfoQueue.Enqueue(file);
                        }
                    }
                    finally
                    {
                        semaphoreSlim.Release();
                    }
                });
            }
        }

        private static void _processQueueBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (queueing || FileInfoQueue.Count() > 0)
            {
                //FileInfo file = null;
                //FileInfoQueue.TryDequeue(out file);

                //if (file != null)
                //{
                //    string _genuineFolderFullName = System.IO.Path.GetDirectoryName(file.FullName);

                //    if (!_genuineFolderFullName.Equals(GlobalSettings.systemdrive.Name))
                //    {
                //        string _folderFullName = _genuineFolderFullName;

                //        if (_genuineFolderFullName.Split('\\').Count() > maxFolderDepth)
                //        {
                //            _folderFullName = _genuineFolderFullName.Substring(0, FindNthOccur(_genuineFolderFullName, '\\', maxFolderDepth));
                //        }

                //        lock (_locker)
                //        {
                //            long _fn;
                //            if (FolderInformationList.TryGetValue(_folderFullName, out _fn))
                //            {
                //                FolderInformationList[_folderFullName] = _fn + file.Length;
                //            }
                //            else
                //            {
                //                FolderInformationList.TryAdd(_folderFullName, file.Length);
                //            }
                //        }
                //    }
                //}

                SemaphoreSlim semaphoreSlim = new SemaphoreSlim(4, 4);
                FileInfoQueue.AsParallel().AsOrdered().ForAll(item =>
                {
                    try
                    {
                        semaphoreSlim.Wait();

                        FileInfo file = null;
                        FileInfoQueue.TryDequeue(out file);

                        if (file != null)
                        {
                            string _genuineFolderFullName = System.IO.Path.GetDirectoryName(file.FullName);

                            if (!_genuineFolderFullName.Equals(GlobalSettings.systemdrive.Name) && DirectoryIsNotExcluded(file.FullName))
                            {
                                string _folderFullName = _genuineFolderFullName;

                                if (_genuineFolderFullName.Split('\\').Count() > maxFolderDepth)
                                {
                                    _folderFullName = _genuineFolderFullName.Substring(0, FindNthOccur(_genuineFolderFullName, '\\', maxFolderDepth));
                                }

                                lock (_locker)
                                {
                                    long _fn;
                                    if (FolderInformationList.TryGetValue(_folderFullName, out _fn))
                                    {
                                        FolderInformationList[_folderFullName] = _fn + file.Length;
                                    }
                                    else
                                    {
                                        FolderInformationList.TryAdd(_folderFullName, file.Length);
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        semaphoreSlim.Release();
                    }
                });
#if DEBUG
                //Trace.WriteLine($"FileInfoQueue: {FileInfoQueue.Count()} | FolderInformationList: {FolderInformationList.Count}");
                //Trace.WriteLine($">>>>>>>>>>>>>>>>>>>>>> Queueing: {queueing}");
#endif
            }
#if DEBUG
            Console.WriteLine($"FileInfoQueue: {FileInfoQueue.Count()}");
#endif
            queueprocessed = true;
        }

        private static int FindNthOccur(string str, char ch, int maxFolderDepth)
        {
            int occur = 0;

            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == ch)
                {
                    occur += 1;
                }
                if (occur == maxFolderDepth)
                    return i;
            }
            return -1;
        }

        private static bool DirectoryIsNotExcluded(string fullName)
        {
            bool directoryIsNotExcluded = true;

            foreach (var excludedDir in excludedDirectories)
            {
                if (fullName.StartsWith(excludedDir))
                {
                    return false;
                }
            }

            return directoryIsNotExcluded;
        }

        private static List<string> GetExcludedDirectories()
        {
            List<string> gethiddenDirectories = new List<string>();

            gethiddenDirectories.Add($@"{GlobalSettings.systemdrive}ESD");
            gethiddenDirectories.Add($@"{GlobalSettings.systemdrive}PerfLogs");
            gethiddenDirectories.Add($@"{GlobalSettings.systemdrive}Program Files");
            gethiddenDirectories.Add($@"{GlobalSettings.systemdrive}Program Files (x86)");
            gethiddenDirectories.Add($@"{GlobalSettings.systemdrive}ProgramData");
            gethiddenDirectories.Add($@"{GlobalSettings.systemdrive}Users\Administrator");
            gethiddenDirectories.Add($@"{GlobalSettings.systemdrive}Users\All Users");
            gethiddenDirectories.Add($@"{GlobalSettings.systemdrive}Users\Public");
            gethiddenDirectories.Add($@"{GlobalSettings.systemdrive}Windows");
            gethiddenDirectories.Add($@"{GlobalSettings.systemdrive}Windows.old");
            gethiddenDirectories.Add($@"{GlobalSettings.systemdrive}WINNT");

            return gethiddenDirectories;
        }

        private static async IAsyncEnumerable<IEnumerable<FileInfo>> LoadFilesAsync(IEnumerable<DirectoryInfo> directories, string searchPattern, SearchOption searchOption)
        {
            var options = new EnumerationOptions() { IgnoreInaccessible = true, BufferSize = 16384 };

            if (searchOption == SearchOption.AllDirectories)
            {
                options.RecurseSubdirectories = true;
            }

            var dirs = directories.Where(dir => dir.Exists).Select(dir => dir);

            foreach (var directory in dirs)
            {
                var files = await Task.Run(() =>
                            directory.EnumerateFiles(searchPattern, options));
                yield return files;
            }
        }
    }
}
