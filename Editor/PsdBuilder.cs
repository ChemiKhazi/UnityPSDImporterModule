using System;
using System.Collections.Generic;
using System.Linq;
using PhotoshopFile;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace subjectnerdagreement.psdexport
{
	public class PsdBuilder
	{
		public static void BuildToUi(GameObject root, PSDLayerGroupInfo group,
									PsdExportSettings settings, PsdFileInfo fileInfo,
									SpriteAlignment createAlign)
		{
			BuildPsd(root, false, createAlign,
					group, settings, fileInfo,
					CreateUiGO, UiImgCreator, UiGetGroupPos);
		}

		public static void BuildToSprites(GameObject root, PSDLayerGroupInfo group,
										PsdExportSettings settings, PsdFileInfo fileInfo,
										SpriteAlignment createAlign)
		{
			BuildPsd(root, true, createAlign,
					group, settings, fileInfo,
					CreateSpriteGO, SpriteCreator, SprGetGroupPos);
		}

		#region General handler
		private static void BuildPsd(GameObject root, bool isSprite, SpriteAlignment align,
									PSDLayerGroupInfo group, PsdExportSettings settings, PsdFileInfo fileInfo,
									Func<string, GameObject, GameObject> objectFactory,
									Action<int, GameObject, Sprite, TextureImporterSettings> componentFactory,
									Func<GameObject, SpriteAlignment, Vector3> getRootPosition)
		{
			// Run the export on non exported layers
			PSDExporter.Export(settings, fileInfo, false);

			// Find all the layers being exported
			var exportLayers = PSDExporter.GetExportLayers(settings, fileInfo);

			// Stores the root object for each encountered group
			Dictionary<PSDLayerGroupInfo, GameObject> groupHeaders = new Dictionary<PSDLayerGroupInfo, GameObject>();

			// Store the last parent, for traversal
			GameObject lastParent = root;

			GameObject rootBase = null;
			Transform rootBaseT = null;

			int groupVisibleMask = 1;
			int groupDepth = 0;

			// Loop through all the layers of the PSD file
			// backwards so they appear in the expected order
			// Going through all the layers, and not just the exported layers
			// so that the groups can be setup
			for (int i = group.end; i >= group.start; i--)
			{
				// Skip if layer is hidden
				if (fileInfo.LayerVisibility[i] == false)
					continue;

				var groupInfo = fileInfo.GetGroupByLayerIndex(i);
				bool inGroup = groupInfo != null;

				// Skip if layer belongs to a hidden group
				if (inGroup && groupInfo.visible == false)
					continue;

				// When inside a group...
				if (inGroup)
				{
					// Inverted because starting backwards
					bool startGroup = groupInfo.end == i;
					bool closeGroup = groupInfo.start == i;

					// Go up or down group depths
					if (startGroup)
					{
						groupDepth++;
						groupVisibleMask |= ((groupInfo.visible ? 1 : 0) << groupDepth);
					}
					if (closeGroup)
					{
						// Reset group visible flag when closing group
						groupVisibleMask &= ~(1 << groupDepth);
						groupDepth--;
					}

					// First, check if parents of this group is visible in the first place
					bool parentVisible = true;
					for (int parentMask = groupDepth - 1; parentMask > 0; parentMask--)
					{
						bool isVisible = (groupVisibleMask & (1 << parentMask)) > 0;
						parentVisible &= isVisible;
					}
					// Parents not visible, continue to next layer
					if (!parentVisible)
						continue;

					// Finally, check if layer being processed is start/end of group
					if (startGroup || closeGroup)
					{
						// If start or end of the group, call HandleGroupObject
						// which creates the group layer object and assignment of lastParent
						HandleGroupObject(groupInfo, groupHeaders, startGroup, ref lastParent, objectFactory);

						// A bunch of book keeping needs to be done at the start of a group
						if (startGroup)
						{
							// If this is the start of the group being constructed
							// store as the rootBase
							if (i == group.end)
							{
								rootBase = lastParent;
								rootBaseT = rootBase.transform;
							}
						}

						// Start or end group doesn't have visible sprite object, skip to next layer
						continue;
					}
				} // End processing of group start/end

				// If got to here, processing a visual layer

				// Skip if the export layers list doesn't contain this index
				if (exportLayers.Contains(i) == false)
					continue;

				// If got here and root base hasn't been set, that's a problem
				if (rootBase == null)
				{
					throw new Exception("Trying to create image layer before root base has been set");
				}

				// Get layer info
				Layer layer = settings.Psd.Layers[i];

				// Create the game object for the sprite
				GameObject spriteObject = objectFactory(layer.Name, lastParent);

				// Reparent created object to last parent
				if (lastParent != null)
					spriteObject.transform.SetParent(lastParent.transform, false);

				// Retrieve sprite from asset database
				string sprPath = PSDExporter.GetLayerFilename(settings, i);
				Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(sprPath);

				// Get the pivot settings for the sprite
				TextureImporter sprImporter = (TextureImporter)AssetImporter.GetAtPath(sprPath);
				TextureImporterSettings sprSettings = new TextureImporterSettings();
				sprImporter.ReadTextureSettings(sprSettings);
				sprImporter = null;

				// Add components to the sprite object for the visuals
				componentFactory(i, spriteObject, sprite, sprSettings);

				Transform spriteT = spriteObject.transform;

				// Reposition the sprite object according to PSD position
				Vector2 spritePivot = GetPivot(sprSettings);

				Vector3 layerPos = Vector3.zero;
				layerPos.x = ((layer.Rect.width * spritePivot.x) + layer.Rect.x);
				layerPos.y = (-(layer.Rect.height * (1 - spritePivot.y)) - layer.Rect.y);

				if (isSprite)
					layerPos /= settings.PixelsToUnitSize;

				// Scaling factor, if sprites were scaled down
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
				layerPos *= posScale;

				// Sprite position is based on root object position initially
				spriteT.position = (rootBaseT.position + layerPos);
			} // End layer loop

			// Loop through the groups and reposition according to alignment
			var groups = groupHeaders.Values.ToArray();
			for (int grpIndex = 0; grpIndex < groups.Length; grpIndex++)
			{
				var groupObject = groups[grpIndex];
				if (groupObject == null)
					continue;
				
				Transform groupT = groupObject.transform;

				// Get the position from the root pos function
				Vector3 groupPos = getRootPosition(groupObject, align);

				// Create a new object
				GameObject newRoot = objectFactory(groupObject.name, groupObject);

				// Reparent new object
				Transform newRootT = newRoot.transform;
				newRootT.SetParent(groupT.parent);

				// Reposition the new object
				newRootT.position = groupPos;

				// Reparent the children from the old root object to new root
				while (groupT.childCount>0)
				{
					groupT.GetChild(0).SetParent(newRootT, true);
				}
				
				// If the group we're handling is rootBaseT, position the
				// replacement group header over old root
				if (groupT == rootBaseT)
				{
					newRootT.position = rootBaseT.position;
				}

				// Destroy the old root
				Object.DestroyImmediate(groups[grpIndex]);
			}
		} // End BuildPsd()

		private static void HandleGroupObject(PSDLayerGroupInfo groupInfo,
						Dictionary<PSDLayerGroupInfo, GameObject> groupHeaders,
						bool startGroup, ref GameObject lastParent,
						Func<string, GameObject, GameObject> objectCreator)
		{
			if (startGroup)
			{
				GameObject groupRoot = objectCreator(groupInfo.name, lastParent);

				lastParent = groupRoot;
				groupHeaders.Add(groupInfo, groupRoot);
			}
			// If not startGroup, closing group
			else
			{
				var header = groupHeaders[groupInfo].transform;
				if (header.parent != null)
					lastParent = groupHeaders[groupInfo].transform.parent.gameObject;
				else
					lastParent = null;
			}
		}
		#endregion

		#region Object factories
		private static GameObject CreateSpriteGO(string name, GameObject parent)
		{
			GameObject spriteGO = new GameObject(name);
			Transform spriteT = spriteGO.transform;

			if (parent != null)
			{
				spriteT.SetParent(parent.transform);
				spriteGO.layer = parent.layer;
				spriteGO.tag = parent.tag;
			}

			spriteT.localPosition = Vector3.zero;
			spriteT.localScale = Vector3.one;

			return spriteGO;
		}

		private static GameObject CreateUiGO(string name, GameObject parent)
		{
			GameObject uiGO = CreateSpriteGO(name, parent);
			uiGO.AddComponent<RectTransform>();
			return uiGO;
		}
		#endregion

		#region Sprite factories

		private static Vector2 GetPivot(SpriteAlignment spriteAlignment)
		{
			Vector2 pivot = new Vector2(0.5f, 0.5f);
			if (spriteAlignment == SpriteAlignment.TopLeft ||
				spriteAlignment == SpriteAlignment.LeftCenter ||
				spriteAlignment == SpriteAlignment.BottomLeft)
			{
				pivot.x = 0f;
			}
			if (spriteAlignment == SpriteAlignment.TopRight ||
				spriteAlignment == SpriteAlignment.RightCenter ||
				spriteAlignment == SpriteAlignment.BottomRight)
			{
				pivot.x = 1;
			}
			if (spriteAlignment == SpriteAlignment.TopLeft ||
				spriteAlignment == SpriteAlignment.TopCenter ||
				spriteAlignment == SpriteAlignment.TopRight)
			{
				pivot.y = 1;
			}
			if (spriteAlignment == SpriteAlignment.BottomLeft ||
				spriteAlignment == SpriteAlignment.BottomCenter ||
				spriteAlignment == SpriteAlignment.BottomRight)
			{
				pivot.y = 0;
			}
			return pivot;
		}

		private static Vector2 GetPivot(TextureImporterSettings sprSettings)
		{
			SpriteAlignment align = (SpriteAlignment) sprSettings.spriteAlignment;
			if (align == SpriteAlignment.Custom)
				return sprSettings.spritePivot;
			return GetPivot(align);
		}

		private static void SpriteCreator(int index, GameObject sprObj, Sprite sprite, TextureImporterSettings sprSettings)
		{
			var spr = sprObj.AddComponent<SpriteRenderer>();
			spr.sprite = sprite;
			spr.sortingOrder = index;
		}

		private static void UiImgCreator(int index, GameObject sprObj, Sprite sprite, TextureImporterSettings sprSettings)
		{
			var uiImg = sprObj.AddComponent<Image>();

			uiImg.sprite = sprite;
			uiImg.SetNativeSize();
			uiImg.rectTransform.SetAsFirstSibling();

			Vector2 sprPivot = GetPivot(sprSettings);
			uiImg.rectTransform.pivot = sprPivot;
		}
		#endregion

		private static Vector3 SprGetGroupPos(GameObject groupRoot, SpriteAlignment alignment)
		{
			Transform t = groupRoot.transform;
			var spriteList = t.GetComponentsInChildren<SpriteRenderer>();

			Vector3 min = new Vector3(float.MaxValue, float.MaxValue);
			Vector3 max = new Vector3(float.MinValue, float.MinValue);

			foreach (var sprite in spriteList)
			{
				var bounds = sprite.bounds;
				min = Vector3.Min(min, bounds.min);
				max = Vector3.Max(max, bounds.max);
			}

			Vector2 pivot = GetPivot(alignment);
			Vector3 pos = Vector3.zero;
			pos.x = Mathf.Lerp(min.x, max.x, pivot.x);
			pos.y = Mathf.Lerp(min.y, max.y, pivot.y);
			return pos;
		}

		private static Vector3 UiGetGroupPos(GameObject groupRoot, SpriteAlignment alignment)
		{
			//return groupRoot.transform.position;
			Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
			Vector2 max = new Vector2(float.MinValue, float.MinValue);

			var tList = groupRoot.GetComponentsInChildren<RectTransform>();
			foreach (var rectTransform in tList)
			{
				if (rectTransform.gameObject == groupRoot)
					continue;
				
				var rectSize = rectTransform.sizeDelta;
				var rectPivot = rectTransform.pivot;

				var calcMin = rectTransform.position;
				calcMin.x -= rectSize.x*rectPivot.x;
				calcMin.y -= rectSize.y*rectPivot.y;

				var calcMax = calcMin + new Vector3(rectSize.x, rectSize.y);

				min = Vector2.Min(min, calcMin);
				max = Vector2.Max(max, calcMax);
			}

			Vector2 pivot = GetPivot(alignment);
			Vector3 pos = Vector3.zero;
			pos.x = Mathf.Lerp(min.x, max.x, pivot.x);
			pos.y = Mathf.Lerp(min.y, max.y, pivot.y);
			return pos;
		}
	}
}