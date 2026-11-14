using Simulant.Game;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Timeline;
using System;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U7a.Presets
{
    internal sealed partial class CrystallizeTimeLogic
    {
        private SimTimeline BuildMechanicTimeline()
        {
            var timeline = new SimTimeline();
            timeline.At(0, () => { Map(0x28, 0x20); _gaia.Cast(0x9D6A); _shiva.Cast(0x9D30); }, "暗之巫女与希瓦的时间结晶双读条");
            timeline.At(10.8, Assign, "施加随机点名与蓝色头标");
            timeline.At(11, () =>
            {
                foreach (var actor in _sand) actor.EnableDraw();
                for (uint i=0x22; i<=0x27; i++) Map(i, 2);
                _crystal.Cast(40174);
            }, "沙漏与北侧水晶");
            timeline.At(13.1, () => { _shiva.Entity.SetTargetable(false); _shiva.Entity.DisableDraw(); _wyrm.Entity.DisableDraw(); }, "希瓦离场");
            timeline.At(14.169, () =>
            {
                _gaia.Entity.SetTargetable(false);
                for (var i=0; i<6; i++)
                {
                    var wave=_plan.HourglassWave(i);
                    if (wave != 1) _sand[i].Entity.ActorControl(ActorControlCategory.Tether, 0, wave == 0 ? 134u : 133u, _gaia.Id, 15);
                }
            }, "沙漏加减速连线");
            timeline.At(14.252, () => { SpawnHeads(); _gaia.Cast(40293); }, "龙头生成与时间加减速");
            timeline.At(15.903, () => { for (uint i=0x22; i<=0x27; i++) Map(i, 0x20); }, "时间结晶地板连线");
            timeline.At(18.9, WatchHeads, "检查龙头与白圈接触");
            timeline.At(19.739, () =>
            {
                for (var i=0; i<6; i++)
                {
                    var wave=_plan.HourglassWave(i);
                    if (wave == 1) continue;
                    _sand[i].Entity.ActorControl(ActorControlCategory.TetherCancel);
                    _helpers[i].Entity.ExecuteActionEffect(wave == 0 ? 40294u : 40295u, _sand[i].Entity);
                }
            }, "沙漏速度变化");
            for (var i=0; i<6; i++)
            {
                var index=i;
                var hit=CrystallizeTimePlan.SandHits[_plan.HourglassWave(i)];
                timeline.At(hit-1.5, () => _sand[index].Cast(40299), "沙漏预兆");
            }
            for (var wave=0; wave<3; wave++)
            {
                var index=wave;
                timeline.At(CrystallizeTimePlan.SandHits[wave], () => ResolveSand(index), $"第 {wave+1} 组沙漏判定");
                var slots=wave == 0 ? new uint[] {0x22,0x25} : (wave == 2) == (Purple == CrystalPurple.MainDiagonal) ? new uint[] {0x24,0x27} : new uint[] {0x23,0x26};
                timeline.At(18.886 + wave*2, () => { foreach (var slot in slots) Map(slot, 0x80); }, "地板时间线预告");
                timeline.At(CrystallizeTimePlan.SandHits[wave]-1.5, () => { foreach (var slot in slots) Map(slot, 0x200); }, "地板时间线消退");
            }
            timeline.At(22, () =>
            {
                foreach (var side in new[] {-1,1}) Move(_plan.Red(CrystalDebuff.RedWind,side), _plan.Wind(side), 23, urgent:true);
            }, "红风穿入击退位置");
            timeline.At(22.888, ResolveWater, "黑暗大水四人分摊");
            timeline.At(22.98, () =>
            {
                var wind=Party[_plan.Red(CrystalDebuff.RedWind,-_plan.NorthSlowSide)].Pos;
                foreach (var slot in _plan.WaterGroup.Where(i => !_plan.IsRed(i))) Move(slot, CrystallizeTimePlan.WindRider(wind), 24.4, urgent:true);
            }, "蓝色三人对准红风击退");
            timeline.At(24.837, ResolveIceWindDark, "冰、风、暗同时结算");
            timeline.At(24.94, () =>
            {
                foreach (var side in new[] {-1,1}) Move(_plan.Red(CrystalDebuff.RedIce,side), _plan.RedAfterIce(side), 27.85, urgent:true);
            }, "红冰分摊或躲避第二轮沙漏");
            timeline.At(25.8, () =>
            {
                foreach (var slot in _plan.BlueOrder) Move(slot, _plan.Stack, 27.6, urgent:true);
                foreach (var side in new[] {-1,1})
                    Move(_plan.Red(CrystalDebuff.RedWind,side), side == _plan.NorthSlowSide ? CrystallizeTimePlan.Sector(19,20*side,0.2f,0.5) : _plan.RedHead(side), 27.2, urgent:true);
            }, "击退后五人分摊与红风等候撞头");
            timeline.At(27.5, () => Move(_plan.Red(CrystalDebuff.RedWind,_plan.NorthSlowSide), _plan.RedHead(_plan.NorthSlowSide), 29.5, urgent:true), "第二轮沙漏后红风撞头");
            timeline.At(27.7, () => CheckCleansed(Enumerable.Range(0,8).Where(i => _plan.Debuffs[i] == CrystalDebuff.RedIce), "红冰龙头"), "红冰清除检查");
            timeline.At(27.885, ResolveEarth, "黑暗神圣五人分摊");
            timeline.At(27.98, () =>
            {
                foreach (var slot in Enumerable.Range(0,8).Where(i => _plan.Debuffs[i] != CrystalDebuff.RedWind)) Move(slot, _plan.NorthRest, 32.1, urgent:true);
            }, "躲避最后一组沙漏与龙头爆炸");
            AddWaveTimeline(timeline);
            AddPuddleRoutes(timeline);
            timeline.At(41.875, () =>
            {
                for (var i=0; i<8; i++) ClearStatus(i,4174);
                Helper(18,Center).Execute(40281);
            }, "寂灭");
            timeline.At(43.759, SaveRewind, "记录回返位置");
            timeline.At(44.16, () => { _gaia.EnableDraw(); _gaia.Entity.ActorMove(new Vector3(Center + new Vector2(0,5),0),0,0); _gaia.Cast(40288); }, "碎灵一击读条");
            timeline.At(44, () => MoveAll(_plan.Spread,47.05,urgent:true), "回返记录后分散");
            timeline.At(46.609, () => { _shiva.EnableDraw(); _shiva.Entity.ActorMove(new Vector3(Center,0),Heading(_plan.WaveStart(0)-Center),0); _shiva.Cast(40229); }, "第一次圣翼读条");
            timeline.At(47.375, ResolveSpiritTaker, "碎灵一击随机跳跃");
            timeline.At(50.759, Return, "回返与眩晕");
            timeline.At(52.758, () => WingCast(0), "第一次圣翼击退预备");
            timeline.At(53.241, () => ResolveWing(0), "第一次圣翼击退");
            timeline.At(55.343, () => _shiva.Cast(40230), "第二次圣翼读条");
            timeline.At(57.264, () => WingCast(1), "第二次圣翼击退预备");
            timeline.At(57.697, () => ResolveWing(1), "第二次圣翼击退");
            timeline.At(60.2, () =>
            {
                for (var i=0; i<8; i++) ClearStatus(i,4163);
                MoveAll(CrystallizeTimePlan.AkhMorn,66.9,71.5);
            }, "击退落地后左右分组");
            timeline.At(61.55, () =>
            {
                for (uint i=0x22;i<=0x27;i++) Map(i,8);
                Map(0x28,0x40);
                _shiva.Entity.ActorMove(new Vector3(Center + new Vector2(-3.54f,0),0),0,0);
                _gaia.Entity.ActorMove(new Vector3(Center + new Vector2(3.54f,0),0),0,0);
                _wyrm.Entity.ActorMove(new Vector3(Center + new Vector2(-3.54f,0),0),0,0);
                _wyrm.EnableDraw();
            }, "双 Boss 返回场中");
            timeline.At(63.018, () => { _shiva.Entity.SetTargetable(true); _gaia.Entity.SetTargetable(true); }, "双 Boss 可选中");
            timeline.At(63.118, () => { _shiva.Cast(40247,target:MT); _gaia.Cast(40302,target:ST); }, "死亡轮回双读条");
            for (var hit=0;hit<4;hit++)
            { var index=hit; timeline.At(68.018+hit*1.1, () => ResolveAkhMorn(index), $"死亡轮回第 {hit+1} 次分摊"); }
            timeline.At(71.418, () => { _shiva.Cast(40249); _gaia.Cast(40304); MoveAll(_ => CrystallizeTimePlan.MornAfah,77.8,79); }, "无尽顿悟双读条与八人集合");
            timeline.At(78.318, ResolveMornAfah, "无尽顿悟八人分摊");
            timeline.At(81, () =>
            {
                foreach (var actor in Party) actor.SuspendBehavior(3600);
                _host.LogSim($"[时间结晶] 流程结束，记录 {_mistakes} 项失误。点击停止可回收实体、头标和场地标点。");
            }, "时间结晶流程结束");
            return timeline;
        }

        private void AddWaveTimeline(SimTimeline timeline)
        {
            for (var wave=0; wave<2; wave++)
            for (var step=0; step<4; step++)
            {
                var w=wave; var s=step; var hit=CrystallizeTimePlan.WaveHits[w][s];
                timeline.At(hit-(step == 0 ? 3 : 2), () =>
                {
                    var pos=_plan.WavePosition(w,s); var heading=Heading(_plan.WaveDirection(w));
                    if (s == 0)
                    { _shiva.EnableDraw(); _shiva.Entity.ActorMove(new Vector3(pos,0),heading,0); _shiva.Cast(40251); }
                    Helper(8+w*4+s,pos,heading).Cast(s == 0 ? 40252u : 40253u);
                }, "光波预兆与读条");
                timeline.At(hit, () => ResolveWave(w,s), $"第 {w+1} 组光波第 {s+1} 次");
            }
        }

        private void AddPuddleRoutes(SimTimeline timeline)
        {
            timeline.At(32.46, () =>
            {
                var puddles=Enumerable.Range(0,4).Select(i=>_puddles[i]==null?_plan.PuddleDefault(i+1):_puddlePositions[i]).ToArray();
                for (var slot=0;slot<8;slot++)
                {
                    var member=slot;
                    foreach (var move in _plan.RewindRoute(slot,puddles))
                        Scheduler.Schedule(_origin+move.Start,()=>Move(member,move.Area,move.Arrival,urgent:true),"光波、白圈与回返走位");
                }
            },"按白圈实际位置安排光波走位");
        }

        private void WingCast(int wave)
        {
            _shiva.Entity.ActorMove(new Vector3(_plan.WaveStart(wave),0),Heading(_plan.WaveDirection(wave)),0);
            _shiva.Cast(40332);
        }

        private void SaveRewind()
        {
            CheckCleansed(Enumerable.Range(0,8), "红蓝龙咒解除");
            for (var slot=0;slot<8;slot++)
            { _rewind[slot]=Party[slot].Pos; ClearStatus(slot,4208); Status(slot,2452,7); }
        }

        private void Return()
        {
            for (var slot=0;slot<8;slot++)
            {
                var actor=Party[slot];
                actor.SuspendBehavior(9.3);
                ClearStatus(slot,2452); Status(slot,4163,9);
                // 回返为瞬移；自身不接受 ActorMove，直接同步原生对象及显示对象的位置。
                if (slot == Self) actor.Pos=_rewind[slot];
                else actor.Entity.ActorMove(new Vector3(_rewind[slot],0),actor.Heading,0);
            }
        }
    }
}
