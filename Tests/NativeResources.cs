using System;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Bemo;
class NativeResources {
 [DllImport("user32.dll")] static extern uint GetGuiResources(IntPtr p,uint kind);
 [STAThread] static int Main(){try{using(var f=new Form()){f.Size=new Size(320,240);var h=f.Handle;
 Action cycle=()=>{using(var captured=Win32Helper.PrintWindow(h))using(var scaled=Win32Helper.ScaleImage(captured,2,2)){Win32Helper.UpdateLayeredWindow(h,Point.Empty,scaled,255);}};
 for(int i=0;i<20;i++)cycle();uint before=GetGuiResources(Process.GetCurrentProcess().Handle,0);var sw=Stopwatch.StartNew();for(int i=0;i<2000;i++)cycle();uint after=GetGuiResources(Process.GetCurrentProcess().Handle,0);
 Console.WriteLine("2000 native capture/scale/layer updates: {0} ms; GDI {1} -> {2}",sw.ElapsedMilliseconds,before,after);if(after>before+2)throw new Exception("GDI growth");}
 Console.WriteLine("PASS native resource loop");return 0;}catch(Exception ex){Console.WriteLine(ex);return 1;}}
}
