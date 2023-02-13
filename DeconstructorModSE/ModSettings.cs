using DeconstructorModSE.Sync;
using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using VRage.Game;
using VRage.Utils;

namespace DeconstructorModSE
{
	[ProtoContract(UseProtoMembersOnly = true)]
	public class ModSettings
	{
		[ProtoMember(1)]
		public float Efficiency_Min = 0;

		[ProtoMember(2)]
		public float Efficiency_Max = 99;

		[ProtoMember(3)]
		public float Range = 1000; // in meters, radius of sphere around grinder to search for grids to grind

		[ProtoMember(4)]
		public float Power = 1f; // in MW

		public static void Save(ModSettings settings)
		{
			if (!MyAPIGateway.Session.IsServer) return;
			try
			{
				using (TextWriter writer = MyAPIGateway.Utilities.WriteFileInWorldStorage("ModSettings.xml", typeof(DeconstructorMod)))
				{
					writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
				}
			}
			catch
			{
				MyLog.Default.Error("[Deconstructor Mod]: Error saving mod settings.");
			}
		}

		public static ModSettings Load()
		{
			if (!MyAPIGateway.Session.IsServer)
			{
				PacketSettings p = new PacketSettings();
				p.RequestSettings();
				MyLog.Default.Info("[Deconstructor Mod] Requesting mod settings from server.");
				return default(ModSettings);
			}
			if (MyAPIGateway.Utilities.FileExistsInWorldStorage("ModSettings.xml", typeof(DeconstructorMod)))
			{
				ModSettings settings = null;
				try
				{
					using (TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage("ModSettings.xml", typeof(DeconstructorMod)))
					{
						settings = MyAPIGateway.Utilities.SerializeFromXML<ModSettings>(reader.ReadToEnd());
					}
					if (settings == null)
						settings = new ModSettings();
					return settings;
				}
				catch
				{
					MyLog.Default.Error("[Deconstructor Mod]: Error loading mod settings, using default values.");
				}
			}
			return default(ModSettings);
		}
	}
}