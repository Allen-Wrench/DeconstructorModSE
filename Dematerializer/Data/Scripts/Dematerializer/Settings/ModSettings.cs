﻿using Dematerializer.Sync;
using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using VRage.Game;
using VRage.Utils;

namespace Dematerializer
{
	[Serializable]
	[ProtoContract]
	public class ModSettings
	{
		[ProtoMember(1)]
		public TierSettings Tier1 = new TierSettings("Dematerializer_1", 500, 1000, 75);

		[ProtoMember(2)]
		public TierSettings Tier2 = new TierSettings("Dematerializer_2", 1000, 2500, 82.5f);

		[ProtoMember(3)]
		public TierSettings Tier3 = new TierSettings("Dematerializer_3", 1500, 5000, 87.5f);

		[ProtoMember(4)]
		public TierSettings Tier4 = new TierSettings("Dematerializer_4", 2000, 10000, 95);
		

		public ModSettings() { }

		public static void Save(ModSettings settings)
		{
			if (!MyAPIGateway.Session.IsServer) return;
			if (settings == null)
				settings = new ModSettings();
			try
			{
				using (TextWriter writer = MyAPIGateway.Utilities.WriteFileInWorldStorage("ModSettings.xml", typeof(DematerializerBlock)))
				{
					writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
				}
			}
			catch
			{
				MyLog.Default.Error("[Dematerializer Mod]: Error saving mod settings.");
			}
		}

		public static ModSettings Load()
		{
			if (!MyAPIGateway.Session.IsServer)
			{
				PacketSettings p = new PacketSettings();
				p.RequestModSettings();
				MyLog.Default.Info("[Dematerializer Mod]: Requesting mod settings from server.");
				return null;
			}

			ModSettings settings = new ModSettings();
			if (MyAPIGateway.Utilities.FileExistsInWorldStorage("ModSettings.xml", typeof(DematerializerBlock)))
			{
				try
				{
					using (TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage("ModSettings.xml", typeof(DematerializerBlock)))
					{
						settings = MyAPIGateway.Utilities.SerializeFromXML<ModSettings>(reader.ReadToEnd());
					}
				}
				catch
				{
					MyLog.Default.Error("[Dematerializer Mod]: Error loading mod settings, using default values.");
					settings = new ModSettings();
				}
			}

			Save(settings);
			return settings;
		}
	}

	[Serializable]
	[ProtoContract]
	public class TierSettings
	{
		[ProtoMember(100)]
		public string SubtypeName; // SubType name of block

		[ProtoMember(101)]
		public float Range; // in meters, radius of sphere around grinder to search for grids to grind

		[ProtoMember(102)]
		public float Power; // power consumption in MW when grinding.

		[ProtoMember(103)]
		public float Efficiency; // higher = faster grinding speed (max 100 = instant grinding (i think))

		public TierSettings()
		{
			SubtypeName = "Dematerializer";
			Range = 500;
			Power = 100;
			Efficiency = 75;
		}

		public TierSettings(string subtypeName, float range, float power, float efficiency)
		{
			SubtypeName = subtypeName;
			Range = range;
			Power = power;
			Efficiency = efficiency;
		}
	}
}