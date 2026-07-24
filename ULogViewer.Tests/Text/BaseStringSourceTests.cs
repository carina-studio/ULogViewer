using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CarinaStudio.ULogViewer.Text;

/// <summary>
/// Base implementation of tests of <see cref="IStringSource"/>.
/// </summary>
abstract class BaseStringSourceTests
{
	// Static fields.
	static readonly string[] TestStrings =
	[
		"A",
		"abc",
		"Hello123",
		" \t  ",
		"中文測試",
		"日誌📋",
		"Hello, World! 這是測試。",
		"The quick brown fox jumps over the lazy dog. 0123456789 !@#$%^&*()",
		string.Concat(Enumerable.Repeat("Lorem ipsum dolor sit amet, consectetur adipiscing elit. ", 8)),
	];


	/// <summary>
	/// Test for number of bytes occupied by the source.
	/// </summary>
	[Test]
	public void ByteCountTest()
	{
		// check source with empty string
		Assert.That(this.CreateSource("").ByteCount, Is.GreaterThan(0L));

		// check sources with non-empty strings
		foreach (var s in this.CreateTestStrings())
			Assert.That(this.CreateSource(s).ByteCount, Is.GreaterThan(0L), $"Byte count of source of '{s}' should be positive.");
	}


	/// <summary>
	/// Test for copying non-empty string to empty buffer.
	/// </summary>
	[Test]
	public void CopyToEmptyBufferTest()
	{
		foreach (var s in this.CreateTestStrings())
			Assert.That(this.CreateSource(s).TryCopyTo(Span<char>.Empty), Is.False, $"Copying '{s}' to empty buffer should fail.");
	}


	/// <summary>
	/// Test for copying string to buffer with exactly same size.
	/// </summary>
	[Test]
	public void CopyToExactSizeBufferTest()
	{
		foreach (var s in this.CreateTestStrings())
		{
			// copy to buffer
			var source = this.CreateSource(s);
			var buffer = new char[s.Length];
			Assert.That(source.TryCopyTo(buffer.AsSpan()), Is.True, $"Copying '{s}' to buffer should succeed.");

			// check content
			Assert.That(new string(buffer), Is.EqualTo(s));
		}
	}


	/// <summary>
	/// Test for copying string to buffer which is larger than string.
	/// </summary>
	[Test]
	public void CopyToLargerBufferTest()
	{
		foreach (var s in this.CreateTestStrings())
		{
			// copy to buffer
			var source = this.CreateSource(s);
			var buffer = new char[s.Length + 16];
			Array.Fill(buffer, '\uffff');
			Assert.That(source.TryCopyTo(buffer.AsSpan()), Is.True, $"Copying '{s}' to buffer should succeed.");

			// check content
			Assert.That(new string(buffer, 0, s.Length), Is.EqualTo(s));
		}
	}


	/// <summary>
	/// Test for copying string to buffer which is smaller than string.
	/// </summary>
	[Test]
	public void CopyToSmallerBufferTest()
	{
		foreach (var s in this.CreateTestStrings().Where(it => it.Length >= 2 && it.All(char.IsAscii)))
		{
			// copy to buffer
			var source = this.CreateSource(s);
			var buffer = new char[s.Length - 1];
			Assert.That(source.TryCopyTo(buffer.AsSpan()), Is.True, $"Copying '{s}' to smaller buffer should succeed.");

			// check content
			Assert.That(new string(buffer), Is.EqualTo(s[..^1]), $"Content of buffer should be prefix of '{s}'.");
		}
	}


	/// <summary>
	/// Create source instance to be tested.
	/// </summary>
	/// <param name="s">String to be stored in source.</param>
	/// <returns><see cref="IStringSource"/>.</returns>
	protected abstract IStringSource CreateSource(string s);


	/// <summary>
	/// Create strings for testing which are supported by the implementation.
	/// </summary>
	/// <returns>List of strings for testing.</returns>
	protected IList<string> CreateTestStrings() =>
		TestStrings.Where(it => it.Length <= this.MaxSupportedLength).ToArray();


	/// <summary>
	/// Test for source with empty string.
	/// </summary>
	[Test]
	public void EmptyStringTest()
	{
		// create source
		var source = this.CreateSource("");

		// check state
		Assert.That(source.Length, Is.Zero);
		Assert.That(source.ToString(), Is.Empty);
		Assert.That(source.IsNullOrEmpty(), Is.True);

		// copy to empty buffer
		Assert.That(source.TryCopyTo(Span<char>.Empty), Is.True);

		// copy to non-empty buffer without modification
		var buffer = new char[4];
		Array.Fill(buffer, '\uffff');
		Assert.That(source.TryCopyTo(buffer.AsSpan()), Is.True);
		Assert.That(buffer, Is.All.EqualTo('\uffff'));
	}


	/// <summary>
	/// Test for checking whether source is null or empty.
	/// </summary>
	[Test]
	public void IsNullOrEmptyTest()
	{
		// check null source
		Assert.That(((IStringSource?)null).IsNullOrEmpty(), Is.True);

		// check source with empty string
		Assert.That(this.CreateSource("").IsNullOrEmpty(), Is.True);

		// check sources with non-empty strings
		foreach (var s in this.CreateTestStrings())
			Assert.That(this.CreateSource(s).IsNullOrEmpty(), Is.False, $"Source of '{s}' should not be treated as empty.");
	}


	/// <summary>
	/// Test for number of characters in source.
	/// </summary>
	[Test]
	public void LengthTest()
	{
		foreach (var s in this.CreateTestStrings())
			Assert.That(this.CreateSource(s).Length, Is.EqualTo(s.Length), $"Length of source of '{s}' should be same as string.");
	}


	/// <summary>
	/// Maximum number of characters supported by the implementation.
	/// </summary>
	protected virtual int MaxSupportedLength => int.MaxValue;


	/// <summary>
	/// Test for getting string from source.
	/// </summary>
	[Test]
	public void ToStringTest()
	{
		foreach (var s in this.CreateTestStrings())
		{
			// check string
			var source = this.CreateSource(s);
			Assert.That(source.ToString(), Is.EqualTo(s));

			// check string decoded repeatedly
			Assert.That(source.ToString(), Is.EqualTo(s), "Repeated ToString() should return same string.");
		}
	}
}
