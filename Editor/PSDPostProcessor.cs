using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace kontrabida.psdexport
{
	public class PSDPostProcessor : AssetPostprocessor
	{
		static void OnPostprocessAllAssets(string[] modified,
											string[] deleted,
											string[] moved,
											string[] movedFrom)
		{
			foreach (var modPath in modified)
			{
				if (modPath.ToLower().EndsWith(".psd") == false)
					continue;
				var target = AssetDatabase.LoadAssetAtPath<Texture2D>(modPath);
				var exportSettings = new PsdExportSettings(target);
				if (exportSettings.AutoReExport)
				{
					PsdFileInfo psdInfo = new PsdFileInfo(exportSettings.Psd);
					exportSettings.LoadLayers(psdInfo);
					PSDExporter.Export(exportSettings, psdInfo);
				}
			}
		}
	}
}
