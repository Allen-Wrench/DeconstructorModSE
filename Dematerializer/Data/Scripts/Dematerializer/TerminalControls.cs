using Sandbox.Definitions;
using Sandbox.Engine.Platform;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Dematerializer
{
	public static class TerminalControlsInit
	{
		private static DematerializerSession DSession => DematerializerSession.Instance;
		public static bool _TerminalInit = false;

		public static void InitControls<T>()
		{
			var searchButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>("SearchGrids");
			searchButton.Visible = VisibilityCheck;
			searchButton.Enabled = EnabledCheck;
			searchButton.SupportsMultipleBlocks = false;
			searchButton.Title = MyStringId.GetOrCompute("Search for Grids");
			searchButton.Action = SearchButton_action;
			MyAPIGateway.TerminalControls.AddControl<T>(searchButton);

			var gridList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, T>("Grids");
			gridList.Visible = VisibilityCheck;
			gridList.Enabled = EnabledCheck;
			gridList.Multiselect = false;
			gridList.SupportsMultipleBlocks = false;
			gridList.VisibleRowsCount = 5;
			gridList.Title = MyStringId.GetOrCompute("Grindable Grids");
			gridList.ItemSelected = List_selected;
			gridList.ListContent = List_content;
			MyAPIGateway.TerminalControls.AddControl<T>(gridList);

			var hideGridButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>("HideGrid");
			hideGridButton.Visible = VisibilityCheck;
			hideGridButton.Enabled = SelectedEnabledCheck;
			hideGridButton.SupportsMultipleBlocks = false;
			hideGridButton.Title = MyStringId.GetOrCompute("Hide Selected Grid");
			hideGridButton.Action = HideButton_action;
			MyAPIGateway.TerminalControls.AddControl<T>(hideGridButton);

			var clearHiddenGridsButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>("ClearHiddenGrids");
			clearHiddenGridsButton.Visible = VisibilityCheck;
			clearHiddenGridsButton.Enabled = EnabledCheck;
			clearHiddenGridsButton.SupportsMultipleBlocks = false;
			clearHiddenGridsButton.Title = MyStringId.GetOrCompute("Clear Hidden Grids");
			clearHiddenGridsButton.Action = ClearHiddenGridsButton_action;
			MyAPIGateway.TerminalControls.AddControl<T>(clearHiddenGridsButton);

			var TimerBox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, T>("Timer");
			TimerBox.Visible = VisibilityCheck;
			TimerBox.Enabled = x => false;
			TimerBox.SupportsMultipleBlocks = false;
			TimerBox.Getter = TextBoxGetter;
			TimerBox.Title = MyStringId.GetOrCompute("Grind Time");
			MyAPIGateway.TerminalControls.AddControl<T>(TimerBox);

			//var efficiency = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>("Efficiency");
			//efficiency.Enabled = EnabledCheck;
			//efficiency.Visible = VisibilityCheck;
			//efficiency.SetLimits(0, 99);
			//efficiency.SupportsMultipleBlocks = false;
			//efficiency.Title = MyStringId.GetOrCompute("Efficiency");
			//efficiency.Tooltip = MyStringId.GetOrCompute("Reduces dematerializeion time, but increases power required");
			//efficiency.Setter = Slider_setter;
			//efficiency.Getter = Slider_getter;
			//efficiency.Writer = Slider_writer;
			//MyAPIGateway.TerminalControls.AddControl<T>(efficiency);

			var dematerializeButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>("StartDecon");
			dematerializeButton.Visible = VisibilityCheck;
			dematerializeButton.Enabled = SelectedEnabledCheck;
			dematerializeButton.SupportsMultipleBlocks = false;
			dematerializeButton.Title = MyStringId.GetOrCompute("Dematerialize");
			dematerializeButton.Action = DematerializeButton_action;
			MyAPIGateway.TerminalControls.AddControl<T>(dematerializeButton);

			var componentList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, T>("Components");
			componentList.Visible = VisibilityCheck;
			componentList.Multiselect = false;
			componentList.SupportsMultipleBlocks = false;
			componentList.VisibleRowsCount = 8;
			componentList.Title = MyStringId.GetOrCompute("Components");
			componentList.Tooltip = MyStringId.GetOrCompute("Click an item to blacklist it. Blacklisted items will not get added to your inventory once dematerialization completes.");
			componentList.ItemSelected = Whitelist_clicked;
			componentList.ListContent = ComponentList_content;
			MyAPIGateway.TerminalControls.AddControl<T>(componentList);

			var blackList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, T>("Blacklist");
			blackList.Visible = VisibilityCheck;
			blackList.Multiselect = false;
			blackList.SupportsMultipleBlocks = false;
			blackList.VisibleRowsCount = 8;
			blackList.Title = MyStringId.GetOrCompute("Blacklisted Items");
			blackList.Tooltip = MyStringId.GetOrCompute("Click an item to remove it from the blacklist.");
			blackList.ItemSelected = Blacklist_clicked;
			blackList.ListContent = Blacklist_content;
			MyAPIGateway.TerminalControls.AddControl<T>(blackList);

			var api = new Dictionary<string, Delegate>();

			api.Add("GetComponents", new Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, List<VRage.Game.ModAPI.Ingame.MyInventoryItem>>(GetComponents));
			api.Add("CheckGrid", new Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, string, StringBuilder>(CheckGrid));
			// more...

			DSession.APIMethods = api;

			var p = MyAPIGateway.TerminalControls.CreateProperty<IReadOnlyDictionary<string, Delegate>, T>("DematerializerModAPI");
			p.Getter = (b) => DSession.APIMethods;
			p.Setter = (b, v) => { };
			MyAPIGateway.TerminalControls.AddControl<T>(p);

			DSession.SearchButton = searchButton;
			DSession.DematerializeButton = dematerializeButton;
			DSession.GridList = gridList;
			DSession.HideGridButton = hideGridButton;
			DSession.ClearHiddenButton= clearHiddenGridsButton;
			//DecSession.EfficiencySlider = efficiency;
			DSession.TimerBox = TimerBox;
			DSession.ComponentList = componentList;
			DSession.Blacklist = blackList;
		}

		public static DematerializerBlock GetBlock(IMyTerminalBlock block) => block?.GameLogic?.GetAs<DematerializerBlock>();

		private static void CheckGrid(Sandbox.ModAPI.Ingame.IMyTerminalBlock dematerializer, string gridName, StringBuilder output)
		{
			var system = GetBlock((IMyTerminalBlock)dematerializer);
			if (system == null) output.AppendLine("block does not exist... how did you get this?");
			var grid = system.Grids.Where(x => x.CustomName == gridName).FirstOrDefault();
			if (grid == null)
				output.AppendLine("Grid does not exist!");

			if (grid.IsSameConstructAs(dematerializer.CubeGrid))
				output.AppendLine("Grid cannot be dematerializeed because it is attached to the same grid as the block");

			if ((grid.GetPosition() - dematerializer.GetPosition()).Length() > system.Range)
				output.AppendLine("Grid is too far away");

			if (grid.Physics == null)
				output.AppendLine("Grid does not exist!");

			var cubGrid = grid as MyCubeGrid;
			if (cubGrid.GetBiggestGridInGroup() != cubGrid)
				output.AppendLine("Grid is not the biggest grid in its group");

			var bigOwners = grid.BigOwners;
			var gridOwner = bigOwners.Count > 0 ? bigOwners[0] : long.MaxValue;
			var relationship = gridOwner != long.MaxValue ? MyIDModule.GetRelationPlayerBlock(dematerializer.OwnerId, gridOwner, MyOwnershipShareModeEnum.Faction) : MyRelationsBetweenPlayerAndBlock.NoOwnership;

			if (relationship == MyRelationsBetweenPlayerAndBlock.Enemies)
				output.AppendLine("Grid is owned by an enemy");

			if (gridOwner != dematerializer.OwnerId && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare)
				output.AppendLine("Grid is not owned by you");

			// if the grid is not factioned, check for owning all major blocks
			//foreach (var block in ((MyCubeGrid)grid).GetFatBlocks())
			//{
			//	if (!Utils.SearchBlocks(block, (IMyTerminalBlock)dematerializer))
			//	{
			//		// if any block we care about is not owned by the grid owner, we don't want to add the grid
			//		if (block == null)
			//			output.AppendLine("Block was null?");
			//		else
			//			output.AppendLine("One or more of the important blocks is not owned by the grid owner");
			//
			//		break;
			//	}
			//}

			if (output.Length == 0)
				output.AppendLine("Grid is valid");
		}

		private static void GetComponents(Sandbox.ModAPI.Ingame.IMyTerminalBlock b, List<VRage.Game.ModAPI.Ingame.MyInventoryItem> items)
		{
			var system = GetBlock((IMyTerminalBlock)b);
			if (system == null) return;
			if (items == null) return;

			for (var i = system.Settings.Items.Count - 1; i > -1; i--)
			{
				var item = system.Settings.Items[i];
				var InvItem = new VRage.Game.ModAPI.Ingame.MyInventoryItem(item.PhysicalContent.GetId(), item.ItemId, item.Amount);
				items.Add(InvItem);
			}
		}

		private static StringBuilder TextBoxGetter(IMyTerminalBlock b)
		{
			var system = GetBlock(b);
			if (system == null) return new StringBuilder();
			var Builder = new StringBuilder();
			if (system.Settings != null && system.Settings.Time != null)
			{
				if (system.Settings.TimeStarted == null)
					return Builder.Append($"{system.Settings.Time:hh'h 'mm'm 'ss's '}");
				else
				{
					TimeSpan time = system.Settings.Time - (DateTime.UtcNow - system.Settings.TimeStarted.Value);
					if (time < TimeSpan.Zero)
						time = TimeSpan.Zero;
					return Builder.Append($"{time:hh'h 'mm'm 'ss's '}");
				}
			}
			else
			{
				return Builder.Append("N/A");
			}
		}

		private static bool VisibilityCheck(IMyTerminalBlock block)
		{
			return GetBlock(block) != null;
		}

		private static bool EnabledCheck(IMyTerminalBlock block)
		{
			var system = GetBlock(block);
			return system != null && !system.Settings.IsGrinding;
		}

		private static bool SelectedEnabledCheck(IMyTerminalBlock block)
		{
			var system = GetBlock(block);
			return system != null && (block as IMyShipGrinder).Enabled && !system.Settings.IsGrinding && system.SelectedGrid != null;
		}

		private static void SearchButton_action(IMyTerminalBlock block)
		{
			var system = GetBlock(block);
			if (system == null) return;
			system.Grids.Clear();
			BoundingSphereD sphere = new BoundingSphereD(block.GetPosition(), system.Range);
			foreach (var entity in MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref sphere))
			{
				IMyCubeGrid grid = entity as IMyCubeGrid;
				if (grid == null || 
					Vector3D.Distance(grid.GetPosition(), system.Entity.GetPosition()) > system.Range ||
					grid.Physics == null || 
					block.CubeGrid.IsSameConstructAs(grid) || 
					(grid as MyCubeGrid).Immune || 
					!(grid as MyCubeGrid).Editable || 
					DSession.ClientSettings.HiddenGrids.Contains(grid.CustomName)) 
						continue;

				if (!system.Grids.Contains(grid))
					system.Grids.Add(grid);
			}

			DSession.UpdateTerminal();
		}

		private static void HideButton_action(IMyTerminalBlock block)
		{
			var system = GetBlock(block);
			if (system == null || system.SelectedGrid == null) return;

			if (!DSession.ClientSettings.HiddenGrids.Contains(system.SelectedGrid.CustomName))
			{
				DSession.ClientSettings.HiddenGrids.Add(system.SelectedGrid.CustomName);
				ClientSettings.Save(DSession.ClientSettings);
			}

			if (system.Grids.Contains(system.SelectedGrid))
				system.Grids.Remove(system.SelectedGrid);

			system.SelectedGrid = null;

			DSession.UpdateTerminal();
		}

		private static void ClearHiddenGridsButton_action(IMyTerminalBlock block)
		{
			var system = GetBlock(block);
			if (DSession.ClientSettings != null && DSession.ClientSettings.HiddenGrids.Count > 0)
			{
				DSession.ClientSettings.HiddenGrids.Clear();
				ClientSettings.Save(DSession.ClientSettings);
				SearchButton_action(block);
			}
		}

		private static void DematerializeButton_action(IMyTerminalBlock block)
		{
			var system = GetBlock(block);
			if (system != null && system.SelectedGrid != null)
			{
				if (Vector3D.Distance(system.SelectedGrid.GetPosition(), system.Entity.GetPosition()) > system.Range)
				{
					DSession.DematerializeButton.Title = MyStringId.GetOrCompute("Out of range!");
					system.SelectedGrid = null;
					DSession.UpdateTerminal();
					MyAPIGateway.Utilities.InvokeOnGameThread(() => { DSession.DematerializeButton.Title = MyStringId.GetOrCompute("Dematerialize"); }, "DematerializerMod", MyAPIGateway.Session.GameplayFrameCounter + 300, 0);
					return;
				}
				DSession.CachedPacketServer.Send(system.Entity.EntityId, system.SelectedGrid.EntityId, DSession.ClientSettings.BlacklistedItems);
			}
		}

		private static void List_selected(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selected)
		{
			var system = GetBlock(block);
			if (system != null && system.Grids != null && system.Grids.Count > 0)
			{
				if (selected.Count > 0)
					system.SelectedGrid = selected.First().UserData as IMyCubeGrid;
				else
					system.SelectedGrid = null;

				DSession.UpdateTerminal();
			}
		}

		private static void ComponentList_content(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> items, List<MyTerminalControlListBoxItem> selected)
		{
			var system = GetBlock(block);
			if (system == null || system.Settings == null) return;
			if (system.Settings.Items != null && system.Settings.Items.Count > 0)
			{
				for (var i = system.Settings.Items.Count - 1; i > -1; i--)
				{
					var item = system.Settings.Items[i];
					string name = item.PhysicalContent != null ? item.PhysicalContent.SubtypeName : item.SubtypeName;
					if (DSession.ClientSettings != null && DSession.ClientSettings.BlacklistedItems.Count > 0 && DSession.ClientSettings.BlacklistedItems.Contains(name))
						continue;
					var BoxItem = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(name + $": {item.Amount}"), MyStringId.NullOrEmpty, item);
					items.Add(BoxItem);
				}
				return;
			}

			if (system.SelectedGrid != null)
			{
				Dictionary<string, MyObjectBuilder_InventoryItem> content = Utils.GetComponents(system.SelectedGrid as MyCubeGrid);

				if (content != null && content.Count > 0)
				{
					foreach (var thing in content)
					{
						if (DSession.ClientSettings != null && DSession.ClientSettings.BlacklistedItems.Count > 0 && DSession.ClientSettings.BlacklistedItems.Contains(thing.Key)) 
							continue;
						items.Add(new MyTerminalControlListBoxItem(MyStringId.GetOrCompute($"{thing.Key}: {thing.Value.Amount:N0}"), MyStringId.NullOrEmpty, thing.Value));
					}
				}
			}
		}

		private static void Blacklist_content(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> items, List<MyTerminalControlListBoxItem> selected)
		{
			if (DSession.ClientSettings != null && DSession.ClientSettings.BlacklistedItems.Count > 0)
			{
				for (var i = DSession.ClientSettings.BlacklistedItems.Count - 1; i > -1; i--)
				{
					string name = DSession.ClientSettings.BlacklistedItems[i];
					var BoxItem = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(name), MyStringId.NullOrEmpty, null);
					items.Add(BoxItem);
				}
			}
		}

		private static void Whitelist_clicked(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selected)
		{
			if (DSession.ClientSettings != null && DSession.ClientSettings.BlacklistedItems != null)
			{
				MyObjectBuilder_InventoryItem item = selected[0].UserData as MyObjectBuilder_InventoryItem;
				if (item == null) return;
				string name = item.PhysicalContent != null ? item.PhysicalContent.SubtypeName : item.SubtypeName;
				if (!string.IsNullOrEmpty(name) && !DSession.ClientSettings.BlacklistedItems.Contains(name))
					DSession.ClientSettings.BlacklistedItems.Add(name);
			}
			DSession.Blacklist.UpdateVisual();
		}

		private static void Blacklist_clicked(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selected)
		{
			if (DSession.ClientSettings != null && DSession.ClientSettings.BlacklistedItems.Count > 0)
			{
				string name = selected[0].Text.String;
				if (DSession.ClientSettings.BlacklistedItems.Contains(name))
					DSession.ClientSettings.BlacklistedItems.Remove(name);
			}
			DSession.ComponentList.UpdateVisual();
		}

		private static void List_content(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> items, List<MyTerminalControlListBoxItem> selected)
		{
			var system = GetBlock(block);
			if (system != null && system.Grids != null && system.Grids.Count > 0)
			{
				foreach (var item in system.Grids)
				{
					var BoxItem = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(item.CustomName), MyStringId.NullOrEmpty, item);
					items.Add(BoxItem);
					if (system.SelectedGrid == item)
						selected.Add(BoxItem);
				}
			}
		}
	}
}