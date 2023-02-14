using System;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using Sandbox.Game.Entities;

namespace DeconstructorModSE.Sync
{
	[ProtoContract(UseProtoMembersOnly = true)]
	public class PacketSettings : PacketBase
	{
		[ProtoMember(1)]
		public long EntityId;

		[ProtoMember(2)]
		public BlockSettings Settings;

        [ProtoMember(3)]
        public ulong RequestingSteamId;

        [ProtoMember(4)]
        public ModSettings ModSettings;

		public PacketSettings() { }

        public void Send(long entityId, BlockSettings settings)
        {
            EntityId = entityId;
            Settings = settings;

            if (MyAPIGateway.Multiplayer.IsServer)
                Networking.RelayToClients(this);
            else
                Networking.SendToServer(this);
        }

		public void RequestModSettings()
		{
            if (MyAPIGateway.Utilities.IsDedicated || MyAPIGateway.Session.IsServer) return;
			RequestingSteamId = MyAPIGateway.Multiplayer.MyId;
			Networking.SendToServer(this);
		}

		public void RequestBlockSettings(long deconstructorEntityId)
		{
			if (MyAPIGateway.Utilities.IsDedicated || MyAPIGateway.Session.IsServer) return;
            EntityId = deconstructorEntityId;
			Networking.SendToServer(this);
		}

        public override void Received(ref bool relay)
        {
            if (MyAPIGateway.Multiplayer.IsServer && RequestingSteamId != 0)
            {
                ModSettings = DeconstructorSession.Instance.ModSettings;
				Networking.SendToPlayer(this, RequestingSteamId);
                return;
            }
            if (!MyAPIGateway.Multiplayer.IsServer && ModSettings != null)
            {
                DeconstructorSession.Instance.SettingsFromServer(ModSettings);
                return;
            }

            if (EntityId == 0) return;

			IMyShipGrinder block = MyAPIGateway.Entities.GetEntityById(EntityId) as IMyShipGrinder;
            if (block == null) return;

			DeconstructorMod logic = block.GameLogic as DeconstructorMod;
            if (logic == null) return;

            if (MyAPIGateway.Multiplayer.IsServer)
            {
                Settings = logic.Settings;
                Networking.SendToPlayer(this, SenderId);

                relay = false;
            }
            if (!MyAPIGateway.Multiplayer.IsServer && Settings != null)
            {
                bool grind = logic.Settings.IsGrinding;
                logic.Settings = Settings;
                if (Settings.SelectedGrid != 0)
                {
                    IMyEntity ent;
                    if (MyAPIGateway.Entities.TryGetEntityById(Settings.SelectedGrid, out ent) && ent is IMyCubeGrid)
                    {
                        logic.SelectedGrid = ent as IMyCubeGrid;
                        if (!grind && Settings.IsGrinding)
                        {
                            logic.DeconstructFX(ent as MyCubeGrid);
                            logic.SetPower(true);
                        }
                    }
                }
                else
                {
                    logic.SelectedGrid = null;
                    logic.Grids.Clear();
				}
                if (!Settings.IsGrinding)
                    logic.SetPower();
				block.RefreshCustomInfo();
				DeconstructorSession.Instance.UpdateTerminal();
            }
        }
    }
}
