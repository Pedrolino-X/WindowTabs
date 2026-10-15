module GroupLifecycle
open System
open System.Drawing
open System.Diagnostics
open System.Reflection
open System.Runtime.Remoting.Proxies
open System.Runtime.Remoting.Messaging
open System.Windows.Forms
open Bemo

type Stub(t:Type, values:Collections.Generic.Dictionary<string,obj>) =
    inherit RealProxy(t)
    override this.Invoke(message:IMessage) =
        let call=message :?> IMethodCallMessage
        let result=
            if call.MethodName="getValue" then
                match values.TryGetValue(string call.Args.[0]) with
                | true,v -> v
                | _ -> box false
            elif call.MethodName="getGroupPreference" then
                let name,key = if call.Args.Length=1 then unbox<string*string> call.Args.[0] else string call.Args.[0],string call.Args.[1]
                match values.TryGetValue("group:"+name+":"+key) with
                | true,v -> box(Some(string v))
                | _ -> null
            elif call.MethodName="setGroupPreference" then
                let name,key,value = if call.Args.Length=1 then unbox<string*string*string> call.Args.[0] else string call.Args.[0],string call.Args.[1],string call.Args.[2]
                values.["group:"+name+":"+key] <- box value
                null
            else
                let resultType=(call.MethodBase :?> MethodInfo).ReturnType
                if resultType=typeof<Void> then null
                elif resultType.IsValueType then Activator.CreateInstance(resultType)
                else null
        ReturnMessage(result,null,0,call.LogicalCallContext,call) :> IMessage

let run (appearance:TabAppearanceInfo) =
    let check condition message = if not condition then failwith message
    let values=Collections.Generic.Dictionary<string,obj>()
    for k,v in ["tabPositionByDefault",box "TopLeft";"hideTabsWhenDownByDefault",box "never";"tabAppearance",box appearance;"enableTabbingByDefault",box true;"includedPaths",box(Set2<string>());"excludedPaths",box(Set2<string>())] do values.[k] <- v
    Services.register<ISettings>((Stub(typeof<ISettings>,values).GetTransparentProxy() :?> ISettings),false)
    Services.register<IProgram>((Stub(typeof<IProgram>,values).GetTransparentProxy() :?> IProgram),false)
    let info=ProcessStartInfo("Tests/GroupFixture.exe")
    info.UseShellExecute <- false
    info.CreateNoWindow <- true
    info.RedirectStandardOutput <- true
    use child=Process.Start(info)
    let handles=child.StandardOutput.ReadLine().Split(',') |> Array.map(fun s->IntPtr(Int64.Parse s))
    let os=OS()
    let pump milliseconds =
        let watch=Stopwatch.StartNew()
        while watch.ElapsedMilliseconds<int64 milliseconds do
            Application.DoEvents()
            Threading.Thread.Sleep(5)
    try
        let filter=FilterService()
        let probe=os.windowFromHwnd(handles.[0])
        check (not(filter.isTabbableWindow(probe))) "off-screen probe unexpectedly discoverable"
        check (filter.isRetainableWindow(probe)) "parked existing window would be removed"
        probe.hide()
        check (not(filter.isRetainableWindow(probe))) "tray-hidden window should still be removable"
        probe.showNoActivate()
        values.["enableTabbingByDefault"] <- box false
        check (not(filter.isRetainableWindow(probe))) "explicit process exclusions ignored"
        values.["enableTabbingByDefault"] <- box true
        printfn "PASS real external off-screen window: discovery rejects it, group retention preserves it; hidden/excluded windows remain removable"
        let plugin=SuperBarPlugin()
        let group=WindowGroup(false,List2([plugin :> IPlugin]))
        let monitor=
            {new ITabStripMonitor with
                member x.tabClick _=()
                member x.tabActivate tab=group.tabActivate(tab,true)
                member x.tabClose _=()
                member x.tabPin _=()
                member x.tabMoved(_,_) = ()
                member x.windowMsg _=()}
        let strip=TabStrip(monitor)
        group.init(strip)
        strip.setPlacement({showInside=false;bounds=Rect(Pt(-20000,-20000),Sz(360,26));scale=1.0})
        let windows=handles.[1..]
        try
            let work=Screen.PrimaryScreen.WorkingArea
            let bounds=Rect(Pt(work.Right-370,work.Bottom-250),Sz(360,240))
            for hwnd in windows do (os.windowFromHwnd hwnd).move(bounds)
            for hwnd in windows do group.addWindow(hwnd,false)
            pump 100
            if not (Environment.GetCommandLineArgs() |> Array.contains "--modal-only") then
                for cycle in 1..5 do
                    group.onEnterMoveSize()
                    group.onExitMoveSize()
                    pump 75
                    check (group.windows.count=3 && strip.visualOrder.length=3) "resize lost a group member"
                    (os.windowFromHwnd windows.[cycle%3]).minimize()
                    pump 150
                    check (windows |> Array.forall(fun h->(os.windowFromHwnd h).isMinimized)) "group did not minimize all windows"
                    let target=windows.[cycle%3]
                    (os.windowFromHwnd target).restore()
                    pump 200
                    printfn "Cycle %d restored=%A minimized=%A" cycle target (windows |> Array.map(fun h->(os.windowFromHwnd h).isMinimized))
                    check (group.windows.count=3 && strip.visualOrder.length=3) "minimize/restore lost tabs"
                    check (windows |> Array.forall(fun h->not((os.windowFromHwnd h).isMinimized))) "siblings remained minimized"
                    check (windows |> Array.forall(fun h->os.isOnScreen((os.windowFromHwnd h).bounds))) "restored sibling stayed parked off-screen"
                printfn "PASS 3 actual external windows: resize and minimize/restore cycles preserve all members and restore every sibling"
                let flags=BindingFlags.Instance ||| BindingFlags.NonPublic
                let field=typeof<SuperBarPlugin>.GetField("taskbarButton",flags)
                let order=strip.visualOrder.list
                group.isSuperBarEnabled <- true
                pump 100
                let button=field.GetValue(plugin) :?> TaskBarButton
                check (not(isNull button) && (os.windowFromHwnd button.window.hwnd).isWindow) "combined taskbar button not created"
                let proxy=button.window.hwnd
                for hwnd in windows do
                    let tab=button.tabWindow(hwnd.ToInt64().ToString())
                    check (os.windowFromHwnd(tab.hwnd)).isWindow "taskbar tab proxy missing"
                (os.windowFromHwnd windows.[0]).minimize()
                pump 150
                let selected=button.tabWindow(windows.[2].ToInt64().ToString())
                let mutable result=IntPtr.Zero
                WinUserApi.SendMessageTimeout(selected.hwnd,WindowMessages.WM_ACTIVATE,IntPtr(ActivateStateValues.WA_ACTIVE),IntPtr.Zero,3,1000,&result) |> ignore
                pump 200
                check (windows |> Array.forall(fun h->not((os.windowFromHwnd h).isMinimized))) "taskbar preview activation did not restore all siblings"
                check (group.zorder.value.head=windows.[2]) "preview activation selected the wrong member"
                group.removeWindow(windows.[2])
                pump 50
                group.addWindow(windows.[2],false)
                pump 75
                check (group.windows.count=3 && (os.windowFromHwnd(button.tabWindow(windows.[2].ToInt64().ToString()).hwnd)).isWindow) "add/remove while merged lost a taskbar tab"
                group.isSuperBarEnabled <- false
                pump 50
                check (isNull(field.GetValue(plugin)) && not((os.windowFromHwnd proxy).isWindow)) "combined button not released"
                check (strip.visualOrder.list=order && group.windows.count=3) "taskbar toggle rebuilt or reordered group"
                printfn "PASS live taskbar merge/unmerge creates and releases proxies without changing group membership or visual order"
                let pinned = os.windowFromHwnd(windows.[0])
                pinned.makeTopMost()
                pump 75
                for cycle in 1..12 do
                    let target = windows.[cycle % 3]
                    group.tabActivate(Tab(target),true)
                    pump 100
                    let ranks = os.windowZorders
                    let rank h = ranks.tryFind(h).def(Int32.MaxValue)
                    check (windows |> Array.forall(fun h -> h=target || rank target < rank h)) "topmost sibling covers selected tab"
                    check (group.zorder.value.head=target && strip.visualOrder.length=3) "topmost switching lost active tab or membership"
                    check ((os.windowFromHwnd target).isTopMost = (target=windows.[0])) "activation changed the window's requested topmost state"
                    if target=windows.[0] then
                        check (os.windowFromHwnd strip.hwnd).isTopMost "strip fell below active topmost window"
                group.tabActivate(Tab(windows.[1]),true)
                pump 100
                check (not pinned.isTopMost) "inactive pinned member was not suspended"
                let secondPinned = os.windowFromHwnd(windows.[1])
                secondPinned.makeTopMost()
                for target in [windows.[0]; windows.[1]; windows.[2]; windows.[0]] do
                    group.tabActivate(Tab(target),true)
                    pump 100
                    check (group.zorder.value.head=target) "multiple topmost windows block switching"
                    check ((os.windowFromHwnd target).isTopMost = (target<>windows.[2])) "multiple topmost requests not preserved"
                pinned.minimize()
                pump 150
                pinned.restore()
                pump 250
                group.tabActivate(Tab(windows.[2]),true)
                pump 150
                printfn "Topmost restore: selected=%A actual=%A topmost=%A" windows.[2] group.zorder.value.head (windows |> Array.map(fun h -> (os.windowFromHwnd h).isTopMost))
                check (group.zorder.value.head=windows.[2] && not((os.windowFromHwnd windows.[2]).isTopMost)) "topmost restore contaminated normal member or overrode tab choice"
                group.tabActivate(Tab(windows.[1]),true)
                pump 100
                check secondPinned.isTopMost "second topmost request lost after restore"
                secondPinned.makeNotTopMost()
                group.tabActivate(Tab(windows.[2]),true)
                group.tabActivate(Tab(windows.[1]),true)
                pump 100
                check (not secondPinned.isTopMost) "explicit removal of active topmost was undone"
                group.removeWindow(windows.[0])
                pump 100
                check pinned.isTopMost "detach did not restore requested topmost"
                group.addWindow(windows.[0],false)
                pump 100
                group.tabActivate(Tab(windows.[2]),true)
                pump 100
                check (group.zorder.value.head=windows.[2] && not pinned.isTopMost) "reattached topmost member blocks switching"
                group.removeWindow(windows.[0])
                pump 75
                check pinned.isTopMost "second detach lost topmost state"
                pinned.makeNotTopMost()
                printfn "PASS topmost member: 12 native tab switches, strip layering, detach/reattach and original topmost restoration"
                group.addWindow(windows.[0],false)
                group.tabActivate(Tab(windows.[0]),true)
                pump 100
                group.minimizeBackgroundTabs <- true
                pump 400
                let onlyActive target =
                    printfn "Background mode active=%A minimized=%A" target (windows |> Array.map(fun h -> (os.windowFromHwnd h).isMinimized))
                    check (group.windows.count=3 && strip.visualOrder.length=3) "background mode lost membership"
                    check (windows |> Array.forall(fun h -> (os.windowFromHwnd h).isMinimized = (h<>target))) "background mode restored siblings or minimized active window"
                    check (group.zorder.value.head=target) "background mode selected wrong window"
                onlyActive windows.[0]
                let positions hwnd =
                    let mutable result=IntPtr.Zero
                    WinUserApi.SendMessageTimeout(hwnd,0x8042,IntPtr.Zero,IntPtr.Zero,3,1000,&result) |> ignore
                    result.ToInt32()
                let mutable followedPositions = 0
                group.bounds.changed.Add(fun() -> followedPositions <- followedPositions + 1)
                let before = windows.[1..] |> Array.map positions
                let movedBounds=Rect(Pt(work.Right-420,work.Bottom-310),Sz(390,260))
                group.onEnterMoveSize()
                for i in 1..100 do
                    (os.windowFromHwnd windows.[0]).move(movedBounds.move(i%20,0))
                    pump 5
                group.onExitMoveSize()
                pump 200
                let after = windows.[1..] |> Array.map positions
                check (before=after) "drag sent positioning messages to parked background windows"
                printfn "FOLLOW: %d group-position updates for 100 native moves" followedPositions
                check (followedPositions >= 40) "live drag still capped by idle 50ms throttle"
                onlyActive windows.[0]
                let finalBounds=(os.windowFromHwnd windows.[0]).bounds
                for cycle in 1..12 do
                    let target=windows.[cycle%3]
                    group.tabActivate(Tab(target),true)
                    pump 180
                    onlyActive target
                    check (os.foreground.hwnd=target) (sprintf "background activation focus: expected=%A actual=%A" target os.foreground.hwnd)
                    check ((os.windowFromHwnd target).bounds=finalBounds) "lazy activation did not apply latest group bounds"
                for cycle in 1..30 do group.tabActivate(Tab(windows.[cycle%3]),true)
                pump 400
                onlyActive windows.[0]
                (os.windowFromHwnd windows.[0]).minimize()
                pump 200
                check (windows |> Array.forall(fun h -> (os.windowFromHwnd h).isMinimized)) "user group minimize failed"
                (os.windowFromHwnd windows.[2]).restore()
                pump 250
                onlyActive windows.[2]
                group.removeWindow(windows.[1])
                pump 200
                check (not((os.windowFromHwnd windows.[1]).isMinimized)) "detached background window stayed minimized"
                let joinBounds = (os.windowFromHwnd windows.[2]).bounds
                (os.windowFromHwnd windows.[1]).move(joinBounds.move(-60,-45))
                (os.windowFromHwnd windows.[1]).setForeground(true)
                group.addWindow(windows.[1],false)
                pump 200
                onlyActive windows.[1]
                check ((os.windowFromHwnd windows.[1]).bounds=joinBounds) "new foreground member kept its cascade offset"
                check (group.placementBounds=joinBounds) "new foreground member changed group anchor"
                printfn "PASS new foreground member joins at existing group bounds (no cascade offset)"
                group.tabActivate(Tab(windows.[2]),true)
                pump 200
                onlyActive windows.[2]
                group.minimizeBackgroundTabs <- false
                pump 250
                check (windows |> Array.forall(fun h -> not((os.windowFromHwnd h).isMinimized))) "disabling background mode did not restore windows"
                printfn "PASS background mode: 100 drag steps with ZERO background positioning messages; 12 switches, current bounds, group minimize/restore, detach/rejoin and disable"
                group.minimizeBackgroundTabs <- true
                group.isSuperBarEnabled <- true
                pump 200
                let merged=field.GetValue(plugin) :?> TaskBarButton
                let proxyTab=merged.tabWindow(windows.[1].ToInt64().ToString())
                WinUserApi.SendMessageTimeout(proxyTab.hwnd,WindowMessages.WM_ACTIVATE,IntPtr(ActivateStateValues.WA_ACTIVE),IntPtr.Zero,3,1000,&result) |> ignore
                pump 200
                onlyActive windows.[1]
                (os.windowFromHwnd windows.[1]).showWindow(ShowWindowCommands.SW_SHOWMAXIMIZED)
                pump 150
                group.tabActivate(Tab(windows.[0]),true)
                pump 200
                onlyActive windows.[0]
                check (os.windowFromHwnd windows.[0]).isMaximized "background switch lost maximized state"
                group.isSuperBarEnabled <- false
            group.minimizeBackgroundTabs <- true
            // Generic owner -> editor -> modal dialog; no Office dependency.
            let send h message value =
                let mutable answer=IntPtr.Zero
                WinUserApi.SendMessageTimeout(h,message,value,IntPtr.Zero,3,1000,&answer) |> ignore
                answer
            (os.windowFromHwnd windows.[0]).restore()
            pump 150
            send windows.[1] 0x8043 windows.[0] |> ignore
            try
                group.tabActivate(Tab(windows.[1]),true)
                pump 250
                check (not((os.windowFromHwnd windows.[0]).isMinimized)) "active owned window's owner was parked"
                send windows.[1] 0x8044 IntPtr.Zero |> ignore
                pump 250
                let dialog=send windows.[1] 0x8045 IntPtr.Zero
                check (dialog<>IntPtr.Zero && not(WinUserApi.IsWindowEnabled(windows.[1]))) "modal fixture did not disable its owner"
                group.main(windows.[0],WinEvent.EVENT_SYSTEM_FOREGROUND)
                group.onExitMoveSize()
                pump 250
                check (os.foreground.hwnd=dialog) "queued owner event or move completion stole modal focus"
                check (group.topWindow=windows.[1] && group.foregroundTab=Some(Tab(windows.[1]))) "modal focus selected the root owner instead of nearest member"
                group.tabActivate(Tab(windows.[2]),true)
                pump 250
                group.tabActivate(Tab(windows.[1]),true)
                pump 250
                printfn "Modal return: target=%A foreground=%A active=%A ownerEnabled=%b dialogVisible=%b editorMinimized=%b lastPopup=%A" dialog os.foreground.hwnd group.topWindow (WinUserApi.IsWindowEnabled(windows.[1])) (WinUserApi.IsWindowVisible(dialog)) ((os.windowFromHwnd windows.[1]).isMinimized) (WinUserApi.GetLastActivePopup(windows.[1]))
                check (os.foreground.hwnd=dialog) "returning to a modal tab did not activate its dialog"
                printfn "PASS generic owner/editor/modal chain: owner stays restored, stale events ignored, dialog focus survives and tab return targets dialog"
            finally
                send windows.[1] 0x8046 IntPtr.Zero |> ignore
                send windows.[1] 0x8043 IntPtr.Zero |> ignore
                pump 150
            // The dialog can gain focus before discovery adds its editor.
            group.removeWindow(windows.[1])
            pump 150
            group.tabActivate(Tab(windows.[0]),true)
            pump 150
            send windows.[1] 0x8043 windows.[0] |> ignore
            send windows.[1] 0x8044 IntPtr.Zero |> ignore
            pump 200
            try
                let dialog=send windows.[1] 0x8045 IntPtr.Zero
                check (dialog<>IntPtr.Zero) "early modal fixture missing"
                group.addWindow(windows.[1],false)
                pump 250
                check (group.topWindow=windows.[1] && group.foregroundTab=Some(Tab(windows.[1])) && os.foreground.hwnd=dialog) "editor discovered after modal focus was parked or stole focus"
                printfn "PASS dialog gains focus before its editor is discovered; joining selects editor and preserves dialog focus"
            finally
                send windows.[1] 0x8046 IntPtr.Zero |> ignore
                send windows.[1] 0x8043 IntPtr.Zero |> ignore
                pump 150
            group.minimizeBackgroundTabs <- false
            pump 250
            group.tabActivate(Tab(windows.[1]),true)
            send windows.[1] 0x8044 IntPtr.Zero |> ignore
            pump 250
            try
                let dialog=send windows.[1] 0x8045 IntPtr.Zero
                group.main(windows.[0],WinEvent.EVENT_SYSTEM_FOREGROUND)
                group.onExitMoveSize()
                pump 250
                check (dialog<>IntPtr.Zero && group.foregroundTab=Some(Tab(windows.[1])) && os.foreground.hwnd=dialog) "legacy mode lost modal foreground ownership"
                printfn "PASS modal foreground ownership with background parking disabled"
            finally
                send windows.[1] 0x8046 IntPtr.Zero |> ignore
                pump 150
            group.minimizeBackgroundTabs <- true
            group.windows.items.iter group.removeWindow
            pump 250
            check (windows |> Array.forall(fun h -> not((os.windowFromHwnd h).isMinimized))) "group teardown stranded parked windows"
            group.addWindow(windows.[0],false)
            group.rememberGroupPreference("MergeTaskbar","false")
            group.rememberGroupPreference("Alignment","TopCenter")
            group.removeWindow(windows.[0])
            group.addWindow(windows.[0],false)
            check (not group.isSuperBarEnabled && group.getTabAlign(windows.[0])=TopCenter) "same-program preference was not restored for a new group"
            let otherPath="Tests/GroupFixtureOther.exe"
            IO.File.Copy("Tests/GroupFixture.exe",otherPath,true)
            let otherInfo=ProcessStartInfo(otherPath,"2")
            otherInfo.UseShellExecute <- false
            otherInfo.RedirectStandardOutput <- true
            otherInfo.CreateNoWindow <- true
            use other=Process.Start(otherInfo)
            let otherHandles=other.StandardOutput.ReadLine().Split(',') |> Array.map(fun s->IntPtr(Int64.Parse s))
            try
                (os.windowFromHwnd otherHandles.[1]).move(bounds)
                group.addWindow(otherHandles.[1],false)
                let saved=values |> Seq.filter(fun kv -> kv.Key.StartsWith("group:")) |> Seq.map(fun kv -> kv.Key,kv.Value) |> Map.ofSeq
                group.rememberGroupPreference("MergeTaskbar","true")
                group.rememberGroupPreference("Alignment","TopRight")
                let after=values |> Seq.filter(fun kv -> kv.Key.StartsWith("group:")) |> Seq.map(fun kv -> kv.Key,kv.Value) |> Map.ofSeq
                check (saved=after) "mixed-program group overwrote a program preference"
                printfn "PASS group preferences: single-program merge/alignment restored; mixed-program edits do not overwrite program rules"
            finally
                group.windows.items.iter group.removeWindow
                pump 150
                for h in otherHandles do (os.windowFromHwnd h).close()
                if not(other.WaitForExit(5000)) then other.Kill()
            if not (Environment.GetCommandLineArgs() |> Array.contains "--modal-only") then
                let stressInfo=ProcessStartInfo("Tests/GroupFixture.exe","33")
                let positions hwnd =
                    let mutable result=IntPtr.Zero
                    WinUserApi.SendMessageTimeout(hwnd,0x8042,IntPtr.Zero,IntPtr.Zero,3,1000,&result) |> ignore
                    result.ToInt32()
                stressInfo.UseShellExecute <- false
                stressInfo.CreateNoWindow <- true
                stressInfo.RedirectStandardOutput <- true
                use stress=Process.Start(stressInfo)
                let allStress=stress.StandardOutput.ReadLine().Split(',') |> Array.map(fun s->IntPtr(Int64.Parse s))
                let stressWindows=allStress.[1..]
                try
                    let raw=Stopwatch.StartNew()
                    for i in 1..100 do
                        (os.windowFromHwnd stressWindows.[0]).move(bounds.move(i%20,0))
                        pump 5
                    raw.Stop()
                    printfn "Native MoveWindow + message pump, 100 steps before grouping: %.1f ms" raw.Elapsed.TotalMilliseconds
                    for h in stressWindows do
                        (os.windowFromHwnd h).move(bounds)
                        group.addWindow(h,false)
                    group.tabActivate(Tab(stressWindows.[0]),true)
                    pump 400
                    let settle=Stopwatch.StartNew()
                    while settle.ElapsedMilliseconds < 10000L && not(stressWindows.[1..] |> Array.forall(fun h -> (os.windowFromHwnd h).isMinimized)) do pump 10
                    check (stressWindows.[1..] |> Array.forall(fun h -> (os.windowFromHwnd h).isMinimized)) "background minimization did not complete"
                    let counts=stressWindows.[1..] |> Array.map positions
                    printfn "32-window before: active=%A foreground=%A minimized=%A counts=%A" group.zorder.value.head os.foreground.hwnd (stressWindows |> Array.map(fun h -> (os.windowFromHwnd h).isMinimized)) counts
                    let watch=Stopwatch.StartNew()
                    let nativeTime=Stopwatch()
                    let pumpTime=Stopwatch()
                    group.onEnterMoveSize()
                    for i in 1..100 do
                        nativeTime.Start()
                        (os.windowFromHwnd stressWindows.[0]).move(bounds.move(i%20,0))
                        nativeTime.Stop()
                        pumpTime.Start()
                        pump 5
                        pumpTime.Stop()
                    group.onExitMoveSize()
                    pump 100
                    watch.Stop()
                    let endCounts=stressWindows.[1..] |> Array.map positions
                    printfn "32-window after: active=%A foreground=%A minimized=%A deltas=%A" group.zorder.value.head os.foreground.hwnd (stressWindows |> Array.map(fun h -> (os.windowFromHwnd h).isMinimized)) (Array.map2 (-) endCounts counts)
                    check (endCounts=counts) "32-window drag moved background windows"
                    check (stressWindows.[1..] |> Array.forall(fun h -> (os.windowFromHwnd h).isMinimized)) "32-window drag restored background windows"
                    for i in [31;15;1;0] do
                        group.tabActivate(Tab(stressWindows.[i]),true)
                        pump 180
                        check (stressWindows |> Array.forall(fun h -> (os.windowFromHwnd h).isMinimized=(h<>stressWindows.[i]))) "32-window switching failed"
                    printfn "PASS 32 actual windows: 100 drag steps %.1f ms including 600ms message pumping; ZERO background positioning messages; switches to first/middle/last" watch.Elapsed.TotalMilliseconds
                    printfn "32-window timing: native MoveWindow %.1f ms; message pumping %.1f ms; rest %.1f ms" nativeTime.Elapsed.TotalMilliseconds pumpTime.Elapsed.TotalMilliseconds (watch.Elapsed.TotalMilliseconds-nativeTime.Elapsed.TotalMilliseconds-pumpTime.Elapsed.TotalMilliseconds)
                finally
                    group.windows.items.iter group.removeWindow
                    pump 200
                    for h in allStress do (os.windowFromHwnd h).close()
                    if not(stress.WaitForExit(5000)) then stress.Kill()
        finally
            group.windows.items.iter group.removeWindow
            group.destroy()

    finally
        for hwnd in handles do (os.windowFromHwnd hwnd).close()
        if not(child.WaitForExit(5000)) then child.Kill()
