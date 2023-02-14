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
		public ModSettings ModSettings { get; private set; }
		public ClientSettings ClientSettings { get; private set; }

		public Networking Net = new Networking(57747);
		public PacketServer CachedPacketServer;
		public PacketClient CachedPacketClient;
		public IMyTerminalControlButton SearchButton { get; set; }
		public IMyTerminalControlListbox GridList { get; set; }
		public IMyTerminalControlButton HideGridButton { get; set; }
		public IMyTerminalControlButton ClearHiddenButton { get; set; }
		public IMyTerminalControlTextbox TimerBox { get; set; }
		public IMyTerminalControlSlider EfficiencySlider { get; set; }
		public IMyTerminalControlButton DeconstructButton { get; set; }
		public IMyTerminalControlListbox ComponentList { get; set; }

		public IReadOnlyDictionary<string, Delegate> APIMethods;

		public override void LoadData()
		{
			Instance = this;
			Net.Register();
			CachedPacketServer = new PacketServer();
			CachedPacketClient = new PacketClient();
			ModSettings = ModSettings.Load();
			ClientSettings = ClientSettings.Load();
		}

		protected override void UnloadData()
		{
			if (MyAPIGateway.Multiplayer.IsServer)
				ModSettings.Save(ModSettings);
			if (!MyAPIGateway.Utilities.IsDedicated)
				ClientSettings.Save(ClientSettings);
			Instance = null;

			Net?.Unregister();
			Net = null;
		}

		public void SettingsFromServer(ModSettings settings)
		{
			ModSettings = settings;
			MyLog.Default.Info("[Deconstructor Mod] Recieved mod settings from server.");
		}

		public void UpdateTerminal()
		{
			SearchButton.UpdateVisual();
			GridList.UpdateVisual();
			HideGridButton.UpdateVisual();
			ClearHiddenButton.UpdateVisual();
			TimerBox.UpdateVisual();
			EfficiencySlider.UpdateVisual();
			DeconstructButton.UpdateVisual();
			ComponentList.UpdateVisual();
		}
	}
}