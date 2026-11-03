using System;
using System.IO;
using System.Reflection;

// Let test building a SerializedNode for AnimusGlobe
var proto = Assembly.LoadFrom("work/apk/assets/bin/Data/Managed/AcierProtocol.dll");
var game = Assembly.LoadFrom("work/apk/assets/bin/Data/Managed/Assembly-CSharp.dll");

var nodeType = game.GetType("Assets.Scripts.Network.Missions.Mission.Serialization.SerializedNode");
Console.WriteLine("nodeType: " + nodeType);
