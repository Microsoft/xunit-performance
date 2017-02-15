﻿using CommandLine;
using System;
using System.IO;
using System.Linq;

namespace Microsoft.Xunit.Performance.Api
{
    sealed class XunitPerformanceHarnessOptions
    {
        public XunitPerformanceHarnessOptions()
        {
            _outputDirectory = Directory.GetCurrentDirectory();
            _runid = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        }

        [Option("p:outputdir", Required = false, HelpText = "Specifies the output directory name.")]
        public string OutputDirectory
        {
            get { return _outputDirectory; }

            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new Exception("The output directory name cannot be null, empty or white space.");
                }

                char[] invalidPathChars = Path.GetInvalidPathChars();
                if (value.Any(c => invalidPathChars.Contains(c)))
                {
                    throw new Exception("Specified output directory name contains invalid path characters.");
                }

                _outputDirectory = Path.IsPathRooted(value) ? value : Path.GetFullPath(value);
                if (!Directory.Exists(_outputDirectory))
                {
                    Directory.CreateDirectory(_outputDirectory);
                }
            }
        }

        [Option("p:runid", Required = false, HelpText = "User defined id given to this harness. This is used to give the output files a name.")]
        public string RunId
        {
            get { return _runid; }

            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new Exception("The RunId cannot be null, empty or white space.");
                }

                char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
                if (value.Any(c => invalidFileNameChars.Contains(c)))
                {
                    throw new Exception("Specified RunId contains invalid file name characters.");
                }

                _runid = value;
            }
        }

        private static string _outputDirectory;
        private static string _runid;
    }
}
