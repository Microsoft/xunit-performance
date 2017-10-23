﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Xunit.Performance.Api.Profilers.Etw
{
    /// <summary>
    /// Loaded .NET module for the corresponding process.
    /// </summary>
    internal sealed class DotNetModule : Module
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetModule"/> class.
        /// </summary>
        /// <param name="fullName"></param>
        /// <param name="checksum"></param>
        /// <param name="id"></param>
        public DotNetModule(string fullName, int checksum, long id)
           : base(fullName, checksum)
        {
            Id = id;
            Methods = new List<DotNetMethod>();
        }

        /// <summary>
        /// Module Id.
        /// </summary>
        public long Id { get; }

        /// <summary>
        /// Collection of loaded <see cref="DotNetMethod"/> objects.
        /// </summary>
        internal IList<DotNetMethod> Methods { get; }
    }
}
