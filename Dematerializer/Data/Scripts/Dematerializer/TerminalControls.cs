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


		public static bool CheckModdedBlock(IMyTerminalBlock block)
		{
			if (block.GameLogic != null && block.GameLogic is DematerializerBlock)
				return false;
			return true;
		}

		public static void InitControls<T>()
		{
			IMyTerminalControl name = null;
			IMyTerminalControl onoff = null;
			List<IMyTerminalControl> list = new List<IMyTerminalControl>();
			MyAPIGateway.TerminalControls.GetControls<T>(out list);
			for (int i = list.Count - 1; i >= 0; i--)
			{
				if (list[i] is IMyTerminalControlOnOffSwitch && list[i].Id == "OnOff")
				{
					onoff = list[i];
					list.RemoveAt(i);
				}
				if (list[i].Id == "Name")
				{
					name = list[i];
					list.RemoveAt(i);
				}
				MyAPIGateway.TerminalControls.RemoveControl<T>(list[i]);
			}

			if (onoff != null)
				MyAPIGateway.TerminalControls.AddControl<T>(onoff);
			if (name != null)
				MyAPIGateway.TerminalControls.AddControl<T>(name);

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

			var dematerializeButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>("Start");
			dematerializeButton.Visible = StartButtonVisible;
			dematerializeButton.Enabled = SelectedEnabledCheck;
			dematerializeButton.SupportsMultipleBlocks = false;
			dematerializeButton.Title = MyStringId.GetOrCompute("Dematerialize");
			dematerializeButton.Action = DematerializeButton_action;
			MyAPIGateway.TerminalControls.AddControl<T>(dematerializeButton);

			var cancelButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>("Cancel");
			cancelButton.Visible = CancelButtonVisible;
			cancelButton.Enabled = CancelButtonVisible;
			cancelButton.SupportsMultipleBlocks = false;
			cancelButton.Title = MyStringId.GetOrCompute("Cancel");
			cancelButton.Action = CancelButton_action;
			MyAPIGateway.TerminalControls.AddControl<T>(cancelButton);

			var componentList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, T>("Components");
			componentList.Visible = VisibilityCheck;
			componentList.Multiselect = false;
			componentList.SupportsMultipleBlocks = false;
			componentList.VisibleRowsCount = 8;
			componentList.Title = MyStringId.GetOrCompute("Components");
			componentList.Tooltip = MyStringId.GetOrCompute("Select items and click button below to blacklist them.");
			componentList.ItemSelected = (IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selection) =>
			{
				MyObjectBuilder_InventoryItem item = selection[0].UserData as MyObjectBuilder_InventoryItem;
				if (item != null)
				{
					AddToBlacklist = item.PhysicalContent != null ? item.PhysicalContent.SubtypeName : item.SubtypeName;
				}
				DSession.BlacklistButton.UpdateVisual();
			};
			componentList.ListContent = ComponentList_content;
			MyAPIGateway.TerminalControls.AddControl<T>(componentList);

			var blacklistButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>("BlacklistButton");
			blacklistButton.Visible = VisibilityCheck;
			blacklistButton.Enabled = (IMyTerminalBlock block) => { return AddToBlacklist != null; };
			blacklistButton.SupportsMultipleBlocks = false;
			blacklistButton.Title = MyStringId.GetOrCompute("Blacklist Selected Item");
			blacklistButton.Tooltip = MyStringId.GetOrCompute("Adds the selected item to the blacklist.");
			blacklistButton.Action = Blacklist_clicked;
			MyAPIGateway.TerminalControls.AddControl<T>(blacklistButton);

			var blackList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, T>("Blacklist");
			blackList.Visible = VisibilityCheck;
			blackList.Multiselect = false;
			blackList.SupportsMultipleBlocks = false;
			blackList.VisibleRowsCount = 8;
			blackList.Title = MyStringId.GetOrCompute("Blacklisted Items");
			blackList.Tooltip = MyStringId.GetOrCompute("Blacklisted items will not get added to your inventory after dematerialization completes.");
			blackList.ItemSelected = (IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selection) => 
			{
				RemoveFromBlacklist = selection[0].Text.String;
				DSession.WhitelistButton.UpdateVisual();
			};
			blackList.ListContent = Blacklist_content;
			MyAPIGateway.TerminalControls.AddControl<T>(blackList);

			var whitelistButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>("WhitelistButton");
			whitelistButton.Visible = VisibilityCheck;
			whitelistButton.Enabled = (IMyTerminalBlock block) => { return RemoveFromBlacklist != null; };
			whitelistButton.SupportsMultipleBlocks = false;
			whitelistButton.Title = MyStringId.GetOrCompute("Whitelist Selected Item");
			whitelistButton.Tooltip = MyStringId.GetOrCompute("Removes the selected item from the blacklist.");
			whitelistButton.Action = Whitelist_clicked;
			MyAPIGateway.TerminalControls.AddControl<T>(whitelistButton);


			foreach (var control in list)
			{
				MyAPIGateway.TerminalControls.AddControl<T>(control);
			}

			DSession.SearchButton = searchButton;
			DSession.DematerializeButton = dematerializeButton;
			DSession.GridList = gridList;
			DSession.HideGridButton = hideGridButton;
			DSession.ClearHiddenButton= clearHiddenGridsButton;
			DSession.TimerBox = TimerBox;
			DSession.ComponentList = componentList;
			DSession.Blacklist = blackList;
			DSession.BlacklistButton = blacklistButton;
			DSession.WhitelistButton = whitelistButton;
			DSession.CancelButton = cancelButton;

			_TerminalInit = true;
		}

		private static string AddToBlacklist;
		private static string RemoveFromBlacklist;

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

			if (output.Length == 0)
				output.AppendLine("Grid is valid");
		}

		private static void GetComponents(Sandbox.ModAPI.Ingame.IMyTerminalBlock b, List<VRage.Game.ModAPI.Ingame.MyInventoryItem> items)
		{
			var system = GetBlock((IMyTerminalBlock)b);
			if (system == null) return;
			if (items == null) return;

			for (var i = system.Items.Count - 1; i > -1; i--)
			{
				var item = system.Items[i];
				var InvItem = new VRage.Game.ModAPI.Ingame.MyInventoryItem(item.PhysicalContent.GetId(), item.ItemId, item.Amount);
				items.Add(InvItem);
			}
		}

		private static StringBuilder TextBoxGetter(IMyTerminalBlock b)
		{
			var system = GetBlock(b);
			if (system == null) return new StringBuilder();
			var Builder = new StringBuilder();
			if (system != null && system.Time != null)
			{
				if (system.TimeStarted == DateTime.MinValue)
					return Builder.Append($"{system.Time:hh'h 'mm'm 'ss's '}");
				else
				{
					TimeSpan time = system.Time - (DateTime.UtcNow - system.TimeStarted);
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

		private static bool CancelButtonVisible(IMyTerminalBlock block)
		{
			var system = GetBlock(block);
			return system != null && system.IsGrinding;
		}
		private static bool StartButtonVisible(IMyTerminalBlock block)
		{
			var system = GetBlock(block);
			return system != null && !system.IsGrinding;
		}

		private static bool EnabledCheck(IMyTerminalBlock block)
		{
			var system = GetBlock(block);
			return system != null && !system.IsGrinding;
		}

		private static bool SelectedEnabledCheck(IMyTerminalBlock block)
		{
			var system = GetBlock(block);
			return system != null && !system.IsGrinding && system.SelectedGrid != null;
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
			system.SelectedGrid = null;

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
				DSession.CachedPacketServer.BeginGrindingRequest(system.Entity.EntityId, system.SelectedGrid.EntityId, DSession.ClientSettings.BlacklistedItems);
			}
		}

		private static void CancelButton_action(IMyTerminalBlock block)
		{
			var system = GetBlock(block);
			if (system != null && system.IsGrinding && system.SelectedGrid != null && system.Time > TimeSpan.Zero && system.TimeStarted != DateTime.MinValue)
			{
				DSession.CachedPacketServer.BeginGrindingRequest(system.Entity.EntityId, 0, null);
			}
		}

		private static void List_selected(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selected)
		{
			var system = GetBlock(block);
			if (system != null && system.Grids != null && system.Grids.Count > 0)
			{
				if (selected.Count > 0)
					system.SelectedGrid = selected[0].UserData as IMyCubeGrid;

				DSession.UpdateTerminal();
			}
		}

		private static void ComponentList_content(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> items, List<MyTerminalControlListBoxItem> selected)
		{
			var system = GetBlock(block);
			if (system == null) return;
			if (system.Items != null && system.Items.Count > 0)
			{
				for (var i = system.Items.Count - 1; i > -1; i--)
				{
					var item = system.Items[i];
					string name = item.PhysicalContent != null ? item.PhysicalContent.SubtypeName : item.SubtypeName;
					if (DSession.ClientSettings != null && DSession.ClientSettings.BlacklistedItems.Count > 0 && DSession.ClientSettings.BlacklistedItems.Contains(name))
						continue;
					var BoxItem = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(name + $": {item.Amount}"), MyStringId.NullOrEmpty, item);
					items.Add(BoxItem);
					if (AddToBlacklist == name)
						selected.Add(BoxItem);
				}
				return;
			}

			if (system.SelectedGrid != null)
			{
				Dictionary<string, MyObjectBuilder_InventoryItem> content = Utils.GetComponentsClient(system.SelectedGrid as MyCubeGrid);
			
				if (content != null && content.Count > 0)
				{
					foreach (var thing in content)
					{
						if (DSession.ClientSettings != null && DSession.ClientSettings.BlacklistedItems.Count > 0 && DSession.ClientSettings.BlacklistedItems.Contains(thing.Key)) 
							continue;
						var BoxItem = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute($"{thing.Key}: {thing.Value.Amount:N0}"), MyStringId.NullOrEmpty, thing.Value);
						items.Add(BoxItem);
						if (AddToBlacklist == thing.Key)
							selected.Add(BoxItem);
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
					var name = DSession.ClientSettings.BlacklistedItems[i];
					var BoxItem = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(name), MyStringId.NullOrEmpty, name);
					items.Add(BoxItem);
					if (RemoveFromBlacklist == name)
						selected.Add(BoxItem);
				}
			}
		}

		private static void Blacklist_clicked(IMyTerminalBlock block)
		{
			DematerializerBlock db = GetBlock(block);
			if (db != null && DSession.ClientSettings != null && DSession.ClientSettings.BlacklistedItems != null && AddToBlacklist != null)
			{
				if (!DSession.ClientSettings.BlacklistedItems.Contains(AddToBlacklist))
					DSession.ClientSettings.BlacklistedItems.Add(AddToBlacklist);

				if (!db.Blacklist.Contains(AddToBlacklist))
					db.Blacklist.Add(AddToBlacklist);

				AddToBlacklist = null;
				ClientSettings.Save(DSession.ClientSettings);
				DSession.UpdateTerminal();
			}
		}

		private static void Whitelist_clicked(IMyTerminalBlock block)
		{
			DematerializerBlock db = GetBlock(block);
			if (db != null && DSession.ClientSettings != null && DSession.ClientSettings.BlacklistedItems.Count > 0 && RemoveFromBlacklist != null)
			{
				if (DSession.ClientSettings.BlacklistedItems.Contains(RemoveFromBlacklist))
					DSession.ClientSettings.BlacklistedItems.Remove(RemoveFromBlacklist);

				if (db.Blacklist.Contains(RemoveFromBlacklist))
					db.Blacklist.Remove(RemoveFromBlacklist);

				RemoveFromBlacklist = null;
				ClientSettings.Save(DSession.ClientSettings);
				DSession.UpdateTerminal();
			}
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