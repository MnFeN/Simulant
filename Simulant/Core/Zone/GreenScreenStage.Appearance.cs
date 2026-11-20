using Simulant.Game;
using Simulant.Game.FFCS.Client.Graphics.Kernel;
using Simulant.Game.FFCS.Client.Graphics.Render;
using Simulant.Game.FFCS.Client.LayoutEngine;
using Simulant.Game.FFCS.Client.LayoutEngine.Layer;
using Simulant.Game.FFCS.Client.System.Resource.Handle;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using SceneLight = Simulant.Game.FFCS.Client.Graphics.Scene.Light;

namespace Simulant.Core.Zone
{
    internal sealed partial class GreenScreenStage
    {
        private MaterialResourceHandle _floorMaterial;
        private uint _layoutId;
        private volatile bool _ready;

        internal bool IsReady => _ready && GameInterop.Connection == _connection && GameInterop.IsReady;
        internal GreenScreenAppearance Appearance { get; private set; } = GreenScreenAppearance.Default;

        internal void BeginTransition() => _ready = false;

        internal bool TryApplyAppearance(GreenScreenAppearance appearance)
        {
            if (!IsReady)
            {
                return false;
            }

            var applied = false;
            _connection.RunInFrame(() =>
            {
                if (!IsReady)
                {
                    return;
                }

                var world = LayoutWorld.Instance();
                var layout = world.IsNull() ? default : world.ActiveLayout;
                if (layout.IsNull() || layout.InitState.Get() != 7 || layout.TerritoryTypeId.Get() != TerritoryId || layout.ManagerBase.Id.Get() != _layoutId)
                {
                    return;
                }

                var current = Appearance;
                if (appearance.FloorColor != current.FloorColor || appearance.Emission != current.Emission
                    || appearance.Diffuse != current.Diffuse || appearance.Specular != current.Specular || appearance.Glare != current.Glare)
                {
                    WriteFloorMaterial(appearance);
                    current.FloorColor = appearance.FloorColor;
                    current.Emission = appearance.Emission;
                    current.Diffuse = appearance.Diffuse;
                    current.Specular = appearance.Specular;
                    current.Glare = appearance.Glare;
                    Appearance = current;
                }

                if (appearance.PointLight != current.PointLight)
                {
                    WriteLight(world, layout, GreenScreenScene.PointLightInstanceId, LightShape.PointLight, appearance.PointLight);
                    current.PointLight = appearance.PointLight;
                    Appearance = current;
                }

                if (appearance.WorldLight != current.WorldLight)
                {
                    WriteLight(world, layout, GreenScreenScene.WorldLightInstanceId, LightShape.WorldLight, appearance.WorldLight);
                    current.WorldLight = appearance.WorldLight;
                    Appearance = current;
                }

                applied = true;
            });
            return applied;
        }

        private void WriteFloorMaterial(GreenScreenAppearance appearance)
        {
            GameFunctions.CheckStatus(ConstantBuffer.LoadSourcePointerFuncPtr);
            var material = _floorMaterial.Material.ThrowIfNull();
            if (material.MaterialResourceHandle.Ptr != _floorMaterial.Ptr)
            {
                throw new InvalidOperationException("地板材质不属于当前绿幕场景。");
            }

            var package = _floorMaterial.ShaderPackageResourceHandle.ThrowIfNull().ShaderPackage.ThrowIfNull();
            var buffer = material.MaterialParameterCBuffer.ThrowIfNull();
            var size = buffer.ByteSize.Get();
            var source = buffer.TryGetSourcePointer();
            if (source.IsZero() || size <= 0 || size != package.MaterialConstantBufferSize.Get())
            {
                throw new InvalidOperationException("地板材质的着色器常量缓冲区不可读。");
            }

            var count = package.MaterialElementCount.Get();
            var elements = package.MaterialElements.ArrayField<ShaderPackage.MaterialElement>(0, count).Get();
            var data = source.ReadBytes(size);
            if (data?.Length != size)
            {
                throw new InvalidOperationException("未能完整读取地板材质的着色器常量。");
            }
            using (var writer = new BinaryWriter(new MemoryStream(data)))
            {
                appearance.WriteMaterialConstants((id, values) =>
                {
                    var element = elements.SingleOrDefault(item => item.CRC == id);
                    if (element.Size != sizeof(float) * values.Length || element.Offset + element.Size > data.Length)
                    {
                        throw new InvalidOperationException($"地板材质的着色器常量 {id:X8} 不匹配。");
                    }

                    writer.BaseStream.Position = element.Offset;
                    foreach (var value in values)
                    {
                        writer.Write(value);
                    }
                });
            }

            var destination = buffer.LoadSourcePointer(0, size);
            if (destination.IsZero() || _connection.Memory.WriteBytes(destination, data) != size)
            {
                throw new InvalidOperationException("未能完整写入地板材质的着色器常量。");
            }
        }

        private static void WriteLight(LayoutWorld world, LayoutManager layout, uint id, LightShape shape, Vector4 color)
        {
            GameFunctions.CheckStatus(SceneLight.UpdateLightCullingFuncPtr);
            var instance = world.GetLayoutInstance(InstanceType.Light, id);
            if (instance.IsNull() || instance.Layout.Ptr != layout.Ptr)
            {
                throw new InvalidOperationException("绿幕场景的灯光实例尚未加载。");
            }

            var light = instance.Ptr.As<LightLayoutInstance>().GraphicsObject.ThrowIfNull();
            var renderLight = light.RenderLight.ThrowIfNull();
            if (renderLight.LightShape != shape)
            {
                throw new InvalidOperationException("绿幕场景的灯光类型不符。");
            }

            // 原生灯光 SetColor 会把输入颜色各分量平方后交给渲染器。
            renderLight.ColorIntensity.Set(new Vector4(color.X * color.X, color.Y * color.Y, color.Z * color.Z, color.W));
            light.UpdateLightCulling();
        }
    }
}
