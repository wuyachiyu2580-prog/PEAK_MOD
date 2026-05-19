using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 房主专属：按快捷键销毁场景中所有号角（不管是否被持有、是否在远处）。
    ///
    /// 用途：
    /// - 配合 F8 "房主补刷号角"使用 —— 先按召回键清场，再按 F8 发一把新的
    /// - 处理"单爬队友把号角永久藏起来"的极端情况
    ///
    /// 实现（两阶段，解决 PUN 所有权异步问题）：
    /// - 阶段1：遍历所有 BugleSFX，IsMine 直接 Destroy，非 IsMine 发 TransferOwnership
    /// - 等待 ~0.8s 让所有权 RPC 传播
    /// - 阶段2：再次遍历，此时 IsMine 的 Destroy
    /// - 还是 IsMine=false 的报日志放弃（罕见：原 owner 已断线等）
    /// </summary>
    internal static class BugleRecaller
    {
        public static void TryRecall()
        {
            if (!PhotonNetwork.IsConnected)
            {
                Plugin.Log?.LogInfo("[BugleRecall] skipped: not connected");
                return;
            }
            if (!PhotonNetwork.IsMasterClient)
            {
                Plugin.Log?.LogInfo("[BugleRecall] skipped: not master (only host can recall bugles)");
                return;
            }

            if (Plugin.Instance == null)
            {
                Plugin.Log?.LogWarning("[BugleRecall] Plugin.Instance null, cannot start coroutine");
                return;
            }

            Plugin.Instance.StartCoroutine(RecallRoutine());
        }

        private static IEnumerator RecallRoutine()
        {
            // ── 阶段 1：销毁自己 own 的 + 请求转移他人 own 的 ──
            BugleSFX[] all = UnityEngine.Object.FindObjectsByType<BugleSFX>(FindObjectsSortMode.None);
            int destroyedPhase1 = 0;
            var pending = new List<int>();

            foreach (var bugle in all)
            {
                if (bugle == null || bugle.photonView == null) continue;
                int vId = bugle.photonView.ViewID;
                var pv = bugle.photonView;

                try
                {
                    if (pv.IsMine)
                    {
                        PhotonNetwork.Destroy(bugle.gameObject);
                        destroyedPhase1++;
                        Plugin.Log?.LogInfo($"[BugleRecall] phase1 destroyed ViewID={vId}");
                    }
                    else
                    {
                        // 记录当前 owner，便于排查阶段2失败是不是断线/特殊 actor
                        var owner = pv.Owner;
                        string ownerInfo = owner == null
                            ? "null(scene?)"
                            : $"{owner.NickName}#{owner.ActorNumber}{(owner.IsInactive ? "[INACTIVE]" : "")}";
                        pv.TransferOwnership(PhotonNetwork.LocalPlayer);
                        pending.Add(vId);
                        Plugin.Log?.LogInfo($"[BugleRecall] phase1 requested ownership ViewID={vId}, currentOwner={ownerInfo}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"[BugleRecall] phase1 failed ViewID={vId}: {ex.Message}");
                }
            }

            if (pending.Count == 0)
            {
                Plugin.Log?.LogInfo($"[BugleRecall] done, destroyed={destroyedPhase1}/{all.Length}");
                yield break;
            }

            // ── 等待所有权 RPC 传播 ──
            yield return new WaitForSeconds(0.8f);

            // ── 阶段 2：销毁已经转到本地的 ──
            int destroyedPhase2 = 0;
            int stillForeign = 0;

            foreach (int vId in pending)
            {
                PhotonView pv = PhotonView.Find(vId);
                if (pv == null) continue; // 已被销毁（可能 OnOwnerLeft 清理了）

                try
                {
                    if (pv.IsMine)
                    {
                        PhotonNetwork.Destroy(pv.gameObject);
                        destroyedPhase2++;
                        Plugin.Log?.LogInfo($"[BugleRecall] phase2 destroyed ViewID={vId}");
                    }
                    else
                    {
                        stillForeign++;
                        // 输出当前 owner：如果和阶段1请求时一样→对方没响应 TransferOwnership
                        // 如果 owner 变了但不是我 → Ownership 在中间更换了（例如原 owner 断线，PUN 的 cleanup 接管）
                        var owner = pv.Owner;
                        string ownerInfo = owner == null
                            ? "null(scene?)"
                            : $"{owner.NickName}#{owner.ActorNumber}{(owner.IsInactive ? "[INACTIVE]" : "")}";
                        Plugin.Log?.LogWarning($"[BugleRecall] phase2 still not owned ViewID={vId}, currentOwner={ownerInfo}, giving up this round");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"[BugleRecall] phase2 failed ViewID={vId}: {ex.Message}");
                }
            }

            int total = destroyedPhase1 + destroyedPhase2;
            Plugin.Log?.LogInfo($"[BugleRecall] done, destroyed={total}/{all.Length} (phase1={destroyedPhase1}, phase2={destroyedPhase2}, stillForeign={stillForeign})");
        }
    }
}
