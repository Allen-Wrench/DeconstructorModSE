﻿using Dematerializer.Sync;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace Dematerializer
{
	[MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
	public class DematerializerSession : MySessionComponentBase
	{
		public static GridLog Logger;
		public static DematerializerSession Instance;
		public ModSettings ModSettings { get; private set; }
		public ClientSettings ClientSettings { get; private set; } = new ClientSettings();

		public Networking Net = new Networking(57747);
		public PacketServer CachedPacketServer;
		public PacketClient CachedPacketClient;
		public readonly Guid SETTINGS_GUID = new Guid("1EAB58EE-7304-45D2-B3C8-9BA2DC31EF90");
		public IMyTerminalControlButton SearchButton { get; set; }
		public IMyTerminalControlListbox GridList { get; set; }
		public IMyTerminalControlButton HideGridButton { get; set; }
		public IMyTerminalControlButton ClearHiddenButton { get; set; }
		public IMyTerminalControlTextbox TimerBox { get; set; }
		public IMyTerminalControlButton DematerializeButton { get; set; }
		public IMyTerminalControlListbox ComponentList { get; set; }
		public IMyTerminalControlListbox Blacklist { get; set; }
		public IMyTerminalControlButton BlacklistButton { get; set; }
		public IMyTerminalControlButton WhitelistButton { get; set; }
		public IMyTerminalControlButton CancelButton { get; set; }

		public override void LoadData()
		{
			Instance = this;
			Net.Register();
			CachedPacketServer = new PacketServer();
			CachedPacketClient = new PacketClient();
			if (!MyAPIGateway.Utilities.IsDedicated)
			{
				ClientSettings = ClientSettings.Load();
				if (!MyAPIGateway.Multiplayer.IsServer)
				{
					Session.OnSessionReady += OnReady;
				}
			}
			if (MyAPIGateway.Multiplayer.IsServer)
			{
				ModSettings = ModSettings.Load();
				Logger = new GridLog();
				MyAPIGateway.Entities.OnEntityAdd += CheckForTag;
			}
		}

		private void OnReady()
		{
			Session.OnSessionReady -= OnReady;
			PacketSettings p = new PacketSettings();
			p.RequestModSettings();
			MyLog.Default.Info("[Dematerializer Mod]: Requesting mod settings from server.");
		}

		public override void SaveData()
		{
			if (MyAPIGateway.Multiplayer.IsServer)
				ModSettings.Save(ModSettings);
			if (!MyAPIGateway.Utilities.IsDedicated)
				ClientSettings.Save(ClientSettings);
			base.SaveData();
		}

		protected override void UnloadData()
		{
			if (MyAPIGateway.Multiplayer.IsServer)
			{
				ModSettings.Save(ModSettings);

				Logger.CloseLog();
				Logger = null;
			}
			if (!MyAPIGateway.Utilities.IsDedicated)
				ClientSettings.Save(ClientSettings);
			MyAPIGateway.Entities.OnEntityAdd -= CheckForTag;
			Instance = null;

			Net?.Unregister();
			Net = null;
		}

		public void SettingsFromServer(ModSettings settings)
		{
			ModSettings = settings;
			MyLog.Default.Info("[Dematerializer Mod] Recieved mod settings from server.");
		}

		public void UpdateTerminal()
		{
			if (MyAPIGateway.Utilities.IsDedicated) return;

			SearchButton.UpdateVisual();
			GridList.UpdateVisual();
			HideGridButton.UpdateVisual();
			ClearHiddenButton.UpdateVisual();
			TimerBox.UpdateVisual();
			DematerializeButton.UpdateVisual();
			ComponentList.UpdateVisual();
			Blacklist.UpdateVisual();
			BlacklistButton.UpdateVisual();
			WhitelistButton.UpdateVisual();
			CancelButton.UpdateVisual();
		}

		public void CheckForTag(IMyEntity entity)
		{
			MyCubeGrid grid = entity as MyCubeGrid;
			if (grid != null && grid.Storage != null && grid.Physics != null)
			{
				if (grid.Storage.ContainsKey(SETTINGS_GUID))
				{
					grid.Immune = false;
					grid.Editable = true;
					grid.IsPreview = false;
					grid.Physics.IsPhantom = false;
					grid.Storage.RemoveValue(SETTINGS_GUID);
				}
				//string data;
				//if (grid.Storage.TryGetValue(SETTINGS_GUID, out data))
				//{
				//	ProcessingTag tag;
				//	try
				//	{
				//		tag = MyAPIGateway.Utilities.SerializeFromBinary<ProcessingTag>(Convert.FromBase64String(data));
				//	}
				//	catch
				//	{
				//		tag = null;
				//	}
				//
				//	IMyEntity ent;
				//	if (tag != null && tag.GrinderId != 0 && MyAPIGateway.Entities.TryGetEntityById(tag.GrinderId, out ent) && ent is IMyShipGrinder)
				//	{
				//		var block = (ent as IMyShipGrinder).GameLogic as DematerializerBlock;
				//		if (block != null && block.Time == tag.Time && block.TimeStarted == tag.TimeStarted)
				//		{
				//			block.ResumeProcessing(grid.EntityId);
				//			return;
				//		}
				//	}
				//
				//	grid.Immune = false;
				//	grid.Editable = true;
				//	grid.IsPreview = false;
				//	grid.Physics.IsPhantom = false;
				//	grid.Close();
				//}
			}
		}
	}
}