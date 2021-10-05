using CarinaStudio.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CarinaStudio.ULogViewer.Cryptography
{
	/// <summary>
	/// Provide encrypt/decrypt functions. 
	/// </summary>
	class Crypto : BaseDisposable
	{
		// Fields.
		readonly Aes aes;


		/// <summary>
		/// Initialize new <see cref="Crypto"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public Crypto(IULogViewerApplication app)
		{
			this.aes = Aes.Create().Also(it =>
			{
				it.Key = app.OpenManifestResourceStream("CarinaStudio.ULogViewer.Cryptography.AesKey").Use(stream =>
				{
					var buffer = new byte[(int)stream.Length];
					stream.Read(buffer, 0, buffer.Length);
					return buffer;
				});
				it.IV = app.OpenManifestResourceStream("CarinaStudio.ULogViewer.Cryptography.AesIV").Use(stream =>
				{
					var buffer = new byte[(int)stream.Length];
					stream.Read(buffer, 0, buffer.Length);
					return buffer;
				});
			});
		}


		/// <summary>
		/// Decrypt data.
		/// </summary>
		/// <param name="encryptedData">Encrypted data.</param>
		/// <returns>Decrypted data.</returns>
		public byte[] Decrypt(byte[] encryptedData) => new MemoryStream(encryptedData).Use(srcStream =>
		{
			using var cryptoStream = new CryptoStream(srcStream, this.aes.CreateDecryptor(), CryptoStreamMode.Read);
			var decryptedData = new List<byte>();
			var buffer = new byte[1024];
			var readCount = cryptoStream.Read(buffer, 0, buffer.Length);
			while (readCount > 0)
			{
				if (readCount == buffer.Length)
					decryptedData.AddRange(buffer);
				else
					decryptedData.AddRange(buffer.ToArray(0, readCount));
				readCount = cryptoStream.Read(buffer, 0, buffer.Length);
			}
			return decryptedData.ToArray();
		});


		/// <summary>
		/// Decrypt string.
		/// </summary>
		/// <param name="encryptedString">Encrypted string.</param>
		/// <returns>Decrypted string.</returns>
		public string Decrypt(string encryptedString)
		{
			if (string.IsNullOrEmpty(encryptedString))
				return "";
			return Convert.FromBase64String(encryptedString).Let(it =>
			{
				return Encoding.UTF8.GetString(this.Decrypt(it));
			});
		}


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			if (disposing)
				this.aes.Dispose();
		}


		/// <summary>
		/// Encrypt data.
		/// </summary>
		/// <param name="data">Data to encrypt.</param>
		/// <returns>Encrypted data.</returns>
		public byte[] Encrypt(byte[] data) => new MemoryStream().Use(destStream =>
		{
			using var cryptoStream = new CryptoStream(destStream, this.aes.CreateEncryptor(), CryptoStreamMode.Write);
			cryptoStream.Write(data);
			cryptoStream.Dispose();
			return destStream.ToArray();
		});


		/// <summary>
		/// Encrypt string.
		/// </summary>
		/// <param name="str">String to encrypt.</param>
		/// <returns>Encrypted string.</returns>
		public string Encrypt(string str)
		{
			if (string.IsNullOrEmpty(str))
				return "";
			return Encoding.UTF8.GetBytes(str).Let(it =>
			{
				return Convert.ToBase64String(this.Encrypt(it));
			});
		}
	}
}
