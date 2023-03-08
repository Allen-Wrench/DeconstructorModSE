using System;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using Sandbox.Game.Entities;

namespace Dematerializer.Sync
{
	[ProtoContract(UseProtoMembersOnly = true)]
	public class PacketSettings : PacketBase
	{
		[ProtoMember(1)]
		public long EntityId;

		//[ProtoMember(2)]
		//public BlockSettings Settings;

        [ProtoMember(3)]
        public ulong RequestingSteamId;

        [ProtoMember(4)]
        public ModSettings ModSettings;

		public PacketSettings() { }

        public void Send(long entityId, BlockSettings settings)
        {
            EntityId = entityId;
            //Settings = settings;

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

		//public void RequestBlockSettings(long dematerializerEntityId)
		//{
		//	if (MyAPIGateway.Utilities.IsDedicated || MyAPIGateway.Session.IsServer) return;
        //    EntityId = dematerializerEntityId;
		//	Networking.SendToServer(this);
		//}

        public override void Received(ref bool relay)
        {
            if (MyAPIGateway.Multiplayer.IsServer && RequestingSteamId != 0)
            {
                ModSettings = DematerializerSession.Instance.ModSettings;
				Networking.SendToPlayer(this, RequestingSteamId);
                return;
            }
            if (!MyAPIGateway.Multiplayer.IsServer && ModSettings != null)
            {
                DematerializerSession.Instance.SettingsFromServer(ModSettings);
                return;
            }

            //if (EntityId == 0) return;
            //
			//IMyShipGrinder block = MyAPIGateway.Entities.GetEntityById(EntityId) as IMyShipGrinder;
            //if (block == null) return;
            //
			//DematerializerBlock logic = block.GameLogic as DematerializerBlock;
            //if (logic == null) return;

            //if (MyAPIGateway.Multiplayer.IsServer)
            //{
            //    Settings = logic.Settings;
            //    Networking.SendToPlayer(this, SenderId);
            //
            //    relay = false;
            //    return;
            //}
            //if (!MyAPIGateway.Multiplayer.IsServer && Settings != null)
            //{
            //    bool grind = logic.IsGrinding;
            //    //logic.Settings = Settings;
            //    if (logic.Time != TimeSpan.Zero)
            //    {
            //        IMyEntity ent;
            //        if (MyAPIGateway.Entities.TryGetEntityById(Settings.SelectedGrid, out ent) && ent is IMyCubeGrid)
            //        {
            //            logic.SelectedGrid = ent as IMyCubeGrid;
            //            if (!grind && Settings.IsGrinding)
            //            {
            //                logic.DematerializeFX(ent as MyCubeGrid);
            //                logic.SetPower(true);
            //            }
            //        }
            //    }
            //    else
            //    {
            //        logic.SelectedGrid = null;
            //        logic.Grids.Clear();
			//		logic.SetPower(false);
			//	}
            //}
        }
    }
}
