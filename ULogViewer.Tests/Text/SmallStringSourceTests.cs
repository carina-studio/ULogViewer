using NUnit.Framework;
using System;

namespace CarinaStudio.ULogViewer.Text;

/// <summary>
/// Tests of <see cref="SmallStringSource"/>.
/// </summary>
[TestFixture]
class SmallStringSourceTests : BaseStringSourceTests
{
	/// <summary>
	/// Test for storing strings with all supported lengths.
	/// </summary>
	[Test]
	public void AllSupportedLengthsTest()
	{
		for (var length = 0; length <= SmallStringSource.MaxLength; ++length)
		{
			// create source
			var s = "AbCdEfGh"[..length];
			var source = new SmallStringSource(s);

			// check state
			Assert.That(source.Length, Is.EqualTo(length));
			Assert.That(source.ToString(), Is.EqualTo(s));

			// copy to buffer
			if (length > 0)
			{
				var buffer = new char[length];
				Assert.That(source.TryCopyTo(buffer.AsSpan()), Is.True, $"Copying string with {length} character(s) to buffer should succeed.");
				Assert.That(new string(buffer), Is.EqualTo(s));
			}
		}
	}


	/// <summary>
	/// Test for creating sources through all constructor overloads.
	/// </summary>
	[Test]
	public void ConstructorOverloadsTest()
	{
		foreach (var s in this.CreateTestStrings())
		{
			Assert.That(new SmallStringSource(s).ToString(), Is.EqualTo(s), "Source created with string should contain same string.");
			Assert.That(new SmallStringSource(s.AsMemory()).ToString(), Is.EqualTo(s), "Source created with memory should contain same string.");
			Assert.That(new SmallStringSource(s.AsSpan()).ToString(), Is.EqualTo(s), "Source created with span should contain same string.");
		}
	}


	/// <inheritdoc/>
	protected override IStringSource CreateSource(string s) =>
		new SmallStringSource(s);


	/// <inheritdoc/>
	protected override int MaxSupportedLength => SmallStringSource.MaxLength;


	/// <summary>
	/// Test for creating source with string which is too long to be supported.
	/// </summary>
	[Test]
	public void OverLengthConstructionTest()
	{
		// create with string
		Assert.Throws<InvalidOperationException>(() => _ = new SmallStringSource(new string('x', SmallStringSource.MaxLength + 1)));

		// create with span
		Assert.Throws<InvalidOperationException>(() => _ = new SmallStringSource(new string('x', SmallStringSource.MaxLength + 1).AsSpan()));
	}
}
