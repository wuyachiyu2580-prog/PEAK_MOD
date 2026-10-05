using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace PeakWorkshopCN
{
    internal static class LiteralRewriter
    {
        // Only adjacent literal pairs are proven built-in text; variable arguments are untouched.
        internal static int Rewrite(List<CodeInstruction> instructions, MethodInfo original, MethodInfo replacement)
        {
            int changed = 0;
            for (int i = 2; i < instructions.Count; i++)
            {
                var call = instructions[i];
                if (call.opcode != OpCodes.Call || !Equals(call.operand, original) ||
                    instructions[i - 2].opcode != OpCodes.Ldstr || instructions[i - 1].opcode != OpCodes.Ldstr ||
                    call.labels.Count != 0 || call.blocks.Count != 0) continue;
                var english = instructions[i - 1].operand as string;
                if (!TranslationCatalog.TryGet(english, out var chinese)) continue;
                instructions.Insert(i, new CodeInstruction(OpCodes.Ldstr, chinese));
                call.operand = replacement;
                changed++;
                i++;
            }
            return changed;
        }

        internal static int RewriteDynamic(List<CodeInstruction> instructions, MethodInfo original, MethodInfo replacement, string source)
        {
            if (!DynamicCatalog.HasRules(source)) return 0;
            int changed = 0;
            for (int i = 0; i < instructions.Count; i++)
            {
                var call = instructions[i];
                if (call.opcode != OpCodes.Call || !Equals(call.operand, original) || call.labels.Count != 0 || call.blocks.Count != 0) continue;
                instructions.Insert(i, new CodeInstruction(OpCodes.Ldstr, source));
                call.operand = replacement;
                changed++;
                i++;
            }
            return changed;
        }
    }
}
