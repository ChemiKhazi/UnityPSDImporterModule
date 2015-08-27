/*
The MIT License (MIT)

Copyright (c) 2013 Banbury

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.Reflection;
using PhotoshopFile;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using Object = UnityEngine.Object;

namespace subjectnerdagreement.psdexport
{
	public class PSDEditorWindow : EditorWindow
	{
		#region Static/Menus
		private static PSDEditorWindow GetPSDEditor()
		{
			var wnd = GetWindow<PSDEditorWindow>();
			wnd.title = "PSD Importer";
			wnd.Show();
			return wnd;
		}

		[MenuItem("Sprites/PSD Import")]
		public static void ShowWindow()
		{
			GetPSDEditor();
		}

		[MenuItem("Assets/PSD Importer")]
		static void ImportPsdWindow()
		{
			var wnd = GetPSDEditor();
			wnd.Image = (Texture2D)Selection.objects[0];
			EditorUtility.SetDirty(wnd);
		}

		[MenuItem("Assets/PSD Importer", true)]
		static bool ImportPsd()
		{
			Object[] arr = Selection.objects;

			if (arr.Length != 1)
				return false;

			string assetPath = AssetDatabase.GetAssetPath(arr[0]);
			return assetPath.ToUpper().EndsWith(".PSD");
		}
		#endregion

		private static string[] _sortingLayerNames;

		private PsdExportSettings settings;
		private PsdFileInfo fileInfo;

		private Texture2D image;
		private int importCount;

		private bool imageLoaded;

		public Texture2D Image
		{
			get { return image; }
			set
			{
				image = value;
				LoadImage();
			}
		}

		void OnEnable()
		{
			SetupSortingLayerNames();
			if (image != null)
				LoadImage();
		}

		void SetupSortingLayerNames()
		{
			if (_sortingLayerNames == null)
			{
				Type internalEditorUtilityType = typeof(InternalEditorUtility);
				PropertyInfo sortingLayersProperty = internalEditorUtilityType.GetProperty("sortingLayerNames", BindingFlags.Static | BindingFlags.NonPublic);
				_sortingLayerNames = (string[])sortingLayersProperty.GetValue(null, new object[0]);
			}
		}

		private bool LoadImage()
		{
			settings = new PsdExportSettings(image);
			showImportSettings = !settings.HasMetaData;

			bool valid = (settings.Psd != null);
			if (valid)
			{
				// Parse the layer info
				fileInfo = new PsdFileInfo(settings.Psd);
				settings.LoadLayers(fileInfo);
			}
			return valid;
		}

		#region GUI Styles
		private GUIStyle styleHeader, styleLabelLeft, styleBoldFoldout, styleLayerRow, styleCreateBox;
		private Texture2D icnFolder, icnTexture;

		void SetupStyles()
		{
			if (icnFolder == null)
				icnFolder = EditorGUIUtility.FindTexture("Folder Icon");
			if (icnTexture == null)
				icnTexture = EditorGUIUtility.FindTexture("Texture Icon");

			if (styleCreateBox == null)
			{
				styleCreateBox = new GUIStyle(EditorStyles.helpBox)
				{
					
				};
			}
			if (styleHeader == null)
			{
				styleHeader = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter,
					fontStyle = FontStyle.Bold
				};
			}
			if (styleLabelLeft == null)
			{
				styleLabelLeft = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleLeft,
					padding = new RectOffset(0, 0, 0, 0)
				};
			}
			if (styleBoldFoldout == null)
			{
				styleBoldFoldout = new GUIStyle(EditorStyles.foldout)
				{
					fontStyle = FontStyle.Bold
				};
			}
			if (styleLayerRow == null)
			{
				styleLayerRow = new GUIStyle(GUI.skin.box) {};
			}
		}
		#endregion

		public void OnSelectionChange()
		{
			Repaint();
		}

		public void OnGUI()
		{
			SetupStyles();

			Vector2 size = new Vector2(150f, 100f);
			imageLoaded = (image != null && settings.Psd != null);

			DrawPsdLayers();

			DrawImportField();

			DrawImportSettings();

			DrawCreateEntry();
		}

		#region PSD Layer display functions
		private Vector2 scrollPos = Vector2.zero;

		private void DrawPsdLayers()
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Layers", styleHeader);

			// Headers
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(30f);
			GUILayout.Label("Name");
			GUILayout.Label("Size", GUILayout.MaxWidth(70f), GUILayout.MinWidth(70f));
			GUILayout.Label("Pivot", GUILayout.MaxWidth(70f), GUILayout.MinWidth(70f));
			EditorGUILayout.EndHorizontal();

			scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

			if (imageLoaded)
			{
				DisplayLayers();
			}
			else
			{
				EditorGUILayout.HelpBox("No PSD file loaded", MessageType.Error, true);
			}

			EditorGUILayout.EndScrollView();

			// Draw import button only when image is loaded
			if (imageLoaded)
			{
				string importBtnText = string.Format("Import {0} Layer(s)", importCount);
				if (GUILayout.Button(importBtnText, GUILayout.Height(25f)))
					ImportLayers();
			}

			// End layers box
			GUILayout.Box(GUIContent.none, GUILayout.Height(4f), GUILayout.ExpandWidth(true));
		}

		private void DisplayLayers()
		{
			int groupDepth = 0;
			int groupVisibleMask = 1;
			int groupOpenMask = 1;

			PsdFile psd = settings.Psd;

			// Loop backwards through the layers to display them in the expected order
			for (int i = psd.Layers.Count - 1; i >= 0; i--)
			{
				Layer layer = psd.Layers[i];

				//var instanceInfo = fileInfo.GetInstancedLayer(i);
				//if (instanceInfo != null && doDebug)
				//	Debug.LogFormat("Layer {0}, index {1}, is instance of {2}", layer.Name, i, instanceInfo.instanceLayer);

				// Layer set seems to appear in the photoshop layers
				// no idea what it does but doesn't seem to be relevant
				if (layer.Name.Equals("</Layer set>"))
					continue;

				// Try to get the group of this layer
				var groupInfo = fileInfo.GetGroupByLayerIndex(i);
				bool inGroup = groupInfo != null;

				bool startGroup = false;
				bool closeGroup = false;

				if (inGroup)
				{
					closeGroup = groupInfo.start == i;
					startGroup = groupInfo.end == i;
				}

				// If start of group, indent
				if (startGroup)
				{
					groupDepth++;

					// Save the mask info
					groupVisibleMask |= ((groupInfo.visible ? 1 : 0) << groupDepth);
					groupOpenMask |= ((groupInfo.opened ? 1 : 0) << groupDepth);
				}
				// If exiting a layer group, unindent and continue to next layer
				if (closeGroup)
				{
					// Reset mask info when closing group
					groupVisibleMask &= ~(1 << groupDepth);
					groupOpenMask &= ~(1 << groupDepth);

					groupDepth--;
					continue;
				}

				bool parentVisible = true;

				// If layer group content...
				if (inGroup)
				{
					bool parentOpen = true;
					parentVisible = true;

					for (int parentMask = groupDepth - 1; parentMask > 0; parentMask--)
					{
						bool isOpen = (groupOpenMask & (1 << parentMask)) > 0;
						bool isVisible = (groupVisibleMask & (1 << parentMask)) > 0;

						parentOpen &= isOpen;
						parentVisible &= isVisible;
					}

					bool skipLayer = !groupInfo.opened || !parentOpen;
					bool disableLayer = !groupInfo.visible || !parentVisible;

					if (startGroup)
					{
						skipLayer = !parentOpen;
						disableLayer = !parentVisible;
					}

					// Skip contents if group folder closed
					if (skipLayer)
					{
						SetHiddenLayer(i, groupInfo.visible && parentVisible);
						continue;
					}

					// If not visible, disable the row
					if (disableLayer)
						GUI.enabled = false;

					// Set parentVisible for settings override in DrawLayerEntry
					parentVisible = !disableLayer;
				}

				if (startGroup)
				{
					DrawLayerGroupStart(groupInfo, i, groupDepth - 1);
				}
				else
				{
					DrawLayerEntry(layer, i, groupDepth, parentVisible);
				}

				GUI.enabled = true;

			} // End layer loop

			importCount = PSDExporter.GetExportCount(settings, fileInfo);
		}

		private void SetHiddenLayer(int layerIndex, bool parentVisible)
		{
			if (settings.layerSettings.ContainsKey(layerIndex) == false)
			{
				settings.layerSettings.Add(layerIndex, new PsdExportSettings.LayerSetting()
				{
					doExport = true,
					layerIndex = layerIndex,
					pivot = settings.Pivot
				});
			}
			settings.layerSettings[layerIndex].doExport = fileInfo.LayerVisibility[layerIndex] && parentVisible;
		}

		private bool DrawLayerEntry(Layer layer, int layerIndex, int indentLevel, bool parentVisible)
		{
			EditorGUILayout.BeginHorizontal();

			// Draw layer visibility toggle
			bool visToggle = EditorGUILayout.Toggle(fileInfo.LayerVisibility[layerIndex], GUILayout.MaxWidth(15f));

			GUILayout.Space(indentLevel * 20f);

			// Draw the layer name
			GUIContent layerDisplay = new GUIContent()
			{
				text = layer.Name
			};
			GUILayout.Label(layerDisplay, styleLabelLeft, GUILayout.ExpandWidth(true));
			fileInfo.LayerVisibility[layerIndex] = visToggle;

			// If layer visible, show layer export settings
			var layerSetting = settings.layerSettings[layerIndex];
			layerSetting.doExport = visToggle && parentVisible;
			if (layerSetting.doExport)
			{
				layerSetting.scaleBy = (PSDExporter.ScaleDown)EditorGUILayout.EnumPopup(layerSetting.scaleBy,
																				GUILayout.MaxWidth(70f),
																				GUILayout.MinWidth(70f));
				layerSetting.pivot = (SpriteAlignment)EditorGUILayout.EnumPopup(layerSetting.pivot,
																				GUILayout.MaxWidth(70f),
																				GUILayout.MinWidth(70f));
				settings.layerSettings[layerIndex] = layerSetting;
			}

			EditorGUILayout.EndHorizontal();

			return visToggle;
		}

		private bool doGroupSelect;
		private PSDLayerGroupInfo selectedGroup;

		private bool DrawLayerGroupStart(PSDLayerGroupInfo groupInfo,
										int layerIndex, int indentLevel)
		{
			if (groupInfo == selectedGroup || doGroupSelect)
				EditorGUILayout.BeginHorizontal(styleLayerRow);
			else
				EditorGUILayout.BeginHorizontal();

			// Draw group visibility toggle
			bool visToggle = EditorGUILayout.Toggle(groupInfo.visible, GUILayout.MaxWidth(15f));

			GUILayout.Space(indentLevel * 20f);

			// Draw the layer group name
			GUIContent groupDisplay = new GUIContent()
			{
				image = icnFolder,
				text = groupInfo.name
			};
			groupInfo.opened = EditorGUILayout.Foldout(groupInfo.opened, groupDisplay);

			if (doGroupSelect)
			{
				if (GUILayout.Button("Select Group", GUILayout.ExpandWidth(false)))
				{
					selectedGroup = groupInfo;
					doGroupSelect = false;
				}
			}

			// Save the data into group info and file info
			groupInfo.visible = visToggle;
			fileInfo.LayerVisibility[layerIndex] = visToggle;

			EditorGUILayout.EndHorizontal();

			return visToggle;
		}
		#endregion

		#region Import UI
		private void DrawImportField()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				string psdString = "PSD File";
				if (imageLoaded)
					psdString = image.name;

				EditorGUI.BeginChangeCheck();
				var img = (Texture2D)EditorGUILayout.ObjectField(psdString, image,
															typeof(Texture2D), false,
															GUILayout.ExpandWidth(true));
				bool changed = EditorGUI.EndChangeCheck();
				if (changed)
					Image = img;
			}
		}

		private bool showImportSettings;

		private void DrawImportSettings()
		{
			showImportSettings = EditorGUILayout.Foldout(showImportSettings, "Import Settings", styleBoldFoldout);
			if (!showImportSettings || !imageLoaded)
				return;

			EditorGUI.BeginChangeCheck();

			GUILayout.Label("Source Image Scale");
			settings.ScaleBy = GUILayout.Toolbar(settings.ScaleBy, new string[] { "1X", "2X", "4X" });

			settings.AutoReExport = EditorGUILayout.Toggle("Auto Re-Export", settings.AutoReExport);

			settings.PixelsToUnitSize = EditorGUILayout.FloatField("Pixels To Unit Size", settings.PixelsToUnitSize);
			if (settings.PixelsToUnitSize <= 0)
			{
				EditorGUILayout.HelpBox("Pixels To Unit Size should be greater than 0.", MessageType.Warning);
			}

			settings.PackingTag = EditorGUILayout.TextField("Packing Tag", settings.PackingTag);

			// Default pivot
			var newPivot = (SpriteAlignment)EditorGUILayout.EnumPopup("Default Pivot", settings.Pivot);
			// When pivot changed, change the other layer settings as well
			if (newPivot != settings.Pivot)
			{
				List<int> changeLayers = new List<int>();
				foreach (var layerKeyPair in settings.layerSettings)
				{
					if (layerKeyPair.Value.pivot == settings.Pivot)
					{
						changeLayers.Add(layerKeyPair.Value.layerIndex);
					}
				}
				foreach (int changeLayer in changeLayers)
				{
					settings.layerSettings[changeLayer].pivot = newPivot;
				}
				settings.Pivot = newPivot;
			}

			if (settings.Pivot == SpriteAlignment.Custom)
			{
				settings.PivotVector = EditorGUILayout.Vector2Field("Custom Pivot", settings.PivotVector);
			}

			// Path picker
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Import Path", GUILayout.Width(EditorGUIUtility.labelWidth)))
				PickExportPath();
			GUI.enabled = false;
			EditorGUILayout.TextField(GUIContent.none, settings.ExportPath);
			GUI.enabled = true;
			EditorGUILayout.EndHorizontal();

			if (EditorGUI.EndChangeCheck())
			{
			}

			if (GUILayout.Button("Save Import Settings"))
				settings.SaveMetaData();
		}

		private void PickExportPath()
		{
			string path = EditorUtility.SaveFolderPanel("Export Path", settings.ExportPath, "");
			if (string.IsNullOrEmpty(path))
			{
				settings.ExportPath = "";
			}
			else
			{
				int inPath = path.IndexOf(Application.dataPath);
				if (inPath < 0 || Application.dataPath.Length == path.Length)
					path = "";
				else
					path = path.Substring(Application.dataPath.Length + 1);

				settings.ExportPath = path;
			}
		}

		private void ImportLayers()
		{
			PSDExporter.Export(settings, fileInfo);
		}
		#endregion

		#region Sprite creation
		private bool showCreateSprites;
		private SpriteAlignment createPivot;
		private bool createAtSelection = false;
		private int createSortLayer = 0;

		private void DrawCreateEntry()
		{
			showCreateSprites = EditorGUILayout.Foldout(showCreateSprites, "Sprite Creation", styleBoldFoldout);

			if (!showCreateSprites || !imageLoaded)
				return;

			using (new EditorGUILayout.HorizontalScope(GUILayout.Height(30f)))
			{
				MessageType boxType = MessageType.Warning;
				string boxMsg = "Select a Layer Group";
				
				if (selectedGroup != null)
				{
					boxMsg = string.Format("Creating Group: {0}", selectedGroup.name);
					boxType = MessageType.Info;
				}

				EditorGUILayout.HelpBox(boxMsg, boxType);
				//GUILayout.Label(boxMsg, GUILayout.ExpandWidth(true));

				string selectMsg = doGroupSelect ? "Cancel" : "Select Group";
				if (GUILayout.Button(selectMsg, GUILayout.MaxWidth(100f), GUILayout.ExpandHeight(true)))
				{
					doGroupSelect = !doGroupSelect;
					if (!doGroupSelect)
						selectedGroup = null;
				}
			}

			bool createSprites = false;
			bool createUiImgs = false;

			using (new EditorGUILayout.HorizontalScope())
			{
				if (selectedGroup == null)
					GUI.enabled = false;

				const float buttonHeight = 50f;

				using (new EditorGUILayout.VerticalScope())
				{
					GUILayout.Label("2D Sprites", styleHeader);
					createSprites = GUILayout.Button("Create 2D Sprites",
													GUILayout.Height(buttonHeight),
													GUILayout.ExpandHeight(true));
				}

				using (new EditorGUILayout.VerticalScope())
				{
					// Check if selection passes 
					bool selectionOk = Selection.activeGameObject != null;
					string btnText = "Select UI Root";

					// Something is being selected, check if selection
					// is part of UI
					if (selectionOk)
					{
						btnText = "Create UI Images";
					}

					GUI.enabled = selectionOk;

					GUILayout.Label("UI Images", styleHeader);
					createUiImgs = GUILayout.Button(btnText,
													GUILayout.Height(buttonHeight),
													GUILayout.ExpandHeight(true));
				}
				GUI.enabled = true;
			}

			if (createSprites)
			{
				PsdBuilder.BuildToSprites(Selection.activeGameObject, selectedGroup, settings, fileInfo);
			}
		}

		private void CreateSprites()
		{
			int zOrder = settings.Psd.Layers.Count;

			// Find scaling factor
			float posScale = 1f;
			switch (settings.ScaleBy)
			{
				case 1:
					posScale = 0.5f;
					break;
				case 2:
					posScale = 0.25f;
					break;
			}

			GameObject root = new GameObject(settings.Filename);

			// Create the offset vector
			Vector3 createOffset = Vector3.zero;
			if (createPivot != SpriteAlignment.TopLeft)
			{
				Vector2 docSize = new Vector2(settings.Psd.ColumnCount, settings.Psd.RowCount);
				docSize *= posScale;

				if (createPivot == SpriteAlignment.Center ||
					createPivot == SpriteAlignment.LeftCenter ||
					createPivot == SpriteAlignment.RightCenter)
				{
					createOffset.y = (docSize.y / 2) / settings.PixelsToUnitSize;
				}
				if (createPivot == SpriteAlignment.BottomCenter ||
					createPivot == SpriteAlignment.BottomLeft ||
					createPivot == SpriteAlignment.BottomRight)
				{
					createOffset.y = docSize.y / settings.PixelsToUnitSize;
				}

				if (createPivot == SpriteAlignment.Center ||
					createPivot == SpriteAlignment.TopCenter ||
					createPivot == SpriteAlignment.BottomCenter)
				{
					createOffset.x = -(docSize.x / 2) / settings.PixelsToUnitSize;
				}
				if (createPivot == SpriteAlignment.RightCenter ||
					createPivot == SpriteAlignment.TopRight ||
					createPivot == SpriteAlignment.BottomRight)
				{
					createOffset.x = -(docSize.x) / settings.PixelsToUnitSize;
				}
			}

			// Loop through the layers
			Dictionary<PSDLayerGroupInfo, GameObject> groupHeaders = new Dictionary<PSDLayerGroupInfo, GameObject>();
			GameObject lastParent = root;
			for (int i = settings.Psd.Layers.Count - 1; i >= 0; i--)
			{
				var groupInfo = fileInfo.GetGroupByLayerIndex(i);
				if (groupInfo != null && !groupInfo.visible)
					continue;

				if (!fileInfo.LayerVisibility[i])
					continue;

				Layer layer = settings.Psd.Layers[i];

				bool inGroup = groupInfo != null;

				if (inGroup)
				{
					bool startGroup = groupInfo.end == i;
					bool closeGroup = groupInfo.start == i;

					if (startGroup)
					{
						GameObject groupRoot = new GameObject(layer.Name);
						groupRoot.transform.parent = lastParent.transform;
						groupRoot.transform.localPosition = Vector3.zero;
						groupRoot.transform.localScale = Vector3.one;

						lastParent = groupRoot;
						groupHeaders.Add(groupInfo, groupRoot);
						continue;
					}
					if (closeGroup)
					{
						lastParent = groupHeaders[groupInfo].transform.parent.gameObject;
						continue;
					}
				}

				// Try to get the sprite from the asset database first
				string assetPath = AssetDatabase.GetAssetPath(image);
				string path = Path.Combine(Path.GetDirectoryName(assetPath),
					Path.GetFileNameWithoutExtension(assetPath) + "_" + layer.Name + ".png");

				// Sprites doesn't exist, create it
				Sprite spr = (Sprite)AssetDatabase.LoadAssetAtPath(path, typeof(Sprite));
				if (spr == null)
				{
					spr = PSDExporter.CreateSprite(settings, i);
				}

				// Get the pivot settings for the sprite
				TextureImporter spriteSettings = (TextureImporter)AssetImporter.GetAtPath(path);
				TextureImporterSettings sprImport = new TextureImporterSettings();
				spriteSettings.ReadTextureSettings(sprImport);

				GameObject go = new GameObject(layer.Name);
				SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
				sr.sprite = spr;
				sr.sortingOrder = zOrder--;
				if (_sortingLayerNames != null)
				{
					sr.sortingLayerName = _sortingLayerNames[createSortLayer];
				}

				Vector3 goPos = Vector3.zero;
				Vector2 sprPivot = new Vector2(0.5f, 0.5f);
				if (sprImport.spriteAlignment == (int)SpriteAlignment.Custom)
				{
					sprPivot = sprImport.spritePivot;
				}
				if (sprImport.spriteAlignment == (int)SpriteAlignment.TopLeft ||
					sprImport.spriteAlignment == (int)SpriteAlignment.LeftCenter ||
					sprImport.spriteAlignment == (int)SpriteAlignment.BottomLeft)
				{
					sprPivot.x = 0f;
				}
				if (sprImport.spriteAlignment == (int)SpriteAlignment.TopRight ||
					sprImport.spriteAlignment == (int)SpriteAlignment.RightCenter ||
					sprImport.spriteAlignment == (int)SpriteAlignment.BottomRight)
				{
					sprPivot.x = 1;
				}
				if (sprImport.spriteAlignment == (int)SpriteAlignment.TopLeft ||
					sprImport.spriteAlignment == (int)SpriteAlignment.TopCenter ||
					sprImport.spriteAlignment == (int)SpriteAlignment.TopRight)
				{
					sprPivot.y = 1;
				}
				if (sprImport.spriteAlignment == (int)SpriteAlignment.BottomLeft ||
					sprImport.spriteAlignment == (int)SpriteAlignment.BottomCenter ||
					sprImport.spriteAlignment == (int)SpriteAlignment.BottomRight)
				{
					sprPivot.y = 0;
				}

				goPos.x = ((layer.Rect.width * sprPivot.x) + layer.Rect.x);
				goPos.x /= settings.PixelsToUnitSize;
				goPos.y = (-(layer.Rect.height * (1 - sprPivot.y)) - layer.Rect.y);
				goPos.y /= settings.PixelsToUnitSize;
				goPos.x *= posScale;
				goPos.y *= posScale;

				goPos += createOffset;

				go.transform.parent = lastParent.transform;
				go.transform.localScale = Vector3.one;
				go.transform.localPosition = goPos;

				if (createAtSelection && Selection.activeGameObject != null)
				{
					go.layer = Selection.activeGameObject.layer;
				}
			}

			if (createAtSelection && Selection.activeGameObject != null)
			{
				root.transform.parent = Selection.activeGameObject.transform;
				root.transform.localScale = Vector3.one;
				root.transform.localPosition = Vector3.zero;
				root.layer = Selection.activeGameObject.layer;
			}
		}
		#endregion
	}
}