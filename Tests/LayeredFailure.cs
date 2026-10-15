using System; using System.Drawing; using Bemo;
namespace FaultTest {
class Handles {public static int Live,Step,Fail; public static void Tick(){if(++Step==Fail)throw new Exception("injected allocation failure");} public static IntPtr New(){Tick();Live++;return new IntPtr(Live+10);} }
class FakeBitmap {public Size Size {get{return new Size(10,10);}} public IntPtr GetHbitmap(Color c){return Handles.New();}}
class WinUserApi {public static IntPtr GetWindowDC(IntPtr h){return Handles.New();} public static void ReleaseDC(IntPtr w,IntPtr d){Handles.Live--;} public static void UpdateLayeredWindow(IntPtr h,IntPtr zero,ref POINT dest,ref SIZE size,IntPtr dc,ref POINT src,int key,ref BLENDFUNCTION blend,int flags){Handles.Tick();}}
class WinGdiApi {public static IntPtr CreateCompatibleDC(IntPtr h){return Handles.New();} public static IntPtr SelectObject(IntPtr h,IntPtr o){Handles.Tick();return new IntPtr(99);} public static void DeleteObject(IntPtr h){Handles.Live--;} public static void DeleteDC(IntPtr h){Handles.Live--;}}
class Before {
        public static void UpdateLayeredWindow(IntPtr hwnd, Point location, FakeBitmap bitmap, byte alpha)
        {
            try
            {
                BLENDFUNCTION blend = new BLENDFUNCTION();
                blend.BlendOp = AlphaChannelFlags.AC_SRC_OVER;
                blend.BlendFlags = 0;
                blend.AlphaFormat = AlphaChannelFlags.AC_SRC_ALPHA;
                blend.SourceConstantAlpha = alpha;
                POINT srcLocation = new POINT();
                POINT dstLocation = POINT.FromPoint(location);
                SIZE dstSize = SIZE.FromSize(bitmap.Size);
                IntPtr hdc = WinUserApi.GetWindowDC(hwnd);
                IntPtr bmpDc = WinGdiApi.CreateCompatibleDC(hdc);
                IntPtr hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                IntPtr originalBitmap = WinGdiApi.SelectObject(bmpDc, hBitmap);
                WinUserApi.UpdateLayeredWindow(hwnd, IntPtr.Zero, ref dstLocation, ref dstSize, bmpDc, ref srcLocation, 0, ref blend, UpdateLayeredWindowFlags.ULW_ALPHA);
                WinGdiApi.SelectObject(bmpDc, originalBitmap);
                WinGdiApi.DeleteObject(hBitmap);
                WinGdiApi.DeleteDC(bmpDc);
                WinUserApi.ReleaseDC(hwnd, hdc);
            }
            catch
            {
            }
        }
}
class After {
        public static void UpdateLayeredWindow(IntPtr hwnd, Point location, FakeBitmap bitmap, byte alpha)
        {
            IntPtr hdc = IntPtr.Zero, bmpDc = IntPtr.Zero, hBitmap = IntPtr.Zero, originalBitmap = IntPtr.Zero;
            try
            {
                BLENDFUNCTION blend = new BLENDFUNCTION();
                blend.BlendOp = AlphaChannelFlags.AC_SRC_OVER;
                blend.BlendFlags = 0;
                blend.AlphaFormat = AlphaChannelFlags.AC_SRC_ALPHA;
                blend.SourceConstantAlpha = alpha;
                POINT srcLocation = new POINT();
                POINT dstLocation = POINT.FromPoint(location);
                SIZE dstSize = SIZE.FromSize(bitmap.Size);
                hdc = WinUserApi.GetWindowDC(hwnd);
                bmpDc = WinGdiApi.CreateCompatibleDC(hdc);
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                originalBitmap = WinGdiApi.SelectObject(bmpDc, hBitmap);
                WinUserApi.UpdateLayeredWindow(hwnd, IntPtr.Zero, ref dstLocation, ref dstSize, bmpDc, ref srcLocation, 0, ref blend, UpdateLayeredWindowFlags.ULW_ALPHA);
            }
            catch
            {
            }
            finally
            {
                if (originalBitmap != IntPtr.Zero && originalBitmap != new IntPtr(-1)) WinGdiApi.SelectObject(bmpDc, originalBitmap);
                if (hBitmap != IntPtr.Zero) WinGdiApi.DeleteObject(hBitmap);
                if (bmpDc != IntPtr.Zero) WinGdiApi.DeleteDC(bmpDc);
                if (hdc != IntPtr.Zero) WinUserApi.ReleaseDC(hwnd, hdc);
            }
        }
}
class Run { static int Main(){for(int failure=1;failure<=5;failure++){Handles.Live=Handles.Step=0;Handles.Fail=failure;Before.UpdateLayeredWindow(IntPtr.Zero,Point.Empty,new FakeBitmap(),255);int oldLive=Handles.Live;Handles.Live=Handles.Step=0;After.UpdateLayeredWindow(IntPtr.Zero,Point.Empty,new FakeBitmap(),255);Console.WriteLine("allocation/native failure {0}: leaked handles before={1}, after={2}",failure,oldLive,Handles.Live);if(Handles.Live!=0)return 1;}return 0;}}}
