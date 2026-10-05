using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace PeakWorkshopCN
{
    internal static class RawLiteralRewriter
    {
        internal static int Rewrite(List<CodeInstruction> instructions, string source)
        {
            if (source != "PeakMapForge.Airport.WorkshopPanel.LobbyPlayerRow" && source != "PeakMapForge.Airport.WorkshopPanel.NpcCategoryTiles") return 0;
            var translate = AccessTools.Method(typeof(WorkshopPatches), nameof(WorkshopPatches.TranslateRaw));
            int changed = 0;
            for (int i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].opcode != OpCodes.Ldstr) continue;
                var value = instructions[i].operand as string;
                if (value != "Kick" && value != "1 NPC" && value != "{0} NPCs") continue;
                instructions.Insert(i + 1, new CodeInstruction(OpCodes.Call, translate));
                changed++;
                i++;
            }
            return changed;
        }
    }
}
