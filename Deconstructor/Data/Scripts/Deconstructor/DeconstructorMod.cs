using DeconstructorModSE.Sync;
using Sandbox.Common.ObjectBuilders;
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

namespace DeconstructorModSE
{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_ShipGrinder), false, "LargeDeconstructor")]
	public class DeconstructorMod : MyGameLogicComponent
	{
		private DeconstructorSession Mod => DeconstructorSession.Instance;
		//Settings
		public ModSettings ModSettings => Mod.ModSettings;
		public float Range => ModSettings.Range;
		public float Power => ModSettings.Power;
		public float Efficiency_Min => ModSettings.Efficiency_Min;
		public float Efficiency_Max => ModSettings.Efficiency_Max;

		public const int SETTINGS_CHANGED_COUNTDOWN = 10;
		private int syncCounter;
		public readonly Guid SETTINGS_GUID = new Guid("1EAB58EE-7304-45D2-B3C8-9BA2DC31EF90");
		public BlockSettings Settings = new BlockSettings();
		private IMyShipGrinder deconstructor;
		private IMyInventory MyInventory;
		public List<IMyCubeGrid> Grids = new List<IMyCubeGrid>();
		public List<MyObjectBuilder_InventoryItem> Items = new List<MyObjectBuilder_InventoryItem>();
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

		public float Efficiency
		{
			get { return Settings.Efficiency; }
			set
			{
				var val = MathHelper.Clamp(value, Efficiency_Min, Efficiency_Max);
				if (Settings.Efficiency != val)
				{
					Settings.Efficiency = val;
					if (_SGrid != null)
					{
						GetGrindTime(_SGrid);

						if (!MyAPIGateway.Utilities.IsDedicated)
							Mod.TimerBox.UpdateVisual();
					}
				}
			}
		}

		public void DeconstructFX(MyCubeGrid grid)
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
				DeconstructGrid(target);
				SaveSettings(true);
				//Mod.CachedPacketClient.Send(deconstructor.EntityId, Settings.IsGrinding, Settings.Efficiency, Settings.Time, Settings.TimeStarted.Value);
			}
		}

		public float GetPowerRequired()
		{
			if (!Settings.IsGrinding) return sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);
			if (_SGrid == null) return Power;

			float time = 0;
			TimeSpan dt = Utils.GetGrindTime(this, ref _SGrid, false);
			if (dt == null) return Power;
			time = (float)dt.TotalSeconds;
			var removedTime = time / (1 - (Efficiency / 100));
			return Power + (removedTime / 1000 / 60 / 2);
		}

		public void SetPower(bool Working = false)
		{
			var powerRequired = GetPowerRequired();
			if (Working)
			{
				sink.SetRequiredInputByType(MyResourceDistributorComponent.ElectricityId, powerRequired); // In MW
				sink.SetMaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId, powerRequired); // In MW
			}
			else
			{
				sink.SetRequiredInputByType(MyResourceDistributorComponent.ElectricityId, Power); // In MW
				sink.SetMaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId, Power); // In MW
			}
			sink.Update();
		}

		public void DeconstructGrid(IMyCubeGrid SelectedGrid)
		{
			if (SelectedGrid == null) return;
			SetPower(true);
			Settings.IsGrinding = true;
			Settings.Error = "";
			Utils.DeconstructGrid(MyInventory, ref SelectedGrid, ref Settings.Items);

			MyCubeGrid grid = SelectedGrid as MyCubeGrid;
			grid.DismountAllCockpits();
			grid.ChangePowerProducerState(VRage.MyMultipleEnabledEnum.AllDisabled, -1);
			grid.Physics.Clear();
			grid.Physics.Flags = RigidBodyFlag.RBF_DISABLE_COLLISION_RESPONSE;
			grid.Immune = true;
			grid.Editable = false;
			grid.IsPreview = true;
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
			if (!MyAPIGateway.Utilities.IsDedicated && !DeconstructorTerminalInit._TerminalInit)
			{
				DeconstructorTerminalInit._TerminalInit = true;
				DeconstructorTerminalInit.InitControls<IMyShipGrinder>();
			}

			deconstructor = Entity as IMyShipGrinder;
			if (deconstructor == null && deconstructor.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
				return;

			MyInventory = deconstructor.GetInventory();
			sink = deconstructor.ResourceSink as MyResourceSinkComponent;

			deconstructor.AppendingCustomInfo += AddCustomInfo;

			LoadSettings();

			NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;

			SaveSettings(true);

			if (!MyAPIGateway.Multiplayer.IsServer)
			{
				PacketSettings s = new PacketSettings();
				s.RequestBlockSettings(deconstructor.EntityId);
			}
		}

		private void AddCustomInfo(IMyTerminalBlock block, StringBuilder info)
		{
			if (Settings.IsGrinding)
			{
				info.Append($"Power Required: {Math.Round(GetPowerRequired(), 2)}Mw\n");
			}
			if (!string.IsNullOrEmpty(Settings.Error))
				info.Append(Settings.Error + "\n");
		}

		// Saving
		private bool LoadSettings()
		{
			if (deconstructor.Storage == null)
				return false;
			
			string rawData;
			if (!deconstructor.Storage.TryGetValue(SETTINGS_GUID, out rawData))
				return false;

			try
			{
				var loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<BlockSettings>(Convert.FromBase64String(rawData));

				if (loadedSettings != null)
				{
					Settings.Efficiency = loadedSettings.Efficiency;
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
				DeconstructorLog.Error($"Error loading settings!\n{e}");
			}

			return false;
		}

		public void SaveSettings(bool sync = false)
		{
			if (!MyAPIGateway.Multiplayer.IsServer) return;

			if (deconstructor == null)
				return; // called too soon or after it was already closed, ignore

			if (Settings == null)
				throw new NullReferenceException($"Settings == null on entId={Entity?.EntityId}; modInstance={DeconstructorSession.Instance != null}");

			if (MyAPIGateway.Utilities == null)
				throw new NullReferenceException($"MyAPIGateway.Utilities == null; entId={Entity?.EntityId}; modInstance={DeconstructorSession.Instance != null}");

			if (deconstructor.Storage == null)
				deconstructor.Storage = new MyModStorageComponent();

			deconstructor.Storage.SetValue(SETTINGS_GUID, Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(Settings)));
			if (sync)
				SyncSettings();
		}

		public void SyncSettings()
		{
			PacketSettings s = new PacketSettings();
			s.Send(deconstructor.EntityId, Settings);
		}

		public override bool IsSerialized() => true;

		public override MyObjectBuilder_ComponentBase Serialize(bool copy = false)
		{
			if (Settings.IsGrinding && Settings.TimeStarted != null)
			{
				Settings.Time -= (DateTime.UtcNow - Settings.TimeStarted.Value);
				Settings.TimeStarted = null;
			}
			SaveSettings();
			return base.Serialize(copy);
		}

		public override void UpdateBeforeSimulation()
		{
			if (MyAPIGateway.Utilities.IsDedicated || Dissasemble == null || !deconstructor.IsFunctional || !deconstructor.IsWorking || !deconstructor.Enabled) return;

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
			if (deconstructor.IsFunctional && deconstructor.IsWorking && deconstructor.Enabled)
			{
				deconstructor.RefreshCustomInfo();

				if (Settings.IsGrinding)
				{
					if (Settings.TimeStarted == null)
						Settings.TimeStarted = DateTime.UtcNow;

					if (!MyAPIGateway.Utilities.IsDedicated)
					{
						DeconstructorSession.Instance.TimerBox.UpdateVisual();
						DeconstructorSession.Instance.ComponentList.UpdateVisual();
					}

					if (MyAPIGateway.Session.IsServer && (Settings.Time - (DateTime.UtcNow - Settings.TimeStarted.Value)) <= TimeSpan.Zero)
					{
						if (SelectedGrid != null)
						{
							SelectedGrid.Close();
							SelectedGrid = null;
						}

						if (Settings.Items != null && Settings.Items.Count > 0)
						{
							bool result = !Utils.SpawnItems(MyInventory, ref Settings.Items);
							if (Settings.Items.Count > 0)
							{
								if (!result && Settings.Error == "")
									Settings.Error = "Waiting for available inventory space...";
								else if (result && Settings.Error != "")
									Settings.Error = "";

								SaveSettings(true);
								return;
							}
							else
								Settings.Error = "Deconstruction Complete!";
						}

						Settings.Time = TimeSpan.Zero;
						Settings.TimeStarted = null;
						Settings.IsGrinding = false;
						Grids.Clear();
						SetPower();
						SaveSettings(true);
					}
				}
				return;
			}
			if (Settings.TimeStarted != null)
			{
				Settings.Time -= (DateTime.UtcNow - Settings.TimeStarted.Value);
				Settings.TimeStarted = null;
			}
		}

		public override void Close()
		{
			deconstructor.AppendingCustomInfo -= AddCustomInfo;
		}
	}
}