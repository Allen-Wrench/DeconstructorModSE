using Dematerializer.Sync;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine.Platform;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.ComponentSystem;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using IMyShipGrinder = Sandbox.ModAPI.IMyShipGrinder;

namespace Dematerializer
{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_ShipGrinder), false, new string[] {"Dematerializer_1", "Dematerializer_2", "Dematerializer_3", "Dematerializer_4"})]
	public class DematerializerBlock : MyGameLogicComponent
	{
		private DematerializerSession Mod => DematerializerSession.Instance;
		//Settings
		public ModSettings ModSettings => Mod.ModSettings;

		private static MyDefinitionId ElectricId => MyResourceDistributorComponent.ElectricityId;
		public static Dictionary<string, TierSettings> Tiers = new Dictionary<string, TierSettings>();
		private string name;
		public float Range => Tiers?[name].Range ?? 500;
		public float Power => Tiers?[name].Power ?? 1000;
		public float Efficiency => Tiers?[name].Efficiency ?? 75;

		public readonly Guid SETTINGS_GUID = new Guid("1EAB58EE-7304-45D2-B3C8-9BA2DC31EF90");
		public BlockSettings Settings = new BlockSettings();
		public List<string> Blacklist = new List<string>();
		private IMyShipGrinder dematerializer;
		private IMyInventory MyInventory;
		public List<IMyCubeGrid> Grids = new List<IMyCubeGrid>();
		private IMyCubeGrid _SGrid;

		private MyResourceSinkComponent sink;
		private int syncCountdown;
		private IEnumerator<int> Dissasemble;
		private int count = 0;
		private int repeat = 1;
		private int iteration = 0;

		public IMyCubeGrid SelectedGrid
		{
			get { return _SGrid; }
			set
			{
				if (_SGrid != value)
				{
					_SGrid = value;
					Settings.SelectedGrid = value != null ? value.EntityId : 0;

					if (value != null)
						GetGrindTime(value);

					if (!MyAPIGateway.Utilities.IsDedicated)
					{
						Mod.UpdateTerminal();
					}
				}
			}
		}

		public void DematerializeFX(MyCubeGrid grid)
		{
			if (Dissasemble == null && grid != null && Settings.TimeStarted != null)
			{
				int ticks = (int)((Settings.Time - (DateTime.UtcNow - Settings.TimeStarted.Value)).TotalSeconds * 60);
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
				NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
			}
		}

		private IEnumerator<int> Decon(IMyCubeGrid grid)
		{
			List<IMySlimBlock> blocks = new List<IMySlimBlock>();
			grid.GetBlocks(blocks);
			for (int i = blocks.Count - 1; i >= 0; i--)
			{
				if (blocks[i] == null) continue;
				blocks[i].PlayConstructionSound(MyIntegrityChangeEnum.DeconstructionProcess, true);
				blocks[i].Dithering = 1f;
				if (i % repeat == 0)
					yield return i;
			}
		}

		public void SyncServer(long grid)
		{
			if (!MyAPIGateway.Multiplayer.IsServer)
				return;

			IMyEntity entity;
			MyAPIGateway.Entities.TryGetEntityById(grid, out entity);

			if (entity != null && !entity.MarkedForClose && entity is IMyCubeGrid)
			{
				IMyCubeGrid target = entity as IMyCubeGrid;
				SelectedGrid = target;
				Settings.TimeStarted = DateTime.UtcNow;
				GetGrindTime(target);
				DematerializeGrid(target);
				SaveSettings(true);
			}
		}

		public float GetPowerRequired()
		{
			if (!Settings.IsGrinding) return 0;
			else return Power;

			//float time = 0;
			//TimeSpan dt = Utils.GetGrindTime(this, ref _SGrid, false);
			//if (dt == null) return Power;
			//time = (float)dt.TotalSeconds;
			//var removedTime = time / (1 - (Efficiency / 100));
			//return Power + (removedTime / 1000 / 60 / 2);
		}

		public void SetPower(bool Working = false)
		{
			if (sink == null)
				sink = dematerializer.ResourceSink as MyResourceSinkComponent;

			if (!Working)
			{
				sink.SetRequiredInputByType(ElectricId, 0); // In MW
				sink.SetMaxRequiredInputByType(ElectricId, 0); // In MW
			}
			else
			{
				sink.SetRequiredInputByType(ElectricId, Power); // In MW
				sink.SetMaxRequiredInputByType(ElectricId, Power); // In MW
			}
			sink.Update();
		}

		public void DematerializeGrid(IMyCubeGrid SelectedGrid)
		{
			if (SelectedGrid == null) return;
			SetPower(true);
			Settings.IsGrinding = true;
			Settings.Error = "";
			Utils.DematerializeGrid(MyInventory, ref SelectedGrid, ref Settings.Items);

			MyCubeGrid grid = SelectedGrid as MyCubeGrid;
			grid.DismountAllCockpits();
			grid.ChangePowerProducerState(VRage.MyMultipleEnabledEnum.AllDisabled, -1);
			grid.Physics.Clear();
			grid.Physics.Flags = RigidBodyFlag.RBF_DISABLE_COLLISION_RESPONSE;
			grid.Immune = true;
			grid.Editable = false;
			grid.IsPreview = true;
			grid.Render.Transparency = 0.75f;
			grid.Render.UpdateTransparency();
		}

		public void GetGrindTime(IMyCubeGrid SelectedGrid, bool calcEff = true)
		{
			Settings.Time = Utils.GetGrindTime(this, ref SelectedGrid, calcEff);
		}

		public override void Init(MyObjectBuilder_EntityBase objectBuilder)
		{
			NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
		}

		public override void UpdateOnceBeforeFrame()
		{
			if (!MyAPIGateway.Utilities.IsDedicated && !TerminalControlsInit._TerminalInit)
			{
				TerminalControlsInit._TerminalInit = true;
				TerminalControlsInit.InitControls<IMyShipGrinder>();
			}

			dematerializer = Entity as IMyShipGrinder;
			if (dematerializer == null || dematerializer.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
				return;

			name = ((MyCubeBlockDefinition)dematerializer.SlimBlock.BlockDefinition).BlockPairName;

			MyInventory = dematerializer.GetInventory();
			sink = dematerializer.ResourceSink as MyResourceSinkComponent;

			dematerializer.AppendingCustomInfo += AddCustomInfo;

			LoadSettings();

			NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;

			SaveSettings(true);

			if (!MyAPIGateway.Multiplayer.IsServer)
			{
				PacketSettings s = new PacketSettings();
				s.RequestBlockSettings(dematerializer.EntityId);
			}
		}

		private void AddCustomInfo(IMyTerminalBlock block, StringBuilder info)
		{
			bool active = Settings.IsGrinding && dematerializer.Enabled && SelectedGrid != null;
			string status = "";
			if (active)
				status = "Dematerializing...";
			else if (!Settings.IsGrinding && dematerializer.Enabled && SelectedGrid == null)
				status = "Idle";
			else if (Settings.IsGrinding && !dematerializer.Enabled)
				status = "Block is powered off";

			info.Append($"Power Required: {(active ? Power : 0):N0}Mw\n");
			info.Append($"Status: {status}\n");
			info.Append($"Error: {Settings.Error}\n");
		}

		// Saving
		private bool LoadSettings()
		{
			if (dematerializer.Storage == null)
				return false;
			
			string rawData;
			if (!dematerializer.Storage.TryGetValue(SETTINGS_GUID, out rawData))
				return false;

			try
			{
				var loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<BlockSettings>(Convert.FromBase64String(rawData));

				if (loadedSettings != null)
				{
					Settings.IsGrinding = loadedSettings.IsGrinding;
					Settings.Time = loadedSettings.Time;
					Settings.TimeStarted = loadedSettings.TimeStarted;
					Settings.Items = loadedSettings.Items;
					Settings.SelectedGrid = loadedSettings.SelectedGrid;
					if (Settings.SelectedGrid != 0)
					{
						IMyEntity ent;
						if (MyAPIGateway.Entities.TryGetEntityById(Settings.SelectedGrid, out ent) && ent is IMyCubeGrid)
							SelectedGrid = ent as IMyCubeGrid;
					}
					return true;
				}
			}
			catch (Exception e)
			{
				DematerializerLog.Error($"Error loading settings!\n{e}");
			}

			return false;
		}

		public void SaveSettings(bool sync = false)
		{
			if (!MyAPIGateway.Multiplayer.IsServer) return;

			if (dematerializer == null)
				return; // called too soon or after it was already closed, ignore

			if (Settings == null)
				throw new NullReferenceException($"Settings == null on entId={Entity?.EntityId}; modInstance={DematerializerSession.Instance != null}");

			if (MyAPIGateway.Utilities == null)
				throw new NullReferenceException($"MyAPIGateway.Utilities == null; entId={Entity?.EntityId}; modInstance={DematerializerSession.Instance != null}");

			if (dematerializer.Storage == null)
				dematerializer.Storage = new MyModStorageComponent();

			dematerializer.Storage.SetValue(SETTINGS_GUID, Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(Settings)));
			if (sync)
				SyncSettings();
		}

		public void SyncSettings()
		{
			PacketSettings s = new PacketSettings();
			s.Send(dematerializer.EntityId, Settings);
		}

		public override bool IsSerialized() => true;

		public override MyObjectBuilder_ComponentBase Serialize(bool copy = false)
		{
			if (MyAPIGateway.Multiplayer.IsServer)
			{
				if (Settings.IsGrinding && Settings.TimeStarted != null)
				{
					Settings.Time -= (DateTime.UtcNow - Settings.TimeStarted.Value);
					Settings.TimeStarted = null;
				}
				SaveSettings();
			}
			return base.Serialize(copy);
		}

		public override void UpdateBeforeSimulation()
		{
			if (MyAPIGateway.Utilities.IsDedicated || Dissasemble == null || !dematerializer.IsFunctional || !dematerializer.IsWorking || !dematerializer.Enabled) return;

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

		public override void UpdateAfterSimulation100()
		{
			if (!MyAPIGateway.Utilities.IsDedicated)
			{
				DematerializerSession.Instance.UpdateTerminal();
			}

			if (dematerializer.IsFunctional && dematerializer.IsWorking && dematerializer.Enabled)
			{
				if (Settings.IsGrinding)
				{
					if (dematerializer.CubeGrid.ResourceDistributor.TotalRequiredInputByType(ElectricId, dematerializer.CubeGrid) > dematerializer.CubeGrid.ResourceDistributor.MaxAvailableResourceByType(ElectricId, dematerializer.CubeGrid))
					{
						dematerializer.Enabled = false;
						Settings.Error = "Not enough power!";
						dematerializer.RefreshCustomInfo();
						return;
					}
					if (SelectedGrid != null && Vector3D.Distance(SelectedGrid.GetPosition(), dematerializer.GetPosition()) > Range)
					{
						dematerializer.Enabled = false;
						Settings.Error = "Target is out of range!";
						dematerializer.RefreshCustomInfo();
						return;
					}

					if (!MyAPIGateway.Multiplayer.IsServer) return;

					if (Settings.TimeStarted == null)
					{
						Settings.TimeStarted = DateTime.UtcNow;
						SaveSettings(true);
					}

					if ((Settings.Time - (DateTime.UtcNow - Settings.TimeStarted.Value)) <= TimeSpan.Zero)
					{
						if (SelectedGrid != null)
						{
							SelectedGrid.Close();
							SelectedGrid = null;
						}

						if (Settings.Items != null && Settings.Items.Count > 0)
						{
							bool result = !Utils.SpawnItems(MyInventory, ref Settings.Items, Blacklist);
							if (Settings.Items.Count > 0)
							{
								if (!result && Settings.Error == "")
									Settings.Error = "Waiting for available inventory space...";
								else if (result && Settings.Error != "")
									Settings.Error = "";

								SaveSettings(true);
								return;
							}
						}

						Settings.Error = "Dematerialization Complete!";
						Settings.Time = TimeSpan.Zero;
						Settings.TimeStarted = null;
						Settings.IsGrinding = false;
						Grids.Clear();
						SaveSettings(true);
					}
				}

				dematerializer.RefreshCustomInfo();
				return;
			}
			else if (Settings.IsGrinding && Settings.TimeStarted != null && MyAPIGateway.Multiplayer.IsServer)
			{
				Settings.Time -= (DateTime.UtcNow - Settings.TimeStarted.Value);
				Settings.TimeStarted = null;
				Settings.Error = "Paused";
				SaveSettings(true);
			}
		}

		public override void Close()
		{
			dematerializer.AppendingCustomInfo -= AddCustomInfo;
		}
	}
}