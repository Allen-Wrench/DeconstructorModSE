using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace Dematerializer
{
	[ProtoContract]
	public class ClientSettings
	{
		[ProtoMember(1)]
		public List<string> HiddenGrids = new List<string>();

		[ProtoMember(2)]
		public List<string> BlacklistedItems = new List<string>();

		public void Save() => Save(this);

		public static void Save(ClientSettings settings)
		{
			if (MyAPIGateway.Utilities.IsDedicated) return;
			try
			{
				using (TextWriter writer = MyAPIGateway.Utilities.WriteFileInLocalStorage("ClientSettings.xml", typeof(DematerializerBlock)))
				{
					writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
				}
			}
			catch
			{
				MyLog.Default.Error("[Dematerializer Mod]: Error saving client settings.");
			}
		}

		public static ClientSettings Load()
		{
			ClientSettings settings = new ClientSettings();
			if (MyAPIGateway.Utilities.IsDedicated) 
				return settings;

			if (MyAPIGateway.Utilities.FileExistsInLocalStorage("ClientSettings.xml", typeof(DematerializerBlock)))
			{
				try
				{
					using (TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage("ClientSettings.xml", typeof(DematerializerBlock)))
					{
						settings = MyAPIGateway.Utilities.SerializeFromXML<ClientSettings>(reader.ReadToEnd());
					}
				}
				catch
				{
					MyLog.Default.Error("[Dematerializer Mod]: Error loading client settings, using default values.");
				}
				if (settings == null)
					settings = new ClientSettings();
			}
			else
				Save(settings);
			return settings;
		}
	}
}