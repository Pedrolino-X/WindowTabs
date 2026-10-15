namespace Bemo
open System
open System.Drawing

type ISprite =
    // Sprite implementations return a newly allocated image owned by the caller.
    abstract member image : Img
    abstract member children : List2<Pt * ISprite>
   
[<AutoOpen>]
module Sprite = 
    type ISprite with
        member this.render : Img =
            let image = this.image
            try
                use gr = image.graphics
                let draw (childLocation:Pt, child:ISprite) =
                    let childImage = child.render
                    use childBitmap = childImage.bitmap
                    gr.DrawImageUnscaled(childBitmap, childLocation.Point)
                this.children.reverse.iter draw
                image
            with _ ->
                image.bitmap.Dispose()
                reraise()

        member this.tryHit(pt:Pt, path:List2<ISprite>) =
            let path = path.prepend(this)
            let hitPath = this.children.tryPick <| fun (location, child) ->
                let pt = pt.sub(location)
                child.tryHit(pt, path)
            match hitPath with
            | Some(path) -> Some(path)
            | None ->
                let image = this.image
                use bitmap = image.bitmap
                if image.containsPoint(pt) 
                then Some(path)
                else None

        member this.hit pt = this.tryHit(pt, List2([])).def(List2([]))
