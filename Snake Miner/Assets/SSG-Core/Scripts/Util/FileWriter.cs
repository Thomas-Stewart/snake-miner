using System.IO;
using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	public static class FileWriter
	{
		private const string FILE_PATH = "SaveData/{0}.json";

		public static void SaveToFile(string fileName, string content)
		{
			var filePath = string.Format(FILE_PATH, fileName);
			WriteToFile(filePath, content);
		}

		private static void WriteToFile(string path, string content)
		{
			var exportFileInfo = new FileInfo(path);

			if (!Directory.Exists(exportFileInfo.Directory?.FullName))
			{
				Directory.CreateDirectory(exportFileInfo.Directory?.FullName);
				Debug.Log("Folder created at: " + path);
			}

			File.WriteAllText(path, content);

			Debug.Log("String written to file: " + path);
		}

		public static void DeleteFile(string fileName)
		{
			var filePath = string.Format(FILE_PATH, fileName);

			if (File.Exists(filePath))
			{
				File.Delete(filePath);
				Debug.Log("File deleted: " + filePath);
			}
			else
			{
				Debug.LogWarning("File not found: " + filePath);
			}
		}

		public static string ReadFile(string fileName)
		{
			var filePath = string.Format(FILE_PATH, fileName);

			if (File.Exists(filePath))
			{
				var content = File.ReadAllText(filePath);
				Debug.Log("File read: " + filePath);
				return content;
			}
			else
			{
				Debug.LogWarning("File not found: " + filePath);
				return null;
			}
		}
	}
}