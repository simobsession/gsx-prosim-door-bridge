using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

class Setup : Form {
 readonly TextBox install=new TextBox(), prosim=new TextBox(), host=new TextBox(), config=new TextBox(), aircraft=new TextBox(), profile=new TextBox();
 readonly TextBox output=new TextBox();
 readonly CheckBox animation=new CheckBox();
 readonly Button run=new Button();
 static string Root=AppDomain.CurrentDomain.BaseDirectory;
 static string Quote(string s) { return "'"+s.Replace("'","''")+"'"; }
 public Setup() {
  Text="GSX ProSim Door Bridge Configuration — 0.2.5"; ClientSize=new System.Drawing.Size(740,570); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen;
  Controls.Add(new Label { Left=18,Top=15,Width=700,Height=40,Text="Configure this portable utility, corrected GSX profile and L1 animation fix.\nClose MSFS before installing. ProSimSDK.dll must be in your ProSim folder." });
  install.Text=Root.TrimEnd(Path.DirectorySeparatorChar);
  config.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft Flight Simulator 2024");
  profile.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),@"Virtuali\Airplanes\prosim-b738-2026\800");
  prosim.Text=Directory.Exists(@"C:\ProSimB738")?@"C:\ProSimB738":""; host.Text="127.0.0.1";
  try { foreach(var p in Process.GetProcessesByName("ProSimB738")) { prosim.Text=Path.GetDirectoryName(p.MainModule.FileName); break; } } catch {}
  aircraft.Text=Path.Combine(config.Text,@"Packages\Community\prosim-b738");
  try { var m=Regex.Match(File.ReadAllText(Path.Combine(config.Text,"UserCfg.opt")),"(?m)^InstalledPackagesPath\\s+\"([^\"]+)\""); if(m.Success) aircraft.Text=Path.Combine(m.Groups[1].Value,@"Community\prosim-b738"); } catch {}
  Row("Portable app folder",install,65,true); Row("ProSim folder",prosim,105,true); Row("ProSim host / IP",host,145,false); Row("MSFS config folder",config,185,true); Row("ProSim aircraft package",aircraft,225,true); Row("GSX profile folder",profile,265,true);
  animation.SetBounds(18,310,680,25); animation.Text="Install L1 animation fix (backs up aircraft files)"; animation.Checked=true; Controls.Add(animation);
  run.SetBounds(18,350,160,32); run.Text="Apply configuration"; run.Click+=async (s,e)=>await Install(); Controls.Add(run);
  var close=new Button {Left=555,Top=350,Width=160,Height=32,Text="Close"}; close.Click+=(s,e)=>Close(); Controls.Add(close);
  output.SetBounds(18,400,697,150); output.Multiline=true; output.ReadOnly=true; output.ScrollBars=ScrollBars.Vertical; Controls.Add(output);
  FormClosing+=(s,e)=> { if(!run.Enabled) e.Cancel=true; };
 }
 void Row(string label,TextBox box,int y,bool browse) {
  Controls.Add(new Label {Left=18,Top=y+5,Width=165,Text=label}); box.SetBounds(185,y,browse?440:530,25); Controls.Add(box);
  if(browse) { var b=new Button {Left=635,Top=y-1,Width=80,Height=28,Text="Browse…"}; b.Click+=(s,e)=> { using(var d=new FolderBrowserDialog {Description=label,SelectedPath=box.Text}) {if(d.ShowDialog(this)==DialogResult.OK) box.Text=d.SelectedPath;} }; Controls.Add(b); }
 }
 async Task Install() {
  try {
   if(Process.GetProcessesByName("FlightSimulator2024").Length>0) throw new Exception("Close MSFS before installing or updating.");
   
   if(!File.Exists(Path.Combine(prosim.Text,"ProSimSDK.dll"))) throw new Exception("Choose the ProSim folder containing ProSimSDK.dll.");
   if(!File.Exists(Path.Combine(config.Text,"EXE.xml"))) throw new Exception("Choose the MSFS config folder containing EXE.xml.");
   if(animation.Checked && !File.Exists(Path.Combine(aircraft.Text,"layout.json"))) throw new Exception("Choose the installed prosim-b738 aircraft package folder containing layout.json.");
   if(String.IsNullOrWhiteSpace(host.Text)||String.IsNullOrWhiteSpace(profile.Text)) throw new Exception("Enter the ProSim host and GSX profile folder.");
   string dest=Path.GetFullPath(install.Text.Trim());
   if(dest.StartsWith(Path.GetTempPath(),StringComparison.OrdinalIgnoreCase)) throw new Exception("Choose a permanent installation folder outside the temporary folder.");
   run.Enabled=false; output.Text="Applying configuration…";
   string[] files={"GSX-ProSim-Door-Bridge.exe","SimConnect.dll","Install.ps1","Patch-L1Animation.ps1","Start-Bridge.ps1","Stop-Bridge.ps1","README.txt","INSTALLATION.txt","Setup.exe"};
   foreach(string f in files) if(!File.Exists(Path.Combine(Root,f))) throw new Exception("Installer file missing: "+f+". Extract the complete ZIP first.");
   Directory.CreateDirectory(dest);
   foreach(string f in files) {string src=Path.GetFullPath(Path.Combine(Root,f)), dst=Path.Combine(dest,f); if(!String.Equals(src,dst,StringComparison.OrdinalIgnoreCase)) File.Copy(src,dst,true);}
   Directory.CreateDirectory(Path.Combine(dest,"Profiles"));
   string cfg="prosim-b738-2026-gsx.cfg"; string srcCfg=Path.Combine(Root,"Profiles",cfg), dstCfg=Path.Combine(dest,"Profiles",cfg);
   if(!String.Equals(Path.GetFullPath(srcCfg),Path.GetFullPath(dstCfg),StringComparison.OrdinalIgnoreCase)) File.Copy(srcCfg,dstCfg,true);
   string command="$ErrorActionPreference='Stop'; try { & "+Quote(Path.Combine(dest,"Install.ps1"))+" -ProSimDirectory "+Quote(prosim.Text)+" -ProSimHost "+Quote(host.Text)+" -SimulatorConfigDirectory "+Quote(config.Text)+" -AircraftProfileDirectory "+Quote(profile.Text)+" -AircraftPackageDirectory "+Quote(aircraft.Text)+(animation.Checked?"":" -SkipAnimationFix")+"; exit 0 } catch { Write-Output $_.Exception.Message; exit 1 }";
   string encoded=Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
   var psi=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand "+encoded) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   string result=await Task.Run(()=> { using(var p=Process.Start(psi)) { var a=p.StandardOutput.ReadToEndAsync(); var b=p.StandardError.ReadToEndAsync(); p.WaitForExit(); Task.WaitAll(a,b); if(p.ExitCode!=0) throw new Exception(a.Result+Environment.NewLine+b.Result); return a.Result; } });
   output.Text="Configuration applied.\r\n"+result+"\r\nRestart Couatl to load the GSX profile. The next MSFS launch starts the bridge. Restart the utility after changing its settings.\r\nInstalled in: "+dest;
  } catch(Exception ex) { output.Text="Installation did not complete.\r\n"+ex.Message; MessageBox.Show(this,ex.Message,"Installation",MessageBoxButtons.OK,MessageBoxIcon.Error); }
  finally {run.Enabled=true;}
 }
 [STAThread] static void Main(string[] args) { if(args.Length==1 && args[0]=="--self-test") { if(Quote("a'b")!="'a''b'") Environment.Exit(1); return; } Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new Setup()); }
}

