using System;
using System.Collections.Generic;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;

namespace Dematerializer.Sync
{
	[ProtoContract]
	public class PacketClient : PacketBase
	{
		public PacketClient()
		{
		}

		[ProtoMember(1)]
		public long EntityId;

		[ProtoMember(2)]
		public long TargetId;

		[ProtoMember(3)]
		public List<MyObjectBuilder_InventoryItem> ItemList = new List<MyObjectBuilder_InventoryItem>();

		public void SendProgressUpdate(long entityId, long targetId, List<MyObjectBuilder_InventoryItem> items)
		{
			EntityId = entityId;
			TargetId = targetId;
			if (items != null)
				ItemList = items;
			else
				ItemList = new List<MyObjectBuilder_InventoryItem>(); 

			Networking.RelayToClients(this);
		}

		public override void Received(ref bool relay)
		{
			relay = false;

			var block = MyAPIGateway.Entities.GetEntityById(EntityId) as IMyShipGrinder;
			if (block == null || block.GameLogic == null) return;

			var logic = block.GameLogic.GetAs<DematerializerBlock>();
			if (logic == null) return;

			if (ItemList.Count > 0)
			{
				logic.Items = ItemList;
			}

			if (TargetId != 0)
			{
				IMyCubeGrid grid = MyAPIGateway.Entities.GetEntityById(TargetId) as IMyCubeGrid;
				if (grid == null) return;

				if (logic.Dissasemble != null)
				{
					logic.Dissasemble.Dispose();
					logic.Dissasemble = null;
					logic.NeedsUpdate &= ~VRage.ModAPI.MyEntityUpdateEnum.EACH_FRAME;
				}
				logic.Grids.Remove(grid);
				grid.Close();
				return;
			}

			logic.Items.Clear();
		}
	}
}