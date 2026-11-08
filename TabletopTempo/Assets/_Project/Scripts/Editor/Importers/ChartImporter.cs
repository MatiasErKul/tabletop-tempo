using System;
using System.IO;
using TabletopTempo.Core.Rhythm;
using TabletopTempo.Rhythm;
using UnityEditor.AssetImporters;

namespace TabletopTempo.EditorTools.Importers
{
    /// <summary>
    /// Imports .ttchart (JSON from tools/music/ttmusic.py) as a <see cref="ChartAsset"/> and runs the
    /// chart validation, reporting problems as import warnings/errors in the Console.
    /// </summary>
    [ScriptedImporter(1, "ttchart")]
    public sealed class ChartImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            string json = File.ReadAllText(ctx.assetPath);
            var asset = ChartAsset.FromJson(json);
            asset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            try
            {
                var chart = Chart.FromJson(json);
                foreach (var w in chart.Validate()) ctx.LogImportWarning($"{asset.name}: {w}");
            }
            catch (Exception e)
            {
                ctx.LogImportError($"{asset.name}: invalid chart: {e.Message}");
            }
            ctx.AddObjectToAsset("chart", asset);
            ctx.SetMainObject(asset);
        }
    }
}
