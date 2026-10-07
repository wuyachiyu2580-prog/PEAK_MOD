using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace PeakWorkshopCN
{
    internal static class LiteralRewriter
    {
        // Only adjacent literal pairs are proven built-in text; variable arguments are untouched.
        internal static int Rewrite(List<CodeInstruction> instructions, MethodInfo original, MethodInfo replacement, string source = null)
        {
            int changed = 0;
            for (int i = 2; i < instructions.Count; i++)
            {
                var call = instructions[i];
                if (call.opcode != OpCodes.Call || !Equals(call.operand, original) ||
                    instructions[i - 2].opcode != OpCodes.Ldstr || instructions[i - 1].opcode != OpCodes.Ldstr) continue;
                var english = instructions[i - 1].operand as string;
                if (!TranslationCatalog.TryGet(english, out var chinese)) continue;
                if (english == "Home" && source == "PeakMapForge.Editor.EditorUi.HpContent") chinese = "Home";
                var argument = new CodeInstruction(OpCodes.Ldstr, chinese);
                MoveEntry(call, argument);
                instructions.Insert(i, argument);
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
                if (call.opcode != OpCodes.Call || !Equals(call.operand, original)) continue;
                var argument = new CodeInstruction(OpCodes.Ldstr, source);
                MoveEntry(call, argument);
                instructions.Insert(i, argument);
                call.operand = replacement;
                changed++;
                i++;
            }
            return changed;
        }

        // Branches targeting the old call already carry its two arguments. They must
        // execute the new third argument too. End markers stay after the original call.
        private static void MoveEntry(CodeInstruction call, CodeInstruction argument)
        {
            argument.labels.AddRange(call.labels);
            call.labels.Clear();
            for (int i = call.blocks.Count - 1; i >= 0; i--)
                if (call.blocks[i].blockType != ExceptionBlockType.EndExceptionBlock)
                { argument.blocks.Insert(0, call.blocks[i]); call.blocks.RemoveAt(i); }
        }
    }
}
