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
		public List<TierSettings> BlockConfigs = new List<TierSettings>
		{
			new TierSettings("Deconstructor_1", 250, 1, 25),
			new TierSettings("Deconstructor_2", 500, 2, 50),
			new TierSettings("Deconstructor_3", 1000, 3, 75),
			new TierSettings("Deconstructor_4", 2000, 4, 99)
		};

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
				p.RequestModSettings();
				MyLog.Default.Info("[Deconstructor Mod]: Requesting mod settings from server.");
				return new ModSettings();
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
			return new ModSettings();
		}

		[ProtoContract(UseProtoMembersOnly = true)]
		public class TierSettings
		{
			[ProtoMember(1)]
			public readonly string SubtypeName = "Deconstructor"; // SubType name of block

			[ProtoMember(2)]
			public readonly float Range = 250; // in meters, radius of sphere around grinder to search for grids to grind

			[ProtoMember(3)]
			public readonly float Power = 1f; // base power consumption (not grinding) in MW. when grinding, increases according to efficiency.

			[ProtoMember(4)]
			public readonly float Efficiency = 25; // higher = faster grinding speed (max 100 = instant grinding)

			public TierSettings() { }

			public TierSettings(string subtypeName, float range, float power, float efficiency)
			{
				SubtypeName = subtypeName;
				Range = range;
				Power = power;
				Efficiency = efficiency;
			}
		}
	}
}