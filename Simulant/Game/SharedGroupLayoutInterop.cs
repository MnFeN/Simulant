using Simulant.Game.FFCS.Client.LayoutEngine;
using Simulant.Game.FFCS.Client.LayoutEngine.Group;
using System;

namespace Simulant.Game
{
    // FFCS 未声明这两个入口。签名来自 Brio 的 SGLService，并与国服 2026.11.03.0000.0000 对照。
    // https://github.com/Etheirys/Brio/blob/199a13077ddc18d74a794a69c5d04f6fbd4d4a63/Brio/Game/Core/SGLService.cs
    internal static class SharedGroupLayoutInterop
    {
        // RVA 0x7987C0
        [SigPattern("E8 * * * * 48 89 04 ? C6 44")]
        private static IntPtr CreateWorldObjectFuncPtr { get; set; }

        // RVA 0x7989E0
        [SigPattern("48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 20 48 8B 19 48 8B F9 48 8B F2")]
        private static IntPtr DestroyWorldObjectFuncPtr { get; set; }

        /// <summary>创建原生场景组。<br />
        /// · layerKey 为布局层键，0xFFFF 选择全局层。<br />
        /// · instanceKey 写入 ILayoutInstance.Id.InstanceKey，传 0 时由函数合成。<br />
        /// · transform 和 sgbPath 分别为初始变换及场景组资源路径。<br />
        /// · a5 在核对的客户端函数体中未读取，原生调用点可见空字符串。<br />
        /// · active 为初始启用状态，RVA 0x6FCC2D 将其传给 SetActive。<br />
        /// · generatedKeyBits 仅在合成键时按位或入键值。<br />
        /// · worldObjectType 参与键合成及注册判断，低四位写入 ILayoutInstance.Flags1。<br />
        /// · layer 为可选 LayerManager 指针，用于覆盖按键查到的活动层。<br />
        /// · registrationData 在类型 0xA–0xC 时可随场景组登记。销毁入口使用同一指针，具体类型待确认。
        /// </summary>
        internal static SharedGroupLayoutInstance CreateWorldObject(ushort layerKey, uint instanceKey, in Transform transform,
            string sgbPath, IntPtr a5, bool active, uint generatedKeyBits, int worldObjectType, IntPtr layer, IntPtr registrationData)
        {
            GameFunctions.CheckStatus(CreateWorldObjectFuncPtr, DestroyWorldObjectFuncPtr);
            return CreateWorldObjectFuncPtr.Call<IntPtr>(layerKey, instanceKey, RemoteInput.Struct(transform), RemoteInput.CString(sgbPath),
                a5, active, generatedKeyBits, worldObjectType, layer, registrationData).As<SharedGroupLayoutInstance>();
        }

        // 原生入口接收 SharedGroupLayoutInstance**，销毁后会清空传入的指针槽。
        internal static void DestroyWorldObject(SharedGroupLayoutInstance group, IntPtr registrationData)
        {
            GameFunctions.CheckStatus(DestroyWorldObjectFuncPtr);
            DestroyWorldObjectFuncPtr.Call(RemoteInput.Struct(group.Ptr), registrationData);
        }
    }
}
