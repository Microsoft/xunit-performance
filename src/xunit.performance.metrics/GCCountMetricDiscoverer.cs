﻿using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Xunit.Performance.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Microsoft.Xunit.Performance
{
    class GCCountMetricDiscoverer : IPerformanceMetricDiscoverer
    {
        public IEnumerable<PerformanceMetric> GetMetrics(IAttributeInfo metricAttribute)
        {
            yield return new GCCountMetric();
        }

        class GCCountMetric : PerformanceMetric
        {
            public GCCountMetric() 
                : base("GCCount", "count", PerformanceMetricInterpretation.LowerIsBetter)
            {
            }

            public override IEnumerable<ProviderInfo> ProviderInfo
            {
                get
                {
                    yield return new UserProviderInfo()
                    {
                        ProviderGuid = ClrTraceEventParser.ProviderGuid,
                        Level = TraceEventLevel.Informational,
                        Keywords = (ulong)ClrTraceEventParser.Keywords.GC
                    };
                }
            }

            public override PerformanceMetricEvaluator CreateEvaluator(PerformanceMetricEvaluationContext context)
            {
                return new GCCountEvaluator(context);
            }
        }

        class GCCountEvaluator : PerformanceMetricEvaluator
        {
            private readonly PerformanceMetricEvaluationContext _context;
            private int _count;

            public GCCountEvaluator(PerformanceMetricEvaluationContext context)
            {
                _context = context;
                _context.TraceEventSource.Clr.GCStart += GCStart;
            }

            private void GCStart(GCStartTraceData ev)
            {
                _count++;
            }

            public override void BeginIteration(TraceEvent beginEvent)
            {
                _count = 0;
            }

            public override double EndIteration(TraceEvent endEvent)
            {
                return _count;
            }
        }
    }
}
