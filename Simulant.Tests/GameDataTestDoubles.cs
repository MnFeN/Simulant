using System;
using System.Collections.Generic;
using Lumina.Excel;
using Lumina.Excel.Sheets;

// 离线行为测试只替换表的存储；真实强类型行及客户端读取另由 Native/GameDataTests 验证。
namespace Simulant.Game.Data
{
    public sealed class GameDataService
    {
        public static GameDataService Instance { get; } = new GameDataService();
        private readonly Dictionary<Type, object> _tables = new Dictionary<Type, object>();
        public ExcelSheet<T> GetSheet<T>() where T : struct, IExcelRow<T>
        {
            if (!_tables.TryGetValue(typeof(T), out var table))
                _tables.Add(typeof(T), table = new ExcelSheet<T>());
            return (ExcelSheet<T>)table;
        }
    }
}

namespace Lumina.Excel
{
    public interface IExcelRow<T> { }

    public sealed class ExcelSheet<T> : Dictionary<int, T> where T : struct, IExcelRow<T>
    {
        public bool TryGetRow(uint id, out T row) => TryGetValue(unchecked((int)id), out row);
        public T? GetRowOrDefault(uint id) => TryGetRow(id, out var row) ? row : (T?)null;
    }

    public struct RowRef<T> where T : struct, IExcelRow<T>
    {
        public uint RowId;
        public T? ValueNullable;
        public T Value => ValueNullable ?? throw new InvalidOperationException("测试行引用不存在。");
        public static implicit operator RowRef<T>(int id) => new RowRef<T> { RowId = unchecked((uint)id) };
    }

    public struct TestText
    {
        private string _text;
        public string ExtractText() => _text ?? string.Empty;
        public static implicit operator TestText(string text) => new TestText { _text = text };
    }
}

namespace Lumina.Excel.Sheets
{
    public struct ModelChara : IExcelRow<ModelChara> { }
    public struct Battalion : IExcelRow<Battalion> { }
    public struct ActionTimeline : IExcelRow<ActionTimeline> { }

    public struct BNpcBase : IExcelRow<BNpcBase>
    {
        public RowRef<ModelChara> ModelChara;
        public RowRef<Battalion> Battalion;
    }

    public struct BNpcName : IExcelRow<BNpcName>
    {
        public TestText Singular;
    }

    public struct Action : IExcelRow<Action>
    {
        public uint RowId;
        public ushort Cast100ms;
        public sbyte Range;
        public RowRef<ActionTimeline> AnimationEnd;
        public byte CastType;
        public float XAxisModifier;
        public float EffectRange;
        public RowRef<Omen> Omen;
    }

    public struct Omen : IExcelRow<Omen>
    {
        public TestText Path;
    }
}
