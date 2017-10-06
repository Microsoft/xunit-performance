﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System;
using System.Collections.Generic;
using System.Linq;
using static Microsoft.Xunit.Performance.Api.PerformanceLogger;

namespace Microsoft.Xunit.Performance.Api.Profilers.Etw
{
    /// <summary>
    /// Provides a simple interface to extract ETW process/modules data from an *.etl file.
    /// </summary>
    public sealed class SimpleEtwTraceEventParser
    {
        /// <summary>
        /// Gets profile data from the provided ScenarioInfo object
        /// </summary>
        /// <param name="scenarioInfo"></param>
        /// <returns>A collection of the profiled processes for the given scenario.</returns>
        /// <remarks>
        /// FIXME: Some assumptions:
        ///     1. The scenario launches a single process, but itself can launch child processes.
        ///     2. Process started/stopped within the ETW session.
        ///     3. Pmc source intervals were constant during the whole session.
        /// </remarks>
        public IReadOnlyCollection<EtwProcess> GetProfileData(ScenarioExecutionResult scenarioInfo)
        {
            var processes = new List<EtwProcess>();
            var pmcSourceIntervals = new Dictionary<int, long>();

            Func<int, bool> IsOurProcess = (processId) => {
                return processId == scenarioInfo.ProcessExitInfo.ProcessId || processes.Any(process => process.Id == processId);
            };
            Func<EtwModule, ImageLoadTraceData, bool> AreTheSameModule = (EtwModule module, ImageLoadTraceData obj) => {
                return module.AddressRange != null // Anonymously Hosted DynamicMethods Assembly
                    && module.AddressRange.Start == obj.ImageBase
                    && module.AddressRange.Size == obj.ImageSize
                    && module.Checksum == obj.ImageChecksum
                    && module.FullName == obj.FileName;
            };
            Func<EtwModule, PMCCounterProfTraceData, bool> IsWithinAddressAndTimeRange = (EtwModule module, PMCCounterProfTraceData obj) => {
                return module.AddressRange != null // Anonymously Hosted DynamicMethods Assembly
                    && module.AddressRange.IsWithinRange(obj.InstructionPointer)
                    && (module.LoadTimeStamp <= obj.TimeStamp && module.UnloadTimeStamp > obj.TimeStamp);
            };

            WriteDebugLine($"Parsing: {scenarioInfo.EventLogFileName}");
            using (var source = new ETWTraceEventSource(scenarioInfo.EventLogFileName))
            {
                if (source.EventsLost > 0)
                    throw new Exception($"Events were lost in trace '{scenarioInfo.EventLogFileName}'");

                ////////////////////////////////////////////////////////////////
                // Process data
                var parser = new KernelTraceEventParser(source);
                parser.ProcessStart += (ProcessTraceData obj) => {
                    if (IsOurProcess(obj.ProcessID) || IsOurProcess(obj.ParentID))
                    {
                        processes.Add(new EtwProcess {
                            Id = obj.ProcessID,
                            ParentId = obj.ParentID,
                            Name = obj.ImageFileName,
                            StartDateTime = obj.TimeStamp,
                            PerformanceMonitorCounterData = new Dictionary<int, long>(),
                            Modules = new List<EtwModule>(),
                        });
                    }
                };
                parser.ProcessStop += (ProcessTraceData obj) => {
                    if (IsOurProcess(obj.ProcessID))
                        processes.Single(process => process.Id == obj.ProcessID).ExitDateTime = obj.TimeStamp;
                };

                parser.ImageLoad += (ImageLoadTraceData obj) => {
                    var process = processes.SingleOrDefault(p => p.Id == obj.ProcessID);
                    if (process == null)
                        return;

                    var module = process.Modules
                        .SingleOrDefault(m => !m.IsLoaded && AreTheSameModule(m, obj));

                    // If the module was not in the list or the same module is loaded, then add it.
                    // Otherwise, the module was probably unloaded and reloaded.
                    if (module == null)
                    {
                        module = new EtwModule(obj.FileName, obj.ImageChecksum);
                        process.Modules.Add(module);
                    }

                    // Assuming nothing else has changed, and keeping the list of already measured Pmc.
                    module.IsLoaded = true;
                    module.AddressRange = new EtwAddressRange(obj.ImageBase, obj.ImageSize);
                    module.LoadTimeStamp = obj.TimeStamp;
                    module.UnloadTimeStamp = DateTime.MaxValue;
                };
                parser.ImageUnload += (ImageLoadTraceData obj) => {
                    var process = processes.SingleOrDefault(p => p.Id == obj.ProcessID);
                    if (process == null)
                        return;

                    // Check if the unloaded module is on the list.
                    // The module must be loaded, in the same address space, and same file name.
                    var module = process.Modules
                        .SingleOrDefault(m => m.IsLoaded && AreTheSameModule(m, obj));
                    if (module == null)
                        return;
                    module.IsLoaded = false;
                    module.UnloadTimeStamp = obj.TimeStamp;
                };

                ////////////////////////////////////////////////////////////////
                // PMC data
                var pmcRollovers = new List<EtwPerfInfoPMCSample>();
                parser.PerfInfoCollectionStart += (SampledProfileIntervalTraceData obj) => {
                    // Update the Pmc intervals.
                    if (scenarioInfo.PerformanceMonitorCounters.Any(pmc => pmc.Id == obj.SampleSource))
                    {
                        if (!pmcSourceIntervals.ContainsKey(obj.SampleSource))
                            pmcSourceIntervals.Add(obj.SampleSource, obj.NewInterval);
                        else
                            pmcSourceIntervals[obj.SampleSource] = obj.NewInterval;
                    }
                };
                parser.PerfInfoPMCSample += (PMCCounterProfTraceData obj) => {
                    // If this is our process and it is a pmc we care to measure.
                    var process = processes.SingleOrDefault(p => p.Id == obj.ProcessID);
                    if (process == null && scenarioInfo.PerformanceMonitorCounters.Any(pmc => pmc.Id == obj.ProfileSource))
                        return;

                    if (!process.PerformanceMonitorCounterData.ContainsKey(obj.ProfileSource))
                        process.PerformanceMonitorCounterData.Add(obj.ProfileSource, 0);
                    process.PerformanceMonitorCounterData[obj.ProfileSource] += pmcSourceIntervals[obj.ProfileSource];

                    if (process.Modules.Count() < 1)
                        return;

                    var module = process.Modules.SingleOrDefault(m => {
                        return m.IsLoaded && IsWithinAddressAndTimeRange(m, obj);
                    });

                    if (module == null)
                    {
                        // This might fall in managed code. We need to buffer and test it afterwards.
                        pmcRollovers.Add(new EtwPerfInfoPMCSample {
                            InstructionPointer = obj.InstructionPointer,
                            ProcessId = obj.ProcessID,
                            ProfileSourceId = obj.ProfileSource,
                            TimeStamp = obj.TimeStamp,
                        });
                        return;
                    }

                    if (!module.PerformanceMonitorCounterData.ContainsKey(obj.ProfileSource))
                        module.PerformanceMonitorCounterData.Add(obj.ProfileSource, 0);
                    module.PerformanceMonitorCounterData[obj.ProfileSource] += pmcSourceIntervals[obj.ProfileSource];
                };

                source.Process();

                return processes;
            }
        }
    }
}
