using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Bemo;

class ExplorerLaunch
{
    static HashSet<IntPtr> owned = new HashSet<IntPtr>();
    static void Release(object value) { if(value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    static IntPtr WaitFor(string path, HashSet<IntPtr> exclude)
    {
        var watch = Stopwatch.StartNew();
        while(watch.ElapsedMilliseconds < 15000)
        {
            var windows = new SHDocVw.ShellWindowsClass();
            try
            {
                for(int i=0;i<windows.Count;i++)
                {
                    object item=windows.Item(i);
                    try
                    {
                        var browser=item as SHDocVw.IWebBrowser2;
                        if(browser == null) continue;
                        var hwnd=new IntPtr(browser.HWND);
                        if(exclude.Contains(hwnd)) continue;
                        object document=browser.Document;
                        try
                        {
                            var view=document as Shell32.IShellFolderViewDual;
                            if(view == null) continue;
                            var folder=view.Folder;
                            try
                            {
                                var self=((Shell32.Folder2)folder).Self;
                                try
                                {
                                    if(String.Equals(self.Path,path,StringComparison.OrdinalIgnoreCase))
                                    { owned.Add(hwnd); return hwnd; }
                                }
                                finally { Release(self); }
                            }
                            finally { Release(folder); }
                        }
                        finally { Release(document); }
                    }
                    finally { Release(item); }
                }
            }
            finally { Release(windows); }
            Thread.Sleep(150);
        }
        throw new Exception("Explorer launch did not create a distinct window at the requested fixture path");
    }
    static void Start(string args)
    {
        using(var process=Process.Start(new ProcessStartInfo("explorer.exe",args) { UseShellExecute=true })) {}
    }
    static HashSet<IntPtr> Snapshot()
    {
        var result=new HashSet<IntPtr>();
        var windows=new SHDocVw.ShellWindowsClass();
        try
        {
            for(int i=0;i<windows.Count;i++)
            {
                object item=windows.Item(i);
                try { var browser=item as SHDocVw.IWebBrowser2; if(browser!=null) result.Add(new IntPtr(browser.HWND)); }
                finally { Release(item); }
            }
        }
        finally { Release(windows); }
        return result;
    }
    [STAThread] static int Main()
    {
        string root=Path.GetFullPath(Path.Combine("Tests", "Explorer 新建测试 " + DateTime.Now.ToString("HHmmss")));
        string first=Path.Combine(root,"当前 文件夹");
        string second=Path.Combine(root,"右侧 文件夹");
        Directory.CreateDirectory(first); Directory.CreateDirectory(second);
        try
        {
            Start("/n,\""+first+"\"");
            var a=WaitFor(first,new HashSet<IntPtr>());
            Start("/n,\""+second+"\"");
            var b=WaitFor(second,new HashSet<IntPtr>());
            // Explicit source HWND, independent of the last-created window.
            var path=Shell.tryExplorerPath(a);
            if(path == null || path.Value != first) throw new Exception("source folder query failed");
            string args=Shell.explorerLaunchArguments(a);
            Start(args);
            var duplicate=WaitFor(first,new HashSet<IntPtr>{a,b});
            if(duplicate==a || duplicate==b) throw new Exception("Explorer reused a source window");
            Console.WriteLine("PASS Explorer: explicit source HWND, Chinese/spaced folder path, separate same-folder window; newer/rightmost folder not used");
            foreach(string location in new [] { Path.GetPathRoot(root), "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}" })
            {
                var before=Snapshot();
                Console.WriteLine("Testing source: " + location);
                Start("/n,\""+location+"\"");
                var source=WaitFor(location,before);
                before=Snapshot();
                var launchArgs=Shell.explorerLaunchArguments(source);
                Console.WriteLine("Testing duplicate arguments: " + launchArgs);
                Start(launchArgs);
                WaitFor(location,before);
            }
            Console.WriteLine("PASS Explorer: drive root trailing slash and This PC virtual folder both duplicate into distinct windows");
            return 0;
        }
        catch(Exception e) { Console.WriteLine(e); return 1; }
        finally
        {
            foreach(var hwnd in owned) WinUserApi.PostMessage(hwnd,WindowMessages.WM_CLOSE,IntPtr.Zero,IntPtr.Zero);
        }
    }
}
