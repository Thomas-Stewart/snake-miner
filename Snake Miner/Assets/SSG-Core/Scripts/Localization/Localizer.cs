using System.Collections.Generic;
using System.IO;
using SSG.Util;
using UnityEditor;
using UnityEngine;

namespace SSG_Core.Scripts.Localization
{
	public static class Localizer
	{
		private const string  FILE_NAME = "Translations";
		private const string ENGLISH_LANGUAGE_CODE = "en";

		private static readonly List<string> _languageCodes = new List<string>();
		private static readonly Dictionary<string, Dictionary<string, string>> _translations = new Dictionary<string, Dictionary<string, string>>();
		private static readonly HashSet<string> _missingLocalizationWarnings = new HashSet<string>();

		private static bool _debugLocalization = false;
		public static event System.Action OnLanguageChanged;
		internal static uint Revision { get; private set; }

#if UNITY_EDITOR
		[MenuItem("Tools/Localization/LoadFile")]
#endif
		public static void LoadFile()
		{
			_languageCodes.Clear();
			_translations.Clear();
			_missingLocalizationWarnings.Clear();

			var csvFile = Resources.Load<TextAsset>(FILE_NAME);

			if (csvFile != null)
			{
				var reader = new StringReader(csvFile.text);
				var line = reader.ReadLine();
				var headers = line != null ? ParseCsvLine(line) : new string[0];

				for (var i = 1; i < headers.Length; i++)
				{
					var languageCode = headers[i];
					if (!string.IsNullOrWhiteSpace(languageCode))
						_languageCodes.Add(languageCode);
				}

				while (reader.Peek() != -1)
				{
					line = reader.ReadLine();
					if (line == null) continue;

					var values = ParseCsvLine(line);

					if (values.Length >= 2)
					{
						var key = values[0].ToLower();
						if (string.IsNullOrEmpty(key))
							continue;

						_translations[key] = new Dictionary<string, string>();
						for (var i = 0; i < _languageCodes.Count; i++)
						{
							var valueIndex = i + 1;
							if (valueIndex < values.Length)
								_translations[key][_languageCodes[i]] = values[valueIndex];
						}
					}
				}

				reader.Close();
			}
			else
			{
				Debug.LogWarning("No Localization File Found!");
			}

			IncrementRevision();
		}

		public static string GetText(string locKey)
		{
			locKey = locKey.ToLower();
			if (_translations.TryGetValue(locKey, out var values))
			{
				if (_debugLocalization)
					return locKey.ToUpperInvariant();
				return GetTranslation(values).Replace("\\n", "\n");
			}

			if (_translations.Keys.Count != 0 && _missingLocalizationWarnings.Add(locKey))
				Debug.LogWarning($"Could not find localization value for key: {locKey}");
			return locKey;
		}

		public static IEnumerable<string> GetAllLocIds()
		{
			return _translations.Keys;
		}

		public static int GetLanguageCount()
		{
			return _languageCodes.Count;
		}

		public static int GetCurrentLanguageIndex()
		{
			var languageCode = GetCurrentLanguageCode();
			for (var i = 0; i < _languageCodes.Count; i++)
			{
				if (_languageCodes[i] == languageCode)
					return i;
			}

			return 0;
		}

		public static string GetCurrentLanguageCode()
		{
			var savedLanguageCode = SaveUtil.GetLanguageCode();
			return _languageCodes.Contains(savedLanguageCode) ? savedLanguageCode : ENGLISH_LANGUAGE_CODE;
		}

		public static string GetLanguageCode(int index)
		{
			if (index < 0 || index >= _languageCodes.Count)
				return ENGLISH_LANGUAGE_CODE;

			return _languageCodes[index];
		}

		public static void SetLanguageByIndex(int index)
		{
			if (index < 0 || index >= _languageCodes.Count)
				return;

			var languageCode = _languageCodes[index];
			if (languageCode == GetCurrentLanguageCode())
				return;

			SaveUtil.SetLanguageCode(languageCode);
			IncrementRevision();
			OnLanguageChanged?.Invoke();
		}

		private static string GetTranslation(IReadOnlyDictionary<string, string> values)
		{
			var languageCode = GetCurrentLanguageCode();
			if (values.TryGetValue(languageCode, out var value) && !string.IsNullOrEmpty(value))
				return value;

			if (values.TryGetValue(ENGLISH_LANGUAGE_CODE, out value) && !string.IsNullOrEmpty(value))
				return value;

			return string.Empty;
		}

		private static string[] ParseCsvLine(string line)
		{
			var values = new List<string>();
			var value = string.Empty;
			var inQuotes = false;

			for (var i = 0; i < line.Length; i++)
			{
				var character = line[i];
				if (character == '"')
				{
					if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
					{
						value += '"';
						i++;
						continue;
					}

					inQuotes = !inQuotes;
					continue;
				}

				if (character == ',' && !inQuotes)
				{
					values.Add(value);
					value = string.Empty;
					continue;
				}

				value += character;
			}

			values.Add(value);
			return values.ToArray();
		}

#if UNITY_EDITOR
		[MenuItem("Tools/Localization/Toggle Debug Localization")]
		public static void ToggleDebugLocalization()
		{
			_debugLocalization = !_debugLocalization;
			IncrementRevision();
		}
#endif

		private static void IncrementRevision()
		{
			unchecked
			{
				Revision++;
			}
		}
	}
}
