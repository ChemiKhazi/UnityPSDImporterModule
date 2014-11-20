using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PhotoshopFile;
using UnityEditor;
using UnityEngine;

namespace kontrabida.psdexport
{
	/// <summary>
	/// Defines settings for PSDExporter to use
	/// </summary>
	public class PsdExportSettings
	{
		/// <summary>
		/// Defines export settings for each layer
		/// </summary>
		public class LayerSetting
		{
			public int layerIndex;
			public bool doExport;
			public PSDExporter.ScaleDown scaleBy;
			public SpriteAlignment pivot;
		}

		/// <summary>
		/// The list of layer export settings
		/// </summary>
		public Dictionary<int, LayerSetting> layerSettings;

		/// <summary>
		/// The PsdFile type for the image this setting references
		/// </summary>
		public PsdFile Psd { get; protected set; }
		/// <summary>
		/// Filename of the PSD
		/// </summary>
		public string Filename { get; protected set; }
		/// <summary>
		/// Unity Texture2D reference to the PSD
		/// </summary>
		public Texture2D Image { get; protected set; }

		/// <summary>
		/// Pixels to Unit Size when exported to Unity sprites
		/// </summary>
		public float PixelsToUnitSize { get; set; }
		/// <summary>
		/// The packing tag to assign to the exported sprites
		/// </summary>
		public string PackingTag { get; set; }
		/// <summary>
		/// The scale of the PSD file relative to exported sprites
		/// </summary>
		public int ScaleBy { get; set; }
		/// <summary>
		/// The default pivot point for the Unity sprites
		/// </summary>
		public Vector2 PivotVector { get; set; }

		private SpriteAlignment _pivot;
		public SpriteAlignment Pivot
		{
			get { return _pivot; }
			set
			{
				_pivot = value;
				if (_pivot == SpriteAlignment.Custom)
					return;

				Vector2 pivotCustom = new Vector2(0.5f, 0.5f);
				if (_pivot == SpriteAlignment.TopCenter ||
				    _pivot == SpriteAlignment.TopLeft ||
				    _pivot == SpriteAlignment.TopRight)
				{
					pivotCustom.y = 1;
				}
				if (_pivot == SpriteAlignment.BottomCenter ||
				    _pivot == SpriteAlignment.BottomLeft ||
				    _pivot == SpriteAlignment.BottomRight)
				{
					pivotCustom.y = 0f;
				}

				if (_pivot == SpriteAlignment.LeftCenter ||
				    _pivot == SpriteAlignment.TopLeft ||
				    _pivot == SpriteAlignment.BottomLeft)
				{
					pivotCustom.x = 0f;
				}
				if (_pivot == SpriteAlignment.RightCenter ||
				    _pivot == SpriteAlignment.TopRight ||
				    _pivot == SpriteAlignment.BottomRight)
				{
					pivotCustom.x = 1f;
				}
				PivotVector = pivotCustom;
			}
		}

		public PsdExportSettings(Texture2D image)
		{
			string path = AssetDatabase.GetAssetPath(image);
			if (!path.ToUpper().EndsWith(".PSD"))
				return;

			Psd = new PsdFile(path, Encoding.Default);
			Filename = Path.GetFileNameWithoutExtension(path);
			Image = image;

			ScaleBy = 0;
			Pivot = SpriteAlignment.Center;
			PixelsToUnitSize = 100f;

			LoadMetaData();
		}

		private void LoadMetaData()
		{
			string[] pivotNameStrings = Enum.GetNames(typeof(SpriteAlignment));
			Array pivotNameVals = Enum.GetValues(typeof(SpriteAlignment));

			string[] labels = AssetDatabase.GetLabels(Image);
			foreach (var label in labels)
			{
				if (label.Equals("ImportX1"))
					ScaleBy = 0;
				if (label.Equals("ImportX2"))
					ScaleBy = 1;
				if (label.Equals("ImportX4"))
					ScaleBy = 2;

				if (label.StartsWith("ImportAnchor"))
				{
					string pivotType = label.Substring(12);
					if (pivotType.StartsWith("Custom"))
					{
						// Get the coordinates value inside the string "[]"
						string values = pivotType.Substring(pivotType.IndexOf("["),
															pivotType.IndexOf("]"));
						string[] vals = values.Split(',');
						PivotVector = new Vector2(float.Parse(vals[0]), float.Parse(vals[1]));
						Pivot = SpriteAlignment.Custom;
					}
					else
					{
						// Find by enum
						for (int i = 0; i < pivotNameStrings.Length; i++)
						{
							if (pivotType == pivotNameStrings[i])
								Pivot = (SpriteAlignment)pivotNameVals.GetValue(i);
						}
					}
				} // End import anchor if

				if (label.StartsWith("ImportPTU|"))
				{
					string ptuVal = label.Substring(10);
					PixelsToUnitSize = Single.Parse(ptuVal);
				}

				if (label.StartsWith("ImportPackTag|"))
				{
					string packTag = label.Substring(14);
					PackingTag = packTag;
				}
			} // End label loop
		}

		public void SaveMetaData()
		{
			string[] labels = new string[4];

			if (ScaleBy == 0)
				labels[0] = "ImportX1";
			if (ScaleBy == 1)
				labels[0] = "ImportX2";
			if (ScaleBy == 2)
				labels[0] = "ImportX4";

			labels[1] = "ImportAnchor" + Pivot.ToString();
			if (Pivot == SpriteAlignment.Custom)
			{
				labels[1] = "ImportAnchorCustom[" + PivotVector.x + "," + PivotVector.y + "]";
			}

			labels[2] = "ImportPTU|" + PixelsToUnitSize;
			labels[3] = "ImportPackTag|" + PackingTag;
			AssetDatabase.SetLabels(Image, labels);
		}

		/// <summary>
		/// Setup the layer settings using a PsdFileInfo,
		/// tries to read settings from file labels if available
		/// </summary>
		/// <param name="fileInfo"></param>
		public void LoadLayers(PsdFileInfo fileInfo)
		{
			layerSettings = new Dictionary<int, LayerSetting>();
			foreach (var layerIndex in fileInfo.LayerIndices)
			{
				string layerName = Psd.Layers[layerIndex].Name;
				bool visible = fileInfo.LayerVisibility[layerIndex];
				LoadLayerSetting(layerName, layerIndex, visible);
			}
		}

		private void LoadLayerSetting(string layerName, int layerIndex, bool visible)
		{
			LayerSetting setting = new LayerSetting
			{
				doExport = visible,
				layerIndex = layerIndex,
				pivot = Pivot,
				scaleBy = PSDExporter.ScaleDown.Default
			};

			string layerPath = GetLayerPath(layerName);
			Sprite layerSprite = (Sprite)AssetDatabase.LoadAssetAtPath(layerPath, typeof(Sprite));
			if (layerSprite != null)
			{
				string[] pivotNameStrings = Enum.GetNames(typeof(SpriteAlignment));
				Array pivotNameVals = Enum.GetValues(typeof(SpriteAlignment));

				string[] labels = AssetDatabase.GetLabels(layerSprite);
				foreach (var label in labels)
				{
					if (label.Equals("ImportX1"))
						setting.scaleBy = PSDExporter.ScaleDown.Default;
					if (label.Equals("ImportX2"))
						setting.scaleBy = PSDExporter.ScaleDown.Half;
					if (label.Equals("ImportX4"))
						setting.scaleBy = PSDExporter.ScaleDown.Quarter;

					if (label.StartsWith("ImportAnchor"))
					{
						string pivotType = label.Substring(12);
						// Find by enum
						for (int i = 0; i < pivotNameStrings.Length; i++)
						{
							if (pivotType == pivotNameStrings[i])
								setting.pivot = (SpriteAlignment)pivotNameVals.GetValue(i);
						}
					} // End import anchor if
				} // End label loop
			} // End sprite label loading

			layerSettings.Add(layerIndex, setting);
		}

		public void SaveLayerMetaData()
		{
			foreach (var keypair in layerSettings)
			{
				SaveLayerSetting(keypair.Value);
			}
		}

		private void SaveLayerSetting(LayerSetting setting)
		{
			// Not exporting, don't save layer settings
			if (!setting.doExport)
				return;

			// Get the asset
			var layer = Psd.Layers[setting.layerIndex];
			string layerPath = GetLayerPath(layer.Name);
			var layerSprite = AssetDatabase.LoadAssetAtPath(layerPath, typeof(Sprite));

			if (layerSprite == null)
				return;

			// Write out the labels for the layer settings
			string[] labels = new string[2];

			if (setting.scaleBy == PSDExporter.ScaleDown.Default)
				labels[0] = "ImportX1";
			if (setting.scaleBy == PSDExporter.ScaleDown.Half)
				labels[0] = "ImportX2";
			if (setting.scaleBy == PSDExporter.ScaleDown.Quarter)
				labels[0] = "ImportX4";

			labels[1] = "ImportAnchor" + setting.pivot;

			AssetDatabase.SetLabels(layerSprite, labels);
		}

		public string GetLayerPath(string layerName)
		{
			string assetPath = AssetDatabase.GetAssetPath(Image);
			string directoryPath = Path.GetDirectoryName(assetPath);
			string layerFile = Path.GetFileNameWithoutExtension(assetPath);
			layerFile += "_" + layerName + ".png";
			string layerPath = Path.Combine(directoryPath, layerFile);
			return layerPath;
		}
	}
}