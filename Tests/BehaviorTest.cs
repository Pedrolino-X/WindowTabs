using System;using System.Collections.Generic;using System.Drawing;using System.Reflection;using System.Runtime.Remoting.Messaging;using System.Runtime.Remoting.Proxies;using System.Windows.Forms;using Bemo;
class Stub:RealProxy {
 public static Dictionary<string,object> Values=new Dictionary<string,object>{{"tabPositionByDefault","TopLeft"},{"hideTabsWhenDownByDefault","doubleclick"},{"hideTabsDelayMilliseconds",3000},{"changeTabPositionOnSnap","change"},{"language","English"},{"combineTaskbarIconsByDefault",true}};
 public Stub(Type t):base(t){}
 public override IMessage Invoke(IMessage m){var c=(IMethodCallMessage)m;object value=null;
 if(c.MethodName=="getValue"){string key=(string)c.Args[0];if(!Values.TryGetValue(key,out value))value=false;}
 else if(c.MethodName=="setValue"){var pair=(Tuple<string,object>)c.Args[0];Values[pair.Item1]=pair.Item2;}
 else {var t=((MethodInfo)c.MethodBase).ReturnType;if(t!=typeof(void)&&t.IsValueType)value=Activator.CreateInstance(t);}
 return new ReturnMessage(value,null,0,c.LogicalCallContext,c);}
}
class BehaviorTest {
 static IEnumerable<Control> All(Control c){yield return c;foreach(Control child in c.Controls)foreach(var x in All(child))yield return x;}
 [STAThread] static int Main(){try{Application.EnableVisualStyles();GS.Services.register<ISettings>((ISettings)new Stub(typeof(ISettings)).GetTransparentProxy(),false);GS.Services.register<IProgram>((IProgram)new Stub(typeof(IProgram)).GetTransparentProxy(),false);GS.Services.register<IFilterService>((IFilterService)new Stub(typeof(IFilterService)).GetTransparentProxy(),false);
 var view=(ISettingsView)new HotKeyView();using(var f=new Form()){f.Size=new Size(900,700);f.Controls.Add(view.control);view.control.Dock=DockStyle.Fill;f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();var handle=f.Handle;
 int radios=0,checks=0,combos=0;foreach(var c in All(view.control)){var combo=c as ComboBox;if(combo!=null){combo.SelectedIndex=1;combos++;}var radio=c as RadioButton;if(radio!=null){radio.Checked=true;radios++;}var check=c as CheckBox;if(check!=null){check.Checked=!check.Checked;checks++;}}
 if((bool)Stub.Values["combineTaskbarIconsByDefault"])throw new Exception("Taskbar default checkbox did not save");if(combos<1||radios<5||checks<6)throw new Exception("Original settings controls missing");if((string)Stub.Values["tabPositionByDefault"]!="TopRight")throw new Exception("Position setting did not update");
 using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save("behavior-preview.png");}Console.WriteLine("PASS original Behavior page: {0} dropdowns, {1} radio buttons, {2} checkboxes; edits reach in-memory settings only",combos,radios,checks);}
 return 0;}catch(Exception ex){Console.WriteLine(ex.GetType().FullName);Console.WriteLine(ex.Message);Console.WriteLine(ex.StackTrace);return 1;}}
}


