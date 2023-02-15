using ProtoBuf;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.ModAPI;

namespace DeconstructorModSE
{
	[ProtoContract(UseProtoMembersOnly = true)]
	public class BlockSettings
	{
		[ProtoMember(1)]
		public long SelectedGrid;

		[ProtoMember(2)]
		public bool IsGrinding;

		[ProtoMember(3)]
		public TimeSpan Time;

		[ProtoMember(4)]
		public DateTime? TimeStarted;

		[ProtoMember(5)]
		public List<MyObjectBuilder_InventoryItem> Items = new List<MyObjectBuilder_InventoryItem>();

		[ProtoMember(6)]
		public string Error = "";
	}
}