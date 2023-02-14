using ProtoBuf;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.IO;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace DeconstructorModSE
{
	[ProtoContract(UseProtoMembersOnly = true)]
	public class ClientSettings
	{
		[ProtoMember(1)]
		public List<string> HiddenGrids = new List<string>();

		public static void Save(ClientSettings settings)
		{
			if (MyAPIGateway.Utilities.IsDedicated) return;
			try
			{
				using (TextWriter writer = MyAPIGateway.Utilities.WriteFileInLocalStorage("ClientSettings.xml", typeof(DeconstructorMod)))
				{
					writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
				}
			}
			catch
			{
				MyLog.Default.Error("[Deconstructor Mod]: Error saving client settings.");
			}
		}

		public static ClientSettings Load()
		{
			if (MyAPIGateway.Utilities.IsDedicated) return new ClientSettings();
			ClientSettings settings = new ClientSettings();
			if (MyAPIGateway.Utilities.FileExistsInLocalStorage("ClientSettings.xml", typeof(DeconstructorMod)))
			{
				try
				{
					using (TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage("ClientSettings.xml", typeof(DeconstructorMod)))
					{
						settings = MyAPIGateway.Utilities.SerializeFromXML<ClientSettings>(reader.ReadToEnd());
					}
					if (settings == null)
						return new ClientSettings();
				}
				catch
				{
					MyLog.Default.Error("[Deconstructor Mod]: Error loading client settings, using default values.");
					settings = new ClientSettings();
				}
			}
			else
				Save(settings);
			return settings;
		}
	}
}