module ResourceTests
open System
open System.Drawing
open System.Diagnostics
open System.Runtime.InteropServices
open System.Text
open System.Windows.Forms
open Bemo
[<DllImport("user32.dll", CharSet=CharSet.Unicode)>]
extern bool GetClassInfoW(IntPtr instance, string name, WNDCLASS& cls)
[<DllImport("user32.dll")>]
extern uint32 GetGuiResources(IntPtr process, uint32 kind)
let check value message = if not value then failwith message
[<STAThread; EntryPoint>]
let main args =
    try
        let scope=CellScope()
        let mutable attempts=0
        let computation=CellComputation(scope,fun()->attempts<-attempts+1;if attempts=1 then failwith "injected" else box 42)
        try computation.value |> ignore with _ -> ()
        check (not scope.inComputation) "exception left computation stack occupied"
        check (unbox<int> computation.value=42) "computation did not recover"
        printfn "PASS computation exception cleans stack and retries"
        let tile=Img(Sz(32,24))
        use tileBitmap=tile.bitmap
        do
            use gfx=tile.graphics
            gfx.Clear(Color.CornflowerBlue)
        let leaf=
            {new ISprite with
                member x.image=tile.clone()
                member x.children=List2()}
        let background=Img(Sz(2048,32))
        use backgroundBitmap=background.bitmap
        let root=
            {new ISprite with
                member x.image=background.clone()
                member x.children=List2([for i in 0..63 -> Pt(i*32,0),leaf])}
        let render() =
            let img=root.render
            use bmp=img.bitmap
            check (bmp.GetPixel(10,10).ToArgb()=Color.CornflowerBlue.ToArgb()) "sprite pixels changed"
        for i in 1..20 do render()
        let before=Process.GetCurrentProcess().PrivateMemorySize64
        for i in 1..1000 do render()
        let afterMemory=Process.GetCurrentProcess().PrivateMemorySize64
        check (tile.bitmap.GetPixel(0,0).ToArgb()=Color.CornflowerBlue.ToArgb()) "shared sprite image was disposed"
        check (afterMemory-before<64L*1048576L) "sprite native memory growth"
        printfn "PASS 64-child sprite / 1000 renders: private MB %d -> %d, shared image survives" (before/1048576L) (afterMemory/1048576L)
        let appearance=
            let inactiveTab = Color.FromRGB(0x9FC4F0)
            let mouseOverTab = Color.FromRGB(0xBDD5F4)
            {
                tabHeight = 25
                tabMaxWidth = 200
                tabPinnedTabWidth = 90
                tabPinnedTabWidthIcon = true
                tabOverlap = 20
                tabHeightOffset = 1
                tabIndentFlipped = 150
                tabIndentNormal = 4
                tabInactiveTextColor = Color.FromRGB(0x000000)
                tabSelectedTextColor = Color.FromRGB(0x000000)
                tabMouseOverTextColor = Color.FromRGB(0x000000)
                tabActiveTextColor = Color.FromRGB(0x000000)
                tabFlashTextColor = Color.FromRGB(0x000000)
                tabInactiveTabColor = inactiveTab
                tabSelectedTabColor = ColorMix.midpoint inactiveTab mouseOverTab
                tabMouseOverTabColor = mouseOverTab
                tabActiveTabColor = Color.FromRGB(0xFAFCFE)
                tabFlashTabColor = Color.FromRGB(0xFFBBBB)
                tabInactiveBorderColor = Color.FromRGB(0x3A70B1)
                tabSelectedBorderColor = Color.FromRGB(0x3A70B1)
                tabMouseOverBorderColor = Color.FromRGB(0x3A70B1)
                tabActiveBorderColor = Color.FromRGB(0x3A70B1)
                tabFlashBorderColor = Color.FromRGB(0x3A70B1)
            }
        if args |> Array.exists ((=) "--plus-visual") then
            let monitor =
                {new ITabStripMonitor with
                    member x.tabClick _ = ()
                    member x.tabActivate _ = ()
                    member x.tabClose _ = ()
                    member x.tabPin _ = ()
                    member x.tabMoved(_,_) = ()
                    member x.windowMsg _ = ()}
            let strip=TabStrip(monitor)
            try
                strip.setTabAppearance(appearance,1.0)
                strip.setPlacement({showInside=false;bounds=Rect(Pt(-20000,-20000),Sz(800,26));scale=1.0})
                for i,title in [1,"Tools - 文件资源管理器";2,"此电脑 - 文件资源管理器"] do
                    let tab=Tab(IntPtr(i))
                    strip.addTab(tab)
                    strip.setTabAlign(tab,TopLeft)
                    strip.setTabInfo(tab,{text=title;iconSmall=SystemIcons.Application;iconBig=SystemIcons.Application;preview=(fun()->Img(Sz(1,1)));isRenamed=false})
                strip.visible <- true
                let flags=Reflection.BindingFlags.Instance ||| Reflection.BindingFlags.NonPublic
                let img=typeof<TabStrip>.GetProperty("render",flags).GetValue(strip,null) :?> Img
                use bitmap=img.bitmap
                bitmap.Save("Tests/new-tab-strip.png")
                let r=typeof<TabStrip>.GetField("newTabBounds",flags).GetValue(strip) :?> Rectangle
                check (r.Left > 360 && r.Right < 450) "sparse strip plus is not beside the last tab"
                printfn "PASS sparse strip plus position; saved native renderer preview"
            finally strip.destroy()
            exit 0
        if args |> Array.exists (fun a -> a = "--lifecycle-only" || a = "--modal-only") then
            GroupLifecycle.run appearance
            printfn "PASS targeted lifecycle tests"
            exit 0
        use font=new Font("Segoe UI",9.0f)
        let tabInfo : TabDisplayInfo={bgColor=None;fillColor=None;underlineColor=None;borderColor=None;text="WindowTabs original tab";textFont=font;textBrush=Brushes.Black;icon=SystemIcons.Application}
        let order=List2([0..63])
        let strip : TabStripSprite<int>={tabs=order.fold (Map2()) (fun m i->m.add i tabInfo);appearance=appearance;hover=None;captured=None;visualOrder=order;zorder=order;size=Sz(1600,26);slide=None;tabAlignments=Map2();direction=TabUp;transparent=false;pinnedTabs=Set2();selectedTabs=Set2();dragGroup=[];scale=1.0}
        let drawStrip direction=
            let img={strip with direction=direction}.render
            use bmp=img.bitmap
            check (bmp.Width=1600 && bmp.Height=26) "original strip size changed"
        for count in [1;3;20] do
            for width in [320;1600] do
                for scale in [1.0;1.5;2.0] do
                    let ids=List2([0..count-1])
                    let centered={strip with visualOrder=ids;zorder=ids;tabs=ids.fold (Map2()) (fun m i->m.add i tabInfo);tabAlignments=ids.fold (Map2()) (fun m i->m.add i TopCenter);size=Sz(width,int(26.0*scale));scale=scale}
                    let children=centered.sprite.children.list
                    let positions=children |> List.map(fun (p,s)->p,(s :?> TabSprite<int>))
                    let left=positions |> List.map(fun(p,_)->p.x) |> List.min
                    let right=positions |> List.map(fun(p,s)->p.x+s.size.width) |> List.max
                    check (abs(left-(width-right))<=2) "centered tabs have unequal outer margins"
                    for p,s in positions do
                        check (centered.tabLocation(s.id)=p) "centered drawing and click geometry differ"
                        let hit=centered.tryHitForTooltip(Pt(p.x+s.size.width/2,p.y+5))
                        check (hit=Some(s.id)) "centered tab hit test selects wrong member"
                    let dragging={centered with slide=Some(0,width/2)}
                    check (dragging.movedTab.Value |> fun(_,_,a)->a=TopCenter) "dragging loses centered alignment"
                    use pixels=centered.render.bitmap
                    check (pixels.Width=width) "centered rendering size changed"
        printfn "PASS 36 centered layouts: equal margins, narrow windows, DPI, drawing/hit geometry and drag alignment"
        for count in [64;256;1024] do
            let ids=List2([0..count-1])
            let sample={strip with visualOrder=ids;zorder=ids;tabs=ids.fold (Map2()) (fun m i->m.add i tabInfo);pinnedTabs=Set2(List2([0..7]));tabAlignments=Map2(List2([for i in 0..count-1 -> i,(if i%3=0 then TopRight else TopLeft)]))}
            for dragging in [false;true] do
                let sample=if dragging then {sample with slide=Some(10,480);dragGroup=[9;10;11]} else sample
                let watch=Stopwatch.StartNew()
                let children=sample.sprite.children
                watch.Stop()
                printfn "PERF %d tabs drag=%b layout=%.3f ms" count dragging watch.Elapsed.TotalMilliseconds
                let watch=Stopwatch.StartNew()
                let img=sample.render
                img.bitmap.Dispose()
                watch.Stop()
                printfn "PERF %d tabs drag=%b render=%.3f ms" count dragging watch.Elapsed.TotalMilliseconds
                let cache=Collections.Generic.Dictionary<int,TabSprite<int>*Img>()
                try
                    let warm=sample.renderCached(cache)
                    warm.bitmap.Dispose()
                    let watch=Stopwatch.StartNew()
                    for frame in 1..50 do
                        let next=if dragging then {sample with slide=Some(10,400+frame*7)} else {sample with hover=Some(frame%count,TabClose)}
                        let img=next.renderCached(cache)
                        img.bitmap.Dispose()
                    watch.Stop()
                    printfn "PERF %d tabs drag=%b cached changing frames=%.3f ms/frame" count dragging (watch.Elapsed.TotalMilliseconds/50.0)
                finally for (_,image) in cache.Values do image.bitmap.Dispose()
        let pixelCache=Collections.Generic.Dictionary<int,TabSprite<int>*Img>()
        for seed in 0..23 do
            let count=1+seed%12
            let ids=List2([0..count-1])
            let sample={strip with visualOrder=ids;zorder=ids.reverse;tabs=ids.fold (Map2()) (fun m i->m.add i tabInfo);size=Sz(90+seed*61,26);pinnedTabs=Set2(ids.where(fun i->(i+seed)%3=0));tabAlignments=Map2(ids.map(fun i->i,(if (i+seed)%2=0 then TopLeft else TopRight)));direction=(if seed%2=0 then TabUp else TabDown);scale=(if seed%3=0 then 1.5 else 1.0)}
            let sample=if seed%3=0 then {sample with slide=Some(0,seed*40-100)} elif seed%3=1 && count>2 then {sample with slide=Some(1,seed*40);dragGroup=[0;1;2]} else sample
            let reference=Microsoft.FSharp.Reflection.FSharpValue.MakeRecord(typeof<ReferenceTabStripSprite<int>>,Microsoft.FSharp.Reflection.FSharpValue.GetRecordFields(box sample)) :?> ReferenceTabStripSprite<int>
            check (sample.movedTab=reference.movedTab) "drag insertion changed"
            check (sample.adjustedVisualOrder.list=reference.adjustedVisualOrder.list) "drag order changed"
            for (pt,child) in sample.sprite.children.list do
                let id=(child :?> TabSprite<int>).id
                check (pt=reference.tabLocation id) (sprintf "layout changed fixture %d tab %d" seed id)
            for x in -2..7..sample.size.width+2 do
                check (sample.tryHit(Pt(x,12))=reference.tryHit(Pt(x,12))) "hit target changed"
            let actual=sample.renderCached(pixelCache)
            let expected=reference.render
            use a=actual.bitmap
            use b=expected.bitmap
            for y in 0..a.Height-1 do
                for x in 0..a.Width-1 do
                    check (a.GetPixel(x,y)=b.GetPixel(x,y)) (sprintf "render pixels changed fixture %d at %d,%d" seed x y)
        for (_,image) in pixelCache.Values do image.bitmap.Dispose()
        printfn "PASS 24 original layout/pixel/hit comparisons: pinned, left/right, DPI, drag, multi-drag, narrow strips"
        for i in 1..10 do drawStrip TabUp
        let initial=Process.GetCurrentProcess().PrivateMemorySize64
        let initialGdi=GetGuiResources(Process.GetCurrentProcess().Handle,0u)
        for i in 1..400 do drawStrip (if i%2=0 then TabUp else TabDown)
        let final=Process.GetCurrentProcess().PrivateMemorySize64
        let finalGdi=GetGuiResources(Process.GetCurrentProcess().Handle,0u)
        check (final-initial<64L*1048576L && finalGdi<=initialGdi+2u) "actual tab strip resource growth"
        printfn "PASS actual 64-tab strip / 400 renders: private MB %d -> %d; GDI %d -> %d" (initial/1048576L) (final/1048576L) initialGdi finalGdi
        let monitor=
            {new ITabStripMonitor with
                member x.tabClick _ = ()
                member x.tabActivate _ = ()
                member x.tabClose _ = ()
                member x.tabPin _ = ()
                member x.tabMoved(_,_) = ()
                member x.windowMsg _ = ()}
        let live=TabStrip(monitor)
        try
            live.setTabAppearance(appearance,1.0)
            let placement={showInside=false;bounds=Rect(Pt(-20000,-20000),Sz(1200,26));scale=1.0}
            live.setPlacement(placement)
            for i in 1..256 do live.addTab(Tab(IntPtr(i)))
            live.visible <- true
            let flags=Reflection.BindingFlags.Instance ||| Reflection.BindingFlags.NonPublic
            let mutable newTabClicks = 0
            live.newTabRequested.Add(fun() -> newTabClicks <- newTabClicks + 1)
            let plusBounds() = typeof<TabStrip>.GetField("newTabBounds",flags).GetValue(live) :?> Rectangle
            let click message (pt:Point) =
                let mutable result = IntPtr.Zero
                WinUserApi.SendMessageTimeout(live.hwnd,message,IntPtr.Zero,IntPtr((pt.Y <<< 16) ||| pt.X),3,1000,&result) |> ignore
            for scale in [1.0;1.5;2.0] do
                for align in [TopLeft;TopCenter;TopRight] do
                    live.setTabAppearance(Dpi.scaleAppearance scale appearance,scale)
                    live.setPlacement({placement with bounds=Rect(placement.bounds.location,Sz(1200,Dpi.px scale 26));scale=scale})
                    for tab in live.visualOrder.list do live.setTabAlign(tab,align)
                    let r = plusBounds()
                    check (r.Width > 0 && r.Left >= 0 && r.Right <= 1200) "new-tab button outside strip"
                    let point = Point(r.Left+r.Width/2,r.Top+r.Height/2)
                    let beforeClick = newTabClicks
                    click WindowMessages.WM_LBUTTONDOWN point
                    click WindowMessages.WM_LBUTTONUP point
                    check (newTabClicks=beforeClick+1) "new-tab button did not fire exactly once"
                    click WindowMessages.WM_LBUTTONUP point
                    check (newTabClicks=beforeClick+1) "new-tab release without press launched a window"
                    click WindowMessages.WM_LBUTTONDOWN point
                    click WindowMessages.WM_LBUTTONUP (Point(0,0))
                    check (newTabClicks=beforeClick+1) "new-tab drag-out did not cancel click"
            printfn "PASS new-tab button: left/center/right at 100/150/200%% DPI; press/release and drag-out cancellation"
            live.setTabAppearance(appearance,1.0)
            live.setPlacement(placement)
            let cellField=typeof<TabStrip>.GetFields(flags) |> Array.find(fun f->f.FieldType=typeof<CellScope>)
            let liveScope=cellField.GetValue(live) :?> CellScope
            let listeners=typeof<CellScope>.GetField("_listeners",flags).GetValue(liveScope) :?> System.Collections.IEnumerable
            let painter=listeners |> Seq.cast<CellComputation> |> Seq.last
            let version=painter.version
            let watch=Stopwatch.StartNew()
            for i in 1..1000 do
                live.setPlacement({placement with bounds=Rect(Pt(-20000+i%300,-20000),Sz(1200,26))})
            watch.Stop()
            check (painter.version=version) "position-only movement repainted strip"
            printfn "PASS actual 256-tab layered window: 1000 moves %.3f ms, zero strip repaints" watch.Elapsed.TotalMilliseconds
            live.setPlacement({placement with bounds=Rect(Pt(-20000,-20000),Sz(1400,26))})
            check (painter.version>version && live.bounds.size.width=1400) "resize did not repaint"
            let oldVersion=painter.version
            live.setTabInfo(Tab(IntPtr(1)),{text="Changed";iconSmall=SystemIcons.Application;iconBig=SystemIcons.Application;preview=(fun()->Img(Sz(1,1)));isRenamed=false})
            check (painter.version>oldVersion) "title invalidation lost after position updates"
            live.visible <- false
            live.visible <- true
            let mem=Process.GetCurrentProcess().PrivateMemorySize64
            let gdi=GetGuiResources(Process.GetCurrentProcess().Handle,0u)
            for i in 1..300 do
                live.setPlacement({placement with showInside=true;bounds=Rect(Pt(-20000,-20000),Sz(1200+i%2,26))})
                live.isShrunk <- (i%2=0)
            let after=Process.GetCurrentProcess().PrivateMemorySize64
            let afterGdi=GetGuiResources(Process.GetCurrentProcess().Handle,0u)
            printfn "Actual layered resize/sliver / 300 cycles: private MB %d -> %d, GDI %d -> %d; title and visibility updates survive" (mem/1048576L) (after/1048576L) gdi afterGdi
            check (after-mem<64L*1048576L && afterGdi<=gdi+2u) "live strip/sliver resource growth"
            live.isShrunk <- false
            live.setPlacement(placement)
            let watch=Stopwatch.StartNew()
            for i in 1..1000 do live.slide <- Some(Tab(IntPtr(1)), i%1100)
            watch.Stop()
            printfn "PASS actual 256-tab window: 1000 changing drag frames %.3f ms/frame" (watch.Elapsed.TotalMilliseconds/1000.0)
            live.visible <- false
            for i in 1..256 do live.removeTab(Tab(IntPtr(i)))
            let cache=typeof<TabStrip>.GetField("tabImages",flags).GetValue(live) :?> Collections.Generic.Dictionary<Tab,TabSprite<Tab>*Img>
            check (cache.Count=0) "removed tabs retained cached surfaces"
            printfn "PASS removing all 256 tabs releases all cached tab surfaces"
        finally live.destroy()
        GroupLifecycle.run appearance
        let info : TbButton = {icon=SystemIcons.Application;text="WindowTabs isolated preview test";bounds=Rect(Pt(0,0),Sz(600,400));activate=ignore;toggleMinimizeRestore=ignore;tabs=List2();close=ignore}
        use button=new TaskBarButton(info)
        for i in 1..300 do
            let window=button.createDwmWindow {style=WindowsStyles.WS_POPUP;exStyle=WindowsExtendedStyles.WS_EX_TOOLWINDOW;handler=(fun msg->msg.def());icon=(fun()->SystemIcons.Application);activate=ignore}
            let name=StringBuilder(128)
            WinUserApi.GetClassName(window.hwnd,name,name.Capacity)|>ignore
            button.releaseWindow(window.hwnd)
            let mutable cls=WNDCLASS()
            check (not(GetClassInfoW(WinBaseApi.GetModuleHandle(IntPtr.Zero),name.ToString(),&cls))) "closed tab left window class registered"
        printfn "PASS 300 closed taskbar windows unregister classes and release callbacks"
        let mutable last : Img option=None
        let tab : TbButtonTab={icon=SystemIcons.Application;text="fixture";activate=ignore;close=ignore;preview=(fun _->let img=Img(Sz(600,400)) in last<-Some img;Some img)}
        button.update {info with tabs=List2(["fixture",tab])}
        let hwnd=(button.tabWindow "fixture").hwnd
        let send message =
            let mutable result=IntPtr.Zero
            WinUserApi.SendMessageTimeout(hwnd,message,IntPtr.Zero,IntPtr((240<<<16)|||160),3,1000,&result)|>ignore
            let mutable disposed=false
            try last.Value.bitmap.GetPixel(0,0)|>ignore with :? ArgumentException->disposed<-true
            check disposed "preview callback retained bitmap"
        for i in 1..20 do send 0x323;send 0x326
        Application.DoEvents()
        let gdi=GetGuiResources(Process.GetCurrentProcess().Handle,0u)
        let gen2=GC.CollectionCount(2)
        for i in 1..500 do send 0x323;send 0x326
        let after=GetGuiResources(Process.GetCurrentProcess().Handle,0u)
        printfn "1000 actual DWM preview callbacks: GDI %d -> %d; Gen2 collections %d" gdi after (GC.CollectionCount(2)-gen2)
        check (after<=gdi+2u) "DWM preview GDI growth"
        check (GC.CollectionCount(2)=gen2) "preview forces full GC"
        printfn "PASS DWM preview callback ownership and collection behavior"
        0
    with ex -> printfn "%O" ex;1
