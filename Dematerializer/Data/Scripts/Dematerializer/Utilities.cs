using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Dematerializer
{
	public static class Utils
	{
		private static Dictionary<MyDefinitionId, MyPhysicalInventoryItem> TempItems = new Dictionary<MyDefinitionId, MyPhysicalInventoryItem>();

		public static List<MyObjectBuilder_InventoryItem> GetComponentsServer(IMyInventory inventory, IMyCubeGrid SelectedGrid)
		{
			List<MyObjectBuilder_InventoryItem> Items = new List<MyObjectBuilder_InventoryItem>();
			var Blocks = new List<IMySlimBlock>();
			SelectedGrid.GetBlocks(Blocks);
			
			Dictionary<string, int> missing = new Dictionary<string, int>();
			List<VRage.Game.ModAPI.Ingame.MyInventoryItem> InvItems = new List<VRage.Game.ModAPI.Ingame.MyInventoryItem>();
			MyPhysicalInventoryItem phys;
			MyDefinitionId Id;
			
			foreach (var block in Blocks)
			{
				if (!block.IsFullIntegrity)
					block.GetMissingComponents(missing);
			
				foreach (var comp in (block.BlockDefinition as MyCubeBlockDefinition).Components)
				{
					int count = comp.Count;
					if (missing.ContainsKey(comp.Definition.Id.SubtypeName))
						count -= missing[comp.Definition.Id.SubtypeName];
					if (count <= 0)
						continue;
					MyObjectBuilder_PhysicalObject obj = MyObjectBuilderSerializer.CreateNewObject(comp.DeconstructItem.Id) as MyObjectBuilder_PhysicalObject;
					if (obj == null) continue;
					phys = new MyPhysicalInventoryItem(count, obj);
					Id = phys.Content.GetObjectId();
					if (!TempItems.ContainsKey(Id))
						TempItems.Add(Id, phys);
					else
						TempItems[Id] = new MyPhysicalInventoryItem(phys.Amount + TempItems[Id].Amount, phys.Content);
				}
				missing.Clear();

				if (block.FatBlock != null && block.FatBlock.HasInventory)
				{
					for (int i = 0; i < block.FatBlock.InventoryCount; i++)
					{
						block.FatBlock.GetInventory().GetItems(InvItems);
						foreach (var item in InvItems)
						{
							MyObjectBuilder_PhysicalObject p = MyObjectBuilderSerializer.CreateNewObject((MyDefinitionId)item.Type) as MyObjectBuilder_PhysicalObject;
							if (p == null) continue;
							MyPhysicalInventoryItem pi = new MyPhysicalInventoryItem(item.Amount, p);
							Id = pi.Content.GetObjectId();
							if (!TempItems.ContainsKey(Id))
								TempItems.Add(Id, pi);
							else
								TempItems[Id] = new MyPhysicalInventoryItem(item.Amount + TempItems[Id].Amount, pi.Content);
						}
						InvItems.Clear();
					}
				}
			}

			foreach (var item in TempItems)
			{
				Items.Add(item.Value.GetObjectBuilder());
			}
			TempItems.Clear();
			return Items;
		}

		public static Dictionary<string, MyObjectBuilder_InventoryItem> GetComponentsClient(MyCubeGrid grid)
		{
			Dictionary<string, MyObjectBuilder_InventoryItem> content = new Dictionary<string, MyObjectBuilder_InventoryItem>();
			Dictionary<string, int> missing = new Dictionary<string, int>();
			foreach (IMySlimBlock b in grid.CubeBlocks)
			{
				foreach (var comp in (b.BlockDefinition as MyCubeBlockDefinition).Components)
				{
					string name = comp.DeconstructItem.Id.SubtypeName;
					MyObjectBuilder_InventoryItem obj = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_InventoryItem>(comp.DeconstructItem.Id.SubtypeName);
					obj.Amount = comp.Count;
					if (content.ContainsKey(name))
						obj.Amount += content[name].Amount;
					content[name] = obj;
				}
				if (!b.IsFullIntegrity)
				{
					b.GetMissingComponents(missing);
					foreach (var kvp in missing)
					{
						if (content.ContainsKey(kvp.Key))
						{
							MyObjectBuilder_InventoryItem obj = content[kvp.Key];
							obj.Amount -= kvp.Value;
							if (obj.Amount < 0)
								obj.Amount = 0;
							content[kvp.Key] = obj;
						}
					}
					missing.Clear();
				}
			}
			foreach (var inv in grid.Inventories)
			{
				for (int i = 0; i < inv.InventoryCount; i++)
				{
					foreach (var item in inv.GetInventory(i).GetItems())
					{
						string name = item.Content.SubtypeName;
						MyObjectBuilder_InventoryItem obj = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_InventoryItem>(item.Content.GetObjectId().SubtypeName);
						obj.Amount = item.Amount;
						if (content.ContainsKey(name))
							obj.Amount += content[name].Amount;
						content[name] = obj;
					}
				}
			}
			return content;
		}

		public static TimeSpan GetGrindTime(DematerializerBlock MyBlock, ref IMyCubeGrid SelectedGrid)
		{
			if (MyBlock == null || SelectedGrid == null)
				return TimeSpan.Zero;

			float totalTime = 0;
			var Blocks = new List<IMySlimBlock>();
			SelectedGrid.GetBlocks(Blocks);
			
			if (Blocks.Count == 0)
				return TimeSpan.Zero;

			float grindRatio = 0;
			float integrity = 0;
			float grindTime;
			MyCubeBlockDefinition def;

			foreach (var block in Blocks)
			{
				def = block.BlockDefinition as MyCubeBlockDefinition;
				if (def != null)
				{
					grindRatio = def.DisassembleRatio;
					integrity = def.IntegrityPointsPerSec;
				}

				grindTime = block.MaxIntegrity / integrity / DematerializerSession.Instance.Session.WelderSpeedMultiplier / (1f / grindRatio) / DematerializerSession.Instance.Session.GrinderSpeedMultiplier;
				totalTime += grindTime * block.BuildLevelRatio * 0.25f;
			}

			totalTime *= (100.0f - MyBlock.Efficiency) / 100.0f;

			return TimeSpan.FromSeconds(totalTime);
		}

		public static bool SpawnItems(IMyInventory MyInventory, ref List<MyObjectBuilder_InventoryItem> items)
		{
			MyFixedPoint amount;
			bool stalled = true;

			for (var i = items.Count - 1; i >= 0; i--)
			{
				amount = GetMaxAmountPossible(MyInventory, items[i]);
				if (amount > 0)
				{
					stalled = false;
					MyInventory.AddItems(amount, items[i].PhysicalContent);
					if ((items[i].Amount - amount) > 0)
					{
						items[i].Amount -= amount;
					}
					else
					{
						items.RemoveAtFast(i);
					}
				}
			}
			return (!stalled);
		}

		public static MyFixedPoint GetMaxAmountPossible(IMyInventory inv, MyObjectBuilder_InventoryItem Item)
		{
			var def = MyDefinitionManager.Static.GetPhysicalItemDefinition(Item.PhysicalContent.GetId());
			var MaxAmount = (int)((inv.MaxVolume - inv.CurrentVolume).RawValue / (def.Volume * 1000000));

			return MaxAmount > Item.Amount ? Item.Amount : MaxAmount;
		}
	}
}