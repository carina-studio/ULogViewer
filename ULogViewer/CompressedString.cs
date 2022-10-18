using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// String which stored in compressed data to reduce memory usage.
	/// </summary>
	class CompressedString
	{
		/// <summary>
		/// <see cref="CompressedString"/> represent empty string.
		/// </summary>
		public static readonly CompressedString Empty = new CompressedString("", Level.None);


		/// <summary>
		/// Compression level.
		/// </summary>
		public enum Level
		{
			/// <summary>
			/// No compression.
			/// </summary>
			None,
			/// <summary>
			/// Fast compression.
			/// </summary>
			Fast,
			/// <summary>
			/// Optimal compression.
			/// </summary>
			Optimal,
		}


		// Constants.
		const uint FLAGS_COMPRESSED_MASK = 0x80000000;
		const int FLAGS_COMPRESSED_SHIFT_COUNT = 31;
		const uint FLAGS_UTF8_ENCODING_SIZE_MASK = 0x7fffffff;


		// Static fields.
		static readonly long BaseSize = typeof(CompressedString).EstimateInstanceSize();
		[ThreadStatic]
		static MemoryStream? CompressionMemoryStream;
		[ThreadStatic]
		static MemoryStream? DecompressionMemoryStream;


		// Fields.
		readonly object? data;
		readonly uint flags;


		// Constructor.
		CompressedString(string value, Level level)
		{
			if (level == Level.None || value.Length < 4)
				this.data = value;
			else
			{
				var utf8Bytes = Encoding.UTF8.GetBytes(value);
				this.data = utf8Bytes;
				this.flags = ((uint)utf8Bytes.Length & FLAGS_UTF8_ENCODING_SIZE_MASK);
				if (level == Level.Optimal && value.Length >= 64)
				{
					CompressionMemoryStream ??= new();
					using (var stream = new DeflateStream(CompressionMemoryStream, CompressionMode.Compress, true))
						stream.Write(utf8Bytes, 0, utf8Bytes.Length);
					if (CompressionMemoryStream.Position < utf8Bytes.Length)
					{
						var compressedData = CompressionMemoryStream.ToArray();
						this.data = compressedData;
						this.flags |= (1u << FLAGS_COMPRESSED_SHIFT_COUNT);
					}
					CompressionMemoryStream.SetLength(0);
				}
			}
		}


		/// <summary>
		/// Create new <see cref="CompressedString"/> instance.
		/// </summary>
		/// <param name="value">String value.</param>
		/// <param name="level">Compression level.</param>
		/// <returns><see cref="CompressedString"/> or Null if <paramref name="value"/> is Null.</returns>
		public static CompressedString? Create(string? value, Level level)
		{
			if (value == null)
				return null;
			if (value.Length == 0)
				return CompressedString.Empty;
			return new CompressedString(value, level);
		}


		/// <summary>
		/// Get size of compressed string in bytes.
		/// </summary>
		public long Size 
		{ 
			get => this.data switch
			{
				string str => TypeExtensions.EstimateArrayInstanceSize(sizeof(char), str.Length),
				byte[] bytes => TypeExtensions.EstimateArrayInstanceSize(sizeof(byte), bytes.Length),
				_ => 0,
			} + BaseSize;
		}


		// Decompress to string.
		public override string ToString()
		{
			if (this.data is string str)
				return str;
			if (this.data is not byte[] bytes)
				return "";
			if ((this.flags & FLAGS_COMPRESSED_MASK) != 0)
			{
				DecompressionMemoryStream ??= new();
				DecompressionMemoryStream.Write(bytes, 0, bytes.Length);
				DecompressionMemoryStream.Position = 0;
				var utf8Bytes = new byte[(int)(this.flags & FLAGS_UTF8_ENCODING_SIZE_MASK)];
				using (var stream = new DeflateStream(DecompressionMemoryStream, CompressionMode.Decompress, true))
					stream.Read(utf8Bytes, 0, utf8Bytes.Length);
				DecompressionMemoryStream.SetLength(0);
				return Encoding.UTF8.GetString(utf8Bytes);
			}
			return Encoding.UTF8.GetString(bytes);
		}
	}
}
