using ProtoBuf;
using Sandbox.ModAPI;

namespace DeconstructorModSE.Sync
{
	[ProtoContract(UseProtoMembersOnly = true)]
	public class PacketSettings : PacketBase
	{
		[ProtoMember(1)]
		public long EntityId;

		[ProtoMember(2)]
		public DeconstructorBlockSettings Settings;

        [ProtoMember(3)]
        public ulong RequestingSteamId;

        [ProtoMember(4)]
        public ModSettings ModSettings;

		public PacketSettings() { }

        public void Send(long entityId, DeconstructorBlockSettings settings)
        {
            EntityId = entityId;
            Settings = settings;

            if (MyAPIGateway.Multiplayer.IsServer)
                Networking.RelayToClients(this);
            else
                Networking.SendToServer(this);
        }

		public void RequestSettings()
		{
            if (MyAPIGateway.Utilities.IsDedicated || MyAPIGateway.Session.IsServer) return;
			RequestingSteamId = MyAPIGateway.Multiplayer.MyId;
			Networking.SendToServer(this);
		}

		public override void Received(ref bool relay)
        {
            if (MyAPIGateway.Multiplayer.IsServer && RequestingSteamId != 0)
            {
                PacketSettings s = new PacketSettings();
                s.ModSettings = DeconstructorSession.Instance.Settings;
                Networking.SendToPlayer(s, RequestingSteamId);
                return;
            }
            else if (!MyAPIGateway.Multiplayer.IsServer && ModSettings != null)
            {
                DeconstructorSession.Instance.SettingsFromServer(ModSettings);
                return;
            }

            var block = MyAPIGateway.Entities.GetEntityById(this.EntityId) as IMyShipGrinder;

            if (block == null)
                return;

            var logic = block.GameLogic?.GetAs<DeconstructorMod>();

            if (logic == null)
                return;

            logic.Settings.Efficiency = this.Settings.Efficiency;

            relay = true;
        }
    }
}
