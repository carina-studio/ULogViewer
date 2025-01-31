﻿using CarinaStudio.Collections;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSourceProvider"/> for <see cref="MemoryLoggerLogDataSource"/>.
/// </summary>
class MemoryLoggerLogDataSourceProvider(IULogViewerApplication app) : BaseLogDataSourceProvider(app)
{
	protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new MemoryLoggerLogDataSource(this);
	public override string Name => "MemoryLogger";
	public override ISet<string> RequiredSourceOptions { get; } = new HashSet<string>().AsReadOnly();
	public override ISet<string> SupportedSourceOptions { get; } = new HashSet<string>().AsReadOnly();
}
