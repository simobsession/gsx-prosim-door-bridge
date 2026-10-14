using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Xml;

public class ProfileEdit {public string Section;public string Original;public string Installed;}
public class IntegrationState {
 public string ExePath,ProfilePath,ProfileOriginal,ProfileInstalledHash,AircraftPath;
 public bool ProfileExisted,Animation;
 public ProfileEdit[] ProfileEdits;
}
static class Installation {
 static readonly UTF8Encoding Utf8=new UTF8Encoding(false);
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer {MaxJsonLength=int.MaxValue};
 const string Entry="GSX ProSim Door Bridge", Original="(L:B738_door_0, number) 1 *", Fixed="(L:B738_door_0, number) (A:INTERACTIVE POINT OPEN:0, percent) 1.99 * max";
 const string AnimationPattern=@"(?s)(<Name>Pass_Door_Left_Front</Name>.*?<Code>)(.*?)(</Code>)";
 static string Pattern(string section){return @"(?ms)(\["+Regex.Escape(section)+@"\]\r?\n(?:(?!\[).)*?code\s*=\s*)([^\r\n]+)";}
 static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
 static string Edit(string text,string section,string value){var re=new Regex(Pattern(section));if(re.Matches(text).Count!=1)throw new InvalidOperationException("Expected one GSX door check in section "+section);return re.Replace(text,m=>m.Groups[1].Value+value);}
 static void Closed(){if(Process.GetProcessesByName("FlightSimulator2024").Length>0)throw new InvalidOperationException("Close MSFS before changing configuration.");}
 static XmlDocument Startup(string path){var xml=new XmlDocument();xml.Load(path);if(xml.DocumentElement.Name!="SimBase.Document")throw new InvalidOperationException("Unexpected MSFS startup file: "+path);return xml;}
 static void RemoveEntry(XmlDocument xml){foreach(XmlNode node in xml.SelectNodes("/SimBase.Document/Launch.Addon")){var name=node.SelectSingleNode("Name");if(name!=null && name.InnerText==Entry)node.ParentNode.RemoveChild(node);}}
 static byte[] XmlBytes(XmlDocument xml){using(var stream=new MemoryStream()){using(var writer=XmlWriter.Create(stream,new XmlWriterSettings {Encoding=Utf8,Indent=true,CloseOutput=false}))xml.Save(writer);return stream.ToArray();}}
 sealed class Change {public string Path;public byte[] Bytes;public DateTime? Time;}
 static List<Change> Animation(string aircraft,bool remove) {
  string layoutPath=Path.Combine(aircraft,"layout.json");
  var layout=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(layoutPath));
  var entries=(System.Collections.IEnumerable)layout["content"];
  var changes=new List<Change>();
  foreach(string variant in new[]{"Blended","Scimitar"}) {
   string relative="SimObjects/Airplanes/prosim-b738-2026/model."+variant+"/prosim-B738-2026.xml", path=Path.Combine(aircraft,relative);
   string text=File.ReadAllText(path);var re=new Regex(AnimationPattern);var matches=re.Matches(text);
   if(matches.Count!=1)throw new InvalidOperationException("Expected one L1 animation: "+path);
   string current=matches[0].Groups[2].Value.Trim();if(current!=Original && current!=Fixed)throw new InvalidOperationException("Unrecognized L1 animation; file preserved: "+path);
   Dictionary<string,object> entry=null;int count=0;
   foreach(Dictionary<string,object> item in entries)if((string)item["path"]==relative){entry=item;count++;}
   if(count!=1)throw new InvalidOperationException("Missing or duplicate layout entry: "+relative);
   string target=remove?Original:Fixed;if(current==target)continue;
   string patched=re.Replace(text,m=>m.Groups[1].Value+target+m.Groups[3].Value);
   var check=new XmlDocument();check.LoadXml("<root>"+Regex.Replace(patched,@"<\?xml[^?]*\?>","")+"</root>");
   byte[] bytes=Utf8.GetBytes(patched);DateTime stamp=DateTime.UtcNow;
   changes.Add(new Change {Path=path,Bytes=bytes,Time=stamp});entry["size"]=bytes.Length;entry["date"]=stamp.ToFileTimeUtc();
  }
  if(changes.Count>0)changes.Add(new Change {Path=layoutPath,Bytes=Utf8.GetBytes(Json.Serialize(layout))});
  return changes;
 }
 // All changes are preflighted. Keep content backups and roll back failed writes.
 static void Commit(string root,List<Change> changes) {
  string backup=Path.Combine(root,"Backups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(backup);
  var originals=new Dictionary<string,byte[]>();var times=new Dictionary<string,DateTime>();
  for(int i=0;i<changes.Count;i++) {
   string path=changes[i].Path;
   if(originals.ContainsKey(path))throw new InvalidOperationException("Duplicate write target: "+path);
   byte[] bytes=File.Exists(path)?File.ReadAllBytes(path):null;originals[path]=bytes;
   if(bytes!=null){times[path]=File.GetLastWriteTimeUtc(path);File.WriteAllBytes(Path.Combine(backup,i+"-"+Path.GetFileName(path)),bytes);}
  }
  string writing="";
  try {foreach(var c in changes){writing=c.Path;Directory.CreateDirectory(Path.GetDirectoryName(c.Path));if(c.Bytes==null){if(File.Exists(c.Path))File.Delete(c.Path);}else{File.WriteAllBytes(c.Path,c.Bytes);if(c.Time.HasValue)File.SetLastWriteTimeUtc(c.Path,c.Time.Value);if(Hash(File.ReadAllBytes(c.Path))!=Hash(c.Bytes))throw new IOException("Write verification failed.");}}}
  catch(Exception e) {
   var failures=new List<string>();
   foreach(var pair in originals)try{if(pair.Value==null){if(File.Exists(pair.Key))File.Delete(pair.Key);}else{File.WriteAllBytes(pair.Key,pair.Value);File.SetLastWriteTimeUtc(pair.Key,times[pair.Key]);}}catch(Exception r){failures.Add(pair.Key+": "+r.Message);}
   throw new IOException("Could not update "+writing+". "+e.Message+" Backups: "+backup+(failures.Count==0?". Previous file contents restored.":". Restoration errors: "+String.Join("; ",failures)),e);
  }
 }
 public static string Configure(string root,string sdk,string host,string config,string profile,string aircraft,bool animation) {
  Closed();root=Path.GetFullPath(root);config=Path.GetFullPath(config);profile=Path.GetFullPath(profile);
  if(!File.Exists(Path.Combine(sdk,"ProSimSDK.dll")))throw new InvalidOperationException("ProSimSDK.dll missing in "+sdk);
  if(Regex.IsMatch(profile,@"(?i)[\\/]Community[\\/]|[\\/]SimObjects[\\/]"))throw new InvalidOperationException("GSX profile must be the user-profile folder under %APPDATA%\\Virtuali\\Airplanes\\prosim-b738-2026\\800, not the aircraft package.");
  string exe=Path.Combine(config,"EXE.xml"),cfg=Path.Combine(profile,"gsx.cfg"),statePath=Path.Combine(root,"IntegrationState.json");
  var xml=Startup(exe);bool existed=File.Exists(cfg);
  byte[] before=File.ReadAllBytes(existed?cfg:Path.Combine(root,"Profiles","prosim-b738-2026-gsx.cfg"));string text=Encoding.UTF8.GetString(before);
  IntegrationState state=File.Exists(statePath)?Json.Deserialize<IntegrationState>(File.ReadAllText(statePath)):null;
  if(state!=null && (!String.Equals(state.ExePath,exe,StringComparison.OrdinalIgnoreCase)||!String.Equals(state.ProfilePath,cfg,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Uninstall the recorded integration before changing target folders.");
  var edits=new List<ProfileEdit>();
  foreach(var pair in new[]{new[]{"exit1","(L:Tony_GSX_ProSim_L1_Open, bool)"},new[]{"service2","(L:B738_door_3, number) 1*"}}) {
   var m=Regex.Matches(text,Pattern(pair[0]));if(m.Count!=1)throw new InvalidOperationException("Unexpected GSX section: "+pair[0]);string old=m[0].Groups[2].Value;
   if(pair[0]=="exit1" && old==pair[1])old="(L:B738_door_0, number) 1*";
   edits.Add(new ProfileEdit {Section=pair[0],Original=old,Installed=pair[1]});text=Edit(text,pair[0],pair[1]);
  }
  if(state==null)state=new IntegrationState {ExePath=exe,ProfilePath=cfg,ProfileExisted=existed,ProfileOriginal=existed?Convert.ToBase64String(Encoding.UTF8.GetString(before).Contains("(L:Tony_GSX_ProSim_L1_Open, bool)")?Utf8.GetBytes(Encoding.UTF8.GetString(before).Replace("(L:Tony_GSX_ProSim_L1_Open, bool)","(L:B738_door_0, number) 1*")):before):"",ProfileEdits=edits.ToArray()};
  var changes=new List<Change>();
  if(animation){aircraft=Path.GetFullPath(aircraft);if(state.Animation && !String.Equals(state.AircraftPath,aircraft,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Uninstall before changing aircraft folders.");changes.AddRange(Animation(aircraft,false));state.Animation=true;state.AircraftPath=aircraft;}
  byte[] installed=Utf8.GetBytes(text);state.ProfileInstalledHash=Hash(installed);changes.Add(new Change {Path=cfg,Bytes=installed});
  RemoveEntry(xml);XmlElement entry=xml.CreateElement("Launch.Addon");
  foreach(var pair in new[]{new[]{"Name",Entry},new[]{"Disabled","False"},new[]{"ManualLoad","False"},new[]{"Path",Path.Combine(root,"GSX-ProSim-Door-Bridge.exe")},new[]{"NewConsole","False"}}){var n=xml.CreateElement(pair[0]);n.InnerText=pair[1];entry.AppendChild(n);}xml.DocumentElement.AppendChild(entry);
  changes.Add(new Change {Path=exe,Bytes=XmlBytes(xml)});
  var settings=new XmlDocument();settings.LoadXml("<Bridge><ProSimDirectory/><ProSimHost/><SimulatorProcess>FlightSimulator2024</SimulatorProcess></Bridge>");settings.SelectSingleNode("/Bridge/ProSimDirectory").InnerText=Path.GetFullPath(sdk);settings.SelectSingleNode("/Bridge/ProSimHost").InnerText=host;
  changes.Add(new Change {Path=Path.Combine(root,"BridgeSettings.xml"),Bytes=XmlBytes(settings)});
  changes.Add(new Change {Path=statePath,Bytes=Utf8.GetBytes(Json.Serialize(state))});Commit(root,changes);
  string stop=Path.Combine(root,"STOP");if(File.Exists(stop))File.Delete(stop);
  return "GSX profile, aircraft integration and MSFS autostart configured. Backups are in the portable folder.";
 }
 public static string Uninstall(string root) {
  Closed();root=Path.GetFullPath(root);string statePath=Path.Combine(root,"IntegrationState.json");
  if(!File.Exists(statePath))throw new InvalidOperationException("No restoration record in this folder. Apply configuration first for a legacy installation.");
  var state=Json.Deserialize<IntegrationState>(File.ReadAllText(statePath));var changes=new List<Change>();
  if(File.Exists(state.ProfilePath)) {
   byte[] current=File.ReadAllBytes(state.ProfilePath);string text=Encoding.UTF8.GetString(current);
   if(!state.ProfileExisted){if(Hash(current)!=state.ProfileInstalledHash)throw new InvalidOperationException("The GSX profile created by the utility was modified; preserve those changes before removal.");changes.Add(new Change {Path=state.ProfilePath,Bytes=null});}
   else {
    byte[] restored;
    if(Hash(current)==state.ProfileInstalledHash)restored=Convert.FromBase64String(state.ProfileOriginal);
    else {foreach(var edit in state.ProfileEdits){var m=Regex.Matches(text,Pattern(edit.Section));if(m.Count!=1)throw new InvalidOperationException("GSX section changed: "+edit.Section);string value=m[0].Groups[2].Value;if(value==edit.Installed)text=Edit(text,edit.Section,edit.Original);else if(value!=edit.Original)throw new InvalidOperationException("GSX door check changed: "+edit.Section);}restored=Utf8.GetBytes(text);}
    changes.Add(new Change {Path=state.ProfilePath,Bytes=restored});
   }
  }
  if(state.Animation)changes.AddRange(Animation(state.AircraftPath,true));
  if(File.Exists(state.ExePath)){var xml=Startup(state.ExePath);RemoveEntry(xml);changes.Add(new Change {Path=state.ExePath,Bytes=XmlBytes(xml)});}
  File.WriteAllText(Path.Combine(root,"STOP"),"");DateTime deadline=DateTime.UtcNow.AddSeconds(15);
  while(true){bool running=false;foreach(var p in Process.GetProcessesByName("GSX-ProSim-Door-Bridge")){using(p){try{if(String.Equals(Path.GetDirectoryName(p.MainModule.FileName),root,StringComparison.OrdinalIgnoreCase))running=true;}catch{throw new InvalidOperationException("Cannot verify bridge shutdown. Exit it from the tray before uninstalling.");}}}if(!running)break;if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Exit the bridge from its tray menu and retry.");Thread.Sleep(250);}
  foreach(string name in new[]{"IntegrationState.json","BridgeSettings.xml","STOP","bridge-exe.log"})changes.Add(new Change {Path=Path.Combine(root,name),Bytes=null});
  Commit(root,changes);
  return "Integration removed. GSX checks restored, L1 animation reversed, autostart removed and runtime settings cleared. Close this window and delete the portable folder, including local backups.";
 }
}

