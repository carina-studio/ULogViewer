using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CarinaStudio.ULogViewer.Json
{
	/// <summary>
	/// Utility methods for JSON.
	/// </summary>
	static class JsonUtility
	{
		/// <summary>
		/// Decode string which is encoded in JSON format.
		/// </summary>
		/// <param name="jsonString">String encoded in JSON format.</param>
		/// <returns>Decoded string.</returns>
		public static string DecodeFromJsonString(string jsonString)
		{
			if (string.IsNullOrEmpty(jsonString))
				return jsonString;
			var jsonReader = new Utf8JsonReader(Encoding.UTF8.GetBytes($"\"{jsonString}\"").AsSpan());
			try
			{
				if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.String)
					return jsonReader.GetString() ?? "";
			}
			catch
			{ }
			return jsonString;
		}


		/// <summary>
		/// Encoded given string in JSON format.
		/// </summary>
		/// <param name="str">String to encode.</param>
		/// <returns>Encoded string.</returns>
		public static string EncodeToJsonString(string str)
		{
			if (string.IsNullOrEmpty(str))
				return str;
			return Encoding.UTF8.GetString(new MemoryStream().Use(memoryStream =>
			{
				using var jsonWriter = new Utf8JsonWriter(memoryStream);
				jsonWriter.WriteStringValue(str);
				jsonWriter.Flush();
				return memoryStream.ToArray();
			})).Let(str => str.Substring(1, str.Length - 2));
		}
	}
}
