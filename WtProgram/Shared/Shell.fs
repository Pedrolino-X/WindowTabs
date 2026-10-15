namespace Bemo
open System
open System.IO

module Shell =
    // Read only on an explicit launch request; never enumerate Explorer on a
    // move/paint/timer. Return a string so no COM objects escape this scope.
    let tryExplorerPath (hwnd:IntPtr) =
        let release (value:obj) =
            if value <> null && Runtime.InteropServices.Marshal.IsComObject(value) then
                Runtime.InteropServices.Marshal.ReleaseComObject(value) |> ignore
        try
            let windows = SHDocVw.ShellWindowsClass()
            try
                let mutable result = None
                let mutable index = 0
                while index < windows.Count && result.IsNone do
                    let item = windows.Item(box index)
                    try
                        match item with
                        | :? SHDocVw.IWebBrowser2 as browser when int64 browser.HWND = hwnd.ToInt64() ->
                            let tabHwnd = ExplorerBrowser.GetTabWindow(item)
                            // Win11 exposes native tabs sharing a top-level HWND.
                            // Skip the hidden views instead of taking the first.
                            if tabHwnd = IntPtr.Zero || WinUserApi.IsWindowVisible(tabHwnd) then
                                let document = browser.Document
                                try
                                    match document with
                                    | :? Shell32.IShellFolderViewDual as view ->
                                        let folder = view.Folder
                                        try
                                            let self = (folder :?> Shell32.Folder2).Self
                                            try
                                                if not(String.IsNullOrEmpty(self.Path)) then result <- Some(self.Path)
                                            finally release self
                                        finally release folder
                                    | _ -> ()
                                finally release document
                        | _ -> ()
                    finally release item
                    index <- index + 1
                result
            finally release windows
        with ex ->
            Diagnostics.Debug.WriteLine(sprintf "Explorer folder query hwnd=%A: %O" hwnd ex)
            None

    let explorerLaunchArguments hwnd =
        match tryExplorerPath hwnd with
        | Some(path) ->
            // Explorer's /n requests a separate top-level window. Preserve
            // Unicode, UNC paths and virtual folders (::{CLSID}).
            // Explorer parses this switch itself; doubling a root's trailing
            // slash (CRT argument quoting) incorrectly opens the default folder.
            "/n,\"" + path + "\""
        | None -> "/n"

    let getShellFolder (hwnd:IntPtr) =
        let shellWindows = SHDocVw.ShellWindowsClass()
        let windows = List2(Seq.fromEnumerable shellWindows)
        windows.tryPick <| fun window ->
            match window with
            | :? SHDocVw.ShellBrowserWindow as shellWindow -> 
                let folderView = shellWindow.Document :?> Shell32.IShellFolderViewDual
                if shellWindow.HWND = int(hwnd) then
                    Some(folderView.Folder)
                else
                    None
            | _ -> None
