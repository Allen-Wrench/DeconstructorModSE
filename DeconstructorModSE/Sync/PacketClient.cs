using System;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;

namespace DeconstructorModSE.Sync
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
		public bool IsGrinding;

		[ProtoMember(3)]
		public float Efficiency;

		[ProtoMember(4)]
		public TimeSpan Time;

		[ProtoMember(5)]
		public DateTime TimeStarted;

		public void Send(long entityId, bool isGrinding, float Eff, TimeSpan time)
		{
			EntityId = entityId;
			IsGrinding = isGrinding;
			Efficiency = Eff;
			Time = time;
			TimeStarted = DateTime.UtcNow;

			Networking.RelayToClients(this);
		}

		public override void Received(ref bool relay)
		{
			var block = MyAPIGateway.Entities.GetEntityById(this.EntityId) as IMyShipGrinder;

			if (block == null)
				return;

			var logic = block.GameLogic?.GetAs<DeconstructorMod>();

			if (logic == null)
				return;

			logic.Settings.IsGrinding = IsGrinding;
			logic.Settings.Efficiency = Efficiency;
			logic.Settings.Time = Time;
			logic.Settings.TimeStarted = TimeStarted;

			if (logic.SelectedGrid != null)
				logic.DeconstructFX(logic.SelectedGrid as MyCubeGrid);
			logic.DeconstructGrid(logic.SelectedGrid);

			relay = false;
		}
	}
}