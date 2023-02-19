using System.Collections.Generic;
using ProtoBuf;
using Sandbox.ModAPI;

namespace Dematerializer.Sync
{
	[ProtoContract]
	public class PacketServer : PacketBase
	{
		public PacketServer()
		{
		}

		[ProtoMember(1)]
		public long EntityId;

		[ProtoMember(2)]
		public long TargetId;

		[ProtoMember(3)]
		public List<string> Blacklist = new List<string>();

		public void Send(long entityId, long targetId, List<string> blacklist)
		{
			EntityId = entityId;
			TargetId = targetId;
			if (blacklist != null)
				Blacklist = blacklist;

			Networking.SendToServer(this);
		}

		public override void Received(ref bool relay)
		{
			var block = MyAPIGateway.Entities.GetEntityById(EntityId) as IMyShipGrinder;

			if (block == null)
				return;

			var logic = block.GameLogic?.GetAs<DematerializerBlock>();

			if (logic == null)
				return;

			logic.Blacklist = Blacklist;
			if (TargetId != 0)
			{
				logic.Settings.Error = "";
				logic.Settings.SelectedGrid = TargetId;
				logic.SyncServer(TargetId);
			}

			relay = false;
		}
	}
}