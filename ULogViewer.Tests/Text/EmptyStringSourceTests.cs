using NUnit.Framework;
using System;

namespace CarinaStudio.ULogViewer.Text;

/// <summary>
/// Tests of <see cref="EmptyStringSource"/>.
/// </summary>
[TestFixture]
class EmptyStringSourceTests
{
	/// <summary>
	/// Test for number of bytes occupied by the source.
	/// </summary>
	[Test]
	public void ByteCountTest() =>
		Assert.That(IStringSource.Empty.ByteCount, Is.GreaterThan(0L));


	/// <summary>
	/// Test for default empty source instance.
	/// </summary>
	[Test]
	public void DefaultInstanceTest() =>
		Assert.That(IStringSource.Empty, Is.InstanceOf<EmptyStringSource>());


	/// <summary>
	/// Test for checking whether source is null or empty.
	/// </summary>
	[Test]
	public void IsNullOrEmptyTest()
	{
		// check null source
		Assert.That(((IStringSource?)null).IsNullOrEmpty(), Is.True);

		// check empty source
		Assert.That(IStringSource.Empty.IsNullOrEmpty(), Is.True);
	}


	/// <summary>
	/// Test for number of characters in source.
	/// </summary>
	[Test]
	public void LengthTest() =>
		Assert.That(IStringSource.Empty.Length, Is.Zero);


	/// <summary>
	/// Test for getting string from source.
	/// </summary>
	[Test]
	public void ToStringTest() =>
		Assert.That(IStringSource.Empty.ToString(), Is.Empty);


	/// <summary>
	/// Test for copying string to buffer.
	/// </summary>
	[Test]
	public void TryCopyToTest()
	{
		// copy to empty buffer
		Assert.That(IStringSource.Empty.TryCopyTo(Span<char>.Empty), Is.True);

		// copy to non-empty buffer without modification
		var buffer = new char[4];
		Array.Fill(buffer, '\uffff');
		Assert.That(IStringSource.Empty.TryCopyTo(buffer.AsSpan()), Is.True);
		Assert.That(buffer, Is.All.EqualTo('\uffff'));
	}
}
