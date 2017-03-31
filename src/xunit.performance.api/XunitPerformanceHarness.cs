using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using static Microsoft.Xunit.Performance.Api.AssemblyModel;
using static Microsoft.Xunit.Performance.Api.Common;
using static Microsoft.Xunit.Performance.Api.PerformanceLogger;

namespace Microsoft.Xunit.Performance.Api
{
    public sealed class XunitPerformanceHarness : IDisposable
    {
        static XunitPerformanceHarness()
        {
            Action<string, string, string, Action, Action<string>> etwProfiler = (assemblyPath, runId, outputDirectory, runner, collectOutputFilesCallback) =>
            {
                ETWProfiler.Record(assemblyPath, runId, outputDirectory, runner, collectOutputFilesCallback);
            };
            Action<string, string, string, Action, Action<string>> genericProfiler = (assemblyPath, runId, outputDirectory, runner, collectOutputFilesCallback) =>
            {
                GenericProfiler.Record(assemblyPath, runId, outputDirectory, runner, collectOutputFilesCallback);
            };
            s_profiler = IsWindowsPlatform ? etwProfiler : genericProfiler;
        }

        public XunitPerformanceHarness(string[] args)
        {
            _args = args;
            _disposed = false;
            _outputFiles = new List<string>();
            _scenarioTestConfiguration = new ScenarioTestConfiguration();

            var options = XunitPerformanceHarnessOptions.Parse(args);
            _scenarioTestConfiguration.TemporaryDirectory = options.TemporaryDirectory;

            // Set the run id.
            _outputDirectory = options.OutputDirectory;
            _typeNames = new List<string>(options.TypeNames);
            _runner = (assemblyPath) =>
            {
                XunitRunner.Run(assemblyPath, _typeNames);
            };

            Configuration.RunId = options.RunId;
            // Set the file log path.
            // TODO: Conditionally set this based on whether we want a csv file written.
            Configuration.FileLogPath = Configuration.RunId + ".csv";
        }

        public BenchmarkConfiguration Configuration => BenchmarkConfiguration.Instance;

        public void RunBenchmarks(string assemblyPath)
        {
            Validate(assemblyPath);

            Action<string> collectOutputFilesCallback = fileName =>
            {
                // FIXME: This will need safe guards when the client calls RunBenchmarks in different threads.
                _outputFiles.Add(fileName);
            };

            Action winRunner = () =>
            {
                _runner(assemblyPath);
            };
            Action nixRunner = () =>
            {
                _runner(assemblyPath);
                ProcessResults(assemblyPath, Configuration.RunId, _outputDirectory, collectOutputFilesCallback);
            };
            Action runner = IsWindowsPlatform ? winRunner : nixRunner;

            s_profiler(assemblyPath, Configuration.RunId, _outputDirectory, runner, collectOutputFilesCallback);
        }

        public void RunScenario(ProcessStartInfo processStartInfo, Action<ScenarioTestConfiguration> preIterationDel,
            Action<ScenarioTestConfiguration> postIterationDel, Func<ScenarioTestConfiguration, ScenarioBenchmark> teardownDel)
        {
            int iterations = _scenarioTestConfiguration.Iterations;
            int timeout = (int)(_scenarioTestConfiguration.TimeoutPerIteration.TotalMilliseconds);

            for (int i = 0; i < iterations; i++)
            {
                preIterationDel(_scenarioTestConfiguration);
                using(var p = new Process())
                {
                    p.StartInfo = processStartInfo;
                    p.Start();
                    if (p.WaitForExit(timeout) == false) 
                    {
                        p.Kill();
                        Console.WriteLine("Timeouted!");
                        return;
                    }
                }
                postIterationDel(_scenarioTestConfiguration);
            }

            ScenarioBenchmark scenarioBenchmark = teardownDel(_scenarioTestConfiguration);
            if (scenarioBenchmark == null) 
            {
                throw new InvalidOperationException("The Teardown Delegate should return a valid instance of ScenarioBenchmark.");
            }

            string scenarioNamespace = scenarioBenchmark.Namespace;

            scenarioBenchmark.Serialize(Configuration.RunId + "-" + scenarioNamespace + ".xml");

            var mdFileName = Configuration.RunId + "-" + scenarioNamespace + "-Statistics.md";
            var csvFileName = Configuration.RunId + "-" + scenarioNamespace + "-Statistics.csv";

            var dt = scenarioBenchmark.GetStatistics();
            var mdTable = MarkdownHelper.GenerateMarkdownTable(dt);
            MarkdownHelper.Write(mdFileName, mdTable);
            WriteInfoLine($"Markdown file saved to \"{mdFileName}\"");
            Console.WriteLine(mdTable);

            dt.WriteToCSV(csvFileName);
            WriteInfoLine($"Statistics written to \"{csvFileName}\"");
        }


        public static string Usage()
        {
            return XunitPerformanceHarnessOptions.Usage();
        }

        private static void Validate(string assemblyPath)
        {
            if (string.IsNullOrEmpty(assemblyPath))
                throw new ArgumentNullException(nameof(assemblyPath));
            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException(assemblyPath);
        }

        private void ProcessResults(string assemblyFileName, string sessionName, string outputDirectory, Action<string> collectOutputFilesCallback)
        {
            var reader = new CSVMetricReader(Configuration.FileLogPath);
            var fileNameWithoutExtension = $"{sessionName}-{Path.GetFileNameWithoutExtension(assemblyFileName)}";
            var statisticsFileName = $"{fileNameWithoutExtension}-Statistics";
            var mdFileName = Path.Combine(outputDirectory, $"{statisticsFileName}.md");

            var assemblyModel = AssemblyModel.Create(assemblyFileName, reader);
            var xmlFileName = Path.Combine(outputDirectory, $"{fileNameWithoutExtension}.xml");
            new AssemblyModelCollection { assemblyModel }.Serialize(xmlFileName);
            WriteInfoLine($"Performance results saved to \"{xmlFileName}\"");
            collectOutputFilesCallback(xmlFileName);

            var dt = assemblyModel.GetStatistics();
            var mdTable = MarkdownHelper.GenerateMarkdownTable(dt);
            MarkdownHelper.Write(mdFileName, mdTable);
            WriteInfoLine($"Markdown file saved to \"{mdFileName}\"");
            collectOutputFilesCallback(mdFileName);
            Console.WriteLine(mdTable);

            var csvFileName = Path.Combine(outputDirectory, $"{statisticsFileName}.csv");
            dt.WriteToCSV(csvFileName);
            WriteInfoLine($"Statistics written to \"{csvFileName}\"");
            collectOutputFilesCallback(csvFileName);

            BenchmarkEventSource.Log.Clear();
        }

        #region IDisposable implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~XunitPerformanceHarness()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                }

                FreeUnManagedResources();

                _disposed = true;
            }
        }

        private void FreeUnManagedResources()
        {
            // Close the log when all test cases have completed execution.
            // HACK: This is a hack because we haven't found a way to close the file from within xunit.
            BenchmarkEventSource.Log.Close();

            // Deleting raw data not used by api users.
            File.Delete(Configuration.FileLogPath);
        }

        #endregion IDisposable implementation

        private static readonly Action<string, string, string, Action, Action<string>> s_profiler;

        private readonly Action<string> _runner;
        private readonly string[] _args;
        private readonly string _outputDirectory;
        private readonly List<string> _outputFiles;
        private readonly List<string> _typeNames;
        private bool _disposed;
        public ScenarioTestConfiguration _scenarioTestConfiguration;
    }
}