using System;
using System.Drawing;
using System.Windows.Forms;
class GroupFixture {
 class TrackedForm : Form {
  int positions;
  Form dialog;
  protected override void WndProc(ref Message m) {
   if(m.Msg==0x8043) { Owner=m.WParam==IntPtr.Zero?null:Form.FromHandle(m.WParam) as Form; return; }
   if(m.Msg==0x8044) {
    BeginInvoke(new Action(()=>{
     using(var d=new Form {Text="Generic modal prompt",Size=new Size(220,120),ShowInTaskbar=false,StartPosition=FormStartPosition.CenterParent}) {
      dialog=d; d.ShowDialog(this); dialog=null;
     }
    })); return;
   }
   if(m.Msg==0x8045) { m.Result=dialog==null?IntPtr.Zero:dialog.Handle; return; }
   if(m.Msg==0x8046) { if(dialog!=null)dialog.Close(); return; }
   if(m.Msg==0x8042) { m.Result=new IntPtr(positions); return; }
   if(m.Msg==0x47) positions++;
   base.WndProc(ref m);
  }
 }
 [STAThread] static void Main(string[] args) {
  Application.EnableVisualStyles();
  int count=args.Length>0?Int32.Parse(args[0]):4;
  var forms=new Form[count]; int remaining=count;
  for(int i=0;i<forms.Length;i++) {
   var f=new TrackedForm {Text="WindowTabs regression fixture "+i,Size=new Size(360,240),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000),ShowInTaskbar=(i==0)};
   if(i>0)f.FormBorderStyle=FormBorderStyle.SizableToolWindow;
   f.FormClosed+=(s,e)=>{if(--remaining==0)Application.ExitThread();};
   f.Controls.Add(new Label {Text="Temporary WindowTabs regression window",Dock=DockStyle.Fill});
   forms[i]=f; f.Show();
  }
  Console.WriteLine(String.Join(",",Array.ConvertAll(forms,f=>f.Handle.ToInt64().ToString())));
  Console.Out.Flush();
  Application.Run();
 }
}
