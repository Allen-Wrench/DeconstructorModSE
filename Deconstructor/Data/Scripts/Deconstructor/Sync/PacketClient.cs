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
		public DateTime TimeStarted;

		[ProtoMember(4)]
		public TimeSpan Time;

		public void Send(long entityId, bool isGrinding, TimeSpan time, DateTime timeStarted)
		{
			EntityId = entityId;
			IsGrinding = isGrinding;
			Time = time;
			TimeStarted = timeStarted;

			Networking.RelayToClients(this);
		}

		public override void Received(ref bool relay)
		{
			var block = MyAPIGateway.Entities.GetEntityById(EntityId) as IMyShipGrinder;

			if (block == null)
				return;

			var logic = block.GameLogic?.GetAs<DeconstructorMod>();

			if (logic == null)
				return;

			logic.Settings.IsGrinding = IsGrinding;
			logic.Settings.Time = Time;
			logic.Settings.TimeStarted = TimeStarted;

			logic.DeconstructGrid(logic.SelectedGrid);
			logic.DeconstructFX(logic.SelectedGrid as MyCubeGrid);

			DeconstructorSession.Instance.UpdateTerminal();

			relay = false;
		}
	}
}