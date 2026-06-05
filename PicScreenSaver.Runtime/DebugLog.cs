using System;
using System.Diagnostics;
using System.IO;

namespace PicScreenSaver.Runtime
{
    /// <summary>
    /// 调试日志——写入桌面，排查进程残留问题
    /// </summary>
    public static class DebugLog
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"PicSaver_{Process.GetCurrentProcess().Id}_{DateTime.Now:HHmmss}.log");

        private static readonly object _lock = new object();

        static DebugLog()
        {
            Write($"PID={Process.GetCurrentProcess().Id} 启动");
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
                Write("ProcessExit 事件触发");
        }

        public static void Write(string msg)
        {
            lock (_lock)
            {
                File.AppendAllText(LogPath,
                    $"{DateTime.Now:HH:mm:ss.fff} [{ThreadId()}] {msg}{Environment.NewLine}");
            }
        }

        private static int ThreadId()
        {
            return System.Threading.Thread.CurrentThread.ManagedThreadId;
        }
    }

    public static class LogExit
    {
        /// <summary>强制退出并写日志</summary>
        public static void Kill(string reason)
        {
            DebugLog.Write($"KILL: {reason}");
            System.Threading.Thread.Sleep(100); // 等文件写入
            Environment.Exit(0);
        }
    }
}
