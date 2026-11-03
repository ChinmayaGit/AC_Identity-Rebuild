using System;
using System.Reflection;
using System.Runtime.Loader;
class P {
  static void Main() {
    var dir = "/Users/chinmaya/Documents/Projects/Own/AC Identity/work/apk/assets/bin/Data/Managed";
    var alc = new AssemblyLoadContext("game", true);
    alc.Resolving += (c, name) => {
      var path = System.IO.Path.Combine(dir, name.Name + ".dll");
      Console.WriteLine("resolve " + name.Name + " -> " + path + " exists=" + System.IO.File.Exists(path));
      if (System.IO.File.Exists(path)) return c.LoadFromAssemblyPath(path);
      return null;
    };
    try {
      var proto = alc.LoadFromAssemblyPath(dir + "/protobuf-net.dll");
      Console.WriteLine("protobuf " + proto.GetName().Version);
      var acier = alc.LoadFromAssemblyPath(dir + "/AcierProtocol.dll");
      Console.WriteLine("acier types " + acier.GetTypes().Length);
      var ser = alc.LoadFromAssemblyPath(dir + "/AcierProtocolSerializer.dll");
      Console.WriteLine("serializer " + ser.GetTypes()[0].FullName);
      var t = ser.GetType("AcierProtocolSerializer");
      var obj = Activator.CreateInstance(t);
      Console.WriteLine("instance " + obj);
    } catch (Exception ex) {
      Console.WriteLine(ex.GetType().Name + ": " + ex.Message);
      if (ex is ReflectionTypeLoadException r) {
        foreach (var e in r.LoaderExceptions) Console.WriteLine("  " + e.Message);
      }
      Console.WriteLine(ex);
    }
  }
}
