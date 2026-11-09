using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace CardKKK.IslandSurvival.Core
{
    /// <summary>仅复制本模块的纯数据公共字段；候选状态与运行配置不会共享可写列表。</summary>
    internal static class SurvivalCopies
    {
        private static readonly Dictionary<Type, FieldInfo[]> Fields = new Dictionary<Type, FieldInfo[]>();
        public static T Copy<T>(T value) { return (T)CopyValue(value); }
        private static object CopyValue(object value)
        {
            if (value == null) return null;
            Type type = value.GetType();
            if (type.IsValueType || type == typeof(string)) return value;
            if (value is IList list)
            {
                var result = (IList)Activator.CreateInstance(type);
                foreach (object item in list) result.Add(CopyValue(item));
                return result;
            }
            if (!Fields.TryGetValue(type, out FieldInfo[] fields))
            {
                fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
                Fields[type] = fields;
            }
            object copy = Activator.CreateInstance(type);
            foreach (FieldInfo field in fields) field.SetValue(copy, CopyValue(field.GetValue(value)));
            return copy;
        }
    }
}
