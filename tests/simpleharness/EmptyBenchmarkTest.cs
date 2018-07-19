﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Xunit.Performance;

namespace simpleharness
{
    public class EmptyBenchmarkTest
    {
        [Benchmark]
        public static void Implementation_1()
        {
            foreach (BenchmarkIteration iter in Benchmark.Iterations)
            {
                using (iter.StartMeasurement())
                {
                }
            }
        }

        [Benchmark]
        public static void Implementation_2()
        {
            Benchmark.Iterate(() => { /*do nothing*/ });
        }
    }
}
