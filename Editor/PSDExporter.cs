using System.Collections.Generic;
using System.IO;
using System.Linq;
using PhotoshopFile;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace kontrabida.psdexport
{
	/// <summary>
	/// Stores preparsed information about the layers in a PSD file
	/// </summary>
	public class PsdFileInfo
	{
		public class InstancedLayerInfo
		{
			public int instanceLayer;
			public List<int> duplicateLayers;
		}

		public PSDLayerGroupInfo[] LayerGroups { get; protected set; }

		/// <summary>
		/// Layer visibility data, indexed by layer
		/// </summary>
		public bool[] LayerVisibility { get; protected set; }
		/// <summary>
		/// A list of layer index that are regular PS layers
		/// </summary>
		public int[] LayerIndices { get; protected set; }

		public PsdFileInfo(PsdFile psd)
		{
			List<int> layerIndices = new List<int>();
			List<PSDLayerGroupInfo> layerGroups = new List<PSDLayerGroupInfo>();
			List<PSDLayerGroupInfo> openGroupStack = new List<PSDLayerGroupInfo>();
			List<bool> layerVisibility = new List<bool>();
			// Reverse loop through layers to get the layers in the
			// same way they are displayed in Photoshop
			for (int i = psd.Layers.Count - 1; i >= 0; i--)
			{
				Layer layer = psd.Layers[i];

				layerVisibility.Add(layer.Visible);

				// Get the section info for this layer
				var secInfo = layer.AdditionalInfo
									.Where(info => info.GetType() == typeof(LayerSectionInfo))
									.ToArray();
				// Section info is basically layer group info
				bool isOpen = false;
				bool isGroup = false;
				bool closeGroup = false;
				if (secInfo.Any())
				{
					foreach (var layerSecInfo in secInfo)
					{
						LayerSectionInfo info = (LayerSectionInfo)layerSecInfo;
						isOpen = info.SectionType == LayerSectionType.OpenFolder;
						isGroup = info.SectionType == LayerSectionType.ClosedFolder | isOpen;
						closeGroup = info.SectionType == LayerSectionType.SectionDivider;
						if (isGroup || closeGroup)
							break;
					}
				}

				if (isGroup)
				{
					// Open a new layer group info, because we're iterating
					// through layers backwards, this layer number is the last logical layer
					openGroupStack.Add(new PSDLayerGroupInfo(layer.Name, i, layer.Visible, isOpen));
				}
				else if (closeGroup)
				{
					// Set the start index of the last LayerGroupInfo
					var closeInfo = openGroupStack.Last();
					closeInfo.start = i;
					// Add it to the layerGroup list
					layerGroups.Add(closeInfo);
					// And remove it from the open group stack 
					openGroupStack.RemoveAt(openGroupStack.Count - 1);
				}
				else
				{
					// Normal layer
					layerIndices.Add(i);
					// look for instances	
					if (layer.Name.Contains(" Copy"))
					{
					}
				}
			} // End layer loop

			// Since loop was reversed...
			layerVisibility.Reverse();
			LayerVisibility = layerVisibility.ToArray();

			LayerIndices = layerIndices.ToArray();

			LayerGroups = layerGroups.ToArray();
		}

		public InstancedLayerInfo GetInstancedLayer(int layerindex)
		{
			return null;
		}

		public PSDLayerGroupInfo GetGroupByLayerIndex(int layerIndex)
		{
			List<PSDLayerGroupInfo> candidates = new List<PSDLayerGroupInfo>();
			// Might be a nested layer group
			foreach (var layerGroupInfo in LayerGroups)
			{
				if (layerGroupInfo.ContainsLayer(layerIndex))
					candidates.Add(layerGroupInfo);
			}
			return candidates.OrderBy(info => info.end - info.start).FirstOrDefault();
		}

		public PSDLayerGroupInfo GetGroupByStartIndex(int startIndex)
		{
			return LayerGroups.FirstOrDefault(info => info.end == startIndex);
		}
	}

	/// <summary>
	/// Data on PSD layer groups
	/// </summary>
	public class PSDLayerGroupInfo
	{
		/// <summary>
		/// Layer group name
		/// </summary>
		public string name;
		/// <summary>
		/// The last layer number contained in this layer group
		/// </summary>
		public int end;
		/// <summary>
		/// The first layer number contained by this layer group
		/// </summary>
		public int start;
		/// <summary>
		/// If this layer group is visible
		/// </summary>
		public bool visible;
		/// <summary>
		/// If this layer group is expanded
		/// </summary>
		public bool opened;

		public PSDLayerGroupInfo(string name, int end, bool visible, bool opened)
		{
			this.name = name;
			this.end = end;
			this.visible = visible;
			this.opened = opened;

			start = -1;
		}

		public bool ContainsLayer(int layerIndex)
		{
			return (layerIndex <= end) && (layerIndex >= start);
		}
	}

	public class PSDExporter
	{
		public enum PivotPos
		{
			Center,
			TopLeft,
			Top,
			TopRight,
			Left,
			Right,
			BottomLeft,
			Bottom,
			BottomRight,
			Custom
		}

		public enum ScaleDown
		{
			Default,
			Half,
			Quarter
		}

		public static void Export(PsdExportSettings settings, PsdFileInfo fileInfo)
		{
			foreach (var keypair in settings.layerSettings)
			{
				PsdExportSettings.LayerSetting layerSetting = keypair.Value;
				// Don't export if not set to export or group is off
				if (!layerSetting.doExport)
					continue;
				var groupInfo = fileInfo.GetGroupByLayerIndex(layerSetting.layerIndex);
				if (groupInfo != null && !groupInfo.visible)
					continue;

				CreateSprite(settings, layerSetting.layerIndex);
			}
			settings.SaveMetaData();
			settings.SaveLayerMetaData();
		}

		public static Sprite CreateSprite(PsdExportSettings settings, int layerIndex)
		{
			var layer = settings.Psd.Layers[layerIndex];
			Texture2D tex = CreateTexture(layer);
			if (tex == null)
				return null;
			Sprite sprite = SaveAsset(settings, tex, layerIndex);
			Object.DestroyImmediate(tex);
			return sprite;
		}

		private static Texture2D CreateTexture(Layer layer)
		{
			if ((int)layer.Rect.width == 0 || (int)layer.Rect.height == 0)
				return null;

			// For possible clip to document functionality
			//int fileWidth = psd.ColumnCount;
			//int fileHeight = psd.RowCount;

			//int textureWidth = (int) layer.Rect.width;
			//int textureHeight = (int) layer.Rect.height;

			Texture2D tex = new Texture2D((int)layer.Rect.width, (int)layer.Rect.height, TextureFormat.RGBA32, true);
			Color32[] pixels = new Color32[tex.width * tex.height];

			Channel red = (from l in layer.Channels where l.ID == 0 select l).First();
			Channel green = (from l in layer.Channels where l.ID == 1 select l).First();
			Channel blue = (from l in layer.Channels where l.ID == 2 select l).First();
			Channel alpha = layer.AlphaChannel;

			for (int i = 0; i < pixels.Length; i++)
			{
				byte r = red.ImageData[i];
				byte g = green.ImageData[i];
				byte b = blue.ImageData[i];
				byte a = 255;

				if (alpha != null)
					a = alpha.ImageData[i];

				int mod = i % tex.width;
				int n = ((tex.width - mod - 1) + i) - mod;
				pixels[pixels.Length - n - 1] = new Color32(r, g, b, a);
			}

			tex.SetPixels32(pixels);
			tex.Apply();

			return tex;
		}

		private static Sprite SaveAsset(PsdExportSettings settings, Texture2D tex, int layer)
		{
			PsdExportSettings.LayerSetting layerSetting = settings.layerSettings[layer];
			string layerName = settings.Psd.Layers[layer].Name;
			string path = settings.GetLayerPath(layerName);

			// Setup scaling variables
			float pixelsToUnits = settings.PixelsToUnitSize;

			// Global settings scaling
			if (settings.ScaleBy > 0)
			{
				tex = ScaleTextureByMipmap(tex, settings.ScaleBy);
			}

			// Then scale by layer scale
			if (layerSetting.scaleBy != ScaleDown.Default)
			{
				int scaleLevel = 1;
				pixelsToUnits = Mathf.RoundToInt(settings.PixelsToUnitSize/2f);
				if (layerSetting.scaleBy == ScaleDown.Quarter)
				{
					scaleLevel = 2;
					pixelsToUnits = Mathf.RoundToInt(settings.PixelsToUnitSize/4f);
				}
				tex = ScaleTextureByMipmap(tex, scaleLevel);
			}

			byte[] buf = tex.EncodeToPNG();
			File.WriteAllBytes(path, buf);
			AssetDatabase.Refresh();

			// Load the texture so we can change the type
			var textureObj = AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D));

			// Get the texture importer for the asset
			TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(path);
			// Read out the texture import settings so import pivot point can be changed
			TextureImporterSettings importSetting = new TextureImporterSettings();
			textureImporter.ReadTextureSettings(importSetting);

			// Set the pivot import setting
			importSetting.spriteAlignment = (int) settings.Pivot;
			// But if layer setting has a different pivot, set as new pivot
			if (settings.Pivot != layerSetting.pivot)
				importSetting.spriteAlignment = (int)layerSetting.pivot;
			// Pivot settings are the same but custom, set the vector
			else if (settings.Pivot == SpriteAlignment.Custom)
				importSetting.spritePivot = settings.PivotVector;

			importSetting.spritePixelsToUnits = pixelsToUnits;
			// Set the rest of the texture settings
			textureImporter.textureType = TextureImporterType.Sprite;
			textureImporter.spriteImportMode = SpriteImportMode.Single;
			textureImporter.spritePackingTag = settings.PackingTag;
			// Write in the texture import settings
			textureImporter.SetTextureSettings(importSetting);

			EditorUtility.SetDirty(textureObj);
			AssetDatabase.WriteImportSettingsIfDirty(path);
			AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

			return (Sprite)AssetDatabase.LoadAssetAtPath(path, typeof(Sprite));
		}

		private static Texture2D ScaleTextureByMipmap(Texture2D tex, int mipLevel)
		{
			if (mipLevel < 0 || mipLevel > 2)
				return null;
			int width = Mathf.RoundToInt(tex.width / (mipLevel * 2));
			int height = Mathf.RoundToInt(tex.height / (mipLevel * 2));

			// Scaling down by abusing mip maps
			Texture2D resized = new Texture2D(width, height);
			resized.SetPixels32(tex.GetPixels32(mipLevel));
			resized.Apply();
			return resized;
		}
	}
}
