namespace Bemo
open System
open System.Drawing
open System.Drawing.Drawing2D
type ReferenceTabStripSprite<'id> when 'id : equality = {

    tabs: Map2<'id, TabDisplayInfo>

    appearance: TabAppearanceInfo

    hover: ('id * TabPart) option

    captured : ('id * TabPart) option

    visualOrder: List2<'id>

    zorder: List2<'id>

    size: Sz

    slide: ('id * int) option

    tabAlignments: Map2<'id, TabAlign>

    direction: TabDirection

    transparent: bool

    pinnedTabs: Set2<'id>

    selectedTabs: Set2<'id>

    // Multi-tab drag group (variant A boundary): the pivot tab is

    // dragGroup.[0] and matches slide.tab. Other elements are dragged

    // along with the pivot, drawn at pivot.x + their relative offset.

    // Empty list = no group drag (= single-tab slide, existing behavior).

    dragGroup: 'id list

    // Device-pixel scale of the strip's monitor (1.0 = 100%). `appearance` is

    // already scaled by it; this is for the constants written here and for the

    // per-tab sprites.

    scale: float

    } with



    member private this.px value = Dpi.px this.scale value



    member private this.tabOverlap = float(this.appearance.tabOverlap)

    member private this.unpinnedTabMaxLen = float(this.appearance.tabMaxWidth)

    member private this.pinnedTabMinLen =

        // Calculate dynamically based on tab height and icon size

        // (mirrors TabSprite.iconSize / edgeWidth, hence the same scaling)

        let tabHeight = this.size.height - 1

        let iconHeight = min (max (this.px 16) (tabHeight - this.px 8)) (this.px 24)

        float(this.px 18 + iconHeight + this.px 18)  // left edge + icon width + right padding

    member private this.pinnedTabSettingLen = float(this.appearance.tabPinnedTabWidth)

    member private this.pinnedTabMaxLen =

        if this.appearance.tabPinnedTabWidthIcon then this.pinnedTabMinLen

        else max this.pinnedTabMinLen this.pinnedTabSettingLen



    member private this.count = this.visualOrder.length

    member private this.pinnedCount =

        this.visualOrder.where(fun tab -> this.pinnedTabs.contains(tab)).length

    member private this.unpinnedCount = this.count - this.pinnedCount



    member private this.isPinned (tab: 'id) = this.pinnedTabs.contains(tab)



    member private this.getTabAlign (tab: 'id) =

        match this.tabAlignments.tryFind(tab) with

        | Some(a) -> a

        | None -> TopLeft



    member private this.getAdjustedAlignment (tab: 'id) =

        match this.movedTab with

        | Some(t, _, alignment) when t = tab -> alignment

        // Group members follow the pivot's adjusted alignment.

        | Some(pivot, _, alignment) when this.isDragGroupMember tab && this.dragGroupPivot = Some pivot ->

            alignment

        | _ -> this.getTabAlign(tab)



    // --- Multi-tab drag group helpers ---

    // The pivot tab is whichever tab is being dragged (= slide.tab). It may

    // sit anywhere within dragGroup (e.g. dragGroup = [A; B; C] with B as

    // pivot). All offsets are computed relative to the pivot's position so

    // group members render at pivot.x + signed offset.

    member private this.dragGroupPivot : 'id option =

        match this.slide with

        | Some(t, _) -> Some t

        | None -> None



    member private this.isDragGroupMember (tab: 'id) =

        this.dragGroup |> List.contains tab



    // Signed offset of a group member from the pivot.

    // - Pivot itself: 0

    // - Members to the right of pivot in dragGroup: positive (Σ tabLen - overlap)

    // - Members to the left of pivot in dragGroup: negative (-Σ tabLen - overlap)

    // Uses Chrome-style tab overlap so the rendered group matches the

    // visual spacing of normal (non-dragged) tabs.

    member private this.dragGroupOffsetOf (tab: 'id) : float =

        match this.dragGroupPivot with

        | None -> 0.0

        | Some pivot ->

            let pIdx = this.dragGroup |> List.tryFindIndex ((=) pivot) |> Option.defaultValue 0

            let tIdx = this.dragGroup |> List.tryFindIndex ((=) tab) |> Option.defaultValue 0

            if tIdx = pIdx then 0.0

            elif tIdx > pIdx then

                // Right of pivot: accumulate positive step widths

                let mutable offset = 0.0

                for i in pIdx .. tIdx - 1 do

                    let t = this.dragGroup.[i]

                    let tLen = if this.isPinned(t) then this.pinnedTabLength else this.unpinnedTabLength

                    offset <- offset + tLen - this.tabOverlap

                offset

            else

                // Left of pivot: accumulate negative step widths

                let mutable offset = 0.0

                for i in tIdx .. pIdx - 1 do

                    let t = this.dragGroup.[i]

                    let tLen = if this.isPinned(t) then this.pinnedTabLength else this.unpinnedTabLength

                    offset <- offset - (tLen - this.tabOverlap)

                offset



    member private this.calcGroupWidth (tabs: List2<'id>) =

        if tabs.isEmpty then 0.0

        else

            let totalLen =

                tabs.list |> List.sumBy (fun t ->

                    if this.isPinned(t) then this.pinnedTabLength else this.unpinnedTabLength)

            totalLen - float(tabs.length - 1) * this.tabOverlap



    member private this.tabOffsetInGroup (tabs: List2<'id>) (tab: 'id) =

        let idx = tabs.list |> List.findIndex ((=) tab)

        tabs.list

        |> Seq.take idx

        |> Seq.fold (fun acc t ->

            let tLen = if this.isPinned(t) then this.pinnedTabLength else this.unpinnedTabLength

            acc + tLen - this.tabOverlap

        ) 0.0



    member private this.hitInGroup (tabs: List2<'id>) (groupStartX: float) (x: float) : Option<'id> =

        if tabs.isEmpty then None

        else

            let relX = x - groupStartX

            let tabList = tabs.list

            let mutable offset = 0.0

            let mutable result = tabList |> List.last

            let mutable found = false

            for i in 0 .. tabList.Length - 1 do

                if not found then

                    let t = tabList.[i]

                    let tLen = if this.isPinned(t) then this.pinnedTabLength else this.unpinnedTabLength

                    let boundary = offset + tLen - this.tabOverlap / 2.0

                    if relX < boundary || i = tabList.Length - 1 then

                        result <- t

                        found <- true

                    offset <- offset + tLen - this.tabOverlap

            Some(result)



    // Pinned tab width: fixed at pinnedTabMaxLen unless space is too tight

    member private this.pinnedTabLength =

        if this.pinnedCount = 0 then this.pinnedTabMaxLen

        else

            let totalOverlap = float(max 0 (this.count - 1)) * this.tabOverlap

            let availableWidth = float(this.size.width) + totalOverlap

            let pinnedMaxTotal = float(this.pinnedCount) * this.pinnedTabMaxLen

            let unpinnedRemaining = availableWidth - pinnedMaxTotal

            // If unpinned tabs still have at least 1px each, keep pinned at max

            if this.unpinnedCount = 0 || unpinnedRemaining / float(this.unpinnedCount) >= 1.0 then

                this.pinnedTabMaxLen

            else

                // Everything too tight, use uniform width

                availableWidth / float(this.count)



    // Unpinned tab width: fills remaining space after pinned tabs

    member private this.unpinnedTabLength =

        if this.unpinnedCount = 0 then this.unpinnedTabMaxLen

        else

            let totalOverlap = float(max 0 (this.count - 1)) * this.tabOverlap

            let availableWidth = float(this.size.width) + totalOverlap

            let pinnedTotal = float(this.pinnedCount) * this.pinnedTabLength

            let unpinnedAvailable = availableWidth - pinnedTotal

            min (unpinnedAvailable / float(this.unpinnedCount)) this.unpinnedTabMaxLen



    member private this.tabSpriteRec (tab:'id) : TabSprite<'id> =

        let isPinned = this.isPinned(tab)

        let tabLen = if isPinned then this.pinnedTabLength else this.unpinnedTabLength

        {

            TabSprite.id = tab

            isTop =

                match this.zorder.tryHead with

                | Some(top) -> top = tab

                | None -> false

            isSelected = this.selectedTabs.contains(tab)

            displayInfo = this.tabs.find(tab)

            appearance =

                if isPinned then

                    { this.appearance with tabMaxWidth = int(this.pinnedTabLength) }

                else

                    this.appearance

            // - 1: the one-device-pixel hairline that separates the tab from

            // the far edge of the strip. Never scaled (see TabSprite.px), and

            // the same expression appears in pinnedTabMinLen and in

            // tryHitForTooltip so drawing and hit testing cannot drift apart.

            size = Sz(int(tabLen), (this.size.height) - 1)

            onlyIcon = isPinned && this.appearance.tabPinnedTabWidthIcon

            isPinned = isPinned

            direction = this.direction

            hover =

                match this.hover with

                | Some(id, part) when id = tab -> Some(part)

                | _ -> None

            captured =

                match this.captured with

                | Some(id, part) when id = tab -> Some(part)

                | _ -> None

            scale = this.scale

        }

    member private this.tabSprite (tab:'id) = this.tabSpriteRec(tab) :> ISprite



    member private this.bgImage =

        let bgColor =

            if this.transparent then Color.FromArgb(0, 0, 0, 0)

            else Color.FromArgb(1, 1, 1, 1)

        // Img rather than a bare Bitmap so this surface is pinned to 96 dpi

        // like every other one (see Drawing.fs).

        let img = Img(if this.size.isEmptyArea then Sz(1, 1) else this.size)

        use gr = img.graphics

        do gr.SmoothingMode <- SmoothingMode.AntiAlias

        let bounds = Rect(Pt(), this.size)

        gr.Clear(bgColor) // Same background as the previously delivered resource repair.

        img



    // Which end of the strip the one-pixel hairline sits at: below the tabs

    // when they point up, above them when they point down. One device pixel

    // at every scale, like the "- 1" it complements (see TabSprite.px).

    member private this.tabYOffset =

        match this.direction with

        | TabUp -> 0

        | TabDown -> 1



    member this.tabLocation tab =

        match this.slide with

        | Some(slideTab, x) when tab = slideTab ->

            // Pivot tab: render at drag x, clamped to its own single-tab

            // width. Group members may extend beyond the strip edge — that

            // is the natural consequence of dragging a group near an edge

            // and the user can drag back.

            let tabLen = if this.isPinned(tab) then this.pinnedTabLength else this.unpinnedTabLength

            let bounds = (0, this.size.width - int(tabLen))

            Pt(between bounds x, this.tabYOffset)

        | Some(pivotTab, pivotX) when tab <> pivotTab && this.isDragGroupMember tab ->

            // Group member: pivot.x + signed offset (left = negative,

            // right = positive). Use the same single-tab clamp as the

            // pivot so the group members track the visible pivot.

            let pivotLen = if this.isPinned(pivotTab) then this.pinnedTabLength else this.unpinnedTabLength

            let pivotBounds = (0, this.size.width - int(pivotLen))

            let clampedPivot = between pivotBounds pivotX

            let offset = this.dragGroupOffsetOf tab

            Pt(clampedPivot + int(offset), this.tabYOffset)

        | _ ->

            let adjusted = this.adjustedVisualOrder

            let tabAlignment = this.getAdjustedAlignment(tab)

            let groupTabs = adjusted.where(fun t -> this.getAdjustedAlignment(t) = tabAlignment)

            let groupStartX =

                match tabAlignment with

                | TopLeft -> 0.0

                | TopRight ->

                    let groupWidth = this.calcGroupWidth(groupTabs)

                    max 0.0 (float(this.size.width) - groupWidth)

            let offset = this.tabOffsetInGroup groupTabs tab

            Pt(int(groupStartX + offset), this.tabYOffset)



    member this.tabSize = Sz(int(this.unpinnedTabLength), (this.size.height) - 1)



    member this.movedTab : ('id * int * TabAlign) option =

        match this.slide with

        | Some(tab, x) ->

            if this.count = 0 then Some(tab, 0, TopLeft)

            else

                // Group-drag detection: judge by the GROUP HEAD's center

                // (= dragGroup.[0]), not the pivot's. This keeps the drop

                // index stable regardless of which group member the user

                // grabbed: a stationary [C,D,E] should give the same

                // visualIdx whether the user pivots on C or D.

                //

                // Concretely, pivot.x is the mouse x; the head sits at

                // pivot.x + dragGroupOffsetOf(head) (negative if head is

                // left of pivot). The center used for boundary tests is

                // the head's center.

                let groupTabs =

                    if List.isEmpty this.dragGroup then [tab] else this.dragGroup

                let inGroup t = List.contains t groupTabs

                let groupHead =

                    if List.isEmpty this.dragGroup then tab else List.head this.dragGroup

                let dragTabLen =

                    if this.isPinned(groupHead) then this.pinnedTabLength else this.unpinnedTabLength

                let groupHeadX = float(x) + this.dragGroupOffsetOf groupHead

                let centerX = groupHeadX + dragTabLen / 2.0



                // Calculate group tabs and widths (exclude every tab in the

                // drag group, not just the pivot).

                let leftWithout = this.visualOrder.where(fun t -> not (inGroup t) && this.getTabAlign(t) = TopLeft).list

                let rightWithout = this.visualOrder.where(fun t -> not (inGroup t) && this.getTabAlign(t) = TopRight).list

                let calcWidth (tabs: 'id list) =

                    if tabs.IsEmpty then 0.0

                    else

                        let total = tabs |> List.sumBy (fun t ->

                            if this.isPinned(t) then this.pinnedTabLength else this.unpinnedTabLength)

                        total - float(tabs.Length - 1) * this.tabOverlap

                let leftWidthWithout = calcWidth leftWithout

                let rightWidthWithout = calcWidth rightWithout



                // For alignment detection, use widths without the dragged tab(s)

                let rightStartXWithout = float(this.size.width) - rightWidthWithout

                let emptyCenterWithout = (leftWidthWithout + rightStartXWithout) / 2.0



                // Determine target alignment

                let targetAlignment = if centerX >= emptyCenterWithout then TopRight else TopLeft



                // For position calculation within the group, include the dragged blob's

                // width in the target alignment group to match how tabLocation renders

                let leftWidthFull =

                    if targetAlignment = TopLeft then

                        let overlap = if leftWithout.IsEmpty then 0.0 else this.tabOverlap

                        leftWidthWithout + dragTabLen - overlap

                    else leftWidthWithout

                let rightWidthFull =

                    if targetAlignment = TopRight then

                        let overlap = if rightWithout.IsEmpty then 0.0 else this.tabOverlap

                        rightWidthWithout + dragTabLen - overlap

                    else rightWidthWithout



                // Calculate target index within the alignment group

                let (groupList, groupStartX) =

                    match targetAlignment with

                    | TopLeft -> (leftWithout, 0.0)

                    | TopRight ->

                        let startX = float(this.size.width) - rightWidthFull

                        (rightWithout, startX)



                // Hybrid swap-judge basis (variant S / -19):

                //   - Right-aligned + multi-tab drag: group CENTER

                //     (variant R / -18 behaved well for this case because

                //      the user reads the whole moving block, not its

                //      left-aligned head).

                //   - All other cases (single tab, or left-aligned drag):

                //     group HEAD's LEFT EDGE (variant Q / -17 felt right

                //      because the leftmost moving tab is the visual

                //      anchor in those cases).

                let isRightMulti =

                    targetAlignment = TopRight && List.length groupTabs > 1

                let groupTotalWidth =

                    let n = List.length groupTabs

                    let total =

                        groupTabs |> List.sumBy (fun t ->

                            if this.isPinned(t) then this.pinnedTabLength else this.unpinnedTabLength)

                    total - float(max 0 (n - 1)) * this.tabOverlap

                let judgeX =

                    if isRightMulti then groupHeadX + groupTotalWidth / 2.0

                    else groupHeadX

                let groupIndex =

                    if groupList.IsEmpty then 0

                    else

                        let mutable idx = groupList.Length

                        let mutable offset = 0.0

                        let mutable found = false

                        for i in 0 .. groupList.Length - 1 do

                            if not found then

                                let t = groupList.[i]

                                let tLen = if this.isPinned(t) then this.pinnedTabLength else this.unpinnedTabLength

                                // Swap when judgeX crosses t's CENTER.

                                // judgeX is the variant-specific basis

                                // (group center for right-multi, group

                                // head's left edge otherwise).

                                let tCenter = groupStartX + offset + tLen / 2.0

                                if judgeX < tCenter then

                                    idx <- i

                                    found <- true

                                offset <- offset + (tLen - this.tabOverlap)

                        max 0 (min idx groupList.Length)



                // Convert group index to visual order index for splicing.

                // visualWithout = visualOrder - the whole drag group.

                let visualWithout = this.visualOrder.where(fun t -> not (inGroup t)).list

                let mutable groupCount = 0

                let mutable visualIdx = visualWithout.Length

                let mutable found2 = false

                for i in 0 .. visualWithout.Length - 1 do

                    if not found2 then

                        let t = visualWithout.[i]

                        if this.getTabAlign(t) = targetAlignment then

                            if groupCount = groupIndex then

                                visualIdx <- i

                                found2 <- true

                            else

                                groupCount <- groupCount + 1

                                visualIdx <- i + 1



                // visualIdx is where the pivot's center landed in

                // visualWithout. Use it directly as the splice index for

                // the whole group: the group's first member lands there

                // and the pivot ends up at spliceIdx + pivotIdxInGroup.

                //

                // The previous "spliceIdx = visualIdx - pivotIdxInGroup"

                // shift made it impossible to splice the group at the very

                // end of visualWithout (the pivot's max visualIdx equals

                // visualWithout.Length, and subtracting pivotIdxInGroup

                // pulled the group back from the tail). Without the shift,

                // the group correctly reaches the tail when the user drags

                // the pivot past the last non-group tab.

                let spliceIdx = max 0 (min visualWithout.Length visualIdx)



                Some(tab, spliceIdx, targetAlignment)

        | None -> None



    member this.adjustedVisualOrder : List2<'id> =

        match this.movedTab with

        | Some(tab, index, _) ->

            if List.isEmpty this.dragGroup then

                this.visualOrder.move((=)tab, index)

            else

                // Splice the entire drag group at `index` of visualOrder-without-group

                let grp = this.dragGroup

                let cur = this.visualOrder.list

                let withoutGroup = cur |> List.filter (fun t -> not (List.contains t grp))

                let safeIndex = max 0 (min withoutGroup.Length index)

                let newOrder =

                    (withoutGroup |> List.truncate safeIndex)

                    @ grp

                    @ (withoutGroup |> List.skip safeIndex)

                List2(newOrder)

        | None -> this.visualOrder





    member this.sprite =

        {

            new ISprite with

            member x.image = this.bgImage

            member x.children =

                // Z-order: when a multi-tab drag group is active, bring its

                // members to the front (end of children = topmost),

                // ordered by visualOrder so left→right Chrome-style overlap

                // is preserved within the group. Without this, a group

                // member that sits behind the pivot in z-order would get

                // covered by the pivot's overlap notch, producing the

                // "[---A---]-B---]" artifact.

                let order =

                    if List.isEmpty this.dragGroup then

                        this.zorder

                    else

                        let grp = this.dragGroup

                        let nonGroup = this.zorder.where(fun t -> not (List.contains t grp))

                        let groupOrdered = this.visualOrder.where(fun t -> List.contains t grp)

                        nonGroup.appendList(groupOrdered)

                order.map <| fun (tab:'id) ->

                    (this.tabLocation tab, this.tabSprite(tab))

        }



    member this.renderTab tab = this.tabSprite(tab).render



    member this.render = this.sprite.render



    member this.tryHit pt =

        // Determine the owning tab purely geometrically (boundary at the

        // midpoint of each tab overlap, independent of z-order) so a tab that

        // is drawn on top never steals the exposed area — including the

        // Close/Pin button — of the tab overlapped behind it. This uses the

        // same partition as the tooltip hit test: every tab gets a constant

        // hover width regardless of which tab is frontmost.

        maybe {

            let! tab = this.tryHitForTooltip pt

            // Resolve the part (Close/Pin/Icon/Background) within that tab by

            // rectangle (see TabSprite.partAt) at tab-local coordinates.

            let part = (this.tabSpriteRec tab).partAt(pt.sub(this.tabLocation tab))

            return tab,part

        }



    // Tab-ownership hit test (used by both tooltip and tryHit): every tab's

    // hover region is a uniform-width rectangle (width = tabLength - overlap),

    // with boundaries at the overlap midpoints, ignoring z-order, so a tab drawn

    // on top never steals the area of the tab behind it AND every tab reacts to

    // hover over the same width. The LAST tab of a group has no right neighbour,

    // so without adjustment it would own its full trapezoid (half an overlap

    // wider than the interior tabs); trim its right limit by overlap/2 to keep

    // the width uniform. The tab's slanted bottom-right corner beyond the trim

    // is intentionally not hover-sensitive (drawing stays trapezoidal).

    member this.tryHitForTooltip (pt: Pt) : Option<'id> =

        if this.count = 0 then None

        else

            let yOff = this.tabYOffset

            let tabH = (this.size.height) - 1

            if pt.y >= yOff && pt.y < yOff + tabH then

                let x = float(pt.x)

                let adjusted = this.adjustedVisualOrder

                let leftTabs = adjusted.where(fun t -> this.getAdjustedAlignment(t) = TopLeft)

                let rightTabs = adjusted.where(fun t -> this.getAdjustedAlignment(t) = TopRight)

                let leftWidth = this.calcGroupWidth(leftTabs)

                let rightWidth = this.calcGroupWidth(rightTabs)

                let rightStartX = float(this.size.width) - rightWidth

                let trim = max 0.0 (this.tabOverlap / 2.0)

                if x < leftWidth - trim && not leftTabs.isEmpty then

                    this.hitInGroup leftTabs 0.0 x

                elif x >= rightStartX && x < float(this.size.width) - trim && not rightTabs.isEmpty then

                    this.hitInGroup rightTabs rightStartX x

                else

                    None

            else

                None

