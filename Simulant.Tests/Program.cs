using Simulant.Simulation.Timeline;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Simulant.Tests
{
    internal static class Program
    {
        private const int TimeoutMs = 5000;

        private static int Main()
        {
            var tests = new Action[]
            {
                ActionAreaTests.CirclesAndDonutsUseClosedBoundaries,
                ActionAreaTests.RectanglesUseActHeadingAndFullDimensions,
                ActionAreaTests.ConesHandleAcuteHalfPlaneAndReflexAngles,
                ActionAreaTests.InvalidAreasAndCoordinatesFailClearly,
                ActionAreaTests.ActionRowsUseVerifiedShapesAndCasterHitboxes,
                ActionAreaTests.ActionRowsResolveStandardOmenDimensions,
                ActionAreaTests.ActionRowsRequireExplicitUnknownDimensions,
                ActionAreaTests.ActionRowsValidateOverridesAndUnsupportedTypes,
                MechanicTests.SnapshotReadsOnceAndKeepsOriginalSlots,
                MechanicTests.AreaMechanicKeepsInvalidSoakersInItsResult,
                MechanicTests.MechanicGroupEvaluatesEverythingBeforeAnyCallback,
                MechanicTests.MechanicGroupDoesNotCleanUnstartedChildren,
                MechanicTests.MechanicNodesUsePlannedTimeAndCancelWithTheirOwner,
                MechanicTests.MechanicPartialStartRetainsCleanupAndActorsForRetry,
                MechanicTests.MechanicCallbackFailureStillCleansUp,
                MechanicTests.MechanicWorldChangeSkipsRemainingCallbacksAndCleanup,
                MechanicTests.MechanicGroupReuseDoesNotCancelAnExistingOwner,
                MechanicTests.MechanicResourceCleanupDoesNotCancelParallelMechanics,
                MechanicTests.MechanicValidationHappensBeforeStart,
                ForsakenPlanTests.InitialAssignmentsKeepRoleDistributionAndPartnerGroups,
                ForsakenPlanTests.InitialPositionsSeparateRolePairsInFourCorners,
                ForsakenPlanTests.EveryRandomRoundHasTwoSoakersAndOneSafeSpellPerMember,
                ForsakenPlanTests.GroupsClearAllStacksAndPreservePreviousLeftRightPriority,
                ForsakenPlanTests.MapEffectsAndCleaveBaitsMatchTheirWorldPositions,
                ForsakenTimingTests.EveryRouteArrivesInTheLastSecondWithoutLeavingBeforeThePreviousSnapshot,
                ForsakenMechanicTests.RandomForsakenPlansResolveThroughProductionMechanics,
                ForsakenMechanicTests.ForsakenMechanicsUseActualPositionsWhenThePlanIsBroken,
                TimelineComposition,
                SnapshotIsolation,
                InvalidInputIsAtomic,
                RejectAsyncVoidActions,
                StableDeadlineOrdering,
                NestedTimelineUsesPlannedTime,
                EarlierDeadlineWakesWorker,
                CancelOneNodeAndRepairHeap,
                ExceptionCancelsBeforeReporting,
                CancelDoesNotInterruptCallback,
                StopRaceDoesNotReportAnError,
                DisposeWaitsForCallback,
                CallbackCanDisposeScheduler,
                ConcurrentCancellation,
                ConcurrentProducersAndStop,
                FiftyThousandNodesUseOneWorker,
                SessionTests.SessionPrepareFailureRollsBack,
                SessionTests.SessionFailureKeepsEffectsBeforeCleanup,
                SessionTests.SessionExplicitStopInterruptsFailureDisplay,
                SessionTests.SessionDelayedFailureCleanupCanRetry,
                SessionTests.SessionInitializationFailureCleansUpOnce,
                SessionTests.SessionStopWaitsForInitializationBeforeCleanup,
                SessionTests.SessionCanRestartAfterStop,
                SessionTests.SessionDisposeIsIdempotent,
                SessionTests.SessionRetainsFailedCleanupAndRejectsRestart,
                SessionTests.SessionDisposeRetriesPendingNetworkCleanup,
                SessionTests.SessionCallbackCanStopWhileExternalStopWaits,
                SessionTests.SessionCallbackCanDisposeWhileExternalStopWaits,
                SessionTests.SessionCallbackCannotRestartWhileExternalStopWaits,
                SessionTests.SessionSynchronousCallbacksCannotRestart,
                EntityTests.RegistryReusesOneWrapperPerLifetime,
                EntityTests.RegistryInvalidatesDisappearedAndReusedSlots,
                EntityTests.RegistryDetectsRecycledAddressFingerprint,
                EntityTests.RegistryTombstoneAndExplicitCreationAreSafe,
                EntityTests.RegistryResetInvalidatesTheWholeWorld,
                EntityTests.RegistryKeepsDistinctObjectsWithSharedGameIds,
                EntityTests.RegistryCleanupRetryMatchesOnlyTheOriginalLifetime,
                MovementTests.MovementSharesOneTickAndSchedulerThread,
                MovementTests.MovementRetargetsTheCurrentPosition,
                MovementTests.MovementStopAndDisposeCancelTheTick,
                MovementTests.MovementDropsDetachedAndRecycledEntities,
                MovementTests.MovementRejectsRestartWhenItsQueuedTickWasCancelled,
                MovementTests.MovementRejectsInvalidInputWithoutReplacingMove,
                MovementTests.MovementAdvanceClampsLongFramesAndPreservesSpeed,
                MovementTests.ArrivalResetsAnimationBeforeOneSchedulerCallback,
                MovementTests.StopRetargetDeleteAndDetachDiscardOldArrival,
                MovementTests.ArrivalCallbackCanChainSelfAndRetargetAnotherActor,
                MovementTests.ArrivalCallbackCanCancelOtherCompletedArrivals,
                MovementTests.ArrivalCallbackCanCancelItsScheduler,
                MovementTests.ArrivalCallbackDoesNotHoldTheMovementLock,
                MovementTests.InvalidArrivalCallbackPreservesTheCurrentMovement,
                MovementTests.MovementCleanupPreservesExternalAnimationEvenWithTheSameId,
                MovementTests.MovementYieldsAnimationToCastingWithoutRestartingIt,
                MovementTests.ActorDetachStopsBehaviorEvenWhenCastCleanupFails,
                MovementTests.NetworkMovementSharesPollingAndUsesActualArrival,
                MovementTests.NetworkMovementStreamsElapsedPositionsWithoutEarlyCompletion,
                MovementTests.NetworkAndLocalMovementReplaceEachOtherAndChainCallbacks,
                MovementTests.NetworkMovementStopFailuresRemainRetryable,
                MovementTests.NetworkMovementTimeoutStopsWithoutArriving,
                MovementTests.NetworkMovementRejectsInvalidOrCancelledRequestsBeforeDispatch,
                MovementTests.NetworkMovementNeverStopsARecycledEntity,
                MovementTests.ManagerRetainsFailedBorrowedNetworkMovementCleanup,
                MovementTests.NetworkMovementDetachingDuringDispatchStillStopsTheOwnedMove,
                CastTests.ReplacingCastCancelsOldCompletionAndDelayedOmen,
                CastTests.CastCancelAndDisposeRemovePendingWork,
                CastTests.StopCastingPreservesMovementAndAllowsRecast,
                CastTests.OmenTagsAreIndependentAcrossActorsAndRecasts,
                CastTests.ExternalCastAndRecycledEntityAreProtected,
                CastTests.ReentrantOmenReplacementSurvivesOuterCallbackFailure,
                CastTests.CompletionRemovalCanStartANewCastWithoutOverwritingIt,
                CastTests.CastCancellationReentryPreservesTheNewerCast,
                CastTests.CastCancellationReentryCannotResumeAfterCleanup,
                CastTests.CompletionRemovalPreservesAnExternalReplacementCast,
                CastTests.DelayedOmenSamplesCurrentPositionWithInvariantNumbers,
                CastTests.CastDisposeCleansEveryActorAfterCallbackFailure,
                CastTests.CompletionReadFailureStillRemovesOmenAndState,
                ManagerTests.ManagerClearsOwnedEntitiesAndOnlyDetachesBorrowedActors,
                ManagerTests.ManagerRejectsCreationOutsideItsSessionAndSupportsRestart,
                ManagerTests.ManagerCleanupContinuesAfterReleaseAndLoggingFailures,
                ManagerTests.ManagerRetriesFailedOwnedCleanupBeforeRestart,
                ManagerTests.ManagerRetriesUndeliveredLocalCreationAndBlocksRestart,
                ManagerTests.ManagerCreationCleanupPreservesOtherManagersEntities,
                ManagerTests.ManagerWorldResetReleasesUndeliveredCreationBarrier,
                ManagerTests.ManagerBlocksRestartDuringCleanupAndPreservesNewControllers,
                ManagerTests.ManagerDeletingOneActorPreservesOtherMovementAndCast,
                ManagerTests.ManagerStopAndRestartDiscardAllPreviousBehaviors,
                ManagerTests.ManagerRestoresPartyBeforeDeletingItsMembersAndRetriesFailure,
                ManagerTests.ManagerUsesOneEnmityRefreshAndCancelsOnClear,
                SessionStatusTests.SessionStatusesPreserveOriginalAndRefreshOwnedSlot,
                SessionStatusTests.SessionStatusesValidateActorsAndCapacityBeforeNativeCall,
                SessionStatusTests.SessionStatusesRetainCleanupAfterPartialSetFailure,
                SessionStatusTests.SessionStatusesRejectReadbackMismatchAndPreserveResponsibility,
                SessionStatusTests.SessionStatusesRetryRemovalBeforeReleasingSource,
                SessionStatusTests.SessionStatusesTreatReplacementAndExpiryAsReleased,
                SessionStatusTests.SessionStatusesWorldChangeSkipsOldAddress,
                SessionStatusTests.SessionStatusesDeleteAndDetachCleanDependenciesFirst,
                SessionStatusTests.SessionStatusesRetryPartialRemovalWithoutTouchingOtherStatuses,
                PacketCodecTests.CoordinateEncodingClampsBeforeIntegerConversion,
                PacketCodecTests.CoordinateAndHeadingEncodingRejectNonFiniteInputs,
                PacketCodecTests.PacketCoordinateAndHeadingQuantizationRemainStable,
                ReplayTests.ReplayReadsHeaderChaptersAndPackets,
                ReplayTests.ReplayPreservesUnknownPacketsAndOriginalOrdering,
                ReplayTests.ReplayReadsZeroAndMaximumPayloads,
                ReplayTests.ReplayAcceptsUnfinalizedAndMismatchedDeclaredLengths,
                ReplayTests.ReplayRejectsInvalidMagicVersionAndChapterCount,
                ReplayTests.ReplayReportsTruncatedHeaderAndChapterTableOffsets,
                ReplayTests.ReplayReportsTruncatedPacketHeaderAndPayloadOffsets,
                ReplayTests.ReplayPreservesUnfinalizedChapterOffsets,
                ReplayTests.ReplayHandlesShortReads,
                ReplayTests.ReplayCanEnumerateAgainAfterCompletionOrAbandonment,
                ReplayTests.ReplayRejectsConcurrentEnumerationAndDisposedAccess,
                ReplayTests.ReplayHonorsStreamOwnershipAndCapabilities,
                ReplayTests.ReplayOpcodeRecognitionRequiresExplicitProfile,
                ReplayTests.ReplayOpcodeLengthConflictAndAmbiguityRemainUnconfirmed,
                ReplayTests.ReplayRecordMarkersAndInvalidClassificationInputs,
                ReplayTests.ReplayExportPreservesInputAndListsEveryPacket,
                ReplayTests.ReplayExportRejectsExistingAndSourceDirectoryOutputs,
                ReplayTests.ReplayExportNeverPublishesTruncatedPacketLists,
                ReplayTests.ReplayExportReportsBothUnfinalizedAndMismatchedHeader,
                PresetPlacementTests.SpreadPlacementKeepsEveryMemberInsideArenaAndSeparated,
                PresetPlacementTests.SpreadPlacementRejectsImpossibleLayoutInsteadOfReturningFallback,
                ZonePhaseTests.PhaseCancellationStopsPendingApplication,
                ZonePhaseTests.PhaseReplacementCannotResumeStaleApplication,
                ZonePhaseTests.PhaseCancellationBetweenPassesPreventsSecondWrite,
                ZonePhaseTests.PhaseApplicationRetainsCallingContextAndRecoversFromFailure,
                FirewallTests.FirewallEnableAndDisableAreIdempotent,
                FirewallTests.FirewallEnableFailuresRollBackEveryCompletedStage,
                FirewallTests.FirewallEnableRollbackFailureRetainsOwnershipAndOriginalErrors,
                FirewallTests.FirewallDisableRetriesOnlyTheFailedHook,
                FirewallTests.FirewallFreeFailureRetriesWithoutRewritingRestoredCode,
                FirewallTests.FirewallCleanupUsesCapturedMemoryAndTargetsAfterProcessSwitch,
                FirewallTests.FirewallProcessSwitchRetainsLiveCleanupFailuresForRetry,
                FirewallTests.FirewallProcessResetDiscardsOnlyUnavailableOwnership,
                FirewallTests.FirewallProcessResetRetryHandlesLaterExitAndResetsMode,
                FirewallTests.FirewallRejectsIncompleteCaptureBeforePatching,
                FirewallTests.FirewallHeartbeatReadStaysBoundToCapturedProcess,
                NetworkSpawnTests.NetworkPacketLayoutPreservesNativeOffsets,
                NetworkSpawnTests.NetworkSpawnDefaultsUseCsvAndActCoordinates,
                NetworkSpawnTests.NetworkSpawnOverridesDoNotRequireCsvRows,
                NetworkSpawnTests.NetworkSpawnCopiesAppearanceIntoEachPacket,
                NetworkSpawnTests.NetworkSpawnNamesEndAtCompleteUtf8Characters,
                NetworkSpawnTests.NetworkSpawnRejectsInvalidValuesBeforeUsingCsv,
                NetworkSpawnTests.NetworkSpawnReportsMissingRowsAndAllowsUnnamedActors,
                NetworkPlayerSpawnTests.PlayerPacketLayoutPreservesNativeOffsets,
                NetworkPlayerSpawnTests.PlayerAppearanceSnapshotDecodesNativeBytesIndependently,
                NetworkPlayerSpawnTests.PlayerSpawnDefaultsUseSourceAppearanceAndExplicitJob,
                NetworkPlayerSpawnTests.PlayerSpawnPacketsOwnTheirAppearanceBytes,
                NetworkPlayerSpawnTests.PlayerSpawnOverridesAndNamesAreIndependentOfSource,
                NetworkPlayerSpawnTests.PlayerJobEquipmentPreservesFaceAccessoriesAndFallback,
                NetworkPlayerSpawnTests.PlayerSpawnRejectsInvalidVitalsJobsAndAppearance,
                NetworkManagerTests.NetworkPlayersAndNpcsSharePollingAndPreserveKinds,
                NetworkManagerTests.NetworkPlayersValidateBeforeSpawningAndCancelWithNpcs,
                NetworkManagerTests.NetworkBatchSharesPollingAndReadyRunsOnce,
                NetworkManagerTests.ClearCancelsPendingNetworkSpawnsAndRejectsReuse,
                NetworkManagerTests.NetworkCleanupRetriesFailedCancellationWithoutDeliveringOldCallbacks,
                NetworkManagerTests.FirstReadyCallbackCanClearPendingBatch,
                NetworkManagerTests.NetworkReadyCallbackCanCancelScheduler,
                NetworkManagerTests.NetworkSpawnTimeoutCancelsRequest,
                NetworkManagerTests.NetworkReadyFailureClearsWholeBatch,
                NetworkManagerTests.NetworkTimeoutClearsReadyAndWaitingBatch,
                NetworkManagerTests.StoppedSchedulerRejectsNewNetworkRequestAndRollsItBack,
                NetworkManagerTests.FailedNetworkQueueRollbackRetainsRequestForCleanup,
                NetworkManagerTests.NetworkDispatchAndRollbackFailureRemainOwnedBySession,
                NetworkManagerTests.NetworkDispatchRollbackSuccessLeavesNoPendingRequest,
                NetworkManagerTests.NetworkPreflightFailureCreatesNoCleanupRecord,
                NetworkManagerTests.InvalidNetworkCallbacksAndTimeoutsDoNotBeginSpawning,
                NetworkManagerTests.NetworkReadyCallbacksRunOutsideManagerLock,
                NetworkManagerTests.RestartDoesNotDeliverOldNetworkRequests
            };

            var failures = 0;
            foreach (var test in tests)
            {
                try
                {
                    test();
                    Console.WriteLine("PASS " + test.Method.Name);
                }
                catch (Exception exception)
                {
                    failures++;
                    Console.Error.WriteLine("FAIL " + test.Method.Name + ": " + exception);
                }
            }

            Console.WriteLine("{0}/{1} tests passed.", tests.Length - failures, tests.Length);
            return failures == 0 ? 0 : 1;
        }

        private static void TimelineComposition()
        {
            Action noop = () => { };
            var repeated = new List<int>();
            var child = new SimTimeline().At(0.2, noop).After(0.3, noop);
            var timeline = new SimTimeline()
                .At(3, noop)
                .At(1, noop)
                .After(0.5, noop)
                .At(4, child)
                .After(0.1, noop)
                .Repeat(6, 0.2, 3, repeated.Add)
                .Repeat(99, 1, 0, repeated.Add)
                .At(99, new SimTimeline())
                .After(0.1, noop);

            SequenceNear(new[] { 3.0, 1, 1.5, 4.2, 4.5, 4.6, 6, 6.2, 6.4, 6.5 },
                timeline.Snapshot().Select(node => node.Seconds));
            foreach (var node in timeline.Snapshot()) node.Action();
            SequenceEqual(new[] { 0, 1, 2 }, repeated);

            // Child insertion order defines the cursor, even when its last node is earlier.
            var unordered = new SimTimeline().At(4, noop).At(1, noop);
            SequenceNear(new[] { 6.0, 3, 3.5 },
                new SimTimeline().At(2, unordered).After(0.5, noop).Snapshot().Select(node => node.Seconds));
        }

        private static void SnapshotIsolation()
        {
            var calls = new List<string>();
            var child = new SimTimeline().At(0, () => calls.Add("original"));
            var parent = new SimTimeline().At(0, child);
            child.At(0, () => calls.Add("late child"));

            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler())
            {
                scheduler.Play(parent);
                parent.At(0, () => calls.Add("late parent"));
                scheduler.Schedule(0.01, finished.Set);
                scheduler.Start();
                Wait(finished, "snapshot execution");
                scheduler.Dispose();
                SequenceEqual(new[] { "original" }, calls);
            }
        }

        private static void InvalidInputIsAtomic()
        {
            Action noop = () => { };
            var timeline = new SimTimeline().At(1, noop);
            using (var scheduler = new SimScheduler())
            {
                foreach (var invalid in new[] { -1.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                {
                    Throws<ArgumentOutOfRangeException>(() => timeline.At(invalid, noop));
                    Throws<ArgumentOutOfRangeException>(() => timeline.After(invalid, noop));
                    Throws<ArgumentOutOfRangeException>(() => timeline.Repeat(0, invalid, 1, _ => { }));
                    Throws<ArgumentOutOfRangeException>(() => scheduler.Schedule(invalid, noop));
                    Throws<ArgumentOutOfRangeException>(() => scheduler.ScheduleAfter(invalid, noop));
                    Throws<ArgumentOutOfRangeException>(() => scheduler.Play(timeline, invalid));
                }
                Throws<ArgumentNullException>(() => timeline.At(0, (Action)null));
                Throws<ArgumentNullException>(() => timeline.At(0, (SimTimeline)null));
                Throws<ArgumentNullException>(() => timeline.Repeat(0, 1, 1, null));
                Throws<ArgumentOutOfRangeException>(() => timeline.Repeat(0, 1, -1, _ => { }));
                Throws<ArgumentOutOfRangeException>(() => timeline.Repeat(double.MaxValue, double.MaxValue, 3, _ => { }));
                Throws<ArgumentOutOfRangeException>(() => timeline.At(double.MaxValue,
                    new SimTimeline().At(double.MaxValue, noop)));
                Throws<ArgumentNullException>(() => scheduler.Schedule(0, null));
                Throws<ArgumentNullException>(() => scheduler.Play(null));
                Assert(timeline.Snapshot().Length == 1, "Rejected definitions must not partially change the timeline.");
                Assert(scheduler.PendingCount == 0, "Rejected schedules must not enqueue nodes.");

                scheduler.Cancel();
                Throws<InvalidOperationException>(scheduler.Start);
                Throws<InvalidOperationException>(() => scheduler.Schedule(0, noop));
                scheduler.Dispose();
                Throws<ObjectDisposedException>(scheduler.Start);
                Throws<ObjectDisposedException>(() => scheduler.ScheduleAfter(0, noop));
                Throws<ObjectDisposedException>(() => scheduler.Play(timeline));
            }
        }

        private static void StableDeadlineOrdering()
        {
            var calls = new List<string>();
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler())
            {
                scheduler.Schedule(0.04, () => calls.Add("first at 40"));
                scheduler.Schedule(0.01, () => calls.Add("at 10"));
                scheduler.Schedule(0.04, () => calls.Add("second at 40"));
                scheduler.Schedule(0.02, () => calls.Add("at 20"));
                scheduler.Schedule(0.05, finished.Set);
                scheduler.Start();
                scheduler.Start();
                Wait(finished, "ordered nodes");
                scheduler.Dispose();
                SequenceEqual(new[] { "at 10", "at 20", "first at 40", "second at 40" }, calls);
            }
        }

        private static void RejectAsyncVoidActions()
        {
            Action asyncAction = async () => await Task.Yield();
            Action<int> asyncRepeat = async _ => await Task.Yield();
            Action multicast = () => { };
            multicast += asyncAction;
            var timeline = new SimTimeline();
            using (var scheduler = new SimScheduler())
            {
                Throws<ArgumentException>(() => timeline.At(0, asyncAction));
                Throws<ArgumentException>(() => timeline.After(0, asyncAction));
                Throws<ArgumentException>(() => timeline.Repeat(0, 1, 3, asyncRepeat));
                Throws<ArgumentException>(() => timeline.At(0, multicast));
                Throws<ArgumentException>(() => scheduler.Schedule(0, asyncAction));
                Throws<ArgumentException>(() => scheduler.ScheduleAfter(0, multicast));
                Assert(timeline.Snapshot().Length == 0 && scheduler.PendingCount == 0,
                    "Async void callbacks must be rejected before adding any nodes.");
            }
        }

        private static void NestedTimelineUsesPlannedTime()
        {
            var calls = new List<string>();
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler())
            {
                SimScheduledAction actualRelative = null;
                scheduler.Schedule(0, () =>
                {
                    entered.Set();
                    Wait(release, "release late parent");
                    scheduler.Play(new SimTimeline().At(0.02, () => calls.Add("planned child")));
                    actualRelative = scheduler.ScheduleAfter(0.02, () =>
                    {
                        calls.Add("actual relative");
                        finished.Set();
                    });
                });
                scheduler.Schedule(0.04, () => calls.Add("absolute marker"));
                scheduler.Start();
                try
                {
                    Wait(entered, "late parent entry");
                    Thread.Sleep(80);
                    release.Set();
                    Wait(finished, "relative callback");
                }
                finally { release.Set(); }
                scheduler.Dispose();
                SequenceEqual(new[] { "planned child", "absolute marker", "actual relative" }, calls);
                Assert(actualRelative.Seconds > 0.04, "ScheduleAfter must use actual elapsed time inside a late callback.");
            }
        }

        private static void EarlierDeadlineWakesWorker()
        {
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler())
            {
                // The maximum finite deadline also exercises the bounded native wait conversion.
                var distant = scheduler.Schedule(double.MaxValue, () => { throw new Exception("Distant node ran."); });
                scheduler.Start();
                Thread.Sleep(20);
                scheduler.ScheduleAfter(0.01, finished.Set);
                Wait(finished, "wake for newly inserted earlier deadline");
                scheduler.Dispose();
                Assert(distant.IsCancelled, "The distant node should remain pending until disposal.");
            }
        }

        private static void CancelOneNodeAndRepairHeap()
        {
            const int count = 2048;
            var seen = new List<int>();
            var handles = new SimScheduledAction[count];
            var deadlines = new double[count];
            var random = new Random(314159);
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler())
            {
                for (var i = 0; i < count; i++)
                {
                    var index = i;
                    deadlines[i] = random.Next(4) * 0.001;
                    handles[i] = scheduler.Schedule(deadlines[i], () => seen.Add(index));
                }
                for (var i = 0; i < count; i += 3)
                {
                    Assert(handles[i].Cancel(), "First queued cancellation should succeed.");
                    Assert(!handles[i].Cancel(), "Repeated cancellation should be harmless.");
                    Assert(handles[i].IsCancelled && handles[i].IsCompleted, "Cancelled handles must be terminal.");
                }
                scheduler.Schedule(0.01, finished.Set);
                scheduler.Start();
                Wait(finished, "remaining heap nodes");
                scheduler.Dispose();
                SequenceEqual(Enumerable.Range(0, count).Where(i => i % 3 != 0)
                    .OrderBy(i => deadlines[i]).ThenBy(i => i), seen);
                Assert(handles.All(handle => handle.IsCompleted), "Every handle must be terminal after disposal.");
            }
        }

        private static void ExceptionCancelsBeforeReporting()
        {
            var expected = new InvalidOperationException("expected node failure");
            Exception reported = null;
            string reportedName = null;
            var stoppedBeforeReport = false;
            var unexpectedRan = false;
            SimScheduler scheduler = null;
            using (var reportedEvent = new ManualResetEventSlim())
            {
                scheduler = new SimScheduler((exception, name) =>
                {
                    reported = exception;
                    reportedName = name;
                    stoppedBeforeReport = !scheduler.IsRunning && scheduler.PendingCount == 0;
                    reportedEvent.Set();
                    throw new Exception("Error reporting itself failed.");
                });
                using (scheduler)
                {
                    var failing = scheduler.Schedule(0, () => { throw expected; }, "failing node");
                    var skipped = scheduler.Schedule(0, () => unexpectedRan = true);
                    scheduler.Start();
                    Wait(reportedEvent, "error handler");
                    scheduler.Dispose();
                    Assert(ReferenceEquals(reported, expected) && reportedName == "failing node", "The error handler needs the original exception and node name.");
                    Assert(stoppedBeforeReport, "Failure must cancel the queue before error handling.");
                    Assert(!unexpectedRan && skipped.IsCancelled && failing.IsCompleted, "Failure must stop remaining work and finish the faulted handle.");
                }
            }
        }

        private static void CancelDoesNotInterruptCallback()
        {
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler())
            {
                var finished = false;
                var active = scheduler.Schedule(0, () =>
                {
                    entered.Set();
                    Wait(release, "cancelled active callback release");
                    finished = true;
                });
                var skipped = scheduler.Schedule(0, () => { throw new Exception("Cancelled node ran."); });
                scheduler.Start();
                try
                {
                    Wait(entered, "active callback");
                    Assert(!active.Cancel(), "Cancellation cannot reclaim a running node.");
                    scheduler.Cancel();
                    var stoppedAt = scheduler.ElapsedSeconds;
                    Thread.Sleep(20);
                    Assert(scheduler.ElapsedSeconds == stoppedAt, "Cancellation must freeze the clock.");
                    Assert(!active.IsCompleted && skipped.IsCancelled && scheduler.PendingCount == 0,
                        "Cancel should clear queued work without interrupting the active callback.");
                }
                finally { release.Set(); }
                scheduler.Dispose();
                Assert(finished && active.IsCompleted, "The already running callback should finish normally.");
            }
        }

        private static void DisposeWaitsForCallback()
        {
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var disposeStarted = new ManualResetEventSlim())
            using (var disposeFinished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler())
            {
                Exception threadFailure = null;
                var completed = false;
                scheduler.Schedule(0, () =>
                {
                    entered.Set();
                    Wait(release, "dispose barrier release");
                    completed = true;
                });
                scheduler.Start();
                Wait(entered, "dispose barrier entry");
                var disposer = BackgroundThread(() =>
                {
                    try
                    {
                        disposeStarted.Set();
                        scheduler.Dispose();
                    }
                    catch (Exception ex) { threadFailure = ex; }
                    finally { disposeFinished.Set(); }
                });
                try
                {
                    disposer.Start();
                    Wait(disposeStarted, "disposer start");
                    Assert(!disposeFinished.Wait(50), "Dispose returned while a callback still owned resources.");
                }
                finally { release.Set(); }
                Wait(disposeFinished, "disposer finish");
                Join(disposer);
                Assert(threadFailure == null && completed, "Dispose must finish after the callback completes.");
            }
        }

        private static void StopRaceDoesNotReportAnError()
        {
            foreach (var disposeInsideCallback in new[] { false, true })
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                var reported = false;
                using (var scheduler = new SimScheduler((_, __) => reported = true))
                {
                    scheduler.Schedule(0, () =>
                    {
                        entered.Set();
                        Wait(release, "stop race release");
                        if (disposeInsideCallback) scheduler.Dispose();
                        scheduler.Schedule(0, () => { });
                    });
                    scheduler.Start();
                    try
                    {
                        Wait(entered, "stop race entry");
                        scheduler.Cancel();
                    }
                    finally { release.Set(); }
                    scheduler.Dispose();
                    Assert(!reported, "An intentional stop racing with a callback must not report a second failure.");
                }
            }
        }

        private static void CallbackCanDisposeScheduler()
        {
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler())
            {
                var active = scheduler.Schedule(0, () =>
                {
                    Assert(scheduler.IsSchedulerThread, "Callback must identify its owning worker.");
                    scheduler.Dispose();
                    finished.Set();
                });
                var pending = scheduler.Schedule(0, () => { throw new Exception("Work after self-disposal ran."); });
                scheduler.Start();
                Wait(finished, "self disposal");
                scheduler.Dispose();
                Assert(active.IsCompleted && pending.IsCancelled, "Self-disposal must finish safely and cancel the queue.");
            }
        }

        private static void ConcurrentCancellation()
        {
            using (var start = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler())
            {
                var handles = Enumerable.Range(0, 4000).Select(_ => scheduler.Schedule(30, () => { })).ToArray();
                var errors = new ConcurrentQueue<Exception>();
                var cancellers = Enumerable.Range(0, 4).Select(worker => BackgroundThread(() =>
                {
                    try
                    {
                        Wait(start, "concurrent cancellation start");
                        for (var i = worker; i < handles.Length; i += 4)
                        {
                            handles[i].Cancel();
                            if (i > 100) scheduler.Cancel();
                        }
                    }
                    catch (Exception ex) { errors.Enqueue(ex); }
                })).ToArray();
                scheduler.Start();
                foreach (var thread in cancellers) thread.Start();
                start.Set();
                foreach (var thread in cancellers) Join(thread);
                scheduler.Dispose();
                Assert(errors.IsEmpty, "Concurrent cancellation must not corrupt the heap.");
                Assert(scheduler.PendingCount == 0 && handles.All(handle => handle.IsCancelled), "All pending nodes must be cancelled.");
            }
        }

        private static void ConcurrentProducersAndStop()
        {
            using (var start = new ManualResetEventSlim())
            using (var firstScheduled = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler())
            {
                var errors = new ConcurrentQueue<Exception>();
                var producers = Enumerable.Range(0, 4).Select(_ => BackgroundThread(() =>
                {
                    try
                    {
                        Wait(start, "concurrent producer start");
                        for (var i = 0; i < 2000; i++)
                        {
                            var handle = scheduler.Schedule(30, () => { });
                            firstScheduled.Set();
                            if (i % 2 == 0) handle.Cancel();
                        }
                    }
                    catch (InvalidOperationException) { /* The permanent stop rejects later producers. */ }
                    catch (Exception ex) { errors.Enqueue(ex); }
                })).ToArray();
                scheduler.Start();
                foreach (var thread in producers) thread.Start();
                start.Set();
                Wait(firstScheduled, "first producer node");
                scheduler.Cancel();
                foreach (var thread in producers) Join(thread);
                scheduler.Dispose();
                Assert(errors.IsEmpty && scheduler.PendingCount == 0, "Concurrent producers must finish without orphaned nodes.");
            }
        }

        private static void FiftyThousandNodesUseOneWorker()
        {
            const int count = 50000;
            ThreadPool.GetAvailableThreads(out var workersBefore, out var portsBefore);
            var stopwatch = Stopwatch.StartNew();
            var next = 0;
            var wrongOrder = false;
            var usedThreadPool = false;
            var threadIds = new HashSet<int>();
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler())
            {
                for (var i = 0; i < count; i++)
                {
                    var expected = i;
                    scheduler.Schedule(0, () =>
                    {
                        wrongOrder |= next != expected;
                        next++;
                        threadIds.Add(Thread.CurrentThread.ManagedThreadId);
                        usedThreadPool |= Thread.CurrentThread.IsThreadPoolThread;
                        if (next == count) finished.Set();
                    });
                }
                Assert(scheduler.PendingCount == count, "Queueing must not execute nodes before Start.");
                scheduler.Start();
                Wait(finished, "50,000 scheduled nodes");
                scheduler.Dispose();
            }
            stopwatch.Stop();
            ThreadPool.GetAvailableThreads(out var workersAfter, out var portsAfter);
            Assert(next == count && !wrongOrder, "Every same-time node must execute exactly once in insertion order.");
            Assert(threadIds.Count == 1 && !usedThreadPool, "All callbacks must use one dedicated non-pool worker.");
            Console.WriteLine("  {0:N0} nodes: {1} ms; workers used: {2}; available pool worker delta: {3}; IO delta: {4} (observations only)",
                count, stopwatch.ElapsedMilliseconds, threadIds.Count, workersAfter - workersBefore, portsAfter - portsBefore);
        }

        private static Thread BackgroundThread(ThreadStart action)
            => new Thread(action) { IsBackground = true };

        private static void Wait(ManualResetEventSlim signal, string description)
            => Assert(signal.Wait(TimeoutMs), "Timed out waiting for " + description + ".");

        private static void Join(Thread thread)
            => Assert(thread.Join(TimeoutMs), "A helper thread did not exit.");

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new Exception("Expected " + typeof(T).Name + ".");
        }

        private static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        {
            var left = expected.ToArray();
            var right = actual.ToArray();
            Assert(left.SequenceEqual(right), "Expected [" + string.Join(", ", left) + "]; actual [" + string.Join(", ", right) + "].");
        }

        private static void SequenceNear(IEnumerable<double> expected, IEnumerable<double> actual)
        {
            var left = expected.ToArray();
            var right = actual.ToArray();
            Assert(left.Length == right.Length && left.Zip(right, (a, b) => Math.Abs(a - b) < 1e-9).All(equal => equal),
                "Timeline node times did not match the defined composition.");
        }
    }
}
