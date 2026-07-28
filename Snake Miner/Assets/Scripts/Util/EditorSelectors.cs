#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Util
{
	public class EditorSelectors : EditorWindow
	{
		private const string SessionKey = "Util.EditorSelectors.SelectedGuids";
		private Vector2 _scrollPosition;

		[MenuItem("Tools/File Selector")]
		public static void ShowWindow()
		{
			GetWindow<EditorSelectors>("File Selector");
		}

		[MenuItem("Assets/Add To File Selector", false, 2000)]
		private static void AddSelectionToFileSelector()
		{
			var selection = Selection.objects;
			if (selection == null || selection.Length == 0)
				return;

			var guidSet = new HashSet<string>(LoadSelectedGuids());
			for (var i = 0; i < selection.Length; i++)
			{
				var asset = selection[i];
				if (asset == null)
					continue;

				var path = AssetDatabase.GetAssetPath(asset);
				if (string.IsNullOrWhiteSpace(path))
					continue;

				var guid = AssetDatabase.AssetPathToGUID(path);
				if (!string.IsNullOrWhiteSpace(guid))
					guidSet.Add(guid);
			}

			SaveSelectedGuids(guidSet);
			ShowWindow();
		}

		[MenuItem("Assets/Add To File Selector", true)]
		private static bool ValidateAddSelectionToFileSelector()
		{
			return Selection.objects != null && Selection.objects.Length > 0;
		}

		private void OnGUI()
		{
			DrawSelectedAssetsSection();
			EditorGUILayout.Space(12f);

			if (GUILayout.Button("Base Scene"))
			{
				if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
				{
					var filePath = "Assets/SSG-Core/Scenes/_Base.unity";
					EditorSceneManager.OpenScene(filePath);
				}
			}
			if (GUILayout.Button("Game Scene"))
			{
				if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
				{
					var filePath = "Assets/Scenes/Level1.unity";
					EditorSceneManager.OpenScene(filePath);
				}
			}
			if (GUILayout.Button("Prefabs"))
			{
				var filePath = "Assets/Prefabs";
				SelectSpecificFile(filePath);
			}
			if (GUILayout.Button("Materials"))
			{
				var filePath = "Assets/Materials";
				SelectSpecificFile(filePath);
			}
			if (GUILayout.Button("Scenes"))
			{
				var filePath = "Assets/Scenes";
				SelectSpecificFile(filePath);
			}
			if (GUILayout.Button("Databases"))
			{
				var filePath = "Assets/Resources";
				SelectSpecificFile(filePath);
			}
			if (GUILayout.Button("Sprites"))
			{
				var filePath = "Assets/Sprites";
				SelectSpecificFile(filePath);
			}
		}

		private void DrawSelectedAssetsSection()
		{
			EditorGUILayout.LabelField("Selected Assets", EditorStyles.boldLabel);
			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Add Current Selection"))
					AddSelectionToFileSelector();

				if (GUILayout.Button("Clear"))
					SaveSelectedGuids(System.Array.Empty<string>());
			}

			var assets = LoadSelectedAssets();
			if (assets.Count == 0)
			{
				EditorGUILayout.HelpBox("Use 'Assets/Add To File Selector' in the Project window to build a reusable list.", MessageType.Info);
				return;
			}

			_scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.MinHeight(140f));
			for (var i = 0; i < assets.Count; i++)
			{
				var asset = assets[i];
				if (asset == null)
					continue;

				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUILayout.ObjectField(asset, typeof(Object), false);
					if (GUILayout.Button("Select", GUILayout.Width(60f)))
					{
						Selection.activeObject = asset;
						EditorGUIUtility.PingObject(asset);
						EditorUtility.FocusProjectWindow();
					}

					if (GUILayout.Button("X", GUILayout.Width(24f)))
					{
						RemoveAssetAtIndex(i);
						GUIUtility.ExitGUI();
					}
				}
			}

			EditorGUILayout.EndScrollView();
		}

		private static void SelectSpecificFile(string filePath)
		{
			var asset = AssetDatabase.LoadAssetAtPath<Object>(filePath);

			if (asset != null)
			{
				Selection.activeObject = asset;
				EditorUtility.FocusProjectWindow();
			}
			else
			{
				Debug.LogError("File not found: " + filePath);
			}
		}

		private static List<Object> LoadSelectedAssets()
		{
			var assets = new List<Object>();
			var dirty = false;
			var guids = LoadSelectedGuids();
			for (var i = 0; i < guids.Count; i++)
			{
				var guid = guids[i];
				if (string.IsNullOrWhiteSpace(guid))
				{
					dirty = true;
					continue;
				}

				var path = AssetDatabase.GUIDToAssetPath(guid);
				if (string.IsNullOrWhiteSpace(path))
				{
					dirty = true;
					continue;
				}

				var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
				if (asset == null)
				{
					dirty = true;
					continue;
				}

				assets.Add(asset);
			}

			if (dirty)
				SaveSelectedGuids(assets.Select(AssetDatabase.GetAssetPath).Where(p => !string.IsNullOrWhiteSpace(p)).Select(AssetDatabase.AssetPathToGUID));

			return assets;
		}

		private static List<string> LoadSelectedGuids()
		{
			var serialized = SessionState.GetString(SessionKey, string.Empty);
			if (string.IsNullOrWhiteSpace(serialized))
				return new List<string>();

			return serialized
				.Split('|')
				.Where(guid => !string.IsNullOrWhiteSpace(guid))
				.Distinct()
				.ToList();
		}

		private static void SaveSelectedGuids(IEnumerable<string> guids)
		{
			var serialized = string.Join("|", guids.Where(guid => !string.IsNullOrWhiteSpace(guid)).Distinct());
			SessionState.SetString(SessionKey, serialized);
		}

		private static void RemoveAssetAtIndex(int index)
		{
			var guids = LoadSelectedGuids();
			if (index < 0 || index >= guids.Count)
				return;

			guids.RemoveAt(index);
			SaveSelectedGuids(guids);
		}
	}
}
#endif
