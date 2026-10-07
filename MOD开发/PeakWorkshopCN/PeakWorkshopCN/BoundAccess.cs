using System;
using System.Reflection;
using System.Reflection.Emit;

namespace PeakWorkshopCN
{
    // Bind once at installation; none of these reflection operations belong in a draw hook.
    internal static class BoundAccess
    {
        internal static Func<object, T> Getter<T>(FieldInfo field)
        {
            if (field == null || field.FieldType != typeof(T)) throw new ArgumentException("Unexpected field signature", nameof(field));
            var method = new DynamicMethod("CN_get_" + field.Name, typeof(T), new[] { typeof(object) }, field.DeclaringType, true);
            var il = method.GetILGenerator();
            if (!field.IsStatic) { il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, field.DeclaringType); }
            il.Emit(field.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            return (Func<object, T>)method.CreateDelegate(typeof(Func<object, T>));
        }

        internal static Action<object, T> Setter<T>(FieldInfo field)
        {
            if (field == null || field.FieldType != typeof(T) || field.IsInitOnly) throw new ArgumentException("Unexpected mutable field signature", nameof(field));
            var method = new DynamicMethod("CN_set_" + field.Name, typeof(void), new[] { typeof(object), typeof(T) }, field.DeclaringType, true);
            var il = method.GetILGenerator();
            if (!field.IsStatic) { il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, field.DeclaringType); }
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(field.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld, field);
            il.Emit(OpCodes.Ret);
            return (Action<object, T>)method.CreateDelegate(typeof(Action<object, T>));
        }
    }
}
