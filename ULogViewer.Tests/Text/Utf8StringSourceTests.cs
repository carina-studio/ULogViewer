using NUnit.Framework;
using System;

namespace CarinaStudio.ULogViewer.Text;

/// <summary>
/// Tests of <see cref="Utf8StringSource"/>.
/// </summary>
[TestFixture]
class Utf8StringSourceTests : BaseStringSourceTests
{
	/// <summary>
	/// Test for creating sources through all constructor overloads.
	/// </summary>
	[Test]
	public void ConstructorOverloadsTest()
	{
		foreach (var s in this.CreateTestStrings())
		{
			Assert.That(new Utf8StringSource(s).ToString(), Is.EqualTo(s), "Source created with string should contain same string.");
			Assert.That(new Utf8StringSource(s.AsMemory()).ToString(), Is.EqualTo(s), "Source created with memory should contain same string.");
			Assert.That(new Utf8StringSource(s.AsSpan()).ToString(), Is.EqualTo(s), "Source created with span should contain same string.");
		}
	}


	/// <inheritdoc/>
	protected override IStringSource CreateSource(string s) =>
		new Utf8StringSource(s);
}
