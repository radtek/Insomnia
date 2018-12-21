using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Modules
{
    class LogFileSweeper : ModuleBase
    {
        int count;

        List<FileSystemWatcher> watchmen;

        protected override void OnActivate(ServiceContext ctx)
        {
            watchmen = new List<FileSystemWatcher>();

            count = int.Parse(ctx.Config.ReadKey("config", "logcount", "5"));
        }

        protected override void OnStart()
        {
            foreach (ModuleBase module in Service.Modules)
            {
                if (module is ISweepable)
                {
                    DirectoryInfo directory = (module as ISweepable).WatchDirectory;

                    if (directory?.Exists ?? false)
                    {
                        AddWatcher(new FileSystemWatcher(directory.FullName));
                    }
                }
            }
        }

        protected override void OnShutdown()
        {
            foreach (FileSystemWatcher watcher in watchmen.ToArray())
                RemoveWatcher(watcher);
        }

        private void SweepFileSystem(FileSystemInfo[] infos, int retainCount)
        {
            var entries = new List<FileSystemInfo>();
            foreach (FileSystemInfo info in infos)
            {
                info.Refresh();
                entries.Add(info);
            }

            if (entries.Count > retainCount)
            {
                entries.Sort((a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));

                foreach (FileSystemInfo info in entries.GetRange(retainCount, entries.Count - retainCount))
                {
                    if (info is DirectoryInfo)
                        ((DirectoryInfo)info).Delete(true);
                    else
                        info.Delete();
                }
            }
        }

        #region FileSystemWatcher
        private void AddWatcher(FileSystemWatcher watcher)
        {
            watcher.Created += Watcher_Created;
            watcher.Error += Watcher_Error;
            watcher.EnableRaisingEvents = true;

            watchmen.Add(watcher);
        }
        private void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            DirectoryInfo directory = new DirectoryInfo((sender as FileSystemWatcher).Path);

            FileSystemInfo[] infos = directory.GetFiles();
            if (infos.Length == 0)
                infos = directory.GetDirectories();
            SweepFileSystem(infos, count);
        }
        private void Watcher_Error(object sender, ErrorEventArgs e)
        {
            EventLog.WriteEntry(e.ToString(), EventLogEntryType.Error);

            RemoveWatcher((FileSystemWatcher)sender);
        }
        private void RemoveWatcher(FileSystemWatcher watcher)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Error -= Watcher_Error;
            watcher.Created -= Watcher_Created;
            watcher.Dispose();

            watchmen.Remove(watcher);
        }
        #endregion

        public interface ISweepable
        {
            DirectoryInfo WatchDirectory { get; }
        }
    }
}