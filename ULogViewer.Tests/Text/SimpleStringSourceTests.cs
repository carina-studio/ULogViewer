using NUnit.Framework;
using System;

namespace CarinaStudio.ULogViewer.Text;

/// <summary>
/// Tests of <see cref="SimpleStringSource"/>.
/// </summary>
[TestFixture]
class SimpleStringSourceTests : BaseStringSourceTests
{
	/// <summary>
	/// Test for creating sources through all constructor overloads.
	/// </summary>
	[Test]
	public void ConstructorOverloadsTest()
	{
		foreach (var s in this.CreateTestStrings())
		{
			Assert.That(new SimpleStringSource(s).ToString(), Is.EqualTo(s), "Source created with string should contain same string.");
			Assert.That(new SimpleStringSource(s.AsMemory()).ToString(), Is.EqualTo(s), "Source created with memory should contain same string.");
			Assert.That(new SimpleStringSource(s.AsSpan()).ToString(), Is.EqualTo(s), "Source created with span should contain same string.");
		}
	}


	/// <inheritdoc/>
	protected override IStringSource CreateSource(string s) =>
		new SimpleStringSource(s);
}
