using Dematerializer.Sync;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine.Platform;
using Sandbox.Game.Components;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Multiplayer;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.Game.ObjectBuilders.ComponentSystem;
using VRage.ModAPI;
using VRage.ObjectBuilder;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRage.Utils;
using VRageMath;
using IMyShipGrinder = Sandbox.ModAPI.IMyShipGrinder;

namespace Dematerializer
{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_ShipGrinder), false, new string[] { "Dematerializer_1", "Dematerializer_2", "Dematerializer_3", "Dematerializer_4" })]
	public class DematerializerBlock : MyGameLogicComponent
	{
		private GridLog Logger => DematerializerSession.Logger;
		private DematerializerSession Mod => DematerializerSession.Instance;
		public ModSettings ModSettings => Mod.ModSettings;

		private bool initialized = false;

		private IMyCubeGrid selectedGrid;
		private MySync<long, SyncDirection.BothWays> clientSelected;
		private MySync<long, SyncDirection.FromServer> gridId;
		private MySync<bool, SyncDirection.FromServer> isGrinding;
		private MySync<bool, SyncDirection.BothWays> update;
		private MySync<long, SyncDirection.FromServer> time;
		private MySync<long, SyncDirection.FromServer> timeStarted;
		private MySync<int, SyncDirection.BothWays> status;

		private List<string> blacklist = new List<string>();

		public IMyCubeGrid SelectedGrid
		{
			get
			{
				if (selectedGrid == null && GridId != 0)
					selectedGrid = MyAPIGateway.Entities.GetEntityById(GridId) as IMyCubeGrid;
				return selectedGrid;
			}
			set
			{
				selectedGrid = value;
				if (!MyAPIGateway.Utilities.IsDedicated)
				{
					if (selectedGrid == null)
					{
						clientSelected.Value = 0;
						//gridId.SetLocalValue(0);
						time.SetLocalValue(0);
						timeStarted.SetLocalValue(0);
					}
					else if (selectedGrid.EntityId != GridId)
					{
						clientSelected.Value = selectedGrid.EntityId;
						//gridId.SetLocalValue(selectedGrid.EntityId);
						time.SetLocalValue(Utils.GetGrindTime(this, ref selectedGrid).Ticks);
					}
				}
			}
		}

		public long GridId
		{
			get
			{
				return gridId;
			}
			//set
			//{
			//	gridId.Value = value;
			//}
		}

		public bool IsGrinding
		{
			get
			{
				return isGrinding.Value;
			}
			//set
			//{
			//	isGrinding.Value = value;
			//}
		}
		public TimeSpan Time
		{
			get
			{
				return TimeSpan.FromTicks(time.Value);
			}
			//set
			//{
			//	time.Value = value.Ticks;
			//}
		}
		public DateTime TimeStarted
		{
			get
			{
				return DateTime.FromBinary(timeStarted.Value);
			}
			//set
			//{
			//	timeStarted.Value = value.ToBinary();
			//}
		}
		public List<MyObjectBuilder_InventoryItem> Items = new List<MyObjectBuilder_InventoryItem>();
		public List<string> Blacklist
		{
			get
			{
				if (blacklist == null)
					blacklist = new List<string>();

				if (MyAPIGateway.Utilities.IsDedicated)
					return blacklist;
				else
					return Mod.ClientSettings.BlacklistedItems;
			}
			set
			{
				if (value == null)
					blacklist = new List<string>();

				if (MyAPIGateway.Utilities.IsDedicated)
					blacklist = value;
				else
				{
					Mod.ClientSettings.BlacklistedItems = value;
					Mod.ClientSettings.Save();
				}
			}
		}
		public Status StatusInfo
		{
			get
			{
				return (Status)status.Value;
			}
			set
			{
				if (MyAPIGateway.Multiplayer.IsServer)
					status.Value = (int)value;
				dematerializer?.RefreshCustomInfo();
			}
		}

		private static MyDefinitionId ElectricId => MyResourceDistributorComponent.ElectricityId;
		private string name;
		public float Range { get; private set; } = 500;
		public float Power { get; private set; } = 1000;
		public float Efficiency { get; private set; } = 75;

		public readonly Guid SETTINGS_GUID = new Guid("1EAB58EE-7304-45D2-B3C8-9BA2DC31EF90");
		//public BlockSettings Settings = new BlockSettings();
		private IMyShipGrinder dematerializer;
		private IMyInventory MyInventory;
		public List<IMyCubeGrid> Grids = new List<IMyCubeGrid>();

		private MyResourceSinkComponent sink;
		internal IEnumerator<int> Dissasemble;
		private int count = 0;
		private int repeat = 1;
		private int iteration = 0;

		private void InitializeBlock()
		{
			dematerializer = Entity as IMyShipGrinder;
			if (dematerializer == null || dematerializer.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
			{
				initialized = false;
				return;
			}

			if (!UpdateModSettings())
			{
				initialized = false;
				return;
			}

			if (!MyAPIGateway.Utilities.IsDedicated && !TerminalControlsInit._TerminalInit)
			{
				try
				{
					TerminalControlsInit.InitControls<IMyShipGrinder>();
				}
				catch
				{
					initialized = false;
				}
				return;
			}

			MyInventory = dematerializer.GetInventory();

			sink = dematerializer.ResourceSink as MyResourceSinkComponent;
			sink.RequiredInputChanged += RequiredInputChanged;
			sink.SetMaxRequiredInputByType(ElectricId, Power);
			//sink.SetRequiredInputFuncByType(ElectricId, PowerRequiredFunc);


			NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;

			dematerializer.AppendingCustomInfo += AddCustomInfo;
			gridId.ValueChanged += GridIdChanged;
			isGrinding.ValueChanged += IsGrindingChanged;
			status.ValueChanged += StatusUpdated;

			if (MyAPIGateway.Multiplayer.IsServer)
			{
				clientSelected.ValueChanged += ClientSelected_ValueChanged;
				LoadSettings();
				SaveSettings();
			}
			initialized = true;
		}

		public bool UpdateModSettings()
		{
			name = ((MyCubeBlockDefinition)dematerializer.SlimBlock.BlockDefinition).BlockPairName;
			if (Mod.ModSettings != null && !string.IsNullOrEmpty(name))
			{
				switch (name)
				{
					case "Dematerializer_1":
						Range = Mod.ModSettings.Tier1.Range;
						Power = Mod.ModSettings.Tier1.Power;
						Efficiency = Mod.ModSettings.Tier1.Efficiency;
						break;
					case "Dematerializer_2":
						Range = Mod.ModSettings.Tier2.Range;
						Power = Mod.ModSettings.Tier2.Power;
						Efficiency = Mod.ModSettings.Tier2.Efficiency;
						break;
					case "Dematerializer_3":
						Range = Mod.ModSettings.Tier3.Range;
						Power = Mod.ModSettings.Tier3.Power;
						Efficiency = Mod.ModSettings.Tier3.Efficiency;
						break;
					case "Dematerializer_4":
						Range = Mod.ModSettings.Tier4.Range;
						Power = Mod.ModSettings.Tier4.Power;
						Efficiency = Mod.ModSettings.Tier4.Efficiency;
						break;
					default:
						return false;
				}
				return true;
			}
			return false;
		}

		private void ClientSelected_ValueChanged(MySync<long, SyncDirection.BothWays> sync)
		{
			gridId.Value = sync.Value;
			if (sync.Value == 0)
			{
				selectedGrid = null;
			}
			else
			{
				selectedGrid = MyAPIGateway.Entities.GetEntityById(sync.Value) as IMyCubeGrid;
			}
			StatusInfo &= ~(Status.Cancelled | Status.Complete);
		}

		private void IsGrindingChanged(MySync<bool, SyncDirection.FromServer> sync)
		{
			SetPower(sync.Value);
			dematerializer.RefreshCustomInfo();
			Mod.UpdateTerminal();
		}

		private void GridIdChanged(MySync<long, SyncDirection.FromServer> sync)
		{
			if (sync.Value == 0)
				selectedGrid = null;
			else //if (SelectedGrid != null && SelectedGrid.EntityId != sync.Value)
				selectedGrid = MyAPIGateway.Entities.GetEntityById(sync.Value) as IMyCubeGrid;
		}

		private void StatusUpdated(MySync<int, SyncDirection.BothWays> sync)
		{
			dematerializer.RefreshCustomInfo();
			Mod.UpdateTerminal();
		}

		private void CheckStatus()
		{
			if (MyAPIGateway.Multiplayer.IsServer)
			{
				if (dematerializer.Enabled && !IsGrinding)
					StatusInfo |= Status.Idle;
				else
					StatusInfo &= ~Status.Idle;

				if (!dematerializer.Enabled)
					StatusInfo |= Status.PoweredOff;
				else
					StatusInfo &= ~Status.PoweredOff;

				if (IsGrinding && StatusInfo == Status.None)
					StatusInfo |= Status.Working;
				else if (StatusInfo != Status.Working)
					StatusInfo &= ~Status.Working;
			}
		}

		public void DematerializeFX(MyCubeGrid grid)
		{
			if (MyAPIGateway.Utilities.IsDedicated) return;

			if (Dissasemble == null && grid != null && TimeStarted != DateTime.MinValue)
			{
				int ticks = (int)(Time.TotalSeconds * 60);
				int num = grid.CubeBlocks.Count;
				if (ticks > num && num != 0)
					count = ticks / num;
				else if (num > ticks && ticks != 0)
					repeat = num / ticks;
				if (count < 1)
					count = 1;
				if (repeat < 1)
					repeat = 1;
				iteration = 0;

				Dissasemble = Decon(grid);
				Dissasemble.MoveNext();
				NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
			}
		}

		private IEnumerator<int> Decon(IMyCubeGrid grid)
		{
			if (grid == null || grid.MarkedForClose) yield break;
			List<IMySlimBlock> blocks = new List<IMySlimBlock>();
			grid.GetBlocks(blocks);
			foreach (IMySlimBlock block in blocks)
			{
				MyCubeBlock b = block.FatBlock as MyCubeBlock;
				if (b != null && b.UseObjectsComponent != null && b.UseObjectsComponent.DetectorPhysics != null)
				{
					b.UseObjectsComponent.DetectorPhysics.Enabled = false;
				}
			}
			for (int i = blocks.Count - 1; i >= 0; i--)
			{
				if (grid == null || grid.MarkedForClose || grid.Closed) yield break;
				if (blocks[i] == null) continue;
				blocks[i].PlayConstructionSound(MyIntegrityChangeEnum.DeconstructionProcess, true);
				SetTransparency(blocks[i], 1f);
				if (i % repeat == 0)
					yield return i;
			}
		}

		//private IEnumerator<int> CancelDecon(IMyCubeGrid grid)
		//{
		//	if (grid == null || grid.MarkedForClose) yield break;
		//	List<IMySlimBlock> blocks = new List<IMySlimBlock>();
		//	grid.GetBlocks(blocks);
		//	for (int i = blocks.Count - 1; i >= 0; i--)
		//	{
		//		if (grid == null || grid.MarkedForClose || grid.Closed) yield break;
		//		if (blocks[i] == null) continue;
		//		SetTransparency(blocks[i], 0f);
		//		if (i % 100 == 0)
		//			yield return 2;
		//	}
		//}

		private void SetTransparency(IMySlimBlock cubeBlock, float transparency)
		{
			transparency = -transparency;
			if (cubeBlock.Dithering == transparency && cubeBlock.CubeGrid.Render.Transparency == transparency)
			{
				return;
			}
			cubeBlock.Dithering = transparency;
			cubeBlock.UpdateVisual();
			MyCubeBlock fatBlock = cubeBlock.FatBlock as MyCubeBlock;
			if (fatBlock != null)
			{
				fatBlock.Render.CastShadows = transparency == 0f ? true : false;
				SetTransparencyForSubparts(fatBlock, transparency);
			}
		}

		private void SetTransparencyForSubparts(MyEntity renderEntity, float transparency)
		{
			if (renderEntity.Subparts == null)
			{
				return;
			}
			foreach (MyEntitySubpart subPart in renderEntity.Subparts.Values)
			{
				subPart.Render.Transparency = transparency;
				subPart.Render.CastShadows = transparency == 0f ? true : false;
				subPart.Render.RemoveRenderObjects();
				subPart.Render.AddRenderObjects();
				SetTransparencyForSubparts(subPart, transparency);
			}
		}

		public List<MyObjectBuilder_InventoryItem> SyncServer(long cubeGridId)
		{
			if (!MyAPIGateway.Multiplayer.IsServer)
				return Items;

			StatusInfo &= ~(Status.Cancelled | Status.Complete);

			IMyEntity entity;
			MyAPIGateway.Entities.TryGetEntityById(cubeGridId, out entity);
			IMyCubeGrid target = entity as IMyCubeGrid;

			if (target != null && !target.MarkedForClose)
			{
				gridId.Value = cubeGridId;
				timeStarted.Value = DateTime.UtcNow.ToBinary();
				GetGrindTime(target);
				Items = Utils.GetComponentsServer(MyInventory, target);
				DematerializeGrid(target);
				isGrinding.Value = true;

				if (target.Storage == null)
					target.Storage = new MyModStorageComponent();

				ProcessingTag tag = new ProcessingTag
				{
					GrinderId = dematerializer.EntityId,
					Time = Time,
					TimeStarted = TimeStarted
				};

				target.Storage.SetValue(SETTINGS_GUID, Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(tag)));
				target.OnMarkForClose += GridMarkedClosed;

				Logger.BeginGrinding(this);
			}
			return Items;
		}

		//public float PowerRequiredFunc()
		//{
		//	if (dematerializer.Enabled && IsGrinding && ((SelectedGrid != null && Time != TimeSpan.Zero) || Items.Count > 0))
		//		return Power;
		//	return 0;
		//}

		public void SetPower(bool Working = false)
		{
			if (sink == null)
				sink = dematerializer.ResourceSink as MyResourceSinkComponent;

			if (!Working)
			{
				sink.SetRequiredInputByType(ElectricId, 0);
			}
			else
			{
				sink.SetRequiredInputByType(ElectricId, Power);
			}
		}

		public void DematerializeGrid(IMyCubeGrid gridSelected)
		{
			if (gridSelected == null) return;

			MyCubeGrid grid = gridSelected as MyCubeGrid;
			grid.DismountAllCockpits();
			grid.ChangePowerProducerState(VRage.MyMultipleEnabledEnum.AllDisabled, -1);
			gridSelected.Physics.SetSpeeds(Vector3.Zero, Vector3.Zero);
			gridSelected.Physics.IsPhantom = true;
			grid.Immune = true;
			grid.Editable = false;
			grid.IsPreview = true;
			foreach (MyCubeBlock fatBlock in grid.GetFatBlocks().ToList())
			{
				if (fatBlock != null && fatBlock.UseObjectsComponent != null && fatBlock.UseObjectsComponent.DetectorPhysics != null)
				{
					fatBlock.UseObjectsComponent.DetectorPhysics.Enabled = false;
				}
			}
		}

		public void GetGrindTime(IMyCubeGrid grid, bool calcEff = true)
		{
			time.Value = Utils.GetGrindTime(this, ref grid).Ticks;
			dematerializer.RefreshCustomInfo();
			Mod.UpdateTerminal();
		}

		public override void Init(MyObjectBuilder_EntityBase objectBuilder)
		{
			NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME;
		}

		private void RequiredInputChanged(MyDefinitionId resourceTypeId, MyResourceSinkComponent receiver, float oldRequirement, float newRequirement)
		{
			if (!IsGrinding && newRequirement > 0)
				SetPower(false);
			dematerializer.RefreshCustomInfo();
			Mod.UpdateTerminal();
		}

		public StringBuilder CurrentStatus(Status flag)
		{
			StringBuilder sb = new StringBuilder();
			if (flag.HasFlag(Status.Idle))
				sb.AppendLine("Idle");
			if (flag.HasFlag(Status.Working))
				sb.AppendLine("Dematerializing");
			if (flag.HasFlag(Status.Paused))
				sb.AppendLine("Paused");
			if (flag.HasFlag(Status.PoweredOff))
				sb.AppendLine("Block is powered off");
			if (flag.HasFlag(Status.NoPower))
				sb.AppendLine("Insufficient power");
			if (flag.HasFlag(Status.OutOfRange))
				sb.AppendLine("Target is out of range");
			if (flag.HasFlag(Status.NoCargoSpace))
				sb.AppendLine("Waiting for availabe inventory space");
			if (flag.HasFlag(Status.TransferringCargo))
				sb.AppendLine("Transferring items into inventory");
			if (flag.HasFlag(Status.Cancelled))
				sb.AppendLine("Operation cancelled");
			if (flag.HasFlag(Status.Complete))
				sb.AppendLine("Processing completed");
			if (flag.HasFlag(Status.GridNotStatic))
				sb.AppendLine("This block will only work on station grids.");
			return sb;
		}

		private void AddCustomInfo(IMyTerminalBlock block, StringBuilder info)
		{
			float requiredPower = sink.RequiredInputByType(ElectricId);
			info.Append($"Power Required: {requiredPower:N0}Mw\n");

			info.Append("Status: ");
			info.Append(CurrentStatus(StatusInfo));
		}

		// Saving
		private bool LoadSettings()
		{
			if (!MyAPIGateway.Session.IsServer || dematerializer.Storage == null)
				return false;

			string rawData;
			if (!dematerializer.Storage.TryGetValue(SETTINGS_GUID, out rawData))
				return false;

			try
			{
				var loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<BlockSettings>(Convert.FromBase64String(rawData));

				if (loadedSettings != null)
				{
					isGrinding.Value = loadedSettings.IsGrinding;
					time.Value = loadedSettings.Time;
					timeStarted.Value = loadedSettings.TimeStarted;
					StatusInfo = (Status)loadedSettings.StatusEnum;
					Items = loadedSettings.Items;
					Blacklist = loadedSettings.Blacklist;
					gridId.Value = loadedSettings.SelectedGrid;
					if (GridId != 0)
					{
						IMyEntity ent;
						if (MyAPIGateway.Entities.TryGetEntityById(GridId, out ent) && ent is IMyCubeGrid)
							SelectedGrid = ent as IMyCubeGrid;
					}
					return true;
				}
			}
			catch (Exception e)
			{
				MyLog.Default.Error($"Error loading settings!\n{e}");
			}

			return false;
		}

		public void SaveSettings()
		{
			if (!MyAPIGateway.Multiplayer.IsServer) return;

			if (dematerializer == null)
				return; // called too soon or after it was already closed, ignore

			if (MyAPIGateway.Utilities == null)
				throw new NullReferenceException($"MyAPIGateway.Utilities == null; entId={Entity?.EntityId}; modInstance={DematerializerSession.Instance != null}");

			if (dematerializer.Storage == null)
				dematerializer.Storage = new MyModStorageComponent();

			BlockSettings settings = new BlockSettings
			{
				SelectedGrid = gridId,
				IsGrinding = isGrinding,
				Time = time,
				TimeStarted = timeStarted,
				Items = Items,
				Blacklist = Blacklist,
				StatusEnum = (long)StatusInfo
			};
			if (IsGrinding && TimeStarted != DateTime.MinValue)
			{
				settings.Time = (Time - (DateTime.UtcNow - TimeStarted)).Ticks;
				settings.TimeStarted = 0;
				settings.StatusEnum |= (long)Status.Paused;
			}

			dematerializer.Storage.SetValue(SETTINGS_GUID, Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(settings)));
		}


		public override bool IsSerialized() => MyAPIGateway.Multiplayer.IsServer;

		public override MyObjectBuilder_ComponentBase Serialize(bool copy = false)
		{
			if (MyAPIGateway.Multiplayer.IsServer)
			{
				SaveSettings();
			}
			return base.Serialize(copy);
		}

		public override void UpdateBeforeSimulation()
		{
			if (MyAPIGateway.Utilities.IsDedicated || !initialized || !dematerializer.IsFunctional || !dematerializer.IsWorking || !dematerializer.Enabled) return;
			if (Dissasemble == null)
			{
				NeedsUpdate &= ~MyEntityUpdateEnum.EACH_FRAME;
				return;
			}

			iteration++;
			if (iteration < count) return;
			iteration = 0;

			if (SelectedGrid == null || !Dissasemble.MoveNext())
			{
				Dissasemble.Dispose();
				Dissasemble = null;
				iteration = 0;
				repeat = 1;
				count = 0;
				NeedsUpdate &= ~MyEntityUpdateEnum.EACH_FRAME;
			}
		}

		public override void UpdateBeforeSimulation10()
		{
			InitializeBlock();
			if (initialized)
			{
				NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
				return;
			}
		}

		public override void UpdateAfterSimulation100()
		{
			if (!initialized) return;
			CheckStatus();

			if (MyAPIGateway.Multiplayer.IsServer && !dematerializer.CubeGrid.IsStatic)
			{
				if (IsGrinding && SelectedGrid != null)
				{
					CancelOperation();
					dematerializer.Enabled = false;
				}
				StatusInfo |= Status.GridNotStatic;
				return;
			}
			else if (MyAPIGateway.Multiplayer.IsServer && dematerializer.CubeGrid.IsStatic && (StatusInfo & Status.GridNotStatic) == Status.GridNotStatic)
			{
				StatusInfo &= ~Status.GridNotStatic;
			}

			//dematerializer.RefreshCustomInfo();
			//Mod.UpdateTerminal();

			if (dematerializer.IsFunctional && dematerializer.IsWorking && dematerializer.Enabled && !StatusInfo.HasFlag(Status.Cancelled))
			{
				if (IsGrinding && !MyAPIGateway.Utilities.IsDedicated)
				{
					if (Dissasemble == null && SelectedGrid != null && TimeStarted != DateTime.MinValue)
						DematerializeFX(SelectedGrid as MyCubeGrid);
				}

				if (MyAPIGateway.Multiplayer.IsServer)
				{
					if (!IsGrinding && SelectedGrid != null)
					{
						if (StatusInfo.HasFlag(Status.NoPower) && sink.IsPowerAvailable(ElectricId, Power))
						{
							StatusInfo &= ~Status.NoPower;
							isGrinding.Value = true;
						}
						if (StatusInfo.HasFlag(Status.OutOfRange) && Vector3D.Distance(SelectedGrid.GetPosition(), dematerializer.GetPosition()) <= Range)
						{
							StatusInfo &= ~Status.OutOfRange;
							isGrinding.Value = true;
						}
					}

					if (IsGrinding && SelectedGrid != null)
					{
						if (!sink.IsPowerAvailable(ElectricId, Power))
						{
							isGrinding.Value = false;
							time.Value = (Time - (DateTime.UtcNow - TimeStarted)).Ticks;
							timeStarted.Value = 0;
							StatusInfo |= Status.NoPower;
							return;
						}

						if (Vector3D.Distance(SelectedGrid.GetPosition(), dematerializer.GetPosition()) > Range)
						{
							isGrinding.Value = false;
							time.Value = (Time - (DateTime.UtcNow - TimeStarted)).Ticks;
							timeStarted.Value = 0;
							StatusInfo |= Status.OutOfRange;
							return;
						}

						if (StatusInfo.HasFlag(Status.Paused))
						{
							timeStarted.Value = DateTime.UtcNow.ToBinary();
							StatusInfo &= ~Status.Paused;
						}
					}

					if (IsGrinding && (Time - (DateTime.UtcNow - TimeStarted)) <= TimeSpan.Zero)
					{
						if (SelectedGrid != null)
						{
							for (int i = Items.Count - 1; i >= 0; i--)
							{
								if (Blacklist.Count > 0 && Blacklist.Contains(Items[i].PhysicalContent?.SubtypeName ?? Items[i].SubtypeName))
									Items.RemoveAtFast(i);
							}
							new PacketClient().SendProgressUpdate(dematerializer.EntityId, GridId, Items);
							Logger.FinishGrinding(this);
							Grids.Remove(SelectedGrid);
							SelectedGrid.OnMarkForClose -= GridMarkedClosed;
							SelectedGrid.Close();
							gridId.Value = 0;
							StatusInfo |= Status.TransferringCargo;
						}

						if (Items != null && Items.Count > 0)
						{
							bool result = !Utils.SpawnItems(MyInventory, ref Items);
							if (Items.Count > 0)
							{
								if (!result)
									StatusInfo |= Status.NoCargoSpace;
								else if (result)
									StatusInfo &= ~Status.NoCargoSpace;

								new PacketClient().SendProgressUpdate(dematerializer.EntityId, 0, Items);

								return;
							}
						}

						new PacketClient().SendProgressUpdate(dematerializer.EntityId, 0, null);
						StatusInfo = Status.Complete;
						time.Value = 0;
						timeStarted.Value = 0;
						isGrinding.Value = false;
					}
				}
				dematerializer.RefreshCustomInfo();
				Mod.UpdateTerminal();
				return;
			}
			else if (IsGrinding && TimeStarted != DateTime.MinValue && MyAPIGateway.Multiplayer.IsServer)
			{
				time.Value = (Time - (DateTime.UtcNow - TimeStarted)).Ticks;
				timeStarted.Value = 0;
				//isGrinding.Value = false;
				dematerializer.Enabled = false;
				StatusInfo |= Status.Paused;
			}
			dematerializer.RefreshCustomInfo();
			Mod.UpdateTerminal();
		}


		public override void MarkForClose()
		{
			if (SelectedGrid != null)
			{
				CancelOperation();
			}
		}

		public override void Close()
		{
			if (dematerializer != null)
				dematerializer.AppendingCustomInfo -= AddCustomInfo;
		}

		public void GridMarkedClosed(IMyEntity entity)
		{
			IMyCubeGrid grid = entity as IMyCubeGrid;
			if (grid != null && grid.EntityId == GridId && IsGrinding && (Time - (DateTime.UtcNow - TimeStarted)) > TimeSpan.Zero)
			{
				CancelOperation();
				//time.Value = (Time - (DateTime.UtcNow - TimeStarted)).Ticks;
				//timeStarted.Value = 0;
				//isGrinding.Value = false;
				//gridId.Value = 0;
				//update.Value = true;
			}
		}

		public void ResumeProcessing(long gId)
		{
			gridId.Value = gId;
			timeStarted.Value = DateTime.UtcNow.ToBinary();
			isGrinding.Value = true;
		}

		public void CancelOperation()
		{
			if (SelectedGrid != null)
			{
				if (!MyAPIGateway.Utilities.IsDedicated)
				{
					MyAPIGateway.Utilities.ShowNotification("Dematerializing has been canceled.", 5000, "Red");
					if (Dissasemble != null)
					{
						Dissasemble.Dispose();
						NeedsUpdate &= ~MyEntityUpdateEnum.EACH_FRAME;
					}
					List<IMySlimBlock> blocks = new List<IMySlimBlock>();
					SelectedGrid.GetBlocks(blocks);
					foreach (IMySlimBlock block in blocks)
					{
						MyCubeBlock b = block.FatBlock as MyCubeBlock;
						if (b != null) continue;
						if (b.UseObjectsComponent != null && b.UseObjectsComponent.DetectorPhysics != null)
						{
							b.UseObjectsComponent.DetectorPhysics.Enabled = true;
						}
						SetTransparency(block, 0f);
					}
					SelectedGrid.Render.Transparency = 0f;
					SelectedGrid.Render.CastShadows = true;
					SelectedGrid.Render.UpdateTransparency();
					SelectedGrid.Render.RemoveRenderObjects();
					SelectedGrid.Render.AddRenderObjects();
				}
				MyCubeGrid grid = SelectedGrid as MyCubeGrid;
				if (grid != null)
				{
					grid.Immune = false;
					grid.Editable = true;
					grid.IsPreview = false;
					if (SelectedGrid.Physics != null)
						SelectedGrid.Physics.IsPhantom = false;
				}

				if (MyAPIGateway.Multiplayer.IsServer)
				{
					Logger.CancelGrinding(this);
					StatusInfo |= Status.Cancelled;
					SelectedGrid.OnClose -= GridMarkedClosed;
					gridId.Value = 0;
					isGrinding.Value = false;
					time.Value = 0;
					timeStarted.Value = 0;
				}

				Items.Clear();
				dematerializer.RefreshCustomInfo();
				Mod.UpdateTerminal();
			}
		}
	}

	[Flags]
	public enum Status
	{
		None = 0,
		Idle = 1,
		Working = 2,
		Paused = 4,
		PoweredOff = 8,
		NoPower = 16,
		OutOfRange = 32,
		NoCargoSpace = 64,
		TransferringCargo = 128,
		Complete = 256,
		Cancelled = 512,
		GridNotStatic = 1024
	}
}