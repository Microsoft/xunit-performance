// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;

namespace Microsoft.Xunit.Performance.Api.Profilers.Etw
{
    /// <summary>
    /// Provides a simple interface for ETW user providers.
    /// </summary>
    internal sealed class EtwUserProvider
    {
        static EtwUserProvider()
        {
            Defaults = new[] {
                new EtwUserProvider {
                    Guid = MicrosoftXunitBenchmarkTraceEventParser.ProviderGuid,
                    Keywords = ulong.MaxValue,
                    Level = TraceEventLevel.Verbose,
                },
                new EtwUserProvider {
                    Guid = ClrTraceEventParser.ProviderGuid,
                    Keywords = (ulong)(ClrTraceEventParser.Keywords.Exception | ClrTraceEventParser.Keywords.GC | ClrTraceEventParser.Keywords.Jit | ClrTraceEventParser.Keywords.JittedMethodILToNativeMap | ClrTraceEventParser.Keywords.Loader),
                    Level = TraceEventLevel.Verbose,
                },
            };
        }

        /// <summary>
        /// The Guid that represents the event provider enable.
        /// </summary>
        public Guid Guid { get; set; } = Guid.Empty;

        /// <summary>
        /// Verbosity to turn on.
        /// </summary>
        public TraceEventLevel Level { get; set; } = TraceEventLevel.Verbose;

        /// <summary>
        /// A bitvector representing the areas to turn on.
        /// Only the low 32 bits are used by classic providers and passed as the 'flags' value.
        /// Zero is a special value which is a provider defined default, which is usually 'everything'
        /// </summary>
        public ulong Keywords { get; set; } = ulong.MaxValue;

        /// <summary>
        /// Additional options for the provider
        /// </summary>
        public TraceEventProviderOptions Options { get; set; } = null;

        /// <summary>
        /// Default ETW user providers enabled by the xUnit-Performance Api.
        /// </summary>
        public static IReadOnlyCollection<EtwUserProvider> Defaults { get; set; }
    }
}
