using System;
using System.Reflection;
using System.Runtime.Serialization;
using Bemo;
using Microsoft.FSharp.Core;
class PreferenceSettings {
 static void Check(bool value,string text){if(!value)throw new Exception(text);}
 static int Main(){try {
  // Inject JSON in the existing read cache. Never write user settings.
  var settings=new Settings(false);
  var flags=BindingFlags.NonPublic|BindingFlags.Instance;
  var cache=typeof(Settings).GetField("cachedSettingsString",flags);
  cache.SetValue(settings,FSharpOption<string>.Some("{\"Version\":\"test\"}"));
  Check(settings.settings.combineTaskbarIconsByDefault,"merge default not enabled");
  var type=Array.Find(typeof(Settings).Assembly.GetTypes(),t=>t.Name=="Program"&&typeof(IProgram).IsAssignableFrom(t));
  var instance=FormatterServices.GetUninitializedObject(type);
  type.GetField("settingsManager",flags).SetValue(instance,settings);
  foreach(var field in type.GetFields(flags)) if(field.Name.StartsWith("init@")&&field.FieldType==typeof(int)) field.SetValue(instance,1);
  var program=(IProgram)instance;
  Check(program.getAutoGroupingEnabled("C:\\Apps\\example.exe"),"new app auto grouping not enabled");
  settings.clearCaches();
  cache.SetValue(settings,FSharpOption<string>.Some("{\"Version\":\"test\",\"CombineTaskbarIconsByDefault\":false,\"AutoGroupingExcludedPaths\":[\"C:\\\\Apps\\\\example.exe\"],\"ProgramGroupPreferences\":{\"example.exe\":{\"MergeTaskbar\":\"false\",\"Alignment\":\"TopCenter\"}}}"));
  Check(!settings.settings.combineTaskbarIconsByDefault,"explicit global default lost");
  Check(!program.getAutoGroupingEnabled("c:\\apps\\EXAMPLE.EXE"),"opt-out path case mismatch");
  Check(program.getAutoGroupingEnabled("C:\\Apps\\other.exe"),"opt-out leaked to another program");
  Check(program.getGroupPreference("EXAMPLE.EXE","MergeTaskbar").Value=="false","merge override missing");
  Check(program.getGroupPreference("EXAMPLE.EXE","Alignment").Value=="TopCenter","alignment override missing");
  Console.WriteLine("PASS production settings reader: enabled defaults, disabled global merge, case-insensitive auto-group opt-out, program merge and center alignment; no settings writes");
  return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
