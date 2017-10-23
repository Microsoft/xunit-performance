// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Xunit.Performance.Api.Profilers.Etw
{
    /// <summary>
    /// Loaded module for the corresponding process.
    /// </summary>
    public class Module
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Module"/> class.
        /// </summary>
        /// <param name="fullName"></param>
        /// <param name="checksum"></param>
        public Module(string fullName, int checksum)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                throw new ArgumentNullException(nameof(fullName));

            FullName = fullName;
            Checksum = checksum;
            LifeSpan = new LifeSpan();

            PerformanceMonitorCounterData = new Dictionary<PerformanceMonitorCounter, long>();
        }

        /// <summary>
        /// The fully qualified name of the module file.
        /// </summary>
        public string FullName { get; }

        /// <summary>
        /// Module's checksum.
        /// </summary>
        public int Checksum { get; }

        /// <summary>
        /// TODO: Should PerformanceMonitorCounterData be exposed via a IReadOnlyDictionary?
        /// </summary>
        public IDictionary<PerformanceMonitorCounter, long> PerformanceMonitorCounterData { get; set; }

        /// <summary>
        /// Represents the address space where this module was loaded.
        /// </summary>
        internal AddressSpace AddressSpace { get; set; }

        /// <summary>
        /// Life span of this module (From the time it was loaded until the time it was unloaded).
        /// </summary>
        internal LifeSpan LifeSpan { get; }
    }
}
