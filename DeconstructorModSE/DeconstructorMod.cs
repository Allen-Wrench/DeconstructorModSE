using EmptyKeys.UserInterface.Generated.DataTemplatesStoreBlock_Bindings;
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
		//Settings
		public ModSettings ModSettings => DeconstructorSession.Instance.Settings;
		public float Range => ModSettings.Range;
		public float Power => ModSettings.Power;
		public float Efficiency_Min => ModSettings.Efficiency_Min;
		public float Efficiency_Max => ModSettings.Efficiency_Max;

		public const int SETTINGS_CHANGED_COUNTDOWN = 10; // div by 10 because it runs in update10
		public readonly Guid SETTINGS_GUID = new Guid("1EAB58EE-7304-45D2-B3C8-9BA2DC31EF90");
		public readonly DeconstructorBlockSettings Settings = new DeconstructorBlockSettings();
		private IMyShipGrinder deconstructor;

		private IMyInventory MyInventory;
		public List<IMyCubeGrid> Grids;
		private IMyCubeGrid _SGrid;

		private IEnumerator<int> Dissasemble;
		private int count;
		private int iteration;

		public void DeconstructFX(MyCubeGrid grid)
		{
			if (Dissasemble == null && grid != null && Settings.TimeStarted != null)
			{
				count = (int)((Settings.Time - (DateTime.UtcNow - Settings.TimeStarted.Value)).TotalSeconds * 60) / grid.CubeBlocks.Count;
				if (count < 1) 
					count = 1;
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
				yield return i;
			}
		}

		public IMyCubeGrid SelectedGrid
		{
			get { return _SGrid; }
			set
			{
				if (_SGrid != value)
				{
					_SGrid = value;
					GetGrindTime(value);
					DeconstructorSession.Instance.TimerBox.UpdateVisual();
				}
			}
		}

		private MyResourceSinkComponent sink;
		private int syncCountdown;
		private DeconstructorSession Mod => DeconstructorSession.Instance;

		public float Efficiency
		{
			get { return Settings.Efficiency; }
			set
			{
				var val = MathHelper.Clamp(value, Efficiency_Min, Efficiency_Max);
				if (Settings.Efficiency != val)
				{
					Settings.Efficiency = val;
					SettingsChanged();
					if (_SGrid != null)
					{
						GetGrindTime(_SGrid);
						DeconstructorSession.Instance.TimerBox.UpdateVisual();
					}
				}
			}
		}

		public void SyncServer(long grid)
		{
			IMyEntity entity;
			MyAPIGateway.Entities.TryGetEntityById(grid, out entity);

			if (entity != null && !entity.MarkedForClose)
			{
				var system = (IMyCubeGrid)entity;
				if (system != null)
				{
					GetGrindTime(system);
					DeconstructGrid(system);
					Mod.CachedPacketClient.Send(deconstructor.EntityId, Settings.IsGrinding, Settings.Efficiency, Settings.Time);
				}
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
			if (SelectedGrid == null || Settings.Items.Count > 0) return;
			SetPower(true);
			Settings.IsGrinding = true;
			Utils.DeconstructGrid(MyInventory, ref SelectedGrid, ref Settings.Items);

			MyCubeGrid grid = SelectedGrid as MyCubeGrid;
			grid.DismountAllCockpits();
			grid.ChangePowerProducerState(VRage.MyMultipleEnabledEnum.AllDisabled, -1);
			grid.Physics.Clear();
			grid.Physics.Flags = RigidBodyFlag.RBF_DISABLE_COLLISION_RESPONSE;
			grid.Immune = true;
			grid.Editable = false;
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
			if (!DeconstructorTerminalInit._TerminalInit)
			{
				DeconstructorTerminalInit._TerminalInit = true;
				DeconstructorTerminalInit.InitControls<IMyShipGrinder>();
			}

			deconstructor = (IMyShipGrinder)Entity;
			if (deconstructor.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
				return;

			MyInventory = deconstructor.GetInventory();
			sink = deconstructor.ResourceSink as MyResourceSinkComponent;

			Grids = new List<IMyCubeGrid>();
			deconstructor.AppendingCustomInfo += AddCustomInfo;

			LoadSettings();

			NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;

			SaveSettings();
		}

		private void AddCustomInfo(IMyTerminalBlock block, StringBuilder info)
		{
			if (Settings.IsGrinding)
			{
				info.Append($"Power Required: {Math.Round(GetPowerRequired() * 1000, 2)}Kw\n");
			}
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
				var loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<DeconstructorBlockSettings>(Convert.FromBase64String(rawData));

				if (loadedSettings != null)
				{
					Settings.Efficiency = loadedSettings.Efficiency;
					Settings.IsGrinding = loadedSettings.IsGrinding;
					Settings.Time = loadedSettings.Time;
					Settings.TimeStarted = loadedSettings.TimeStarted;
					Settings.Items = loadedSettings.Items;
					Settings.HiddenGrids = loadedSettings.HiddenGrids;
					return true;
				}
			}
			catch (Exception e)
			{
				DeconstructorLog.Error($"Error loading settings!\n{e}");
			}

			return false;
		}

		private void SaveSettings()
		{
			if (deconstructor == null)
				return; // called too soon or after it was already closed, ignore

			if (Settings == null)
				throw new NullReferenceException($"Settings == null on entId={Entity?.EntityId}; modInstance={DeconstructorSession.Instance != null}");

			if (MyAPIGateway.Utilities == null)
				throw new NullReferenceException($"MyAPIGateway.Utilities == null; entId={Entity?.EntityId}; modInstance={DeconstructorSession.Instance != null}");

			if (deconstructor.Storage == null)
				deconstructor.Storage = new MyModStorageComponent();

			deconstructor.Storage.SetValue(SETTINGS_GUID, Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(Settings)));
		}

		private void SettingsChanged()
		{
			if (syncCountdown == 0)
				syncCountdown = SETTINGS_CHANGED_COUNTDOWN;
		}

		private void SyncSettings()
		{
			if (syncCountdown > 0 && --syncCountdown <= 0)
			{
				SaveSettings();
			}
		}

		public override bool IsSerialized() => true;

		public override MyObjectBuilder_ComponentBase Serialize(bool copy = false)
		{
			SaveSettings();
			return base.Serialize(copy);
		}

		public override void UpdateBeforeSimulation()
		{
			if (MyAPIGateway.Utilities.IsDedicated || Dissasemble == null) return;

			iteration++;
			if (iteration < count) return;
			iteration = 0;

			if (!Dissasemble.MoveNext())
			{
				Dissasemble.Dispose();
				Dissasemble = null;
				iteration = 0;
				count = 0;
				NeedsUpdate &= ~MyEntityUpdateEnum.EACH_FRAME;
			}
		}

		public override void UpdateAfterSimulation100()
		{
			if (deconstructor.IsFunctional && deconstructor.IsWorking && deconstructor.Enabled)
			{
				if (SelectedGrid == null)
					if (Settings.IsGrinding)
						Settings.IsGrinding = false;

				if (!Settings.IsGrinding && (Grids == null || Grids.Count == 0)) return;

				deconstructor.RefreshCustomInfo();

				if (Settings.TimeStarted != null)
				{
					DeconstructorSession.Instance.TimerBox.UpdateVisual();

					if ((Settings.Time - (DateTime.UtcNow - Settings.TimeStarted.Value)) <= TimeSpan.Zero)
					{
						Settings.Time = TimeSpan.Zero;
						Settings.TimeStarted = null;
						SelectedGrid.Close();
						SelectedGrid = null;
					}
					else
						return;
				}

				if (SelectedGrid == null && Settings.Time == TimeSpan.Zero && Settings.Items.Count > 0)
				{
					Utils.SpawnItems(MyInventory, ref Settings.Items);
					DeconstructorSession.Instance.ComponentList.UpdateVisual();
					return;
				}

				Settings.IsGrinding = false;
				Grids.Clear();
				SetPower();
			}
			else
			{
				SelectedGrid = null;
				Grids.Clear();
				Settings.IsGrinding = false;
			}
		}

		public override void Close()
		{
			deconstructor.AppendingCustomInfo -= AddCustomInfo;
		}
	}
}