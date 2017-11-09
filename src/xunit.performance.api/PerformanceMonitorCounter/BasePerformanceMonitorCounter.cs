﻿using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Xunit.Performance.Sdk;
using System.Collections.Generic;

namespace Microsoft.Xunit.Performance.Api
{
    internal abstract class BasePerformanceMonitorCounter : PerformanceMetric
    {
        public BasePerformanceMonitorCounter(IPerformanceMonitorCounter pmc) : base(pmc.Name, pmc.DisplayName, pmc.Unit)
        {
            _interval = pmc.Interval;
            _profileSourceInfoID = GetProfileSourceInfoId(Id);
        }

        public override IEnumerable<ProviderInfo> ProviderInfo
        {
            get
            {
                yield return new KernelProviderInfo() {
                    Keywords = unchecked((ulong)KernelTraceEventParser.Keywords.PMCProfile),
                    StackKeywords = unchecked((ulong)KernelTraceEventParser.Keywords.PMCProfile),
                };
                yield return new CpuCounterInfo() {
                    CounterName = Id,
                    Interval = _interval,
                };
            }
        }

        public bool IsValidPmc => _profileSourceInfoID > -1;

        protected int ProfileSourceInfoID => _profileSourceInfoID;

        private readonly int _interval;
        private readonly int _profileSourceInfoID;
    }
}
