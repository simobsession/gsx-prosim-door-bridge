using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
class Signals : IDisposable {
 IntPtr handle;
 uint next=1;
 Dictionary<string,uint> ids=new Dictionary<string,uint>();
 Dictionary<uint,double> values=new Dictionary<uint,double>();
 Dictionary<uint,DateTime> times=new Dictionary<uint,DateTime>();
 HashSet<uint> mappedEvents=new HashSet<uint>();
 const string Dll="SimConnect.dll";
 [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] static extern int SimConnect_Open(out IntPtr h,string name,IntPtr window,uint message,IntPtr eventHandle,uint config);
 [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] static extern int SimConnect_Close(IntPtr h);
 [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] static extern int SimConnect_AddToDataDefinition(IntPtr h,uint definition,string name,string units,int type,float epsilon,uint datum);
 [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] static extern int SimConnect_RequestDataOnSimObject(IntPtr h,uint request,uint definition,uint obj,int period,uint flags,uint origin,uint interval,uint limit);
 [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] static extern int SimConnect_GetNextDispatch(IntPtr h,out IntPtr data,out uint size);
 [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] static extern int SimConnect_SetDataOnSimObject(IntPtr h,uint definition,uint obj,uint flags,uint count,uint size,ref double value);
 [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] static extern int SimConnect_MapClientEventToSimEvent(IntPtr h,uint id,string name);
 [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] static extern int SimConnect_TransmitClientEvent_EX1(IntPtr h,uint obj,uint id,uint priority,uint flags,uint d0,uint d1,uint d2,uint d3,uint d4);
 static void Check(int hr) { if(hr<0) Marshal.ThrowExceptionForHR(hr); }
 public Signals() { Check(SimConnect_Open(out handle,"GSX ProSim Door Bridge",IntPtr.Zero,0,IntPtr.Zero,0)); }
 public void Subscribe(string name) {
  SubscribeDefinition(name,"L:"+name,"number");
 }
 public void SubscribeDefinition(string name,string simvar,string units) {
  if(ids.ContainsKey(name))return;uint id=next++;ids[name]=id;
  Check(SimConnect_AddToDataDefinition(handle,id,simvar,units,4,0,uint.MaxValue));
  Check(SimConnect_RequestDataOnSimObject(handle,id,id,0,4,0,0,0,0));
 }
 public void Poll() {
  IntPtr data;uint size;int count=0;
  while(count++<500 && SimConnect_GetNextDispatch(handle,out data,out size)==0) {
   int kind=Marshal.ReadInt32(data,8);
   if(kind==3)throw new Exception("Simulator connection closed");
   if(kind==1)throw new Exception("SimConnect exception "+Marshal.ReadInt32(data,12));
   if(kind==8 && size>=48) { uint id=(uint)Marshal.ReadInt32(data,12);values[id]=BitConverter.Int64BitsToDouble(Marshal.ReadInt64(data,40));times[id]=DateTime.UtcNow; }
  }
 }
 public bool TryRead(string name,out double value) {
  value=0;uint id;DateTime time;
  return ids.TryGetValue(name,out id) && times.TryGetValue(id,out time) && (DateTime.UtcNow-time).TotalSeconds<3 && values.TryGetValue(id,out value);
 }
 public void Set(string name,double value) { Subscribe(name);Check(SimConnect_SetDataOnSimObject(handle,ids[name],0,0,0,8,ref value)); }
 public void SetNativeL1(bool open) {
  uint id=open?10001u:10002u;
  if(mappedEvents.Add(id))Check(SimConnect_MapClientEventToSimEvent(handle,id,open?"OPEN_AIRCRAFT_DOORS":"CLOSE_AIRCRAFT_DOORS"));
  Check(SimConnect_TransmitClientEvent_EX1(handle,0,id,1,16,1,0,0,0,0));
 }
 public void Dispose() { if(handle!=IntPtr.Zero){SimConnect_Close(handle);handle=IntPtr.Zero;} }
}
