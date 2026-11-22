using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.ACT
{
    internal static class TriggernometryPartyVariables
    {
        private const string VariableNamespace = "Triggernometry.Core.Variables.";
        private const string Changer = "Simulant";
        private static readonly string[] Role8 = { "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };

        /// <summary>按 PartyListPanel 的字段和类型写入临时变量。</summary>
        // to-do: Triggernometry 增加自动设置所有变量的方法，直接指定小队顺序即可调用，不需手动构造
        internal static void Write(uint[] ids, string[] names, Job[] jobs, int myIndex, Action<string> warn)
        {
            try
            {
                var plugin = FindPlugin();
                if (plugin == null)
                    return;

                var pluginType = plugin.GetType();
                var assembly = pluginType.Assembly;
                var getVariableStore = GetMethod(pluginType, "GetVariableStore", typeof(bool));
                var store = getVariableStore.Invoke(plugin, new object[] { false });
                var storeType = store.GetType();
                var scalar = (IDictionary)GetProperty(storeType, "Scalar").GetValue(store);
                var list = (IDictionary)GetProperty(storeType, "List").GetValue(store);
                var dict = (IDictionary)GetProperty(storeType, "Dict").GetValue(store);

                var i18n = assembly.GetType("Triggernometry.Localization.I18n", throwOnError: true);
                var isChinese = (bool)GetProperty(i18n, "IsChineseEnvironment").GetValue(null);
                var jobType = assembly.GetType("Triggernometry.FFXIV.Job", throwOnError: true);
                var getJob = GetMethod(jobType, "GetJob", typeof(int));
                var jobName = GetProperty(jobType, isChinese ? "NameCN2" : "NameEN3");
                var jobRole = GetProperty(jobType, "Role");
                var jobRows = jobs.Select(job => getJob.Invoke(null, new object[] { (int)job })).ToArray();
                var jobNames = jobRows.Select(row => (string)jobName.GetValue(row)).ToArray();
                var hasRoleSlots = ids.Length == 4 || ids.Length == Role8.Length;
                var rolesPerGroup = ids.Length / 4;
                var expectedRoles = Enumerable.Repeat("Tank", rolesPerGroup)
                    .Concat(Enumerable.Repeat("Healer", rolesPerGroup))
                    .Concat(Enumerable.Repeat("DPS", rolesPerGroup * 2));
                var isStandard = hasRoleSlots &&
                    jobRows.Select(row => jobRole.GetValue(row).ToString()).SequenceEqual(expectedRoles);

                var hexIds = ids.Select(id => id.ToString("X", CultureInfo.InvariantCulture)).ToArray();
                var scalarType = assembly.GetType(VariableNamespace + "VariableScalar", throwOnError: true);
                var newScalar = GetCtor(scalarType, typeof(string));
                var listType = assembly.GetType(VariableNamespace + "VariableList", throwOnError: true);
                var newList = GetCtor(listType, typeof(IEnumerable<string>));
                var lastChanger = GetProperty(listType, "LastChanger");
                var dictType = assembly.GetType(VariableNamespace + "VariableDictionary", throwOnError: true);
                var newDict = GetCtor(dictType);
                var setValue = GetMethod(dictType, "SetValue", typeof(string), typeof(string), typeof(string));

                object MakeFlag(bool value) => newScalar.Invoke(new object[] { value ? "1" : "0" });

                object MakeList(string[] values)
                {
                    var result = newList.Invoke(new object[] { values });
                    lastChanger.SetValue(result, Changer);
                    return result;
                }

                object MakeDictionary(string[] values)
                {
                    var result = newDict.Invoke(null);
                    for (var slot = 0; slot < hexIds.Length; slot++)
                        setValue.Invoke(result, new object[] { hexIds[slot], values[slot], Changer });
                    return result;
                }

                var isTankOrHealer = myIndex <= 2 * rolesPerGroup;
                var isTankOrMelee = myIndex <= rolesPerGroup || (myIndex > 2 * rolesPerGroup && myIndex <= 3 * rolesPerGroup);
                // 先完整构造 Triggernometry 的变量对象，再按 PartyListPanel 使用的锁发布。
                var scalars = new Dictionary<string, object>
                {
                    ["myIdx"] = newScalar.Invoke(new object[] { myIndex.ToString(CultureInfo.InvariantCulture) }),
                    ["myIdx_isG1"] = ids.Length == Role8.Length ? MakeFlag(myIndex % 2 == 1) : null,
                    ["myIdx_isTH"] = hasRoleSlots ? MakeFlag(isTankOrHealer) : null,
                    ["myIdx_isTM"] = hasRoleSlots ? MakeFlag(isTankOrMelee) : null,
                    ["partyIsStandard"] = MakeFlag(isStandard)
                };
                var lists = new Dictionary<string, object>
                {
                    ["party"] = MakeList(hexIds),
                    ["partyName"] = MakeList(names),
                    ["partyJob"] = MakeList(jobNames)
                };
                var dictionaries = new Dictionary<string, object>
                {
                    ["partyIdToName"] = MakeDictionary(names),
                    ["partyIdToJob"] = MakeDictionary(jobNames),
                    ["partyIdToRole8"] = ids.Length == 8 ? MakeDictionary(Role8) : null,
                    ["partyIdToRole８"] = ids.Length == 8 ? MakeDictionary(Role8.Select(ToFullWidth).ToArray()) : null
                };

                SetVariables(scalar, scalars);
                SetVariables(list, lists);
                SetVariables(dict, dictionaries);
            }
            catch (Exception error)
            {
                warn?.Invoke("写入 Triggernometry 模拟小队变量失败：" + error.GetBaseException().Message);
            }
        }

        private static object FindPlugin()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("Triggernometry.Core.RealPlugin", throwOnError: false);
                if (type == null)
                    continue;

                // Instance 是惰性初始化属性；只读已有实例，避免模拟器启动未启用的插件。
                var instance = (type.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
                    ?? throw new MissingFieldException(type.FullName, "_instance")).GetValue(null);
                if (instance != null)
                    return instance;
            }

            return null;
        }

        private static PropertyInfo GetProperty(Type type, string name)
            => type.GetProperty(name) ?? throw new MissingMemberException(type.FullName, name);

        private static MethodInfo GetMethod(Type type, string name, params Type[] parameterTypes)
            => type.GetMethod(name, parameterTypes)
                ?? throw new MissingMethodException(type.FullName, FormatSignature(name, parameterTypes));

        private static ConstructorInfo GetCtor(Type type, params Type[] parameterTypes)
            => type.GetConstructor(parameterTypes)
                ?? throw new MissingMethodException(type.FullName, FormatSignature(".ctor", parameterTypes));

        private static string FormatSignature(string name, Type[] parameterTypes)
            => $"{name}({string.Join<Type>(", ", parameterTypes)})";

        private static void SetVariables(IDictionary target, Dictionary<string, object> values)
        {
            lock (target)
                foreach (var item in values)
                {
                    if (item.Value == null)
                        target.Remove(item.Key);
                    else
                        target[item.Key] = item.Value;
                }
        }

        private static string ToFullWidth(string role)
            => new string(role.Select(character => (char)(character + 0xFEE0)).ToArray());
    }
}
