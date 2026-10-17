using Simulant.ACT;
using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Core.Environment;
using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Event;
using Simulant.Game.FFCS.Client.Game.Network;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Game.FFCS.Client.Network;
using Simulant.Game.FFCS.Client.System.Framework;
using Simulant.Simulation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Simulant
{
    internal class Test
    {
        private readonly PluginHost _host;
        internal Test(PluginHost host)
        {
            _host = host;
        }

        internal async Task Run()
        {
            await 绝妖星P5塔();
        }

        public async Task 绝妖星P5塔()
        {
            var me = _host.EntityProvider.GetMyself();
            var data = new EObjData
            {
                Index = 0,
                BaseId = 2015294,
                Pos = me.Pos3D + new Vector3(-5, 0, 0),
            };
            var eobj = _host.EntitySpawner.SpawnEObj(data);
            TriggernometryInterop.InvokeNamedCallback("command", $"/e ID: {eobj.Id:X8} #{data.Index} @ {eobj.Native.Ptr.Hex()}");

            data = new EObjData
            {
                Index = 1,
                BaseId = 2015295,
                Pos = me.Pos3D + new Vector3(0, 0, 0),
            };
            eobj = _host.EntitySpawner.SpawnEObj(data);
            TriggernometryInterop.InvokeNamedCallback("command", $"/e ID: {eobj.Id:X8} #{data.Index} @ {eobj.Native.Ptr.Hex()}");

            data = new EObjData
            {
                Index = 2,
                BaseId = 2015296,
                Pos = me.Pos3D + new Vector3(5, 0, 0),
            };
            eobj = _host.EntitySpawner.SpawnEObj(data);
            TriggernometryInterop.InvokeNamedCallback("command", $"/e ID: {eobj.Id:X8} #{data.Index} @ {eobj.Native.Ptr.Hex()}");

            LogObjectArrays();
        }

        public async Task 绝妖星P3法阵()
        {
            var me = _host.EntityProvider.GetMyself();
            var data = new EObjData
            {
                Index = 0,
                BaseId = 0x1EC03A,
                Pos = me.Pos3D + new Vector3(-5, 0, 0),
            };
            var eobj = _host.EntitySpawner.SpawnEObj(data);
            TriggernometryInterop.InvokeNamedCallback("command", $"/e ID: {eobj.Id:X8} #{data.Index} @ {eobj.Native.Ptr.Hex()}");

            data = new EObjData
            {
                Index = 1,
                BaseId = 0x1EC03B,
                Pos = me.Pos3D + new Vector3(0, 0, 0),
            };
            eobj = _host.EntitySpawner.SpawnEObj(data);
            TriggernometryInterop.InvokeNamedCallback("command", $"/e ID: {eobj.Id:X8} #{data.Index} @ {eobj.Native.Ptr.Hex()}");

            data = new EObjData
            {
                Index = 2,
                BaseId = 0x1EC03C,
                Pos = me.Pos3D + new Vector3(5, 0, 0),
            };
            eobj = _host.EntitySpawner.SpawnEObj(data);
            TriggernometryInterop.InvokeNamedCallback("command", $"/e ID: {eobj.Id:X8} #{data.Index} @ {eobj.Native.Ptr.Hex()}");

            LogObjectArrays();
        }

        public async Task 绝伊甸地火Test()
        {
            var data = new EObjData
            {
                Index = 0,
                BaseId = 2014199,
                Pos = _host.EntityProvider.GetMyself().Pos3D,
            };
            var eobj = _host.EntitySpawner.SpawnEObj(data);
            TriggernometryInterop.InvokeNamedCallback("command", $"/e ID: {eobj.Id:X8} #{data.Index} @ {eobj.Native.Ptr.Hex()}");
            LogObjectArrays();
        }

        public async Task 异三角Test()
        {
            var data = new EObjData
            {
                Index = 5,
                BaseId = 2000052,
                SharedTimelineState = 0x2,
                Pos = _host.EntityProvider.GetMyself().Pos3D,
            };
            var eobj = _host.EntitySpawner.SpawnEObj(data);
            TriggernometryInterop.InvokeNamedCallback("command", $"/e ID: {eobj.Id:X8} @ index {data.Index}");
            LogObjectArrays();
        }

        public async Task EObjTowerTest()
        {
            var data = new EObjData
            {
                Index = 0,
                TargetableFlags = 5,
                BaseId = 2009610,
                SharedTimelineState = 0x41,
                Pos = _host.EntityProvider.GetMyself().Pos3D,
            };
            var eobj = _host.EntitySpawner.SpawnEObj(data);
            LogObjectArrays();
        }

        public async Task EObjSharedTimelineStateTest()
        {
            var data = new EObjData
            {
                Index = 6,
                TargetableFlags = 5,
                BaseId = 2009610,
                Pos = _host.EntityProvider.GetMyself().Pos3D,
            };
            var eobj = _host.EntitySpawner.SpawnEObj(data);

            LogObjectArrays();

            for (byte i = 0; i <= 0xFF; i++)
            {
                eobj.SetSharedTimelineState(i);
                TriggernometryInterop.InvokeNamedCallback("command", $"/e Timeline {i} = {i:X2}");

                if (i == 0xFF || _host.Disposed) break;
                await Task.Delay(1000);
            }
        }

        public async Task EObjTargetableFlagsTest()
        {
            
            var data = new EObjData
            {
                Index = 7,
                TargetableFlags = 5,
                BaseId = 2009610,
                // LayoutId = 7597525,
                // EventId = 0x8003757B,
                GimmickId = 0,
                SharedTimelineState = 0x21,
                // SharedGroupState = 4194307,
            };

            for (byte i = 0; i <= 0xFF; i++)
            {
                data.TargetableFlags = i;
                data.Pos = _host.EntityProvider.GetMyself().Pos3D;
                var eobj = _host.EntitySpawner.SpawnEObj(data);
                TriggernometryInterop.InvokeNamedCallback("command", $"/e {i}");

                if (i == 0xFF || _host.Disposed) break;
                await Task.Delay(1000);
            }
        }

        public void LogObjectArrays()
        {
            var objects = GameObjectManager.Instance().Objects;
            LogGameObjects("IndexSorted", objects.IndexSorted);
            LogGameObjects("GameObjectIdSorted", objects.GameObjectIdSorted);
            LogGameObjects("EntityIdSorted", objects.EntityIdSorted);

            void LogGameObjects(string name, GameObjectPtrs ptrs)
            {
                var sb = new StringBuilder();
                sb.AppendLine().AppendLine("== " + name + " ==");

                int i = 0;
                foreach (var obj in ptrs.GameObjects)
                {
                    if (!obj.IsNull())
                        sb.Append('#').Append(i)
                            .Append(" @ ").Append(obj.Ptr.Hex())
                            .Append(", Id=").Append(((uint)obj.EntityId).ToString("X8"))
                            .Append(", Index=").Append((ushort)obj.ObjectIndex)
                            .Append(", Type=").Append((ObjectKind)obj.ObjectKind)
                            .AppendLine();
                    i++;
                }
                _host.LogVerbose(sb.ToString());
            }
        }

        private void EntityExecuteTest(uint baseId, uint actionId)
        {
            var me = _host.EntityProvider.GetMyself();

            var bnpc = _host.EntitySpawner.SpawnBNpc(baseId, 0, 100);

            bnpc.Pos3D = me.Pos3D;
            bnpc.Heading = me.Heading;
                    bnpc.SetReadyToDraw();
                    bnpc.EnableDraw();

            bnpc.Execute(actionId);
            }

        private void EntityCastTest(uint baseId, uint actionId)
        {
            var me = _host.EntityProvider.GetMyself();

            var bnpc = _host.EntitySpawner.SpawnBNpc(baseId, 0, 100);
            bnpc.Pos3D = me.Pos3D;
            bnpc.Heading = me.Heading;
            bnpc.SetReadyToDraw();
            bnpc.EnableDraw();

            bnpc.Cast(actionId);

            _host.LogVerbose($"CastInfo @ {bnpc.Native.CastInfo.Ptr.Hex()}");
            _host.LogVerbose($"VFunc #81 Ret @ {bnpc.Native.Ptr.CallVFunc<IntPtr>(81).Hex()}");
        }

        private void TestEnvManager()
        {
            var phaseData = Content.U6b.U6b.P3;
            _host.EnvironmentService.SetWeather(phaseData.Weather);
            _host.EnvironmentService.SetBgm(phaseData.BGM);

            for (uint slot = 0; slot < phaseData.MapEffectFlags.Count; slot++)
            {
                var flag = phaseData.MapEffectFlags[(int)slot];
                _host.EnvironmentService.PlayMapEffect(slot, flag);
            }
        }

        private void TestInstanceDirectors()
        {
            var ef = EventFramework.Instance;
            _host.LogRuntime($"EventFramework: {(ulong)ef.Ptr:X}");
            _host.LogRuntime($"ContentDirector: {(ulong)ef.ContentDirector.Ptr:X}");
            _host.LogRuntime($"GetContentDirector(): {(ulong)ef.GetContentDirector().Ptr:X}");
            _host.LogRuntime($"GetInstanceContentDirector(): {(ulong)ef.GetInstanceContentDirector().Ptr:X}");
            _host.LogRuntime($"GetPublicContentDirector(): {(ulong)ef.GetPublicContentDirector().Ptr:X}");
        }

        private async void EntityTimelineTest()
        {
            var me = _host.EntityProvider.GetMyself();
            var bnpc = _host.EntitySpawner.SpawnBNpc(4909, 3765, 90);

            bnpc.Pos3D = me.Pos3D + new Vector3(1, 0, 0);
            bnpc.Heading = me.Heading;
            bnpc.SetReadyToDraw();
            bnpc.EnableDraw();

            var intervalMs = 10;
            var speed = 6f;
            var durationMs = 7000;

            bnpc.PlayBaseTimeline(13);

            var elapsed = 0;
            while (elapsed < durationMs)
            {
                bnpc.Y += speed * (intervalMs / 1000f);
                bnpc.Native.DrawObject.Position.Set(bnpc.Native.Position);
                await Task.Delay(intervalMs);
                elapsed += intervalMs;
            }
            bnpc.PlayBlendTimeline(3201);
            await Task.Delay(3000);

            _host.EntitySpawner.Delete(bnpc);
        }


        private void GetOnReceivePtr()
        {
            var ptr = Framework.Instance.NetworkModuleProxy.NetworkModule.PacketReceiverCallback.PacketDispatcher.Ptr.GetVFuncPtr(1);
            _host.LogRuntime($"OnReceive: {(ulong)ptr:X}");
        }
    }
}
