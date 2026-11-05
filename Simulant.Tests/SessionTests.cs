using Simulant.Core;
using Simulant.Simulation;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Simulant.Tests
{
    // These tests compile the real SimSession against the minimal fakes below. They exercise its
    // lifecycle and thread ordering without loading ACT, touching UI controls, or accessing game memory.
    internal static class SessionTests
    {
        private const int TimeoutMs = 5000;

        internal static void SessionPrepareFailureRollsBack()
        {
            using (var probe = new SessionProbe())
            {
                var expected = new InvalidOperationException("option preparation failed");
                probe.PrepareAction = () => { throw expected; };
                var host = new PluginHost();
                var callerThread = Thread.CurrentThread.ManagedThreadId;
                using (var session = CreateSession(host, probe))
                {
                    var actual = Throws<InvalidOperationException>(session.Start);
                    Assert(ReferenceEquals(actual, expected), "Start must propagate the original option error.");
                    Assert(probe.PrepareCount == 1 && probe.PrepareThreadId == callerThread,
                        "Options must be prepared once on the calling thread.");
                    Assert(probe.StartCount == 0, "Failed option preparation must not run initialization.");
                    Assert(probe.StopCount == 1 && probe.Logic.EntityManager.ClearCount == 1,
                        "Failed preparation must roll back exactly once.");
                    Assert(!session.IsRunning && probe.Logic.Scheduler.PendingCount == 0,
                        "A failed start must leave no running or queued work.");
                    Throws<ObjectDisposedException>(() => probe.Logic.Scheduler.Schedule(0, () => { }));
                    session.Stop();
                    Assert(probe.StopCount == 1 && host.Errors.IsEmpty,
                        "Stopping a rolled-back session must not repeat cleanup or report a worker error.");
                }
                Assert(probe.StopCount == 1, "Dispose must not repeat preparation rollback.");
            }
        }

        internal static void SessionInitializationFailureCleansUpOnce()
        {
            using (var probe = new SessionProbe())
            {
                var host = new PluginHost();
                var callerThread = Thread.CurrentThread.ManagedThreadId;
                probe.StartAction = () => { throw new InvalidOperationException("initialization failed"); };
                using (var session = CreateSession(host, probe))
                {
                    session.Start();
                    Wait(probe.Cleaned, "initialization failure cleanup");
                    session.Stop(); // Also waits until the worker has returned from its error handler.
                    Assert(probe.StartCount == 1 && probe.StartThreadId != callerThread,
                        "Initialization must execute on the worker.");
                    Assert(probe.StopCount == 1 && probe.StopThreadId == probe.StartThreadId,
                        "A worker failure must clean up once on that worker.");
                    Assert(probe.Logic.EntityManager.ClearCount == 1 && !session.IsRunning,
                        "A worker failure must clear entities and stop the session.");
                    Assert(host.Errors.Count == 1 && host.Errors.TryPeek(out var error) &&
                        error.Contains("initialization failed"), "The original worker failure must be logged once.");
                }
                Assert(probe.StopCount == 1, "Disposing a failed session must not repeat cleanup.");
            }
        }

        internal static void SessionStopWaitsForInitializationBeforeCleanup()
        {
            using (var probe = new SessionProbe())
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var callbackExited = new ManualResetEventSlim())
            using (var stopReturned = new ManualResetEventSlim())
            {
                var host = new PluginHost();
                var lateCalls = 0;
                probe.StartAction = () =>
                {
                    entered.Set();
                    Wait(release, "initialization release");
                    callbackExited.Set();
                    // External Stop has disposed this scheduler. The resulting exception is normal
                    // shutdown, and must not trigger the failure cleanup path a second time.
                    probe.Logic.Scheduler.Schedule(0, () => Interlocked.Increment(ref lateCalls));
                };
                probe.StopAction = () => Assert(callbackExited.IsSet,
                    "Session cleanup must not run while initialization still owns entity state.");

                using (var session = CreateSession(host, probe))
                {
                    session.Start();
                    Wait(entered, "initialization entry");
                    Exception stopError = null;
                    var stopThread = new Thread(() =>
                    {
                        try { session.Stop(); }
                        catch (Exception error) { stopError = error; }
                        finally { stopReturned.Set(); }
                    }) { IsBackground = true };
                    stopThread.Start();
                    try
                    {
                        Assert(SpinWait.SpinUntil(() => !probe.Logic.Scheduler.IsRunning, TimeoutMs),
                            "Stop did not cancel the scheduler.");
                        Assert(!stopReturned.Wait(50) && probe.StopCount == 0,
                            "Stop must wait for initialization before returning or cleaning entities.");
                    }
                    finally
                    {
                        release.Set();
                        Join(stopThread);
                    }

                    Assert(stopError == null, "Stopping initialization failed: " + stopError);
                    Assert(probe.StopCount == 1 && probe.Logic.EntityManager.ClearCount == 1,
                        "Stop must clean up exactly once after initialization exits.");
                    Assert(probe.StopThreadId == stopThread.ManagedThreadId,
                        "Normal cleanup should run on the Stop caller after joining the worker.");
                    Assert(lateCalls == 0 && host.Errors.IsEmpty,
                        "Cancellation must prevent later work without reporting normal shutdown as a failure.");
                }
            }
        }

        internal static void SessionCanRestartAfterStop()
        {
            using (var probe = new SessionProbe())
            using (var initialized = new AutoResetEvent(false))
            {
                probe.StartAction = () => initialized.Set();
                using (var session = CreateSession(new PluginHost(), probe))
                {
                    session.Start();
                    Assert(initialized.WaitOne(TimeoutMs), "First initialization did not run.");
                    var firstScheduler = probe.Logic.Scheduler;
                    session.Stop();
                    Throws<ObjectDisposedException>(() => firstScheduler.Schedule(0, () => { }));

                    session.Start();
                    Assert(initialized.WaitOne(TimeoutMs), "Second initialization did not run.");
                    var secondScheduler = probe.Logic.Scheduler;
                    Assert(!ReferenceEquals(firstScheduler, secondScheduler) && session.IsRunning,
                        "A restarted session must receive a fresh running scheduler.");
                    Assert(ReferenceEquals(secondScheduler, probe.Logic.EntityManager.Scheduler),
                        "The logic and entity manager must share the new scheduler.");
                    session.Stop();
                    Assert(probe.PrepareCount == 2 && probe.StartCount == 2 && probe.StopCount == 2 &&
                        probe.Logic.EntityManager.ClearCount == 2,
                        "Each run must prepare, initialize, and clean up exactly once.");
                }
                Assert(probe.StopCount == 2, "Dispose must not repeat either completed run's cleanup.");
            }
        }

        internal static void SessionDisposeIsIdempotent()
        {
            using (var probe = new SessionProbe())
            {
                var session = CreateSession(new PluginHost(), probe);
                try
                {
                    session.Start();
                    Wait(probe.Started, "initialization before disposal");
                    session.Dispose();
                    session.Dispose();
                    session.Stop();
                    Assert(!session.IsRunning && probe.StopCount == 1 && probe.Logic.EntityManager.ClearCount == 1,
                        "Repeated Dispose and Stop must leave the session stopped with one cleanup.");
                    Throws<ObjectDisposedException>(session.Start);
                    Throws<ObjectDisposedException>(() => probe.Logic.Scheduler.Schedule(0, () => { }));
                }
                finally { session.Dispose(); }
            }

            using (var probe = new SessionProbe())
            {
                var neverStarted = CreateSession(new PluginHost(), probe);
                neverStarted.Dispose();
                neverStarted.Dispose();
                Assert(probe.StopCount == 0, "Disposing an unstarted session must not invent a cleanup cycle.");
                Throws<ObjectDisposedException>(neverStarted.Start);
            }
        }

        private static SimSession CreateSession(PluginHost host, SessionProbe probe)
            => new SimSession(host, new SessionTestPreset(probe));

        internal static void Wait(ManualResetEventSlim signal, string description)
            => Assert(signal.Wait(TimeoutMs), "Timed out waiting for " + description + ".");

        private static void Join(Thread thread)
            => Assert(thread.Join(TimeoutMs), "A session test helper thread did not exit.");

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static T Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T exception) { return exception; }
            throw new Exception("Expected " + typeof(T).Name + ".");
        }
    }

    internal sealed class SessionProbe : IDisposable
    {
        internal Action PrepareAction;
        internal Action StartAction;
        internal Action StopAction;
        internal readonly ManualResetEventSlim Started = new ManualResetEventSlim();
        internal readonly ManualResetEventSlim Cleaned = new ManualResetEventSlim();
        internal SessionTestLogic Logic;
        internal int PrepareCount;
        internal int StartCount;
        internal int StopCount;
        internal int PrepareThreadId;
        internal int StartThreadId;
        internal int StopThreadId;

        public void Dispose()
        {
            Started.Dispose();
            Cleaned.Dispose();
        }
    }

    internal sealed class SessionTestPreset : SimPresetBase
    {
        internal readonly SessionProbe Probe;
        internal SessionTestPreset(SessionProbe probe) { Probe = probe; }
        public override Type SimLogicType => typeof(SessionTestLogic);
    }

    internal sealed class SessionTestLogic : SimLogicBase
    {
        private readonly SessionProbe _probe;

        public SessionTestLogic(PluginHost host, SimPresetBase preset)
        {
            _probe = ((SessionTestPreset)preset).Probe;
            _probe.Logic = this;
        }

        internal override void Prepare()
        {
            _probe.PrepareThreadId = Thread.CurrentThread.ManagedThreadId;
            Interlocked.Increment(ref _probe.PrepareCount);
            _probe.PrepareAction?.Invoke();
        }

        internal override void Start()
        {
            _probe.StartThreadId = Thread.CurrentThread.ManagedThreadId;
            Interlocked.Increment(ref _probe.StartCount);
            _probe.Started.Set();
            _probe.StartAction?.Invoke();
        }

        internal override void Stop()
        {
            _probe.StopThreadId = Thread.CurrentThread.ManagedThreadId;
            Interlocked.Increment(ref _probe.StopCount);
            _probe.StopAction?.Invoke();
            EntityManager.Clear();
            _probe.Cleaned.Set();
        }
    }
}

// Only the four dependencies of SimSession are replaced. The scheduler and SimSession itself
// are the production source files linked by the test project.
namespace Simulant.Core
{
    public sealed class PluginHost
    {
        internal readonly object EntitySpawner = new object();
        internal readonly ConcurrentQueue<string> Errors = new ConcurrentQueue<string>();
        internal void LogError(string message) => Errors.Enqueue(message);
    }
}

namespace Simulant.Simulation
{
    public abstract class SimPresetBase
    {
        public abstract Type SimLogicType { get; }
    }

    public abstract class SimLogicBase
    {
        internal SimEntityManager EntityManager { get; set; }
        internal SimScheduler Scheduler { get; set; }
        internal abstract void Prepare();
        internal abstract void Start();
        internal abstract void Stop();
    }
}

namespace Simulant.Simulation.Runtime
{
    public sealed class SimEntityManager
    {
        internal SimScheduler Scheduler { get; set; }
        internal int ClearCount;
        public SimEntityManager(object spawner, Action<Exception> onCleanupError = null) { }
        internal void Clear() => Interlocked.Increment(ref ClearCount);
    }
}
