using CarinaStudio.Diagnostics;
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
		public static readonly CompressedString Empty = new("", Level.None);


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
		static readonly long BaseSize = Memory.EstimateInstanceSize<CompressedString>();
		[ThreadStatic]
		static MemoryStream? CompressionMemoryStream;
		[ThreadStatic]
		static MemoryStream? DecompressionMemoryStream;


		// Fields.
		readonly object? data;
		readonly uint flags;
		readonly int length;


		// Constructor.
		CompressedString(string value, Level level)
		{
			if (level == Level.None || value.Length < 32)
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
			this.length = value.Length;
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


		// Decompress to UTF-8 bytes.
		byte[] Decompress(byte[] bytes)
		{
			DecompressionMemoryStream ??= new();
			DecompressionMemoryStream.Write(bytes, 0, bytes.Length);
			DecompressionMemoryStream.Position = 0;
			var utf8Bytes = new byte[(int)(this.flags & FLAGS_UTF8_ENCODING_SIZE_MASK)];
			using (var stream = new DeflateStream(DecompressionMemoryStream, CompressionMode.Decompress, true))
				stream.Read(utf8Bytes, 0, utf8Bytes.Length);
			DecompressionMemoryStream.SetLength(0);
			return utf8Bytes;
		}


		/// <summary>
		/// Get original string and put directly into given buffer.
		/// </summary>
		/// <param name="buffer">Buffer.</param>
		/// <param name="offset">Offset in buffer to put first character.</param>
		/// <returns>Number of characters in original string, or 1's complement of number of characters if size of buffer is insufficient.</returns>
		public int GetString(Span<char> buffer, int offset = 0)
		{
			var length = this.length;
			if (length == 0)
				return 0;
			if (offset < 0)
				throw new ArgumentOutOfRangeException(nameof(offset));
			if (offset + length > buffer.Length)
				return ~length;
			var data = this.data;
			if (data is string str)
				str.AsSpan().CopyTo(offset == 0 ? buffer : buffer[offset..^0]);
			else if (data is byte[] bytes)
			{
				if ((this.flags & FLAGS_COMPRESSED_MASK) != 0)
					bytes = this.Decompress(bytes);
				Encoding.UTF8.GetDecoder().GetChars(bytes.AsSpan(), offset == 0 ? buffer : buffer[offset..^0], true);
			}
			else
				return 0;
			return length;
		}


		/// <summary>
		/// Get number of characters of original string.
		/// </summary>
		public int Length { get => this.length; }


		/// <summary>
		/// Get size of compressed string in bytes.
		/// </summary>
		public long Size 
		{ 
			get => this.data switch
			{
				string str => Memory.EstimateInstanceSize(typeof(string), str.Length),
				byte[] bytes => Memory.EstimateArrayInstanceSize(sizeof(byte), bytes.Length),
				_ => 0,
			} + BaseSize;
		}


		// Decompress to string.
		public override string ToString()
		{
			var data = this.data;
			if (data is string str)
				return str;
			if (data is not byte[] bytes)
				return "";
			if ((this.flags & FLAGS_COMPRESSED_MASK) != 0)
				return Encoding.UTF8.GetString(this.Decompress(bytes));
			return Encoding.UTF8.GetString(bytes);
		}
	}
}
