﻿using System;
using System.Collections.Generic;
using System.Reflection;
using EXILED;

namespace SanyaPlugin
{
    internal static class SanyaPluginConfig
    {
        internal static string infosender_ip;
        internal static int infosender_port;

        //Smod Emulation
        internal static int auto_warhead_start;
        internal static bool auto_warhead_start_lock;
        internal static Dictionary<RoleType, List<ItemType>> defaultitems;
        internal static List<int> tesla_triggerable_teams;

        //SanyaPlugin
        internal static bool tesla_triggerable_disarmed;
        internal static bool generator_unlock_to_open;
        internal static bool generator_finish_to_lock;
        internal static bool generator_activating_opened;

        //Human:Balanced
        internal static int traitor_limitter;
        internal static int traitor_chance_percent;

        //SCP:Balanced
        internal static bool scp049_reset_ragdoll_after_recall;
        internal static bool scp914_intake_death;

        //Damage/Recovery
        internal static float damage_divisor_scp049;
        internal static float damage_divisor_scp0492;
        internal static float damage_divisor_scp096;
        internal static float damage_divisor_scp106;
        internal static float damage_divisor_scp173;
        internal static float damage_divisor_scp939;
        internal static int recovery_amount_scp049;
        internal static int recovery_amount_scp0492;
        internal static int recovery_amount_scp096;
        internal static int recovery_amount_scp106;
        internal static int recovery_amount_scp173;
        internal static int recovery_amount_scp939;

        internal static void Reload()
        {
            try
            {
                infosender_ip = Plugin.Config.GetString("sanya_infosender_ip", "hatsunemiku24.ddo.jp");
                infosender_port = Plugin.Config.GetInt("sanya_infosender_port", 37813);
                tesla_triggerable_teams = new List<int>(Plugin.Config.GetIntList("sanya_tesla_triggerable_teams"));
                tesla_triggerable_disarmed = Plugin.Config.GetBool("sanya_tesla_triggerable_disarmed", false);
                auto_warhead_start = Plugin.Config.GetInt("sanya_auto_warhead_start",-1);
                auto_warhead_start_lock = Plugin.Config.GetBool("sanya_auto_warhead_start_lock", false);

                defaultitems = new Dictionary<RoleType, List<ItemType>>();
                defaultitems.Add(RoleType.ClassD, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_classd").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })));
                defaultitems.Add(RoleType.Scientist, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_scientist").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })));
                defaultitems.Add(RoleType.FacilityGuard, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_guard").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })));
                defaultitems.Add(RoleType.NtfCadet, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_cadet").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })));
                defaultitems.Add(RoleType.NtfLieutenant, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_lieutenant").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })));
                defaultitems.Add(RoleType.NtfCommander, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_commander").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })));
                defaultitems.Add(RoleType.NtfScientist, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_ntfscientist").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })));
                defaultitems.Add(RoleType.ChaosInsurgency, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_ci").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })));

                generator_unlock_to_open = Plugin.Config.GetBool("sanya_generator_unlock_to_open", false);
                generator_finish_to_lock = Plugin.Config.GetBool("sanya_generator_finish_to_lock", false);
                generator_activating_opened = Plugin.Config.GetBool("sanya_generator_activating_opened", false);

                traitor_limitter = Plugin.Config.GetInt("sanya_traitor_limitter", -1);
                traitor_chance_percent = Plugin.Config.GetInt("sanya_traitor_chance_percent", 50);

                scp049_reset_ragdoll_after_recall = Plugin.Config.GetBool("sanya_scp049_reset_ragdoll_after_recall", false);
                scp914_intake_death = Plugin.Config.GetBool("sanya_scp914_intake_death", false);

                damage_divisor_scp049 = Plugin.Config.GetFloat("sanya_damage_divisor_scp049", 1.0f);
                damage_divisor_scp0492 = Plugin.Config.GetFloat("sanya_damage_divisor_scp0492", 1.0f);
                damage_divisor_scp096 = Plugin.Config.GetFloat("sanya_damage_divisor_scp096", 1.0f);
                damage_divisor_scp106 = Plugin.Config.GetFloat("sanya_damage_divisor_scp106", 1.0f);
                damage_divisor_scp173 = Plugin.Config.GetFloat("sanya_damage_divisor_scp173", 1.0f);
                damage_divisor_scp939 = Plugin.Config.GetFloat("sanya_damage_divisor_scp939", 1.0f);
                recovery_amount_scp049 = Plugin.Config.GetInt("sanya_recovery_amount_scp049", -1);
                recovery_amount_scp0492 = Plugin.Config.GetInt("sanya_recovery_amount_scp0492", -1);
                recovery_amount_scp096 = Plugin.Config.GetInt("sanya_recovery_amount_scp096", -1);
                recovery_amount_scp106 = Plugin.Config.GetInt("sanya_recovery_amount_scp106", -1);
                recovery_amount_scp173 = Plugin.Config.GetInt("sanya_recovery_amount_scp173", -1);
                recovery_amount_scp939 = Plugin.Config.GetInt("sanya_recovery_amount_scp939", -1);

                Plugin.Info("[SanyaPluginConfig] Reloaded!");
            }
            catch(Exception e)
            {
                Plugin.Error("[SanyaPluginConfig] Error:");
                Plugin.Error(e.ToString());
            }
        }

        internal static string GetConfigs()
        {
            string returned = "\n";

            FieldInfo[] infoArray = typeof(SanyaPluginConfig).GetFields(BindingFlags.Static | BindingFlags.NonPublic);

            foreach(FieldInfo info in infoArray)
            {
                returned += $"{info.Name}: {info.GetValue(null)}\n";
            }

            return returned;
        }
    }
}