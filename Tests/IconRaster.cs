using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Diagnostics;
class IconRaster {
    static Bitmap Draw(Icon icon,int size,bool owned) {
        var bitmap=new Bitmap(size,size);
        bitmap.SetResolution(96,96);
        using(var g=Graphics.FromImage(bitmap)) {
            g.SmoothingMode=SmoothingMode.AntiAlias;
            g.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.FromArgb(1,0,0,0));
            var rect=new Rectangle(0,0,size,size);
            if(owned) {using(var source=icon.ToBitmap())g.DrawImage(source,rect);}
            else g.DrawIcon(icon,rect);
        }
        return bitmap;
    }
    static int Main() {
        try {
            using(var app=new Icon("WtProgram/Resources/Bemo.ico")) {
                foreach(var icon in new[]{SystemIcons.Application,SystemIcons.Warning,SystemIcons.Information,app})
                    foreach(int size in new[]{16,17,25,33,41,64})
                        using(var old=Draw(icon,size,false))using(var current=Draw(icon,size,true))
                            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                                if(old.GetPixel(x,y)!=current.GetPixel(x,y))throw new Exception("Icon pixels changed");
                long before=Process.GetCurrentProcess().PrivateMemorySize64;
                int gen2=GC.CollectionCount(2);
                for(int i=0;i<10000;i++)using(var bitmap=Draw(app,25,true)){}
                long after=Process.GetCurrentProcess().PrivateMemorySize64;
                Console.WriteLine("PASS 24 icon pixel comparisons with Framework DrawIcon; 10000 owned icon draws: private MB {0}->{1}, Gen2 collections {2}",before/1048576,after/1048576,GC.CollectionCount(2)-gen2);
                if(after-before>32*1048576)throw new Exception("Icon native memory growth");
            }
            return 0;
        }catch(Exception ex){Console.WriteLine(ex);return 1;}
    }
}
