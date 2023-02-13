using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace DeconstructorModSE
{
	public static class Utils
	{
		private static Dictionary<MyDefinitionId, MyPhysicalInventoryItem> TempItems = new Dictionary<MyDefinitionId, MyPhysicalInventoryItem>();

		public static void DeconstructGrid(IMyInventory inventory, ref IMyCubeGrid SelectedGrid, ref List<MyObjectBuilder_InventoryItem> Items)
		{
			Items.Clear();
			var Blocks = new List<IMySlimBlock>();
			SelectedGrid.GetBlocks(Blocks);

			Dictionary<string, int> missing = new Dictionary<string, int>();
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
					if (count < 0)
						count = 0;
					MyObjectBuilder_PhysicalObject obj = MyObjectBuilderSerializer.CreateNewObject(comp.DeconstructItem.Id) as MyObjectBuilder_PhysicalObject;
					phys = new MyPhysicalInventoryItem(count, obj);
					Id = phys.Content.GetObjectId();
					if (!TempItems.ContainsKey(Id))
						TempItems.Add(Id, phys);
					else
						TempItems[Id] = new MyPhysicalInventoryItem(phys.Amount + TempItems[Id].Amount, phys.Content);
				}
				missing.Clear();
			}

			foreach (var inv in (SelectedGrid as MyCubeGrid).Inventories)
			{
				for (int i = 0; i < inv.InventoryCount; i++)
				{
					foreach (var item in inv.GetInventory(i).GetItems())
					{
						Id = item.Content.GetObjectId();
						if (!TempItems.ContainsKey(Id))
							TempItems.Add(Id, item);
						else
							TempItems[Id] = new MyPhysicalInventoryItem(item.Amount + TempItems[Id].Amount, item.Content);
					}
				}
			}

			foreach (var item in TempItems)
			{
				Items.Add(item.Value.GetObjectBuilder());
			}
			TempItems.Clear();
		}

		public static TimeSpan GetGrindTime(DeconstructorMod MyBlock, ref IMyCubeGrid SelectedGrid, bool calcEff = true)
		{
			if (MyBlock == null || SelectedGrid == null)
				return TimeSpan.Zero;

			float totalTime = 0;
			var Blocks = new List<IMySlimBlock>();
			SelectedGrid.GetBlocks(Blocks);
			var gridGroupGrids = new List<IMyCubeGrid>();
			var gridGroup = SelectedGrid.GetGridGroup(GridLinkTypeEnum.Mechanical);
			if (gridGroup != null)
			{
				gridGroup.GetGrids(gridGroupGrids);
				foreach (var grid in gridGroupGrids)
				{
					if (grid == SelectedGrid)
					{
						continue;
					}

					grid.GetBlocks(Blocks);
				}
			}
			if (Blocks.Count == 0)
				return TimeSpan.Zero;

			float grindRatio = 0;
			float integrity = 0;
			float grindTime;
			MyCubeBlockDefinition def;

			foreach (var block in Blocks)
			{
				if (block.BlockDefinition.Id != null)
				{
					def = MyDefinitionManager.Static.GetDefinition(block.BlockDefinition.Id) as MyCubeBlockDefinition;
					if (def != null)
					{
						grindRatio = def.DisassembleRatio;
						integrity = def.IntegrityPointsPerSec;
					}
				}

				grindTime = block.MaxIntegrity / integrity / DeconstructorSession.Instance.Session.WelderSpeedMultiplier / (1f / grindRatio) / DeconstructorSession.Instance.Session.GrinderSpeedMultiplier;
				totalTime += grindTime * block.BuildLevelRatio;
			}

			if (calcEff)
				totalTime *= (100.0f - MyBlock.Settings.Efficiency) / 100.0f;
			else
				totalTime *= 100.0f / 100.0f;

			return TimeSpan.FromSeconds(totalTime);
		}

		public static void SpawnItems(IMyInventory MyInventory, ref List<MyObjectBuilder_InventoryItem> Items)
		{
			MyFixedPoint amount;

			for (var i = Items.Count - 1; i >= 0; i--)
			{
				amount = GetMaxAmountPossible(MyInventory, Items[i]);
				if (amount > 0)
				{
					MyInventory.AddItems(amount, Items[i].PhysicalContent);
					if ((Items[i].Amount - amount) > 0)
					{
						Items[i].Amount -= amount;
					}
					else
					{
						Items.RemoveAtFast(i);
					}
				}
			}
		}

		public static bool SearchBlocks(MyCubeBlock block, IMyTerminalBlock deconstructor)
		{
			if (block == null) return true;
			if (block is IMyCockpit || block is IMyMedicalRoom || block is IMyWarhead || block is IMyLargeTurretBase)
			{
				if (block.OwnerId == deconstructor.OwnerId || block.OwnerId == 0)
					return true;
				else
					return false;
			}

			return true;
		}

		public static MyFixedPoint GetMaxAmountPossible(IMyInventory inv, MyObjectBuilder_InventoryItem Item)
		{
			var def = MyDefinitionManager.Static.GetPhysicalItemDefinition(Item.PhysicalContent.GetId());
			var MaxAmount = (int)((inv.MaxVolume - inv.CurrentVolume).RawValue / (def.Volume * 1000000));

			return MaxAmount > Item.Amount ? Item.Amount : MaxAmount;
		}
	}
}