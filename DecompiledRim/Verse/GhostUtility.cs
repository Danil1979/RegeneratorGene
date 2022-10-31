using System.Collections.Generic;
using RimWorld;
using UnityEngine;

namespace Verse
{
	public static class GhostUtility
	{
		private static Dictionary<int, Graphic> ghostGraphics = new Dictionary<int, Graphic>();

		public static Graphic GhostGraphicFor(Graphic baseGraphic, ThingDef thingDef, Color ghostCol, ThingDef stuff = null)
		{
			if (thingDef.useSameGraphicForGhost)
			{
				return baseGraphic;
			}
			int seed = 0;
			seed = Gen.HashCombine(seed, baseGraphic);
			seed = Gen.HashCombine(seed, thingDef);
			seed = Gen.HashCombineStruct(seed, ghostCol);
			seed = Gen.HashCombine(seed, stuff);
			if (!ghostGraphics.TryGetValue(seed, out var value))
			{
				if (thingDef.graphicData.Linked || thingDef.IsDoor)
				{
					value = GraphicDatabase.Get<Graphic_Single>(thingDef.uiIconPath, ShaderTypeDefOf.EdgeDetect.Shader, thingDef.graphicData.drawSize, ghostCol);
				}
				else
				{
					if (baseGraphic == null)
					{
						baseGraphic = thingDef.graphic;
					}
					GraphicData graphicData = null;
					if (baseGraphic.data != null)
					{
						graphicData = new GraphicData();
						graphicData.CopyFrom(baseGraphic.data);
						graphicData.shadowData = null;
					}
					string path = baseGraphic.path;
					Graphic_Appearances graphic_Appearances;
					value = (((graphic_Appearances = baseGraphic as Graphic_Appearances) == null || stuff == null) ? GraphicDatabase.Get(baseGraphic.GetType(), path, ShaderTypeDefOf.EdgeDetect.Shader, baseGraphic.drawSize, ghostCol, Color.white, graphicData, null) : GraphicDatabase.Get<Graphic_Single>(graphic_Appearances.SubGraphicFor(stuff).path, ShaderTypeDefOf.EdgeDetect.Shader, thingDef.graphicData.drawSize, ghostCol, Color.white, graphicData));
				}
				ghostGraphics.Add(seed, value);
			}
			return value;
		}
	}
}
