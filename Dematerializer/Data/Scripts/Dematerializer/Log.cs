using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using System.Linq;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;

namespace Dematerializer
{
	public class GridLog
	{
		private string Filename;
		private string time => "[" + DateTime.Now.ToShortTimeString() + "] -";

		private TextWriter Logger;

		public GridLog()
		{
			Filename = $"{DateTime.Today.Day} GridHistory.log";

			string previous = "";
			if (MyAPIGateway.Utilities.FileExistsInWorldStorage(Filename, typeof(DematerializerBlock)))
			{
				try
				{
					using (TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(Filename, typeof(DematerializerBlock)))
					{
						if (reader.ReadLine() == DateTime.Today.ToShortDateString())
							previous = reader.ReadToEnd();
					}
				}
				catch { previous = null; }
			}

			Logger = MyAPIGateway.Utilities.WriteFileInWorldStorage(Filename, typeof(DematerializerBlock));

			Log(DateTime.Today.ToShortDateString());
			if (!string.IsNullOrEmpty(previous))
				Log(previous);
			
		}

		public void CloseLog()
		{
			Logger.Flush();
			Logger.Close();
		}

		public void Log(string message)
		{
			Logger.WriteLine(message);
			Logger.Flush();
		}

		public void BeginGrinding(DematerializerBlock instance)
		{
			Logger.WriteLine($"Started   @ {time} | TargetGridOwnerId:{instance.SelectedGrid.BigOwners.FirstOrDefault()} TargetGridName:{instance.SelectedGrid.CustomName} TargetGridEntityId:{instance.SelectedGrid.EntityId} | DematOwner:{(instance.Entity as IMyShipGrinder).OwnerId} DematGridName:{(instance.Entity.GetTopMostParent() as IMyCubeGrid).CustomName}");
			Logger.Flush();
		}

		public void CancelGrinding(DematerializerBlock instance)
		{
			Logger.WriteLine($"Cancelled @ {time} | TargetGridOwnerId:{instance.SelectedGrid.BigOwners.FirstOrDefault()} TargetGridName:{instance.SelectedGrid.CustomName} TargetGridEntityId:{instance.SelectedGrid.EntityId} | DematOwner:{(instance.Entity as IMyShipGrinder).OwnerId} DematGridName:{(instance.Entity.GetTopMostParent() as IMyCubeGrid).CustomName}");
			Logger.Flush();
		}

		public void FinishGrinding(DematerializerBlock instance)
		{
			Logger.WriteLine($"Completed @ {time} | TargetGridOwnerId:{instance.SelectedGrid.BigOwners.FirstOrDefault()} TargetGridName:{instance.SelectedGrid.CustomName} TargetGridEntityId:{instance.SelectedGrid.EntityId} | DematOwner:{(instance.Entity as IMyShipGrinder).OwnerId} DematGridName:{(instance.Entity.GetTopMostParent() as IMyCubeGrid).CustomName}");
			
			string items = "";
			foreach (var item in instance.Items)
			{
				items += $"{item.PhysicalContent?.SubtypeName ?? item.SubtypeName}: {item.Amount:N2}, ";
			}
			Logger.WriteLine($"                      | Items to transfer: {items}");
			items = "";
			foreach (var item in instance.Blacklist)
			{
				items += $"{item}, ";
			}
			Logger.WriteLine($"                      | Items blacklisted: {items}");
			Logger.Flush();
		}
	}
}