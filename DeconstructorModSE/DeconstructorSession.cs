using DeconstructorModSE.Sync;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace DeconstructorModSE
{
	[MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
	public class DeconstructorSession : MySessionComponentBase
	{
		public static DeconstructorSession Instance;
		public ModSettings Settings { get; private set; }
		public Networking Net = new Networking(57747);
		public PacketServer CachedPacketServer;
		public PacketClient CachedPacketClient;
		public IMyTerminalControlButton SearchButton { get; set; }
		public IMyTerminalControlListbox GridList { get; set; }
		public IMyTerminalControlButton HideGridButton { get; set; }
		public IMyTerminalControlTextbox TimerBox { get; set; }
		public IMyTerminalControlSlider EfficiencySlider { get; set; }
		public IMyTerminalControlButton DeconButton { get; set; }
		public IMyTerminalControlListbox ComponentList { get; set; }

		public IReadOnlyDictionary<string, Delegate> APIMethods;

		public override void LoadData()
		{
			Instance = this;
			Net.Register();
			CachedPacketServer = new PacketServer();
			CachedPacketClient = new PacketClient();
			Settings = ModSettings.Load();
		}

		protected override void UnloadData()
		{
			ModSettings.Save(Settings);
			Instance = null;

			Net?.Unregister();
			Net = null;
		}

		public void SettingsFromServer(ModSettings settings)
		{
			Settings = settings;
			MyLog.Default.Info("[Deconstructor Mod] Recieved mod settings from server.");
		}
	}
}