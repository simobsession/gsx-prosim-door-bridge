using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml;
using System.Collections.Concurrent;
using System.Windows.Forms;
using System.Drawing;
using ProSimSDK;

class Bridge {
 static string Root = AppDomain.CurrentDomain.BaseDirectory;
 static string ProSimDirectory, ProSimHost, SimulatorProcess;
 static volatile bool ExitRequested;
 static volatile string Status="Waiting for MSFS";
 static string FatalMessage;
 static void ShowFailure(Exception e) {
  Log("Fatal: "+e);
  MessageBox.Show("The door bridge could not start or continue and will now exit.\n\n"+e.GetBaseException().Message+"\n\nCheck the configured ProSim folder. Send bridge-exe.log from this folder for diagnosis:\n"+Root,"GSX ProSim Door Bridge",MessageBoxButtons.OK,MessageBoxIcon.Error);
 }
 static bool Stopping() { return ExitRequested || File.Exists(Path.Combine(Root,"STOP")); }
 sealed class Tray : ApplicationContext {
  readonly NotifyIcon icon; readonly System.Windows.Forms.Timer timer;
  public Tray() {
   var menu=new ContextMenuStrip();
   var status=menu.Items.Add(Status); status.Enabled=false;
   menu.Items.Add("Configure / apply GSX profile",null,delegate { Process.Start(Path.Combine(Root,"Setup.exe")); });
   menu.Items.Add("Open log",null,delegate { string p=Path.Combine(Root,"bridge-exe.log"); if(File.Exists(p))Process.Start("notepad.exe",p); });
   menu.Items.Add("Exit bridge",null,delegate { ExitRequested=true; Status="Stopping"; });
   icon=new NotifyIcon {Icon=new Icon(typeof(Bridge).Assembly.GetManifestResourceStream("DoorBridge.ico"),32,32),Text="GSX ProSim Door Bridge",ContextMenuStrip=menu,Visible=true};
   timer=new System.Windows.Forms.Timer {Interval=500}; timer.Tick+=delegate {status.Text=Status;icon.Text="GSX Door Bridge: "+Status;}; timer.Start();
   var worker=new Thread(delegate() {try { Run(); }catch(Exception e){Log("Fatal: "+e);FatalMessage=e.GetBaseException().Message;Status="Error — see log";} finally {ExitRequested=true;} }); worker.IsBackground=true;worker.Start();
   timer.Tick+=delegate {if(ExitRequested && !worker.IsAlive) {try {if(FatalMessage!=null)MessageBox.Show("The bridge stopped and will exit.\n\n"+FatalMessage+"\n\nSend bridge-exe.log from:\n"+Root,"GSX ProSim Door Bridge",MessageBoxButtons.OK,MessageBoxIcon.Error);}finally{ExitThread();}}};
  }
  protected override void ExitThreadCore() {timer.Stop();timer.Dispose();icon.Visible=false;icon.Icon.Dispose();icon.Dispose();base.ExitThreadCore();}
 }
 static void Log(string text) { try { File.AppendAllText(Path.Combine(Root,"bridge-exe.log"),DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")+" "+text+Environment.NewLine); } catch {} }
 class Door {
  public string Name,Signal,Variable; public DataRef Ref; public bool Handled; public DateTime LowSince,LastCommand;
  public Door(string n,string s,string v) { Name=n;Signal="FSDT_GSX_AIRCRAFT_"+s+"_TOGGLE";Variable=v; }
 }
 static bool SimRunning() { return Process.GetProcessesByName(SimulatorProcess).Length>0; }
 static void ReadSettings() {
  var xml=new XmlDocument();xml.Load(Path.Combine(Root,"BridgeSettings.xml"));
  ProSimDirectory=xml.SelectSingleNode("/Bridge/ProSimDirectory").InnerText;
  ProSimHost=xml.SelectSingleNode("/Bridge/ProSimHost").InnerText;
  SimulatorProcess=xml.SelectSingleNode("/Bridge/SimulatorProcess").InnerText;
 }
 static void SelfTest() { Log("Self-test: SDK="+typeof(ProSimConnect).Assembly.FullName+"; x64="+Environment.Is64BitProcess); }
 [STAThread] static void Main(string[] args) {
  Log("Bridge 0.2.11 starting; executable="+Application.ExecutablePath);
  try { ReadSettings(); } catch(Exception e) { ShowFailure(e);Environment.ExitCode=1;return; }
  if(String.IsNullOrWhiteSpace(ProSimDirectory) || !File.Exists(Path.Combine(ProSimDirectory,"ProSimSDK.dll"))) {
   MessageBox.Show("Choose your ProSim folder in the configuration window, then start the bridge again.","GSX ProSim Door Bridge");
   Process.Start(Path.Combine(Root,"Setup.exe")); return;
  }
  AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender,ResolveEventArgs e) {
   string path=Path.Combine(ProSimDirectory,new AssemblyName(e.Name).Name+".dll");
   return File.Exists(path)?Assembly.LoadFrom(path):null;
  };
  try {SdkCompatibility.Initialize(ProSimDirectory,Log);} catch(Exception e) {
   if(Array.IndexOf(args,"--self-test")>=0)Log("SDK compatibility failure: "+e);else ShowFailure(e);
   Environment.ExitCode=1;return;
  }
  if(args.Length==2 && args[0]=="--watch-process-test") {
   try { using(var p=Process.GetProcessById(int.Parse(args[1]))) { p.WaitForExit(); } Log("Lifecycle test: watched process exited; bridge exits"); } catch(ArgumentException) { Log("Lifecycle test: process already exited"); } return;
  }
  if(Array.IndexOf(args,"--self-test")>=0) { try { SelfTest();Log("Self-test: direct SimConnect, no FSUIPC"); } catch(Exception e) { Log("Self-test failed: "+e); } return; }
  using(var mutex=new Mutex(false,@"Local\TonyGSXProSimDoorBridge")) {
   if(!mutex.WaitOne(0)) { Log("Another bridge is running; exiting");return; }
   try { string stop=Path.Combine(Root,"STOP"); if(File.Exists(stop))File.Delete(stop); Application.EnableVisualStyles(); Application.Run(new Tray()); } catch(Exception e) { ShowFailure(e);Environment.ExitCode=1; } finally { mutex.ReleaseMutex(); }
  }
 }
 static void Run() {
  // EXE.xml may launch us before the simulator process becomes visible.
  while(!SimRunning() && !Stopping()) Thread.Sleep(500);
  if(Stopping())return;
  Log("Started; lifetime follows MSFS 2024; all six doors automatic");

  try {
   while(SimRunning() && !Stopping()) {
    using(var connection=(ProSimConnect)SdkCompatibility.CreateConnection()) {
     try {
      connection.Connect(ProSimHost,true);
      Door[] doors={new Door("fwd_entry","EXIT_1","Tony_GSX_ProSim_L1_Open"),new Door("aft_entry","EXIT_4","B738_door_1"),new Door("fwd_service","SERVICE_1","B738_door_2"),new Door("aft_service","SERVICE_2","B738_door_3"),new Door("fwd_cargo","CARGO_1","B738_door_4"),new Door("aft_cargo","CARGO_2","B738_door_5")};
      var l1Changes=new ConcurrentQueue<bool>();
      foreach(var d in doors) {
       d.Ref=new DataRef("doors."+d.Name,d.Name=="fwd_entry"?50:250,connection,true);
       if(d.Name=="fwd_entry") {
        bool? last=null;
        d.Ref.onDataChange += delegate(DataRef changed) {
         bool value=Convert.ToBoolean(changed.value);
         if(last.HasValue && last.Value!=value)l1Changes.Enqueue(value);
         last=value;
        };
       }
      }
      using(var signals=new Signals()) {
      foreach(var d in doors){signals.Subscribe(d.Signal);signals.Subscribe(d.Variable);}
      signals.SubscribeDefinition("NativeL1","INTERACTIVE POINT OPEN:0","percent");
      Log("ProSim and direct SimConnect connected");Status="Connected";string previous="";
      while(SimRunning() && connection.isConnected && !Stopping()) {
       signals.Poll();
       bool changedOpen;double changedNative;
       while(l1Changes.TryDequeue(out changedOpen)) {
        if(signals.TryRead("NativeL1",out changedNative) && ((changedOpen && changedNative==0) || (!changedOpen && changedNative>0))) {
         signals.SetNativeL1(changedOpen);
         Log("EVENT ProSim/CDU L1="+changedOpen+" synchronized to native door");
        }
       }
       string snapshot="";
       bool l1Open=Convert.ToBoolean(connection.ReadDataRef("doors.fwd_entry"));
       foreach(var d in doors) {
        if(d.Ref.DataRefState!=DataRefStateEnum.Valid) continue;
        if(d.Name=="fwd_entry") {
         double nativePosition;
         if(!signals.TryRead("NativeL1",out nativePosition))continue;
         // Keep GSX waiting until both ProSim and native MSFS have closed L1.
         signals.Set(d.Variable,(l1Open || nativePosition>1)?1:0);
        }
        double request,position;
        if(!signals.TryRead(d.Signal,out request) || !signals.TryRead(d.Variable,out position))continue;
        if(d.Name=="fwd_entry") {
         // Wait for the published value to return through SimConnect before
         // interpreting a GSX request. The initial cache can still contain zero.
         double nativePosition;
         if(!signals.TryRead("NativeL1",out nativePosition))continue;
         if((position!=0)!=(l1Open || nativePosition>1))continue;
         position=position*199;
        }
        snapshot+=d.Name+":request="+request+",position="+position+",ProSim="+d.Ref.value+"; ";
        DateTime now=DateTime.UtcNow;
        if(request==0) { if(d.LowSince==DateTime.MinValue)d.LowSince=now; if((now-d.LowSince).TotalSeconds>=1)d.Handled=false;continue; }
        d.LowSince=DateTime.MinValue;
        if(request!=1 || d.Handled || (now-d.LastCommand).TotalSeconds<10 || (position>0 && position<199))continue;
        if(!Convert.ToBoolean(connection.ReadDataRef("aircraft.ground")) || Convert.ToDouble(connection.ReadDataRef("aircraft.speed.ground"))>1)continue;
        bool target=position==0;
        if(d.Name=="fwd_entry")signals.SetNativeL1(target);
        d.Ref.value=target;d.Handled=true;d.LastCommand=now;
        Log("COMMAND doors."+d.Name+"="+target+"; previous aircraft position="+position);
       }
       if(snapshot!=previous){Log(snapshot);previous=snapshot;}
       Thread.Sleep(250);
      }
      }
     } catch(Exception e) { if(e is MissingMemberException || e is TypeLoadException || e is System.IO.FileLoadException || e is BadImageFormatException)throw; Status="Reconnecting";Log("Waiting/reconnecting: "+e.Message); }
    }
    for(int i=0;i<10 && SimRunning() && !Stopping();i++)Thread.Sleep(500);
   }
  } finally { Log("Stopped; simulator closed or stop requested"); }
 }
}



