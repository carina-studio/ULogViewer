using CarinaStudio.ULogViewer.Text;
using NUnit.Framework;
using System;
using System.Text;

namespace CarinaStudio.ULogViewer.Logs;

/// <summary>
/// Tests of <see cref="LogBuilder"/>.
/// </summary>
[TestFixture]
class LogBuilderTests
{
	/// <summary>
	/// Test for appending values to properties.
	/// </summary>
	[Test]
	public void AppendTest()
	{
		// append to property which has not been set yet
		var builder = new LogBuilder();
		builder.Append(nameof(Log.Message), "abc");
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _)?.ToString(), Is.EqualTo("abc"));
		builder.Append(nameof(Log.Summary), "abc".AsMemory());
		Assert.That(builder.GetStringOrNull(nameof(Log.Summary), out _)?.ToString(), Is.EqualTo("abc"));

		// append to property which was set as string
		builder = new LogBuilder();
		builder.Set(nameof(Log.Message), "abc");
		builder.Append(nameof(Log.Message), "def");
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _)?.ToString(), Is.EqualTo("abcdef"));
		builder.Append(nameof(Log.Message), "ghi".AsMemory());
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _)?.ToString(), Is.EqualTo("abcdefghi"));

		// append to property which was set as memory
		builder = new LogBuilder();
		builder.Set(nameof(Log.Message), "abc".AsMemory());
		builder.Append(nameof(Log.Message), "def");
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _)?.ToString(), Is.EqualTo("abcdef"), "Value appended to property which was set as memory should be kept.");
		builder.Append(nameof(Log.Message), "ghi".AsMemory());
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _)?.ToString(), Is.EqualTo("abcdefghi"), "Value appended to property which was set as memory should be kept.");

		// append empty value
		builder = new LogBuilder();
		builder.Set(nameof(Log.Message), "abc");
		builder.Append(nameof(Log.Message), "");
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _)?.ToString(), Is.EqualTo("abc"));
	}


	/// <summary>
	/// Test for appending values to next line of properties.
	/// </summary>
	[Test]
	public void AppendToNextLineTest()
	{
		// append to property which has not been set yet
		var builder = new LogBuilder();
		builder.AppendToNextLine(nameof(Log.Message), "line1");
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _)?.ToString(), Is.EqualTo("line1"));
		builder.AppendToNextLine(nameof(Log.Message), "line2");
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _)?.ToString(), Is.EqualTo("line1\nline2"));
		builder.AppendToNextLine(nameof(Log.Message), "line3".AsMemory());
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _)?.ToString(), Is.EqualTo("line1\nline2\nline3"));

		// skip first line if it is empty or white space
		builder = new LogBuilder();
		builder.Set(nameof(Log.Message), "  ");
		builder.AppendToNextLine(nameof(Log.Message), "line1");
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _)?.ToString(), Is.EqualTo("line1"), "White space first line should be dropped.");

		// keep first line even if it is empty
		builder = new LogBuilder();
		builder.Set(nameof(Log.Message), "");
		builder.AppendToNextLine(nameof(Log.Message), "line1", false);
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _)?.ToString(), Is.EqualTo("\nline1"), "Empty first line should be kept.");

		// append to property which was set as memory
		builder = new LogBuilder();
		builder.Set(nameof(Log.Message), "line1".AsMemory());
		builder.AppendToNextLine(nameof(Log.Message), "line2");
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _)?.ToString(), Is.EqualTo("line1\nline2"), "Value appended to property which was set as memory should be kept.");
		builder.AppendToNextLine(nameof(Log.Message), "line3".AsMemory());
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _)?.ToString(), Is.EqualTo("line1\nline2\nline3"), "Value appended to property which was set as memory should be kept.");

		// skip first line which was set as white space memory
		builder = new LogBuilder();
		builder.Set(nameof(Log.Message), " ".AsMemory());
		builder.AppendToNextLine(nameof(Log.Message), "line1".AsMemory());
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _)?.ToString(), Is.EqualTo("line1"), "White space first line should be dropped.");
	}


	/// <summary>
	/// Test for building logs.
	/// </summary>
	[Test]
	public void BuildAndResetTest()
	{
		// build log
		var timestamp = new DateTime(2026, 7, 26, 13, 45, 30);
		var builder = new LogBuilder
		{
			DefaultLogLevel = LogLevel.Info,
		};
		builder.Set(nameof(Log.Message), "message of log");
		builder.Set(nameof(Log.Timestamp), timestamp.ToBinary().ToString());
		builder.Set(nameof(Log.ProcessId), "1234");
		builder.Set(nameof(Log.TimeSpan), "1500");
		var log = builder.Build();

		// check built log
		Assert.That(log.Message?.ToString(), Is.EqualTo("message of log"));
		Assert.That(log.Timestamp, Is.EqualTo(timestamp));
		Assert.That(log.ProcessId, Is.EqualTo(1234));
		Assert.That(log.TimeSpan, Is.EqualTo(TimeSpan.FromMilliseconds(1500)));
		Assert.That(log.Level, Is.EqualTo(LogLevel.Info), "Level should be the default level of builder.");

		// check that builder is not reset by building
		Assert.That(builder.IsNotEmpty(), "Properties should be kept after building log.");

		// build log and reset builder
		builder.Set(nameof(Log.Level), nameof(LogLevel.Error));
		log = builder.BuildAndReset();
		Assert.That(log.Message?.ToString(), Is.EqualTo("message of log"), "Built log should keep its properties after resetting builder.");
		Assert.That(log.Level, Is.EqualTo(LogLevel.Error));
		Assert.That(builder.IsEmpty(), "Properties should be cleared after building log and resetting.");
	}


	// Create string with given length for testing.
	static string CreateTestString(int length)
	{
		var buffer = new StringBuilder(length);
		for (var i = 0; i < length; ++i)
			buffer.Append((char)('a' + (i % 26)));
		return buffer.ToString();
	}


	/// <summary>
	/// Test for getting properties as <see cref="DateTime"/>.
	/// </summary>
	[Test]
	public void GetDateTimeOrNullTest()
	{
		// get value which was set as text
		var builder = new LogBuilder();
		builder.Set(nameof(Log.Timestamp), "2026-07-26 13:45:30");
		Assert.That(builder.GetDateTimeOrNull(nameof(Log.Timestamp)), Is.EqualTo(new DateTime(2026, 7, 26, 13, 45, 30)));

		// get value which was set as binary data
		var timestamp = new DateTime(2026, 7, 26, 13, 45, 30);
		builder.Set(nameof(Log.BeginningTimestamp), timestamp.ToBinary().ToString());
		Assert.That(builder.GetDateTimeOrNull(nameof(Log.BeginningTimestamp)), Is.EqualTo(timestamp));

		// get value which cannot be parsed
		builder.Set(nameof(Log.EndingTimestamp), "not a timestamp");
		Assert.That(builder.GetDateTimeOrNull(nameof(Log.EndingTimestamp)), Is.Null);

		// get value which has not been set
		Assert.That(new LogBuilder().GetDateTimeOrNull(nameof(Log.Timestamp)), Is.Null);

		// get value which is out of range of date time
		builder.Set(nameof(Log.Timestamp), "4000000000000000000");
		Assert.That(builder.GetDateTimeOrNull(nameof(Log.Timestamp)), Is.Null, "Out of range binary data should be treated as unparseable value.");
		builder.Set(nameof(Log.Timestamp), long.MaxValue.ToString());
		Assert.That(builder.GetDateTimeOrNull(nameof(Log.Timestamp)), Is.Null, "Out of range binary data should be treated as unparseable value.");
	}


	/// <summary>
	/// Test for getting properties as enumeration.
	/// </summary>
	[Test]
	public void GetEnumOrNullTest()
	{
		// get value which was set as text
		var builder = new LogBuilder();
		builder.Set(nameof(Log.Level), nameof(LogLevel.Warn));
		Assert.That(builder.GetEnumOrNull<LogLevel>(nameof(Log.Level)), Is.EqualTo(LogLevel.Warn));

		// get default level if value cannot be parsed
		builder.DefaultLogLevel = LogLevel.Error;
		builder.Set(nameof(Log.Level), "no such level");
		Assert.That(builder.GetEnumOrNull<LogLevel>(nameof(Log.Level)), Is.EqualTo(LogLevel.Error), "Default level should be returned for unparseable value.");

		// get default level if value has not been set
		builder = new LogBuilder
		{
			DefaultLogLevel = LogLevel.Debug,
		};
		Assert.That(builder.GetEnumOrNull<LogLevel>(nameof(Log.Level)), Is.EqualTo(LogLevel.Debug), "Default level should be returned if level has not been set.");

		// get value of property other than level
		builder.Set(nameof(Log.Category), nameof(LogLevel.Verbose));
		Assert.That(builder.GetEnumOrNull<LogLevel>(nameof(Log.Category)), Is.EqualTo(LogLevel.Verbose));
		builder.Set(nameof(Log.Category), "no such level");
		Assert.That(builder.GetEnumOrNull<LogLevel>(nameof(Log.Category)), Is.Null, "Default level should not be applied to property other than level.");
	}


	/// <summary>
	/// Test for getting properties as <see cref="int"/>.
	/// </summary>
	[Test]
	public void GetInt32OrNullTest()
	{
		// get decimal value
		var builder = new LogBuilder();
		builder.Set(nameof(Log.ProcessId), "1234");
		Assert.That(builder.GetInt32OrNull(nameof(Log.ProcessId)), Is.EqualTo(1234));
		builder.Set(nameof(Log.ProcessId), "-1234");
		Assert.That(builder.GetInt32OrNull(nameof(Log.ProcessId)), Is.EqualTo(-1234));

		// get hexadecimal value
		builder.Set(nameof(Log.ThreadId), "0x1f");
		Assert.That(builder.GetInt32OrNull(nameof(Log.ThreadId)), Is.EqualTo(0x1f));
		builder.Set(nameof(Log.ThreadId), "0xffffffff");
		Assert.That(builder.GetInt32OrNull(nameof(Log.ThreadId)), Is.EqualTo(-1));

		// get value which was set as memory
		builder.Set(nameof(Log.LineNumber), "5678".AsMemory());
		Assert.That(builder.GetInt32OrNull(nameof(Log.LineNumber)), Is.EqualTo(5678));

		// get value which cannot be parsed
		builder.Set(nameof(Log.ProcessId), "not a number");
		Assert.That(builder.GetInt32OrNull(nameof(Log.ProcessId)), Is.Null);
		builder.Set(nameof(Log.ProcessId), "99999999999999");
		Assert.That(builder.GetInt32OrNull(nameof(Log.ProcessId)), Is.Null, "Out of range value should be treated as unparseable value.");

		// get value which has not been set
		Assert.That(new LogBuilder().GetInt32OrNull(nameof(Log.ProcessId)), Is.Null);
	}


	/// <summary>
	/// Test for getting properties as <see cref="long"/>.
	/// </summary>
	[Test]
	public void GetInt64OrNullTest()
	{
		// get decimal value
		var builder = new LogBuilder();
		builder.Set(nameof(Log.ProcessId), "1234567890123");
		Assert.That(builder.GetInt64OrNull(nameof(Log.ProcessId)), Is.EqualTo(1234567890123L));

		// get hexadecimal value
		builder.Set(nameof(Log.ThreadId), "0x1f");
		Assert.That(builder.GetInt64OrNull(nameof(Log.ThreadId)), Is.EqualTo(0x1fL));
		builder.Set(nameof(Log.ThreadId), "0x1fL");
		Assert.That(builder.GetInt64OrNull(nameof(Log.ThreadId)), Is.EqualTo(0x1fL), "Hexadecimal value with 'L' suffix should be parsed.");

		// get value which cannot be parsed
		builder.Set(nameof(Log.ProcessId), "not a number");
		Assert.That(builder.GetInt64OrNull(nameof(Log.ProcessId)), Is.Null);

		// get value which has not been set
		Assert.That(new LogBuilder().GetInt64OrNull(nameof(Log.ProcessId)), Is.Null);
	}


	/// <summary>
	/// Test for getting properties as <see cref="TimeSpan"/>.
	/// </summary>
	[Test]
	public void GetTimeSpanOrNullTest()
	{
		// get value in milliseconds
		var builder = new LogBuilder();
		builder.Set(nameof(Log.TimeSpan), "1500.5");
		Assert.That(builder.GetTimeSpanOrNull(nameof(Log.TimeSpan)), Is.EqualTo(TimeSpan.FromMilliseconds(1500.5)));

		// get value in time span format
		builder.Set(nameof(Log.BeginningTimeSpan), "01:02:03");
		Assert.That(builder.GetTimeSpanOrNull(nameof(Log.BeginningTimeSpan)), Is.EqualTo(new TimeSpan(1, 2, 3)));

		// get value which cannot be parsed
		builder.Set(nameof(Log.EndingTimeSpan), "not a time span");
		Assert.That(builder.GetTimeSpanOrNull(nameof(Log.EndingTimeSpan)), Is.Null);

		// get value which has not been set
		Assert.That(new LogBuilder().GetTimeSpanOrNull(nameof(Log.TimeSpan)), Is.Null);

		// get value which is not a number or out of range of time span
		builder.Set(nameof(Log.TimeSpan), "NaN");
		Assert.That(builder.GetTimeSpanOrNull(nameof(Log.TimeSpan)), Is.Null, "Not-a-number value should be treated as unparseable value.");
		builder.Set(nameof(Log.TimeSpan), "1e308");
		Assert.That(builder.GetTimeSpanOrNull(nameof(Log.TimeSpan)), Is.Null, "Out of range value should be treated as unparseable value.");
	}


	/// <summary>
	/// Test for selecting implementation of <see cref="IStringSource"/> by memory usage policy.
	/// </summary>
	[Test]
	public void MemoryUsagePolicyTest()
	{
		// check default policy
		var builder = new LogBuilder();
		Assert.That(builder.MemoryUsagePolicy, Is.EqualTo(MemoryUsagePolicy.Balance));

		// check implementations selected by each policy
		(MemoryUsagePolicy Policy, Type SmallType, Type ShortType, Type MediumType, Type LongType)[] expectedTypes =
		[
			(MemoryUsagePolicy.Balance, typeof(SmallStringSource), typeof(CompressedStringSource), typeof(Utf8StringSource), typeof(CompressedStringSource)),
			(MemoryUsagePolicy.BetterPerformance, typeof(SmallStringSource), typeof(Utf8StringSource), typeof(SimpleStringSource), typeof(Utf8StringSource)),
			(MemoryUsagePolicy.LessMemoryUsage, typeof(SmallStringSource), typeof(CompressedStringSource), typeof(CompressedStringSource), typeof(CompressedStringSource)),
		];
		foreach (var (policy, smallType, shortType, mediumType, longType) in expectedTypes)
		{
			builder.MemoryUsagePolicy = policy;
			Assert.That(builder.MemoryUsagePolicy, Is.EqualTo(policy));
			(int Length, Type ExpectedType)[] expectedTypesByLength =
			[
				(SmallStringSource.MaxLength, smallType),
				(64, shortType),
				(128, mediumType),
				(300, longType),
			];
			foreach (var (length, expectedType) in expectedTypesByLength)
			{
				var s = CreateTestString(length);
				builder.Set(nameof(Log.Message), s);
				var source = builder.GetStringOrNull(nameof(Log.Message), out _);
				Assert.That(source, Is.TypeOf(expectedType), $"Unexpected implementation for string with {length} character(s) and {policy} policy.");
				Assert.That(source?.ToString(), Is.EqualTo(s));
			}
		}

		// check empty string
		builder.Set(nameof(Log.Message), "");
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _), Is.SameAs(IStringSource.Empty));
	}


	/// <summary>
	/// Test for enumerating properties in builder.
	/// </summary>
	[Test]
	public void PropertyEnumerationTest()
	{
		// check state of empty builder
		var builder = new LogBuilder();
		Assert.That(builder.IsEmpty());
		Assert.That(builder.IsNotEmpty(), Is.False);
		Assert.That(builder.PropertyCount, Is.Zero);
		Assert.That(builder.PropertyNames, Is.Empty);

		// set properties
		builder.Set(nameof(Log.Message), "message");
		builder.Set(nameof(Log.Summary), "summary".AsMemory());
		builder.Set(nameof(Log.Category), new SmallStringSource("category"));
		Assert.That(builder.IsEmpty(), Is.False);
		Assert.That(builder.IsNotEmpty());
		Assert.That(builder.PropertyCount, Is.EqualTo(3));
		Assert.That(builder.PropertyNames, Is.EquivalentTo([ nameof(Log.Message), nameof(Log.Summary), nameof(Log.Category) ]));

		// override property
		builder.Set(nameof(Log.Message), "another message");
		Assert.That(builder.PropertyCount, Is.EqualTo(3), "Number of properties should be kept after overriding property.");
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _)?.ToString(), Is.EqualTo("another message"));
	}


	/// <summary>
	/// Test for resetting builder.
	/// </summary>
	[Test]
	public void ResetTest()
	{
		// setup builder
		var stringCache = new StringSourceCache();
		var builder = new LogBuilder
		{
			DefaultLogLevel = LogLevel.Warn,
			MemoryUsagePolicy = MemoryUsagePolicy.LessMemoryUsage,
			StringCache = stringCache,
		};
		builder.Set(nameof(Log.Message), "message");
		builder.Set(nameof(Log.ProcessId), "1234");

		// reset builder
		builder.Reset();

		// check that all properties were cleared
		Assert.That(builder.IsEmpty());
		Assert.That(builder.PropertyCount, Is.Zero);
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _), Is.Null);
		Assert.That(builder.GetInt32OrNull(nameof(Log.ProcessId)), Is.Null);

		// check that configuration was kept
		Assert.That(builder.DefaultLogLevel, Is.EqualTo(LogLevel.Warn));
		Assert.That(builder.MemoryUsagePolicy, Is.EqualTo(MemoryUsagePolicy.LessMemoryUsage));
		Assert.That(builder.StringCache, Is.SameAs(stringCache));
	}


	/// <summary>
	/// Test for setting and getting properties as string.
	/// </summary>
	[Test]
	public void SetAndGetStringTest()
	{
		// set and get value as string
		var builder = new LogBuilder();
		builder.Set(nameof(Log.Message), "message of log");
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _)?.ToString(), Is.EqualTo("message of log"));

		// set and get value as memory
		builder.Set(nameof(Log.Summary), "summary of log".AsMemory());
		Assert.That(builder.GetStringOrNull(nameof(Log.Summary), out _)?.ToString(), Is.EqualTo("summary of log"));

		// set and get value as string source
		var stringSource = new SmallStringSource("category");
		builder.Set(nameof(Log.Category), stringSource);
		Assert.That(builder.GetStringOrNull(nameof(Log.Category), out var fromCache), Is.SameAs(stringSource), "String source should be used directly.");
		Assert.That(fromCache, Is.False);

		// override value
		builder.Set(nameof(Log.Message), "another message");
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _)?.ToString(), Is.EqualTo("another message"));

		// get long value
		var longString = CreateTestString(1024);
		builder.Set(nameof(Log.Message), longString);
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _)?.ToString(), Is.EqualTo(longString));

		// get empty value
		builder.Set(nameof(Log.Message), "");
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _), Is.SameAs(IStringSource.Empty));
		builder.Set(nameof(Log.Message), ReadOnlyMemory<char>.Empty);
		Assert.That(builder.GetStringOrNull(nameof(Log.Message), out _), Is.SameAs(IStringSource.Empty));

		// get value which has not been set
		Assert.That(new LogBuilder().GetStringOrNull(nameof(Log.Message), out _), Is.Null);
	}


	/// <summary>
	/// Test for sharing string sources of small strings between builders.
	/// </summary>
	[Test]
	public void SharedSmallStringCacheTest()
	{
		// get same string source for same small ASCII string
		var builder1 = new LogBuilder();
		var builder2 = new LogBuilder();
		foreach (var s in (string[])[ "D", "OK" ])
		{
			builder1.Set(nameof(Log.Message), s);
			builder2.Set(nameof(Log.Message), s.AsMemory());
			var source1 = builder1.GetStringOrNull(nameof(Log.Message), out var fromCache1);
			var source2 = builder2.GetStringOrNull(nameof(Log.Message), out var fromCache2);
			Assert.That(source1?.ToString(), Is.EqualTo(s));
			Assert.That(source1, Is.SameAs(source2), $"String source of '{s}' should be shared between builders.");
			Assert.That(fromCache1);
			Assert.That(fromCache2);
		}

		// get different string sources for small non-ASCII string
		builder1.Set(nameof(Log.Message), "中文");
		builder2.Set(nameof(Log.Message), "中文");
		var nonAsciiSource1 = builder1.GetStringOrNull(nameof(Log.Message), out var nonAsciiFromCache1);
		var nonAsciiSource2 = builder2.GetStringOrNull(nameof(Log.Message), out _);
		Assert.That(nonAsciiSource1?.ToString(), Is.EqualTo("中文"));
		Assert.That(nonAsciiSource1, Is.Not.SameAs(nonAsciiSource2), "String source of non-ASCII string should not be shared between builders.");
		Assert.That(nonAsciiFromCache1, Is.False);

		// get different string sources for single character string and the string with same leading character
		builder1.Set(nameof(Log.Message), "~");
		var singleCharSource = builder1.GetStringOrNull(nameof(Log.Message), out _);
		builder1.Set(nameof(Log.Message), "~\0");
		var twoCharsSource = builder1.GetStringOrNull(nameof(Log.Message), out _);
		Assert.That(singleCharSource?.ToString(), Is.EqualTo("~"));
		Assert.That(twoCharsSource?.ToString(), Is.EqualTo("~\0"), "String source of two characters string should not be shared with single character string.");
	}


	/// <summary>
	/// Test for caching string sources by <see cref="StringSourceCache"/>.
	/// </summary>
	[Test]
	public void StringCacheTest()
	{
		// get same string source for same value
		var stringCache = new StringSourceCache();
		var builder = new LogBuilder
		{
			StringCache = stringCache,
		};
		var cachedString = CreateTestString(32);
		builder.Set(nameof(Log.Message), cachedString);
		var source = builder.GetStringOrNull(nameof(Log.Message), out var fromCache);
		Assert.That(source?.ToString(), Is.EqualTo(cachedString));
		Assert.That(fromCache);
		builder.Set(nameof(Log.Summary), cachedString.AsMemory());
		Assert.That(builder.GetStringOrNull(nameof(Log.Summary), out fromCache), Is.SameAs(source), "String source should be got from cache.");
		Assert.That(fromCache);

		// get string source from cache shared with another builder
		var anotherBuilder = new LogBuilder
		{
			StringCache = stringCache,
		};
		anotherBuilder.Set(nameof(Log.Message), cachedString);
		Assert.That(anotherBuilder.GetStringOrNull(nameof(Log.Message), out _), Is.SameAs(source), "String source should be shared between builders with same cache.");

		// get different string sources for value which is too long to be cached
		var uncachedString = CreateTestString(33);
		builder.Set(nameof(Log.Error), uncachedString);
		var uncachedSource = builder.GetStringOrNull(nameof(Log.Error), out fromCache);
		Assert.That(uncachedSource?.ToString(), Is.EqualTo(uncachedString));
		Assert.That(fromCache, Is.False);
		builder.Set(nameof(Log.Warning), uncachedString);
		Assert.That(builder.GetStringOrNull(nameof(Log.Warning), out _), Is.Not.SameAs(uncachedSource), "Long string source should not be cached.");

		// get value which cannot be kept by cache
		var fullStringCache = new StringSourceCache
		{
			MaxByteCount = 1,
		};
		var builderWithFullCache = new LogBuilder
		{
			StringCache = fullStringCache,
		};
		builderWithFullCache.Set(nameof(Log.Message), cachedString);
		var evictedSource = builderWithFullCache.GetStringOrNull(nameof(Log.Message), out fromCache);
		Assert.That(evictedSource?.ToString(), Is.EqualTo(cachedString));
		Assert.That(fromCache, Is.False, "String source which was not kept by cache should not be reported as cached one.");
		Assert.That(builderWithFullCache.GetStringOrNull(nameof(Log.Message), out _), Is.Not.SameAs(evictedSource));
	}
}
