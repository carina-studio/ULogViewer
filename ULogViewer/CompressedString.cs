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


		// Static fields.
		[ThreadStatic]
		static MemoryStream? CompressionMemoryStream;
		[ThreadStatic]
		static MemoryStream? DecompressionMemoryStream;
		static readonly byte[] EmptyData = new byte[0];


		// Fields.
		readonly byte[] data;
		readonly bool isCompressed;
		readonly string? originalString;
		readonly int utf8EncodingSize;


		// Constructor.
		CompressedString(string value, Level level)
		{
			if (level == Level.None || value.Length < 4)
			{
				this.data = EmptyData;
				this.originalString = value;
			}
			else
			{
				this.data = Encoding.UTF8.GetBytes(value);
				this.utf8EncodingSize = this.data.Length;
				if (level == Level.Optimal && value.Length >= 64)
				{
					if (CompressionMemoryStream == null)
						CompressionMemoryStream = new MemoryStream();
					using (var stream = new DeflateStream(CompressionMemoryStream, CompressionMode.Compress, true))
						stream.Write(this.data, 0, this.data.Length);
					if (CompressionMemoryStream.Position < this.utf8EncodingSize)
					{
						this.data = CompressionMemoryStream.ToArray();
						this.isCompressed = true;
					}
					CompressionMemoryStream.SetLength(0);
				}
				this.originalString = null;
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
		public int Size { get => this.originalString?.Let(it => it.Length << 1) ?? this.data.Length; }


		// Decompress to string.
		public override string ToString()
		{
			if (this.originalString != null)
				return this.originalString;
			var data = this.data;
			if (this.isCompressed)
			{
				if (DecompressionMemoryStream == null)
					DecompressionMemoryStream = new MemoryStream();
				DecompressionMemoryStream.Write(data, 0, data.Length);
				DecompressionMemoryStream.Position = 0;
				data = new byte[this.utf8EncodingSize];
				using (var stream = new DeflateStream(DecompressionMemoryStream, CompressionMode.Decompress, true))
				
					stream.Read(data, 0, data.Length);
				DecompressionMemoryStream.SetLength(0);
			}
			var value = Encoding.UTF8.GetString(data);
			return value;
		}
	}
}
