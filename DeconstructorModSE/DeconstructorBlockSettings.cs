using ProtoBuf;
using System;
using System.Collections.Generic;
using VRage.Game;

namespace DeconstructorModSE
{
	[ProtoContract(UseProtoMembersOnly = true)]
	public class DeconstructorBlockSettings
	{
		[ProtoMember(1)]
		public float Efficiency;

		[ProtoMember(2)]
		public bool IsGrinding;

		[ProtoMember(3)]
		public TimeSpan Time;

		[ProtoMember(4)]
		public DateTime? TimeStarted;

		[ProtoMember(5)]
		public List<MyObjectBuilder_InventoryItem> Items = new List<MyObjectBuilder_InventoryItem>();

		[ProtoMember(6)]
		public List<string> HiddenGrids = new List<string>();
	}
}