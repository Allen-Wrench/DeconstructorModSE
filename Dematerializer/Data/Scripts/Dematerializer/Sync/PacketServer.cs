using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;

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

		[ProtoMember(4)]
		public List<MyObjectBuilder_InventoryItem> ItemList = new List<MyObjectBuilder_InventoryItem>();

		public void BeginGrindingRequest(long entityId, long targetId, List<string> blacklist)
		{
			EntityId = entityId;
			TargetId = targetId;
			if (blacklist != null)
				Blacklist = blacklist;

			Networking.SendToServer(this);
		}

		public void SendItemsReply(List<MyObjectBuilder_InventoryItem> list)
		{
			var block = MyAPIGateway.Entities.GetEntityById(EntityId) as IMyShipGrinder;
			var logic = block?.GameLogic?.GetAs<DematerializerBlock>();

			if (logic != null && logic.Items.Count > 0)
				ItemList = logic.Items;

			Networking.SendToPlayer(this, SenderId);
		}

		public override void Received(ref bool relay)
		{
			var block = MyAPIGateway.Entities.GetEntityById(EntityId) as IMyShipGrinder;

			if (block == null)
				return;

			var logic = block.GameLogic?.GetAs<DematerializerBlock>();

			if (logic == null)
				return;

			if (!MyAPIGateway.Multiplayer.IsServer)
			{
				logic.Items = ItemList;
				if (logic.SelectedGrid != null)
				{
					logic.DematerializeGrid(logic.SelectedGrid);
					logic.DematerializeFX(logic.SelectedGrid as MyCubeGrid);
				}

				DematerializerSession.Instance.UpdateTerminal();
				return;
			}

			if (Blacklist.Count > 0)
				logic.Blacklist = Blacklist;

			if (TargetId != 0)
			{
				List<MyObjectBuilder_InventoryItem> list = logic.SyncServer(TargetId);
				block.RefreshCustomInfo();
				SendItemsReply(list);
			}
			else
			{
				logic.CancelOperation();
			}

			relay = false;
		}
	}
}