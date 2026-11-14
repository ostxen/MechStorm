using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Newtonsoft.Json;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace Unity.Pipeline.Editor.Testing
{
    /// <summary>
    /// Core test execution engine supporting both synchronous and asynchronous execution modes.
    /// Adapted from unity-tools with domain reload handling and dual mode support.
    /// </summary>
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    static class PipelineTestRunner
    {
        // Consts are inlined at compile time, so PipelineTestReloader can read them without
        // loading this class (whose fields need UnityEditor.TestRunner to resolve).
        internal const string TestRequestFile = "Temp/pipeline_test_request.json";
        internal const string TestStatusFile = "Temp/pipeline_test_status.json";

        internal static TestResultCollector m_ActiveCollector;
        private static readonly Dictionary<TestResultCollector, TestRunnerApi> s_OwnedApis = new Dictionary<TestResultCollector, TestRunnerApi>();
        private static bool s_RunStarted;
        private static string s_RunId;
        private static readonly HashSet<string> s_PendingCancellations = new HashSet<string>();

        static PipelineTestRunner()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseOwnedApis;
            EditorApplication.quitting += ReleaseOwnedApis;
        }

        private static void ReleaseOwnedApis()
        {
            EditorApplication.update -= FinishPendingCancellations;
            s_PendingCancellations.Clear();
            foreach (var collector in s_OwnedApis.Keys.ToArray())
                UnregisterCollector(collector);
        }

        private static void RecordStartedRun(string runId)
        {
            s_RunId = runId;
            if (!File.Exists(TestRequestFile)) return;
            var request = JsonConvert.DeserializeObject<TestRequest>(File.ReadAllText(TestRequestFile));
            request.runId = runId;
            File.WriteAllText(TestRequestFile, JsonConvert.SerializeObject(request));
        }

        /// <summary>
        /// Execute tests with the given parameters
        /// </summary>
        /// <param name="mode">"editor"/"editmode", "playmode"/"play", or "all".</param>
        /// <param name="filter">Filter value to match against, per <paramref name="filterType"/>.</param>
        /// <param name="filterType">"testname", "assembly", or "category".</param>
        /// <param name="includeExplicit">Whether to include tests/fixtures marked [Explicit].</param>
        /// <param name="asyncMode">Run asynchronously, returning immediately with a status path to poll.</param>
        /// <param name="timeout">Timeout for synchronous execution.</param>
        /// <returns>The test execution response.</returns>
        public static async Task<TestExecutionResponse> ExecuteTestsAsync(
            string mode,
            string filter,
            string filterType,
            bool includeExplicit,
            bool asyncMode,
            TimeSpan timeout)
        {
            try
            {
                var parsedMode = ParseTestMode(mode);
                if (!parsedMode.IsValid)
                {
                    return CreateErrorResponse($"Invalid mode '{mode}'. Use 'editor', 'playmode', or 'all'");
                }

                // Validate filter type
                var normalizedFilterType = filterType?.ToLower() ?? "testname";
                if (!string.IsNullOrEmpty(filter) &&
                    normalizedFilterType != "testname" && normalizedFilterType != "assembly" && normalizedFilterType != "category")
                {
                    return CreateErrorResponse($"Invalid filterType '{filterType}'. Valid options: testName, assembly, category");
                }

                // Cancel any previous run
                InvalidatePreviousRun();

                if (parsedMode.IsAll)
                {
                    // Handle "all" mode by running both editor and playmode tests
                    return await ExecuteAllModesAsync(filter, normalizedFilterType, includeExplicit, asyncMode, timeout);
                }
                else if (asyncMode)
                {
                    return await ExecuteAsyncMode(parsedMode.TestMode, filter, normalizedFilterType, includeExplicit, timeout);
                }
                else if (parsedMode.TestMode == TestMode.PlayMode)
                {
                    // PlayMode entry triggers a domain reload that drops the in-flight HTTP request,
                    // so a synchronous playmode run can never return results over one call. Fail fast
                    // with guidance instead of hanging until the connection resets.
                    return CreateErrorResponse(
                        "PlayMode tests cannot run synchronously over HTTP: entering play mode triggers a " +
                        $"domain reload that drops the request. Re-run asynchronously ({CommandNames.TestRun} --mode " +
                        $"playmode --async-tests) and poll the {CommandNames.TestStatus} command for results.");
                }
                else
                {
                    return await ExecuteSyncMode(parsedMode.TestMode, filter, normalizedFilterType, includeExplicit, timeout);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PipelineTestRunner] Error: {ex.Message}");
                return CreateErrorResponse($"Failed to run tests: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute tests in all modes (editor + playmode)
        /// </summary>
        private static async Task<TestExecutionResponse> ExecuteAllModesAsync(
            string filter,
            string filterType,
            bool includeExplicit,
            bool asyncMode,
            TimeSpan timeout)
        {
            if (asyncMode)
            {
                return CreateErrorResponse("Async mode is not supported for 'all' mode. Use 'editor' or 'playmode' for async execution.");
            }

            var startTime = DateTime.UtcNow;

            try
            {
                // EditMode runs synchronously over HTTP (no domain reload), so its results are
                // returned in this response.
                Debug.Log("[PipelineTestRunner] Running editor tests (all mode)...");
                var editorResponse = await ExecuteSyncMode(TestMode.EditMode, filter, filterType, includeExplicit, timeout);

                if (!editorResponse.Success)
                {
                    return editorResponse; // Return error immediately
                }

                // PlayMode can't run synchronously over HTTP (entering play mode triggers a domain
                // reload that drops the request), so start it asynchronously and hand back a
                // StatusPath. The caller polls the test_status command for the playmode results.
                Debug.Log("[PipelineTestRunner] Starting playmode tests asynchronously (all mode)...");
                var playmodeStart = await ExecuteAsyncMode(TestMode.PlayMode, filter, filterType, includeExplicit, timeout);

                if (!playmodeStart.Success)
                {
                    return playmodeStart; // Return error immediately
                }

                // Build combined response: editor results inline, playmode running async.
                var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                return new TestExecutionResponse
                {
                    Success = true,
                    Command = CommandNames.TestRun,
                    Duration = Math.Round(duration, 2),
                    Mode = "All",
                    FilterApplied = string.IsNullOrEmpty(filter) ? null : $"{filterType}: {filter}",
                    ExecutionTimeMs = (int)(duration * 1000),
                    Summary = editorResponse.Summary,
                    Results = editorResponse.Results,
                    Result = "playmode_running",
                    StatusPath = playmodeStart.StatusPath,
                    Message = $"EditMode complete: {editorResponse.Summary.Passed}/{editorResponse.Summary.Total} passed. " +
                              $"PlayMode tests started asynchronously — poll the {CommandNames.TestStatus} command for their results.",
                    ExecutedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PipelineTestRunner] All modes execution error: {ex.Message}");
                return CreateErrorResponse($"Failed to execute tests in all modes: {ex.Message}");
            }
        }

        /// <summary>
        /// Synchronous execution - blocks until completion
        /// </summary>
        private static async Task<TestExecutionResponse> ExecuteSyncMode(
            TestMode testMode,
            string filter,
            string filterType,
            bool includeExplicit,
            TimeSpan timeout)
        {
            var startTime = DateTime.UtcNow;
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var collector = new TestResultCollector();

            m_ActiveCollector = collector;
            s_OwnedApis[collector] = api;

            // Set up timeout cancellation
            using (var cts = new CancellationTokenSource(PipelineTimeout.StopBefore(timeout)))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, PipelineCancellation.AmbientToken))
            {
                try
                {
                    // For PlayMode tests, we need to handle domain reloads
                    if (testMode == TestMode.PlayMode)
                    {
                        // Save request for domain reload recovery
                        var request = new TestRequest
                        {
                            filter = filter,
                            filterType = filterType,
                            mode = testMode.ToString(),
                            includeExplicit = includeExplicit,
                            isSync = true
                        };
                        File.WriteAllText(TestRequestFile, JsonConvert.SerializeObject(request));
                    }

                    // Await on Unity's context: timeout/job cancellation only signals a task;
                    // invalidation and Unity object cleanup below stay on the editor thread.
                    var completionTask = ExecuteTestsInternal(api, collector, testMode, filter, filterType, includeExplicit);
                    var resultAdaptor = await AwaitUnlessCanceled(completionTask, linked.Token);

                    // Build response
                    var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                    var response = new TestExecutionResponse
                    {
                        Success = true,
                        Command = CommandNames.TestRun,
                        Duration = Math.Round(duration, 2),
                        Mode = testMode.ToString(),
                        FilterApplied = string.IsNullOrEmpty(filter) ? null : $"{filterType}: {filter}",
                        ExecutionTimeMs = (int)(duration * 1000),
                        Summary = resultAdaptor != null ? new TestSummary
                        {
                            Total = resultAdaptor.PassCount + resultAdaptor.FailCount + resultAdaptor.SkipCount + resultAdaptor.InconclusiveCount,
                            Passed = resultAdaptor.PassCount,
                            Failed = resultAdaptor.FailCount,
                            Skipped = resultAdaptor.SkipCount,
                            Inconclusive = resultAdaptor.InconclusiveCount
                        } : new TestSummary(),
                        Results = collector.Results,
                        ExecutedAt = DateTime.UtcNow
                    };

                    // Clean up PlayMode request file
                    CleanupRequestFile();

                    return response;
                }
                catch (OperationCanceledException)
                {
                    if (ReferenceEquals(m_ActiveCollector, collector))
                    {
                        CleanupRequestFile();
                        InvalidatePreviousRun();
                    }
                    return CreateErrorResponse(cts.IsCancellationRequested
                        ? $"Test execution timed out ({PipelineTimeout.Describe(timeout)} timeout)"
                        : "Test execution cancelled");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PipelineTestRunner] Sync execution error: {ex.Message}");
                    CleanupRequestFile();
                    return CreateErrorResponse($"Test execution failed: {ex.Message}");
                }
                finally
                {
                    UnregisterCollector(collector);
                }
            }
        }

        /// <summary>
        /// Asynchronous execution - returns immediately
        /// </summary>
        // Not async: the work here is synchronous (file I/O + a main-thread call that returns
        // immediately). Returns a completed Task so callers can still await it.
        private static Task<TestExecutionResponse> ExecuteAsyncMode(
            TestMode testMode,
            string filter,
            string filterType,
            bool includeExplicit,
            TimeSpan timeout)
        {
            try
            {
                // Save request to disk (survives domain reloads for PlayMode)
                var request = new TestRequest
                {
                    filter = filter,
                    filterType = filterType,
                    mode = testMode.ToString(),
                    includeExplicit = includeExplicit,
                    isSync = false
                };
                File.WriteAllText(TestRequestFile, JsonConvert.SerializeObject(request));

                // Clear any previous status
                if (File.Exists(TestStatusFile))
                    File.Delete(TestStatusFile);

                // Start test execution now, on the main thread (run_tests is MainThreadRequired,
                // so we are inside the editor update tick that dispatched this command). Must NOT
                // run on a background thread: the test runner creates a TestRunnerApi via
                // ScriptableObject.CreateInstance, which is main-thread-only. RetrieveTestList
                // returns immediately, so the HTTP "running" response below is still sent promptly.
                StartAsyncTestExecution(testMode, filter, filterType, includeExplicit);

                return Task.FromResult(new TestExecutionResponse
                {
                    Success = true,
                    Command = CommandNames.TestRun,
                    Result = "running",
                    StatusPath = TestStatusFile,
                    Mode = testMode.ToString(),
                    FilterApplied = string.IsNullOrEmpty(filter) ? null : $"{filterType}: {filter}",
                    Message = "Tests started in async mode. Poll /test-status for results.",
                    ExecutedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PipelineTestRunner] Async setup error: {ex.Message}");
                return Task.FromResult(CreateErrorResponse($"Failed to start async test execution: {ex.Message}"));
            }
        }

        /// <summary>
        /// Awaits <paramref name="task"/>, or throws <see cref="OperationCanceledException"/> as soon
        /// as <paramref name="token"/> fires, whichever comes first.
        /// </summary>
        internal static async Task<T> AwaitUnlessCanceled<T>(Task<T> task, CancellationToken token)
        {
            var canceled = new TaskCompletionSource<bool>();
            using (token.Register(() => canceled.TrySetResult(true)))
            {
                if (await Task.WhenAny(task, canceled.Task) != task)
                    throw new OperationCanceledException(token);
            }
            return await task;
        }

        /// <summary>
        /// Internal test execution logic
        /// </summary>
        private static Task<ITestResultAdaptor> ExecuteTestsInternal(
            TestRunnerApi api,
            TestResultCollector collector,
            TestMode testMode,
            string filter,
            string filterType,
            bool includeExplicit)
        {
            // Retrieve test list and filter
            api.RetrieveTestList(testMode, (ITestAdaptor rootTest) =>
            {
                try
                {
                    if (collector.IsCancelled) return;
                    var testNames = CollectTestNames(rootTest, filter, filterType, includeExplicit);

                    if (testNames.Count == 0)
                    {
                        Debug.Log("[PipelineTestRunner] No tests to run after filtering");
                        collector.CompleteWithoutTests();
                        return;
                    }

                    Debug.Log($"[PipelineTestRunner] Running {testNames.Count} tests (explicit tests {(includeExplicit ? "included" : "excluded")})");

                    var testFilter = new Filter
                    {
                        testMode = testMode,
                        testNames = testNames.ToArray()
                    };

                    api.RegisterCallbacks(collector);
                    s_RunStarted = true;
                    RecordStartedRun(api.Execute(new ExecutionSettings(testFilter)));
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PipelineTestRunner] Error during test execution: {ex.Message}");
                    collector.SetError(ex);
                }
            });

            return collector.WaitForCompletionAsync();
        }

        /// <summary>
        /// Start async test execution on the main thread. Registers callbacks and kicks off the
        /// run, then returns; completion is reported by the collector writing the status file.
        /// Unlike the sync path, this reports through OnRunFinished and does not await the collector.
        /// </summary>
        private static void StartAsyncTestExecution(TestMode testMode, string filter, string filterType, bool includeExplicit)
        {
            TestRunnerApi api = null;
            TestResultCollector collector = null;
            try
            {
                api = ScriptableObject.CreateInstance<TestRunnerApi>();
                collector = new TestResultCollector();

                m_ActiveCollector = collector;
                s_OwnedApis[collector] = api;

                // Async completion: write results for pollers. Set once, never overwritten.
                collector.OnRunFinished = () =>
                {
                    WriteCompletedResults(collector);
                    RestoreLiveServerIfDisrupted(); // B2: self-heal if a test broke the live server.
                    CleanupRequestFile();
                    UnregisterCollector(collector);
                };

                api.RetrieveTestList(testMode, (ITestAdaptor rootTest) =>
                {
                    try
                    {
                        if (collector.IsCancelled) return;
                        var testNames = CollectTestNames(rootTest, filter, filterType, includeExplicit);

                        if (testNames.Count == 0)
                        {
                            WriteStatusFile(new
                            {
                                status = "completed",
                                duration = 0.0,
                                summary = new { total = 0, passed = 0, failed = 0, skipped = 0, inconclusive = 0 },
                                results = new object[0]
                            });
                            CleanupRequestFile();
                            UnregisterCollector(collector);
                            return;
                        }

                        api.RegisterCallbacks(collector);
                        s_RunStarted = true;
                        RecordStartedRun(api.Execute(new ExecutionSettings(new Filter
                        {
                            testMode = testMode,
                            testNames = testNames.ToArray()
                        })));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[PipelineTestRunner] Async run setup error: {ex.Message}");
                        WriteStatusFile(new { status = "error", message = ex.Message });
                        CleanupRequestFile();
                        UnregisterCollector(collector);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PipelineTestRunner] Async execution error: {ex.Message}");
                WriteStatusFile(new { status = "error", message = ex.Message });
                CleanupRequestFile();
                UnregisterCollector(collector);
            }
        }

        /// <summary>
        /// Re-register a result collector to capture the completion of a PlayMode run that the
        /// Unity Test Framework auto-resumes after a play-mode domain reload (see TestJobDataHolder
        /// in the test-framework package). The previous collector did not survive the reload.
        /// Deliberately does NOT call RetrieveTestList/api.Execute: the run is already in flight, so
        /// starting another one double-executes the task list (running scene tasks during play
        /// mode) and trips the runner's "too many instant steps" guard. The collector rebuilds its
        /// per-test results from the RootResult tree at RunFinished, since the incremental
        /// TestFinished callbacks were delivered to a collector from the previous domain.
        /// </summary>
        private static void ReattachResultCollector(string runId)
        {
            TestRunnerApi api = null;
            TestResultCollector collector = null;
            try
            {
                api = ScriptableObject.CreateInstance<TestRunnerApi>();
                collector = new TestResultCollector(attachedMidRun: true);

                m_ActiveCollector = collector;
                s_OwnedApis[collector] = api;
                s_RunStarted = true;
                s_RunId = runId;

                collector.OnRunFinished = () =>
                {
                    WriteCompletedResults(collector);
                    RestoreLiveServerIfDisrupted();
                    CleanupRequestFile();
                    UnregisterCollector(collector);
                };

                api.RegisterCallbacks(collector);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PipelineTestRunner] Failed to reattach result collector: {ex.Message}");
                WriteStatusFile(new { status = "error", message = ex.Message });
                CleanupRequestFile();
                UnregisterCollector(collector);
            }
        }

        /// <summary>Resume any test run that was in flight before a domain reload dropped it.
        /// Invoked by <see cref="PipelineTestReloader"/>, which checks the request files first.</summary>
        public static void CheckForPendingTests()
        {
            if (!File.Exists(TestRequestFile)) return;
            if (File.Exists(TestStatusFile)) return; // Already completed
            // The run's collector survived, so no reload interrupted it: this is a stale delayCall.
            if (m_ActiveCollector != null) return;

            try
            {
                var json = File.ReadAllText(TestRequestFile);
                var request = JsonConvert.DeserializeObject<TestRequest>(json);

                Debug.Log("[PipelineTestRunner] Reattaching result collector after domain reload");
                if (request.isSync)
                {
                    // For sync mode, we can't really resume after domain reload
                    // The command has already timed out or failed
                    CleanupRequestFile();
                    return;
                }

                // The Unity Test Framework (TestJobDataHolder) auto-resumes the in-flight PlayMode
                // run after a play-mode domain reload. We must NOT start a new run here: re-calling
                // api.Execute double-starts the job, which re-runs the task list (executing scene
                // tasks during play mode) and trips the runner's "too many instant steps" guard.
                // We only need to re-register a collector — our previous one did not survive the
                // reload — to capture the resumed run's completion. Runs on the main thread because
                // CheckForPendingTests is invoked via EditorApplication.delayCall.
                ReattachResultCollector(request.runId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PipelineTestRunner] Failed to resume tests: {ex.Message}");
                WriteStatusFile(new { status = "error", message = $"Failed to resume tests: {ex.Message}" });
                CleanupRequestFile();
            }
        }

        #region Utility Methods

        private struct ParsedTestMode
        {
            public bool IsValid { get; set; }
            public bool IsAll { get; set; }
            public TestMode TestMode { get; set; }

            public static ParsedTestMode Invalid => new ParsedTestMode { IsValid = false };
            public static ParsedTestMode All => new ParsedTestMode { IsValid = true, IsAll = true };
            public static ParsedTestMode Single(TestMode mode) => new ParsedTestMode { IsValid = true, IsAll = false, TestMode = mode };
        }

        private static ParsedTestMode ParseTestMode(string mode)
        {
            switch (mode?.ToLower())
            {
                case "editor":
                case "editmode":
                    return ParsedTestMode.Single(TestMode.EditMode);
                case "playmode":
                case "play":
                    return ParsedTestMode.Single(TestMode.PlayMode);
                case "all":
                    return ParsedTestMode.All;
                default:
                    return ParsedTestMode.Invalid;
            }
        }

        private static List<string> CollectTestNames(ITestAdaptor test, string filter, string filterType, bool includeExplicit)
        {
            var testNames = new List<string>();
            CollectTestNamesRecursive(test, testNames, filter, filterType, includeExplicit);
            return testNames;
        }

        private static void CollectTestNamesRecursive(ITestAdaptor test, List<string> testNames, string filter, string filterType, bool includeExplicit)
        {
            if (test == null) return;

            // Skip [Explicit] tests AND [Explicit] fixtures (the whole subtree) unless includeExplicit.
            // Checked at EVERY node — including suite/fixture nodes — because a class-level [Explicit]
            // is not reliably propagated to [UnityTest] leaf RunState by Unity's adaptor; honoring the
            // declaring type's attribute here is what makes class-level [Explicit] actually exclude.
            if (!includeExplicit && IsExplicit(test))
                return;

            // Only collect leaf tests (actual test methods, not fixtures/suites)
            if (!test.HasChildren && test.IsSuite == false)
            {
                // Apply user filter
                if (!ShouldIncludeTest(test, filter, filterType))
                    return;

                testNames.Add(test.FullName);
            }

            // Recurse into children
            if (test.Children != null)
            {
                foreach (var child in test.Children)
                {
                    CollectTestNamesRecursive(child, testNames, filter, filterType, includeExplicit);
                }
            }
        }

        /// <summary>
        /// True if the test, its method, or its declaring fixture type is marked [Explicit].
        /// Class-level [Explicit] must be honored here because Unity does not always propagate it
        /// to leaf RunState (notably for [UnityTest]).
        /// </summary>
        private static bool IsExplicit(ITestAdaptor test)
        {
            if (test.RunState == RunState.Explicit)
                return true;

            var method = test.Method?.MethodInfo;
            if (method != null && method.GetCustomAttributes(true).Any(a => a.GetType().Name == "ExplicitAttribute"))
                return true;

            var type = test.TypeInfo?.Type;
            if (type != null && type.GetCustomAttributes(true).Any(a => a.GetType().Name == "ExplicitAttribute"))
                return true;

            return false;
        }

        private static bool ShouldIncludeTest(ITestAdaptor test, string filter, string filterType)
        {
            if (string.IsNullOrEmpty(filter))
                return true;

            switch (filterType)
            {
                case "assembly":
                    var assemblyName = test.TypeInfo?.Assembly?.GetName()?.Name;
                    return assemblyName != null &&
                           assemblyName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

                case "testname":
                    return test.FullName != null &&
                           test.FullName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

                case "category":
                    return test.Categories != null &&
                           test.Categories.Any(c => c.Equals(filter, StringComparison.OrdinalIgnoreCase));

                default:
                    return true;
            }
        }

        private static void InvalidatePreviousRun()
        {
            if (m_ActiveCollector != null)
            {
                var collector = m_ActiveCollector;
                collector.IsCancelled = true;
                try
                {
                    if (s_RunStarted)
                        CancelUnderlyingRun(s_RunId);
                }
                finally
                {
                    collector.Cancel();
                    UnregisterCollector(collector);
                }
                Debug.Log("[PipelineTestRunner] Invalidated previous test run");
            }
        }

        // All callers run on the editor thread. The Test Framework used by Unity 2022 has no public
        // cancellation entry point; use the same EditMode runner hook as its cancelable progress bar.
        private static void CancelUnderlyingRun(string runId)
        {
            if (string.IsNullOrEmpty(runId)) return;
#if UNITY_6000_0_OR_NEWER
            TestRunnerApi.CancelTestRun(runId);
#else
            s_PendingCancellations.Add(runId);
            FinishPendingCancellations();
            if (s_PendingCancellations.Count > 0)
            {
                EditorApplication.update -= FinishPendingCancellations;
                EditorApplication.update += FinishPendingCancellations;
            }
#endif
        }

        private static void FinishPendingCancellations()
        {
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
            var holderType = typeof(TestRunnerApi).Assembly.GetType("UnityEditor.TestTools.TestRunner.TestRun.TestJobDataHolder");
            var holder = holderType?.GetProperty("instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy)?.GetValue(null);
            var runs = holderType?.GetField("TestRuns", fields)?.GetValue(holder) as System.Collections.IEnumerable;
            if (runs == null)
            {
                s_PendingCancellations.Clear();
                Debug.LogWarning("[PipelineTestRunner] This test framework does not expose the legacy cancellation registry.");
            }
            else
            {
                var seen = new HashSet<string>();
                foreach (var run in runs)
                {
                    var type = run.GetType();
                    var id = type.GetField("guid", fields)?.GetValue(run) as string;
                    if (id == null || !s_PendingCancellations.Contains(id)) continue;
                    seen.Add(id);
                    var runner = type.GetField("editModeRunner", fields)?.GetValue(run) as UnityEngine.Object;
                    if (runner == null) continue; // TestJobRunner has not reached its run task yet.
                    runner.GetType().GetMethod("OnRunCancel", fields)?.Invoke(runner, null);
                    s_PendingCancellations.Remove(id);
                }
                s_PendingCancellations.IntersectWith(seen);
            }
            if (s_PendingCancellations.Count == 0)
                EditorApplication.update -= FinishPendingCancellations;
        }

        /// <summary>
        /// Unregisters a collector from Unity's TestRunnerApi callback registry (process-wide, not
        /// per-instance) and clears the active-run field if it still points at it. Unregisters
        /// without the TestRunnerApi it was registered through, which a run's asset unload can destroy.
        /// </summary>
        internal static void UnregisterCollector(TestResultCollector collector)
        {
            TestRunnerApi owned = null;
            if (collector != null && s_OwnedApis.TryGetValue(collector, out owned))
            {
                try
                {
#if UNITY_6000_0_OR_NEWER
                    TestRunnerApi.UnregisterTestCallback(collector);
#else
                    var api = owned != null ? owned : ScriptableObject.CreateInstance<TestRunnerApi>();
                    try { api.UnregisterCallbacks(collector); }
                    finally { if (api != owned) UnityEngine.Object.DestroyImmediate(api); }
#endif
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PipelineTestRunner] Could not unregister collector: {ex.Message}");
                }
                finally
                {
                    s_OwnedApis.Remove(collector);
                    if (owned != null) UnityEngine.Object.DestroyImmediate(owned);
                }
            }

            if (ReferenceEquals(m_ActiveCollector, collector))
            {
                m_ActiveCollector = null;
                s_RunStarted = false;
                s_RunId = null;
            }
        }

        private static TestExecutionResponse CreateErrorResponse(string message)
        {
            return new TestExecutionResponse
            {
                Success = false,
                Command = CommandNames.TestRun,
                Error = message,
                Results = new List<TestResult>(),
                Summary = new TestSummary(),
                ExecutedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// B2 self-heal: after a test run, if a test left the live editor server disrupted (its
        /// listener died, or its discovery descriptor was deleted/clobbered), restart it so the
        /// dogfood loop and subsequent commands keep working instead of wedging the session. No-op
        /// when the server is healthy. Runs at run completion — the watchdog is disabled during the
        /// run (PipelineWatchdogTestGuard), so this is what revives the live server afterwards.
        /// </summary>
        private static void RestoreLiveServerIfDisrupted()
        {
            try
            {
                var server = PipelineServerStartup.Server;
                var root = Path.GetDirectoryName(Application.dataPath);
                var descriptor = Unity.Pipeline.Models.EditorInstanceDescriptor.ReadFromProjectRoot(root);

                var disrupted = server == null
                                || !server.IsRunning
                                || descriptor == null
                                || descriptor.Port != server.Port;

                if (disrupted)
                {
                    Debug.Log("[PipelineTestRunner] Live server was disrupted during the test run — restarting it.");
                    PipelineServerStartup.RestartServer();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PipelineTestRunner] Live server restore check failed: {ex.Message}");
            }
        }

        private static void WriteCompletedResults(TestResultCollector collector)
        {
            try
            {
                if (!collector.IsComplete)
                {
                    WriteStatusFile(new { status = "error", message = "Tests did not complete" });
                    return;
                }

                var root = collector.RootResult;
                var result = new
                {
                    status = "completed",
                    duration = Math.Round(root.Duration, 2),
                    summary = new
                    {
                        total = root.PassCount + root.FailCount + root.SkipCount + root.InconclusiveCount,
                        passed = root.PassCount,
                        failed = root.FailCount,
                        skipped = root.SkipCount,
                        inconclusive = root.InconclusiveCount
                    },
                    results = collector.Results.ToArray()
                };

                WriteStatusFile(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PipelineTestRunner] Failed to write results: {ex.Message}");
                WriteStatusFile(new { status = "error", message = ex.Message });
            }
        }

        private static void WriteStatusFile(object data)
        {
            try
            {
                File.WriteAllText(TestStatusFile, JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PipelineTestRunner] Failed to write status file: {ex.Message}");
            }
        }

        private static void CleanupRequestFile()
        {
            try { if (File.Exists(TestRequestFile)) File.Delete(TestRequestFile); } catch { }
        }

        /// <summary>
        /// Get current test status (for async mode polling)
        /// </summary>
        /// <returns>The status file's JSON contents, "running" if a request is pending, or null if there's no run.</returns>
        public static string GetTestStatus()
        {
            if (File.Exists(TestStatusFile))
                return File.ReadAllText(TestStatusFile);
            if (File.Exists(TestRequestFile))
                return JsonConvert.SerializeObject(new { status = "running" });
            return null;
        }

        /// <summary>
        /// Cancel running tests
        /// </summary>
        /// <returns>A status payload confirming cancellation (or that there was nothing to cancel).</returns>
        public static object CancelTests()
        {
            if (m_ActiveCollector == null && !File.Exists(TestRequestFile) && !File.Exists(TestStatusFile))
                return new { status = "no_tests", message = "No test run in progress." };

            bool wasPlayMode = false;
            try
            {
                if (File.Exists(TestRequestFile))
                {
                    var json = File.ReadAllText(TestRequestFile);
                    wasPlayMode = json.Contains("PlayMode");
                }
            }
            catch { }

            InvalidatePreviousRun();
            WriteStatusFile(new { status = "cancelled", message = "Test run cancelled." });
            CleanupRequestFile();

            if (wasPlayMode && EditorApplication.isPlaying)
            {
                Debug.Log("[PipelineTestRunner] Exiting play mode to cancel PlayMode tests");
                EditorApplication.ExitPlaymode();
            }

            Debug.Log("[PipelineTestRunner] Test run cancelled");
            return new { status = "cancelled", message = "Test run cancelled." };
        }

        #endregion

        [Serializable]
        private class TestRequest
        {
            public string filter;
            public string filterType;
            public string mode;
            public bool includeExplicit;
            public bool isSync;
            public string runId;
        }
    }
}

