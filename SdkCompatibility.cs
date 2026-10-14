using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

static class SdkCompatibility {
 static ConstructorInfo constructor;
 public static ConstructorInfo SelectConstructor(Type type) {
  var selected=type.GetConstructor(new Type[]{typeof(string)}) ?? type.GetConstructor(Type.EmptyTypes);
  if(selected==null)throw new NotSupportedException("ProSimConnect has neither a public string constructor nor a public parameterless constructor.");
  return selected;
 }
 static void RequireMethod(Type type,string name,Type result,params Type[] args) {
  var method=type.GetMethod(name,args);
  if(method==null || method.ReturnType!=result)throw new NotSupportedException("Unsupported SDK method: "+type.FullName+"."+name);
 }
 public static void Initialize(string directory,Action<string> log) {
  string path=Path.GetFullPath(Path.Combine(directory,"ProSimSDK.dll"));
  var assembly=Assembly.LoadFrom(path);
  string loaded=assembly.Location;
  var version=FileVersionInfo.GetVersionInfo(loaded);
  using(var stream=File.OpenRead(loaded))using(var sha=SHA256.Create()) {
   log("SDK loaded path="+loaded+"; bytes="+stream.Length+"; SHA256="+BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","")+"; assembly="+assembly.FullName+"; file version="+version.FileVersion+"; product version="+version.ProductVersion);
  }
  if(!String.Equals(path,Path.GetFullPath(loaded),StringComparison.OrdinalIgnoreCase))throw new NotSupportedException("SDK loaded from an unexpected location: "+loaded+". Expected: "+path);
  Type connect=assembly.GetType("ProSimSDK.ProSimConnect",true);
  foreach(var c in connect.GetConstructors())log("SDK ProSimConnect constructor: "+c);
  constructor=SelectConstructor(connect);
  log("SDK selected constructor: "+constructor);
  RequireMethod(connect,"Connect",typeof(void),typeof(string),typeof(bool));
  RequireMethod(connect,"ReadDataRef",typeof(object),typeof(string));
  RequireMethod(connect,"get_isConnected",typeof(bool));
  RequireMethod(connect,"Dispose",typeof(void));
  if(!typeof(IDisposable).IsAssignableFrom(connect))throw new NotSupportedException("ProSimConnect does not implement IDisposable.");
  Type data=assembly.GetType("ProSimSDK.DataRef",true), state=assembly.GetType("ProSimSDK.DataRefStateEnum",true);
  if(data.GetConstructor(new Type[]{typeof(string),typeof(int),connect,typeof(bool)})==null)throw new NotSupportedException("Unsupported SDK DataRef constructor.");
  RequireMethod(data,"get_value",typeof(object));RequireMethod(data,"set_value",typeof(void),typeof(object));RequireMethod(data,"get_DataRefState",state);
  var changed=data.GetEvent("onDataChange");
  if(changed==null)throw new NotSupportedException("SDK DataRef.onDataChange event is missing.");
  RequireMethod(changed.EventHandlerType,"Invoke",typeof(void),data);
  if(!state.IsEnum || !Enum.IsDefined(state,"Valid"))throw new NotSupportedException("SDK DataRefStateEnum.Valid is missing.");
  log("SDK required API checks passed.");
 }
 public static object CreateConnection() {
  if(constructor==null)throw new InvalidOperationException("SDK compatibility has not been checked.");
  return constructor.Invoke(constructor.GetParameters().Length==0?new object[0]:new object[]{""});
 }
}
