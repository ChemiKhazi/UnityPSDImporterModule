using System.Collections.Generic;
using PhotoshopFile;
using UnityEngine;

namespace subjectnerdagreement.psdexport
{
	public class PsdBuilder
	{
		public static void BuildToUi(GameObject root, PSDLayerGroupInfo group, PsdExportSettings settings, PsdFileInfo fileInfo)
		{
			BuildPsd(root, group, settings, fileInfo);
		}

		public static void BuildToSprites(GameObject root, PSDLayerGroupInfo group, PsdExportSettings settings, PsdFileInfo fileInfo)
		{
			BuildPsd(root, group, settings, fileInfo);
		}

		#region General handler
		private static void BuildPsd(GameObject root, PSDLayerGroupInfo group, PsdExportSettings settings, PsdFileInfo fileInfo)
		{
			// Run the export on non exported layers
			PSDExporter.Export(settings, fileInfo, false);

			// Find all the layers being exported
			var exportLayers = PSDExporter.GetExportLayers(settings, fileInfo);

			// Stores the root object for each encountered group
			Dictionary<PSDLayerGroupInfo, GameObject> groupHeaders = new Dictionary<PSDLayerGroupInfo, GameObject>();
			
			// Store the last parent, for traversal
			GameObject lastParent = root;

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
						HandleGroupObject(groupInfo, groupHeaders, startGroup, ref lastParent);

						// Start or end group doesn't have visible sprite object, skip to next layer
						continue;
					}
				}

				// If got to here, processing a visual layer

				// Skip if the export layers index doesn't contain this index
				if (exportLayers.Contains(i) == false)
					continue;

				// Get layer info
				Layer layer = settings.Psd.Layers[i];

				// Create the game object for the sprite
				GameObject spriteObject = new GameObject(layer.Name)
				{
					layer = lastParent.layer,
					tag = lastParent.tag
				};

				// Reparent created object to last parent
				if (lastParent != null)
					spriteObject.transform.SetParent(lastParent.transform, false);

				// Add components to the sprite object

			} // End layer loop
		} // End BuildPsd()

		private static void HandleGroupObject(PSDLayerGroupInfo groupInfo,
						Dictionary<PSDLayerGroupInfo, GameObject> groupHeaders,
						bool startGroup, ref GameObject lastParent)
		{
			if (startGroup)
			{
				GameObject groupRoot = new GameObject(groupInfo.name);

				if (lastParent != null)
					groupRoot.transform.SetParent(lastParent.transform);

				groupRoot.transform.localPosition = Vector3.zero;
				groupRoot.transform.localScale = Vector3.one;

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

		private static void SpriteCreator()
		{
		}
	}
}