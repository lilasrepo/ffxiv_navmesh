using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Navmesh;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct ExdZoneSharedGroup
{
	uint LGBSharedGroup;
	uint RequirementRow0;
	uint RequirementRow1;
	uint RequirementRow2;
	uint RequirementRow3;
	uint RequirementRow4;
	uint RequirementRow5;
	uint Unknown0;
	uint RequirementQuestSequence0;
	uint RequirementQuestSequence1;
	uint RequirementQuestSequence2;
	uint RequirementQuestSequence3;
	uint RequirementQuestSequence4;
	uint RequirementQuestSequence5;
	uint Unknown1;
	byte RequirementType0;
	byte RequirementType1;
	byte RequirementType2;
	byte RequirementType3;
	byte RequirementType4;
	byte RequirementType5;
	byte Unknown8;
	byte Unknown9;
	byte Unknown10;
	byte Unknown11;
	byte Unknown12;
	byte Unknown13;
	byte Unknown14;
	byte Unknown15;

    // porting-note: API12 Lumina ZoneSharedGroup fields are flat-named (no array form).
    //   HEAD RequirementRow[i]          -> API12 Quest{i}     (RowRef<T>)
    //   HEAD RequirementQuestSequence[i] -> API12 Seq{i}       (uint)
    //   HEAD RequirementType[i]         -> API12 Unknown{i+2} (byte)
    public static implicit operator ExdZoneSharedGroup(Lumina.Excel.Sheets.ZoneSharedGroup sg) => new()
    {
        LGBSharedGroup = sg.LGBSharedGroup,
        RequirementRow0 = sg.Quest0.RowId,
        RequirementRow1 = sg.Quest1.RowId,
        RequirementRow2 = sg.Quest2.RowId,
        RequirementRow3 = sg.Quest3.RowId,
        RequirementRow4 = sg.Quest4.RowId,
        RequirementRow5 = sg.Quest5.RowId,
        Unknown0 = sg.Unknown0,
        RequirementQuestSequence0 = sg.Seq0,
        RequirementQuestSequence1 = sg.Seq1,
        RequirementQuestSequence2 = sg.Seq2,
        RequirementQuestSequence3 = sg.Seq3,
        RequirementQuestSequence4 = sg.Seq4,
        RequirementQuestSequence5 = sg.Seq5,
        Unknown1 = sg.Unknown1,
        RequirementType0 = sg.Unknown2,
        RequirementType1 = sg.Unknown3,
        RequirementType2 = sg.Unknown4,
        RequirementType3 = sg.Unknown5,
        RequirementType4 = sg.Unknown6,
        RequirementType5 = sg.Unknown7,
        Unknown8 = sg.Unknown8,
        Unknown9 = sg.Unknown9 ? (byte)1 : (byte)0,
        Unknown10 = sg.Unknown10 ? (byte)1 : (byte)0,
        Unknown11 = sg.Unknown11 ? (byte)1 : (byte)0,
        Unknown12 = sg.Unknown12 ? (byte)1 : (byte)0,
        Unknown13 = sg.Unknown13 ? (byte)1 : (byte)0,
        Unknown14 = sg.Unknown14 ? (byte)1 : (byte)0,
        Unknown15 = sg.Unknown15 ? (byte)1 : (byte)0,
    };
}

public unsafe static class LayoutUtils
{
    // porting-note: HEAD sig "E8 ?? ?? ?? ?? 0F B6 53 6C" targets game 7.5; not resolved on TC 7.1.
    // Stubbed to null; GetZoneSharedGroupsEnabled returns empty (zone-shared-group navmesh tweaks disabled).
    private static delegate* unmanaged<ExdZoneSharedGroup*, uint> _getEnabledRequirementIndex;

    static LayoutUtils()
    {
        try
        {
            _getEnabledRequirementIndex = (delegate* unmanaged<ExdZoneSharedGroup*, uint>)Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 0F B6 53 6C");
        }
        catch
        {
            _getEnabledRequirementIndex = null;
            Service.Log.Warning("LayoutUtils _getEnabledRequirementIndex sig not found (game-7.5 sig vs TC 7.1).");
        }
    }

    public static uint[] GetZoneSharedGroupsEnabled(uint territoryType)
    {
        // porting-note: API12 Lumina TerritoryType lacks the ZoneSharedGroup column (HEAD/game-7.5 added).
        // Without the lumina backing, this function is structurally inert; return empty to caller.
        return [];
    }

	public static string ReadString(byte* data) => data != null ? MemoryHelper.ReadStringNullTerminated((nint)data) : "";
	public static string ReadString(RefCountedString* data) => data != null ? data->DataString : "";

	public static V* FindPtr<K, V>(ref this StdMap<K, Pointer<V>> map, K key) where K : unmanaged, IComparable where V : unmanaged
	{
		return map.TryGetValuePointer(key, out var ptr) && ptr != null ? ptr->Value : null;
	}

	public static ILayoutInstance* FindInstance(LayoutManager* layout, ulong key)
	{
		foreach (var (ikt, ikv) in layout->InstancesByType)
		{
			var iter = ikv.Value->FindPtr(key);
			if (iter != null)
				return iter;
		}
		return null;
	}

	public static LayoutManager.Filter* FindFilter(LayoutManager* layout)
	{
		if (layout->CfcId != 0) // note: some code paths check cfcid match only if TerritoryTypeId != 0; don't think it actually matters
			foreach (var (k, v) in layout->Filters)
				if (v.Value->CfcId == layout->CfcId)
					return v.Value;
		if (layout->TerritoryTypeId != 0)
			foreach (var (k, v) in layout->Filters)
				if (v.Value->TerritoryTypeId == layout->TerritoryTypeId)
					return v.Value;
		return layout->TerritoryTypeId == 0 ? FindPtr(ref layout->Filters, layout->LayerFilterKey) : null;
	}

	public static bool LayerActiveFestival(FileLayerGroupLayer* layer, Span<GameMain.Festival> festivals)
	{
		if (layer->Festival.Id == 0)
			return true; // non-festival, always active

		if (layer->Festival.Phase != 0)
		{
			foreach (var f in festivals)
				if (f.Id == layer->Festival.Id && f.Phase == layer->Festival.Phase)
					return true;
			return false;
		}
		else
		{
			foreach (var f in festivals)
				if (f.Id == layer->Festival.Id)
					return true;
			return false;
		}
	}

	public static bool LayerActiveFilter(FileLayerGroupLayer* layer, uint filterId)
	{
		var filter = layer->Filter;
		return filter == null || filter->Operation switch
		{
			FileLayerGroupLayerFilter.Op.Match => filter->Entries.Contains(filterId),
			FileLayerGroupLayerFilter.Op.NoMatch => !filter->Entries.Contains(filterId),
			_ => true
		};
	}

	public static string FestivalString(GameMain.Festival f) => $"{(uint)(f.Phase << 16) | f.Id:X}";
	public static string FestivalsString(ReadOnlySpan<GameMain.Festival> f) => $"{FestivalString(f[0])}.{FestivalString(f[1])}.{FestivalString(f[2])}.{FestivalString(f[3])}";

	public static string Vec3ToSource(Vector3 v) => $"new Vector3({FloatLiteral(v.X)}, {FloatLiteral(v.Y)}, {FloatLiteral(v.Z)})";

	static string FloatLiteral(float f)
	{
		static bool almostEqual(float f1, float f2) => MathF.Abs(f2 - f1) < 0.1f;

		if (MathF.Abs(f) < 0.001f)
			return "0";

		if (MathF.Abs(f - MathF.Round(f)) < 0.001f)
			return MathF.Round(f).ToString();

		var abs = MathF.Abs(f);

		if (almostEqual(abs, MathF.PI))
			return f < 0 ? "-pi" : "pi";

		if (almostEqual(abs, MathF.PI * 0.5f))
			return f < 0 ? "-hpi" : "hpi";

		return f.ToString("0.###f");
	}
}
