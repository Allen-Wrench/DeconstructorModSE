using ProtoBuf;
using Sandbox.ModAPI;

namespace Dematerializer.Sync
{
	[ProtoInclude(1000, typeof(PacketClient))]
	[ProtoInclude(1001, typeof(PacketServer))]
	[ProtoInclude(1002, typeof(PacketSettings))]
	[ProtoContract(UseProtoMembersOnly = true)]
	public abstract class PacketBase
	{
		[ProtoMember(1)]
		public readonly ulong SenderId;

		protected Networking Networking => DematerializerSession.Instance.Net;

		public PacketBase()
		{
			SenderId = MyAPIGateway.Multiplayer.MyId;
		}

		public abstract void Received(ref bool relay);
	}
}