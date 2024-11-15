using System.Collections.Generic;
using System.Linq;
using Lumina.Excel.Sheets;

namespace WondrousTailsSolver;

/// <summary>
/// Helper class for converting Task ID's into Zone Lists.
/// </summary>
internal static class TaskLookup {
	public static List<uint> GetInstanceListFromId(uint orderDataId) {
		var bingoOrderData = Service.DataManager.GetExcelSheet<WeeklyBingoOrderData>().GetRow(orderDataId);
        
		switch (bingoOrderData.Type) {
			// Specific Duty
			case 0:
				return Service.DataManager.GetExcelSheet<ContentFinderCondition>()
					.Where(c => c.Content.RowId == bingoOrderData.Data.RowId)
					.OrderBy(row => row.SortKey)
					.Select(c => c.TerritoryType.RowId)
					.ToList();
            
			// Specific Level Dungeon
			case 1:
				return Service.DataManager.GetExcelSheet<ContentFinderCondition>()
					.Where(m => m.ContentType.RowId is 2)
					.Where(m => m.ClassJobLevelRequired == bingoOrderData.Data.RowId)
					.OrderBy(row => row.SortKey)
					.Select(m => m.TerritoryType.RowId)
					.ToList();
            
			// Level Range Dungeon
			case 2:
				return Service.DataManager.GetExcelSheet<ContentFinderCondition>()
					.Where(m => m.ContentType.RowId is 2)
					.Where(m => m.ClassJobLevelRequired >= bingoOrderData.Data.RowId - (bingoOrderData.Data.RowId > 50 ? 9 : 49) && m.ClassJobLevelRequired <= bingoOrderData.Data.RowId - 1)
					.OrderBy(row => row.SortKey)
					.Select(m => m.TerritoryType.RowId)
					.ToList();
            
			// Special categories
			case 3:
				return bingoOrderData.Unknown1 switch
				{
					// Treasure Map Instances are Not Supported
					1 => [],
                    
					// PvP Categories are Not Supported
					2 => [],
                    
					// Deep Dungeons
					3 => Service.DataManager.GetExcelSheet<ContentFinderCondition>()
						.Where(m => m.ContentType.RowId is 21)
						.OrderBy(row => row.SortKey)
						.Select(m => m.TerritoryType.RowId)
						.ToList(),
                    
					_ => [],
				};
            
			// Multi-instance raids
			case 4:
				return bingoOrderData.Data.RowId switch {
					// Binding Coil, Second Coil, Final Coil
					2 => [ 241, 242, 243, 244, 245 ],
					3 => [ 355, 356, 357, 358 ],
					4 => [ 193, 194, 195, 196 ],
                    
					// Gordias, Midas, The Creator
					5 => [ 442, 443, 444, 445 ],
					6 => [ 520, 521, 522, 523 ],
					7 => [ 580, 581, 582, 583 ],
                    
					// Deltascape, Sigmascape, Alphascape
					8 => [ 691, 692, 693, 694 ],
					9 => [ 748, 749, 750, 751 ],
					10 => [ 798, 799, 800, 801 ],

					// Eden's Gate: Resurrection or Descent
					11 => [ 849, 850 ],
					// Eden's Gate: Inundation or Sepulture
					12 => [ 851, 852 ],
					// Eden's Verse: Fulmination or Furor
					13 => [ 902, 903 ],
					// Eden's Verse: Iconoclasm or Refulgence
					14 => [ 904, 905 ],
					// Eden's Promise: Umbra or Litany
					15 => [ 942, 943 ],
					// Eden's Promise: Anamorphosis or Eternity
					16 => [ 944, 945 ],

					// Asphodelos: First or Second Circles
					17 => [ 1002, 1004 ],
					// Asphodelos: Third or Fourth Circles
					18 => [ 1006, 1008 ],
					// Abyssos: Fifth or Sixth Circles
					19 => [ 1081, 1083 ],
					// Abyssos: Seventh or Eight Circles
					20 => [ 1085, 1087 ],
					// Anabaseios: Ninth or Tenth Circles
					21 => [ 1147, 1149 ],
					// Anabaseios: Eleventh or Twelwth Circles
					22 => [ 1151, 1153 ],

					// Eden's Gate
					23 => [ 849, 850, 851, 852 ],
					// Eden's Verse
					24 => [ 902, 903, 904, 905 ],
					// Eden's Promise
					25 => [ 942, 943, 944, 945 ],

					// Alliance Raids (A Realm Reborn)
					26 => [ 174, 372, 151 ],
					// Alliance Raids (Heavensward)
					27 => [ 508, 556, 627 ],
					// Alliance Raids (Stormblood)
					28 => [ 734, 776, 826 ],
					// Alliance Raids (Shadowbringers)
					29 => [ 882, 917, 966 ],
					// Alliance Raids (Endwalker)
					30 => [ 1054, 1118, 1178 ],

					_ => [],
				};
		}
        
		Service.PluginLog.Information($"[WondrousTails] Unrecognized ID: {orderDataId}");
		return [];
	}
}
