using NUnit.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// Tests of string compression.
	/// </summary>
	[TestFixture]
	class StringCompressionTests
	{
		// Fields.
		readonly Random random = new Random();


		/// <summary>
		/// Test for compression.
		/// </summary>
		[Test]
		public void CompressionTests()
		{
			var specialChars = new char[] { '中', '文', '測', '試', '★', '●', '§', '㊣' };
			for (var length = 4; length <= (1 << 16); length <<= 1)
			{
				var originalSize = 0;
				var utf8Size = 0;
				var deflateSize = 0;
				for (var t = 0; t <= 100; ++t)
				{
					// generate string
					var chars = new char[length];
					for (var i = 0; i < length; ++i)
					{
						var n = this.random.Next(36 + specialChars.Length);
						if (n <= 9)
							chars[i] = (char)('0' + n);
						else if (n <= 35)
							chars[i] = (char)('a' + (n - 10));
						else
							chars[i] = specialChars[n - 36];
					}

					// get original size
					originalSize += (length << 1);

					// get UTF8 encoding size
					var utf8Bytes = Encoding.UTF8.GetBytes(chars);
					utf8Size += utf8Bytes.Length;

					// get deflate compression size
					var deflateBytes = new MemoryStream().Use(memoryStream =>
					{
						using (var deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress, true))
							deflateStream.Write(utf8Bytes, 0, utf8Bytes.Length);
						return memoryStream.ToArray();
					});
					deflateSize += deflateBytes.Length;
				}
				originalSize /= 100;
				utf8Size /= 100;
				deflateSize /= 100;
				var utf8SizeRatio = (double)utf8Size / originalSize;
				var deflateSizeRatio = (double)deflateSize / originalSize;

				if (deflateSize < utf8Size)
					; // in most cases, size of deflate compression string will be smaller than UTF8 when length >= 64.
			}
		}
	}
}
