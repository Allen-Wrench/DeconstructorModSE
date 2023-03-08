using ProtoBuf;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Sync;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRageMath;
using Sandbox.ModAPI;

namespace Dematerializer
{
	[ProtoContract]
	public class BlockSettings
	{
		[ProtoMember(1)]
		public long SelectedGrid = 0;

		[ProtoMember(2)]
		public bool IsGrinding = false;

		[ProtoMember(3)]
		public long Time = 0;

		[ProtoMember(4)]
		public long TimeStarted = 0;

		[ProtoMember(5)]
		public List<MyObjectBuilder_InventoryItem> Items = new List<MyObjectBuilder_InventoryItem>();

		[ProtoMember(6)]
		public List<string> Blacklist = new List<string>();

		[ProtoMember(7)]
		public long StatusEnum = (long)DematerializerBlock.Status.None;
	}

	[ProtoContract]
	public class ProcessingTag
	{
		[ProtoMember(1)]
		public long GrinderId;

		[ProtoMember(2)]
		public TimeSpan Time;

		[ProtoMember(3)]
		public DateTime TimeStarted;
	}
}