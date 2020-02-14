﻿using System;
using EXILED;
using EXILED.Extensions;
using Harmony;
using UnityEngine;

namespace SanyaPlugin
{
    [HarmonyPatch(typeof(AlphaWarheadController), nameof(AlphaWarheadController.CancelDetonation), new System.Type[] { typeof(UnityEngine.GameObject) })]
    public class CancelWarheadPatch
    {
        public static bool Locked = false;

        public static bool Prefix()
        {
            Log.Debug($"[Patch.CancelDetonation] Locked:{Locked}");
            if(Locked) return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(AlphaWarheadNukesitePanel), nameof(AlphaWarheadNukesitePanel.AllowChangeLevelState))]
    public class ChangeLeverPatch
    {
        public static bool Prefix()
        {
            Log.Debug($"[Patch.ChangeLeverPatch] Locked:{CancelWarheadPatch.Locked}");
            if(CancelWarheadPatch.Locked) return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(Scp049PlayerScript), nameof(Scp049PlayerScript.CallCmdRecallPlayer))]
    public class Recall049Patch
    {
        public static void Postfix(Scp049PlayerScript __instance, ref GameObject target)
        {
            Log.Debug($"[Patch.Recall049Patch] SCP049:{ReferenceHub.GetHub(__instance.gameObject).GetName()} Target:{ReferenceHub.GetHub(target).GetName()}");
            if(SanyaPluginConfig.recovery_amount_scp049 > 0)
            {
                ReferenceHub.GetHub(__instance.gameObject).playerStats.HealHPAmount(SanyaPluginConfig.recovery_amount_scp049);
            }
            if(SanyaPluginConfig.scp049_reset_ragdoll_after_recall)
            {
                foreach(var player in Player.GetHubs())
                {
                    __instance.RpcSetDeathTime(player.gameObject);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Intercom), nameof(Intercom.UpdateText))]
    public class IntercomTextPatch
    {
        public static bool Prefix(Intercom __instance)
        {
            try
            {
                if(!SanyaPluginConfig.intercom_information) return true;

                int leftdecont = (int)((Math.Truncate(((11.74f * 60) * 100f)) / 100f) - (Math.Truncate(PlayerManager.localPlayer.GetComponent<DecontaminationLCZ>().time * 100f) / 100f));
                int leftautowarhead = SanyaPluginConfig.auto_warhead_start - RoundSummary.roundTime;
                int nextRespawn = (int)Math.Truncate(PlayerManager.localPlayer.GetComponent<MTFRespawn>().timeToNextRespawn + PlayerManager.localPlayer.GetComponent<MTFRespawn>().respawnCooldown);
                string contentfix = string.Concat(
                    $"作戦経過時間 : {(RoundSummary.roundTime / 60).ToString("00")}:{(RoundSummary.roundTime % 60).ToString("00")}\n",
                    $"残存SCP : {(RoundSummary.singleton.CountTeam(Team.SCP)).ToString("00")}/{RoundSummary.singleton.classlistStart.scps_except_zombies.ToString("00")}\n",
                    $"残存Dクラス職員 : {(RoundSummary.singleton.CountTeam(Team.CDP)).ToString("00")}/{RoundSummary.singleton.classlistStart.class_ds.ToString("00")}\n",
                    $"残存科学者 : {(RoundSummary.singleton.CountTeam(Team.RSC)).ToString("00")}/{RoundSummary.singleton.classlistStart.scientists.ToString("00")}\n",
                    $"起動済み発電機 : {Generator079.mainGenerator.totalVoltage.ToString("00")}/05\n",
                    $"最下層閉鎖まで : {(leftdecont / 60).ToString("00")}:{(leftdecont % 60).ToString("00")}\n",
                    $"自動施設爆破まで : {(leftautowarhead / 60).ToString("00")}:{(leftautowarhead % 60).ToString("00")}\n",
                    $"接近中の部隊突入まで : {(nextRespawn / 60).ToString("00")}:{(nextRespawn % 60).ToString("00")}\n"
                    );


                if(__instance.Muted)
                {
                    __instance._content = contentfix + "あなたは管理者によってミュートされている";
                }
                else if(Intercom.AdminSpeaking)
                {
                    __instance._content = contentfix + "管理者が放送設備をオーバーライド中";
                }
                else if(__instance.remainingCooldown > 0f)
                {
                    __instance._content = contentfix + "放送設備再起動中 : " + Mathf.CeilToInt(__instance.remainingCooldown) + "秒必要";
                }
                else if(__instance.Networkspeaker != null)
                {
                    if(__instance.speechRemainingTime == -77f)
                    {
                        __instance._content = contentfix + "放送中... : オーバーライド";
                    }
                    else
                    {
                        __instance._content = contentfix + "放送中... : 残り" + Mathf.CeilToInt(__instance.speechRemainingTime) + "秒";
                    }
                }
                else
                {
                    __instance._content = contentfix + "放送設備準備完了";
                }
                if(__instance._contentDirty)
                {
                    __instance.NetworkintercomText = __instance._content;
                    __instance._contentDirty = false;
                }
                if(Intercom.AdminSpeaking != Intercom.LastState)
                {
                    Intercom.LastState = Intercom.AdminSpeaking;
                    __instance.RpcUpdateAdminStatus(Intercom.AdminSpeaking);
                }
            }
            catch(Exception e)
            {
                Log.Error($"[IntercomTextPatch] {e}");
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerInteract), nameof(PlayerInteract.CallCmdUseLocker))]
    public class ContainLockerPatch
    {
        public static bool Prefix(PlayerInteract __instance, ref int lockerId, ref int chamberNumber)
        {
            if(!__instance._playerInteractRateLimit.CanExecute() || (__instance._hc.CufferId > 0 && !__instance.CanDisarmedInteract))
            {
                return false;
            }
            LockerManager singleton = LockerManager.singleton;
            if(lockerId < 0 || lockerId >= singleton.lockers.Length || !__instance.ChckDis(singleton.lockers[lockerId].gameObject.position) || !singleton.lockers[lockerId].supportsStandarizedAnimation || chamberNumber < 0 || chamberNumber >= singleton.lockers[lockerId].chambers.Length || singleton.lockers[lockerId].chambers[chamberNumber].doorAnimator == null || !singleton.lockers[lockerId].chambers[chamberNumber].CooldownAtZero())
            {
                return false;
            }
            singleton.lockers[lockerId].chambers[chamberNumber].SetCooldown();
            string accessToken = singleton.lockers[lockerId].chambers[chamberNumber].accessToken;
            Item itemByID = __instance._inv.GetItemByID(__instance._inv.curItem);

            //-------------------------------
            bool CanAccess = false;
            if(SanyaPluginConfig.inventory_keycard_act)
            {
                Log.Debug($"[ContainLockerPatch] token:{accessToken}");
                foreach(var item in __instance._inv.items)
                {
                    Log.Debug($"[ContainLockerPatch] inv:{item.id} perm:{string.Join(",", __instance._inv.GetItemByID(item.id).permissions)}");
                    if(__instance._inv.GetItemByID(item.id).permissions.Contains(accessToken))
                    {
                        CanAccess = true;
                    }
                }
            }
            //-------------------------------

            if(__instance._sr.BypassMode || string.IsNullOrEmpty(accessToken) || (itemByID != null && itemByID.permissions.Contains<string>(accessToken)) || (SanyaPluginConfig.inventory_keycard_act && CanAccess))
            {
                bool flag = (singleton.openLockers[lockerId] & (1 << chamberNumber)) != 1 << chamberNumber;
                singleton.ModifyOpen(lockerId, chamberNumber, flag);
                singleton.RpcDoSound(lockerId, chamberNumber, flag);
                bool state = true;
                for(int i = 0; i < singleton.lockers[lockerId].chambers.Length; i++)
                {
                    if((singleton.openLockers[lockerId] & (1 << i)) == 1 << i)
                    {
                        state = false;
                        break;
                    }
                }
                singleton.lockers[lockerId].LockPickups(state);
                if(!string.IsNullOrEmpty(accessToken))
                {
                    singleton.RpcChangeMaterial(lockerId, chamberNumber, error: false);
                }
            }
            else
            {
                singleton.RpcChangeMaterial(lockerId, chamberNumber, error: true);
            }
            __instance.OnInteract();

            return false;
        }
    }

    //[HarmonyPatch(typeof(DissonanceUserSetup),nameof(DissonanceUserSetup.CallCmdAltIsActive))]
    //public class VCPatch
    //{
    //    public static void Prefix(DissonanceUserSetup __instance, bool value)
    //    {

    //    }
    //}
}
