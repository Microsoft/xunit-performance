﻿using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.MicrosoftXunitBenchmark;
using Microsoft.Xunit.Performance.Sdk;

namespace Microsoft.Xunit.Performance.Api
{
    internal class GCAllocatedBytesForCurrentThreadEvaluator : PerformanceMetricEvaluator
    {
        public override void BeginIteration(TraceEvent traceEvent)
        {
        }

        public override double EndIteration(TraceEvent traceEvent)
        {
            return ((BenchmarkIterationStopArgs)traceEvent).AllocatedBytes;
        }
    }
}
