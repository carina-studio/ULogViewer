using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Text;

/// <summary>
/// Tests of <see cref="CompressedStringSource"/>.
/// </summary>
[TestFixture]
class CompressedStringSourceTests : BaseStringSourceTests
{
	/// <summary>
	/// Test for storing string which can be compressed effectively.
	/// </summary>
	[Test]
	public void CompressibleStringTest()
	{
		// create source
		var s = string.Concat(Enumerable.Repeat("ULogViewer compressible string. ", 64));
		var source = new CompressedStringSource(s);

		// check compression
		Assert.That(source.ByteCount, Is.LessThan(s.Length), "String should be stored as compressed data.");

		// check round trip
		Assert.That(source.ToString(), Is.EqualTo(s));
		var buffer = new char[s.Length];
		Assert.That(source.TryCopyTo(buffer.AsSpan()), Is.True);
		Assert.That(new string(buffer), Is.EqualTo(s));
	}


	/// <summary>
	/// Test for decoding string from same source on multiple threads concurrently.
	/// </summary>
	[Test]
	public void ConcurrentToStringTest()
	{
		// create source
		var s = string.Concat(Enumerable.Repeat("ULogViewer concurrent decompression. ", 64));
		var source = new CompressedStringSource(s);

		// decode concurrently
		var errorCount = 0;
		var tasks = new Task[8];
		for (var i = 0; i < tasks.Length; ++i)
		{
			tasks[i] = Task.Run(() =>
			{
				for (var j = 0; j < 100; ++j)
				{
					if (source.ToString() != s)
						Interlocked.Increment(ref errorCount);
				}
			}, CancellationToken.None);
		}

		// check result
		Assert.That(Task.WaitAll(tasks, 30000, CancellationToken.None), Is.True, "Concurrent decoding cannot be completed in expected duration.");
		Assert.That(errorCount, Is.Zero, "All concurrent decoding should return same string.");
	}


	/// <summary>
	/// Test for creating sources through all constructor overloads.
	/// </summary>
	[Test]
	public void ConstructorOverloadsTest()
	{
		foreach (var s in this.CreateTestStrings())
		{
			Assert.That(new CompressedStringSource(s).ToString(), Is.EqualTo(s), "Source created with string should contain same string.");
			Assert.That(new CompressedStringSource(s.AsMemory()).ToString(), Is.EqualTo(s), "Source created with memory should contain same string.");
			Assert.That(new CompressedStringSource(s.AsSpan()).ToString(), Is.EqualTo(s), "Source created with span should contain same string.");
		}
	}


	/// <inheritdoc/>
	protected override IStringSource CreateSource(string s) =>
		new CompressedStringSource(s);


	/// <summary>
	/// Test for storing string which cannot be compressed effectively.
	/// </summary>
	[Test]
	public void IncompressibleStringTest()
	{
		// create source
		var s = "abc123";
		var source = new CompressedStringSource(s);

		// check round trip
		Assert.That(source.ToString(), Is.EqualTo(s));

		// copy to larger buffer without touching part beyond string length
		var buffer = new char[s.Length + 10];
		Array.Fill(buffer, '\uffff');
		Assert.That(source.TryCopyTo(buffer.AsSpan()), Is.True);
		Assert.That(new string(buffer, 0, s.Length), Is.EqualTo(s));
		for (var i = s.Length; i < buffer.Length; ++i)
			Assert.That(buffer[i], Is.EqualTo('\uffff'), "Part of buffer beyond string length should not be modified.");
	}
}
