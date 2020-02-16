﻿using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using EXILED;
using UnityEngine;
using MEC;
using Utf8Json;
using EXILED.Extensions;

namespace SanyaPlugin
{
    public class EventHandlers
    {
        public EventHandlers(SanyaPlugin plugin) => this.plugin = plugin;
        internal readonly SanyaPlugin plugin;
        private readonly System.Random random = new System.Random();
        internal List<CoroutineHandle> coroutines = new List<CoroutineHandle>();
        internal bool loaded = false;

        /** Infosender **/
        private readonly UdpClient udpClient = new UdpClient();
        internal Task sendertask;
        internal async Task _SenderAsync()
        {
            Log.Debug($"[Infosender_Task] Started.");

            while(true)
            {
                try
                {
                    if(!this.loaded)
                    {
                        Log.Debug($"[Infosender_Task] Plugin not loaded. Skipped...");
                        await Task.Delay(TimeSpan.FromSeconds(30));
                    }

                    if(Configs.infosender_ip == "none")
                    {
                        Log.Info($"[Infosender_Task] Disabled(config:({Configs.infosender_ip}). breaked.");
                        break;
                    }

                    Serverinfo cinfo = new Serverinfo();

                    DateTime dt = DateTime.Now;
                    cinfo.time = dt.ToString("yyyy-MM-ddTHH:mm:sszzzz");
                    cinfo.gameversion = CustomNetworkManager.CompatibleVersions[0];
                    cinfo.modversion = $"{EventPlugin.Version.Major}.{EventPlugin.Version.Minor}.{EventPlugin.Version.Patch}";
                    cinfo.sanyaversion = SanyaPlugin.Version;
                    cinfo.gamemode = eventmode.ToString();
                    cinfo.name = ServerConsole.singleton.RefreshServerName();
                    cinfo.ip = ServerConsole.Ip;
                    cinfo.port = ServerConsole.Port;
                    cinfo.playing = PlayerManager.players.Count;
                    cinfo.maxplayer = CustomNetworkManager.slots;
                    cinfo.duration = RoundSummary.roundTime;

                    if(cinfo.playing > 0)
                    {
                        foreach(GameObject player in PlayerManager.players)
                        {
                            Playerinfo ply = new Playerinfo
                            {
                                name = ReferenceHub.GetHub(player).nicknameSync.MyNick,
                                userid = ReferenceHub.GetHub(player).characterClassManager.UserId,
                                ip = ReferenceHub.GetHub(player).characterClassManager.RequestIp,
                                role = ReferenceHub.GetHub(player).characterClassManager.CurClass.ToString(),
                                rank = ReferenceHub.GetHub(player).serverRoles.MyText
                            };

                            cinfo.players.Add(ply);
                        }
                    }

                    string json = JsonSerializer.ToJsonString(cinfo);

                    byte[] sendBytes = Encoding.UTF8.GetBytes(json);
                    udpClient.Send(sendBytes, sendBytes.Length, Configs.infosender_ip, Configs.infosender_port);
                    Log.Debug($"[Infosender_Task] {Configs.infosender_ip}:{Configs.infosender_port}");
                }
                catch(Exception e)
                {
                    throw e;
                }
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
        }

        /** Update **/
        internal IEnumerator<float> _EverySecond()
        {
            while(true)
            {
                //自動核 & 自動核ロック -> CancelWarheadPatch
                if(Configs.auto_warhead_start > 0 && !autowarheadstarted)
                {
                    if(RoundSummary.roundTime >= Configs.auto_warhead_start)
                    {
                        autowarheadstarted = true;
                        if(Configs.auto_warhead_start_lock) CancelWarheadPatch.Locked = true;
                        AlphaWarheadOutsitePanel.nukeside.Networkenabled = true;
                        if(Configs.cassie_subtitle && !AlphaWarheadController.Host.NetworkinProgress)
                        {
                            bool isresumed = AlphaWarheadController._resumeScenario != -1;
                            double left = isresumed ? AlphaWarheadController.Host.timeToDetonation : AlphaWarheadController.Host.timeToDetonation - 4;
                            double count = Math.Truncate(left / 10.0) * 10.0;

                            if(!isresumed)
                            {
                                Methods.SendSubtitle(Subtitles.AlphaWarheadStart.Replace("{0}", count.ToString()), 15);
                            }
                            else
                            {
                                Methods.SendSubtitle(Subtitles.AlphaWarheadResume.Replace("{0}", count.ToString()), 10);
                            }


                        }
                        AlphaWarheadController.Host.NetworkinProgress = true;
                    }
                }

                //寝返り
                if(Configs.traitor_limitter > 0)
                {
                    foreach(var player in Player.GetHubs())
                    {
                        if((player.GetTeam() == Team.MTF || player.GetTeam() == Team.CHI)
                            && player.IsHandCuffed()
                            && Vector3.Distance(espaceArea, player.transform.position) <= Escape.radius
                            && RoundSummary.singleton.CountTeam(player.GetTeam()) <= Configs.traitor_limitter)
                        {
                            switch(player.GetTeam())
                            {
                                case Team.MTF:
                                    if(UnityEngine.Random.Range(0, 100) <= Configs.traitor_chance_percent)
                                    {
                                        Log.Info($"[_EverySecond:Traitor] {player.GetName()} : MTF->CHI");
                                        player.characterClassManager.SetPlayersClass(RoleType.ChaosInsurgency, player.gameObject);
                                    }
                                    else
                                    {
                                        Log.Info($"[_EverySecond:Traitor] {player.GetName()} : Traitor Failed(by percent)");
                                        player.characterClassManager.SetPlayersClass(RoleType.Spectator, player.gameObject);
                                    }
                                    break;
                                case Team.CHI:
                                    if(UnityEngine.Random.Range(0, 100) <= Configs.traitor_chance_percent)
                                    {
                                        Log.Info($"[_EverySecond:Traitor] {player.GetName()} : CHI->MTF");
                                        player.characterClassManager.SetPlayersClass(RoleType.NtfCadet, player.gameObject);
                                    }
                                    else
                                    {
                                        Log.Info($"[_EverySecond:Traitor] {player.GetName()} : Traitor Failed(by percent)");
                                        player.characterClassManager.SetPlayersClass(RoleType.Spectator, player.gameObject);
                                    }
                                    break;
                            }
                        }
                    }
                }

                //毎秒
                yield return Timing.WaitForSeconds(1f);
            }
        }
        internal IEnumerator<float> _FixedUpdate()
        {
            while(true)
            {
                //Blackouter
                if(flickerableLight != null && IsEnableBlackout && flickerableLight.remainingFlicker < 0f && !flickerableLight.IsDisabled())
                {
                    //Log.Debug($"{UnityEngine.Object.FindObjectOfType<FlickerableLight>().remainingFlicker}");
                    Log.Debug($"[Blackouter] Fired.");
                    Generator079.mainGenerator.RpcCustomOverchargeForOurBeautifulModCreators(10f, false);
                }

                yield return Timing.WaitForOneFrame;
            }
        }

        /** Flag Params **/
        internal static bool autowarheadstarted = false;
        private Vector3 espaceArea = new Vector3(177.5f, 985.0f, 29.0f);

        /** RoundVar **/
        private FlickerableLight flickerableLight = null;
        private bool IsEnableBlackout = false;

        /** EventModeVar **/
        private SANYA_GAME_MODE eventmode = SANYA_GAME_MODE.NULL;

        public void OnWaintingForPlayers()
        {
            loaded = true;

            if(sendertask?.Status != TaskStatus.Running && sendertask?.Status != TaskStatus.WaitingForActivation)
                sendertask = this._SenderAsync().StartSender();

            coroutines.Add(Timing.RunCoroutine(_EverySecond(), Segment.FixedUpdate));
            coroutines.Add(Timing.RunCoroutine(_FixedUpdate(), Segment.FixedUpdate));

            flickerableLight = UnityEngine.Object.FindObjectOfType<FlickerableLight>();

            PlayerDataManager.playersData.Clear();

            eventmode = (SANYA_GAME_MODE)Methods.GetRandomIndexFromWeight(Configs.event_mode_weight.ToArray());
            switch(eventmode)
            {
                case SANYA_GAME_MODE.NIGHT:
                    {
                        break;
                    }
                default:
                    {
                        eventmode = SANYA_GAME_MODE.NORMAL;
                        break;
                    }
            }

            Log.Info($"[OnWaintingForPlayers] Waiting for Players... EventMode:{eventmode}");
        }

        public void OnRoundStart()
        {
            Log.Info($"[OnRoundStart] Round Start!");

            switch(eventmode)
            {
                case SANYA_GAME_MODE.NIGHT:
                    {
                        IsEnableBlackout = true;
                        coroutines.Add(Timing.RunCoroutine(Coroutines.StartNightMode()));
                        break;
                    }
            }
        }

        public void OnRoundEnd()
        {
            Log.Info($"[OnRoundEnd] Round Ended.");

            if(Configs.data_enabled)
            {
                foreach(ReferenceHub player in Player.GetHubs())
                {
                    if(string.IsNullOrEmpty(player.GetUserId())) continue;

                    if(PlayerDataManager.playersData.ContainsKey(player.GetUserId()))
                    {
                        if(player.GetRoleType() == RoleType.Spectator)
                        {
                            PlayerDataManager.playersData[player.GetUserId()].AddExp(Configs.level_exp_other);
                        }
                        else
                        {
                            PlayerDataManager.playersData[player.GetUserId()].AddExp(Configs.level_exp_win);
                        }
                    }
                }

                foreach(var data in PlayerDataManager.playersData.Values)
                {
                    data.lastUpdate = DateTime.Now;
                    data.playingcount++;
                    PlayerDataManager.SavePlayerData(data);
                }
            }
        }

        public void OnRoundRestart()
        {
            Log.Info($"[OnRoundRestart] Restarting...");

            foreach(var cor in coroutines)
                Timing.KillCoroutines(cor);
            coroutines.Clear();

            flickerableLight = null;
            IsEnableBlackout = false;

            autowarheadstarted = false;
            CancelWarheadPatch.Locked = false;
        }

        public void OnPlayerJoin(PlayerJoinEvent ev)
        {
            if(string.IsNullOrEmpty(ev.Player.GetIpAddress())) return;
            Log.Info($"[OnPlayerJoin] {ev.Player.GetName()} ({ev.Player.GetIpAddress()}:{ev.Player.GetUserId()})");

            if(Configs.data_enabled)
            {
                PlayerData data = PlayerDataManager.LoadPlayerData(ev.Player.GetUserId());

                if(!PlayerDataManager.playersData.ContainsKey(ev.Player.GetUserId()))
                {
                    PlayerDataManager.playersData.Add(ev.Player.GetUserId(), data);

                }

                if(Configs.level_enabled)
                {
                    Timing.RunCoroutine(Coroutines.GrantedLevel(ev.Player, data), Segment.FixedUpdate);
                }
            }
        }

        public void OnPlayerLeave(PlayerLeaveEvent ev)
        {
            if(string.IsNullOrEmpty(ev.Player.GetIpAddress())) return;
            Log.Debug($"[OnPlayerLeave] {ev.Player.GetName()} ({ev.Player.GetIpAddress()}:{ev.Player.GetUserId()})");
        }

        public void OnStartItems(StartItemsEvent ev)
        {
            Log.Debug($"[OnStartItems] {ev.Role}");

            if(Configs.defaultitems.TryGetValue(ev.Role, out List<ItemType> itemconfig) && itemconfig.Count > 0)
            {
                ev.StartItems = itemconfig;
            }
        }

        public void OnPlayerSetClass(SetClassEvent ev)
        {
            if(string.IsNullOrEmpty(ev.Player.GetIpAddress())) return;
            Log.Debug($"[OnPlayerSetClass] {ev.Player.GetName()} -> {ev.Role}");
        }

        public void OnPlayerHurt(ref PlayerHurtEvent ev)
        {
            if(string.IsNullOrEmpty(ev.Player.GetIpAddress()) || ev.Player.characterClassManager.SpawnProtected) return;
            Log.Debug($"[OnPlayerHurt:Before] {ev.Attacker?.GetName()} -{ev.Info.GetDamageName()}({ev.Info.Amount})-> {ev.Player?.GetName()}");

            DamageTypes.DamageType damageTypes = ev.Info.GetDamageType();
            if(damageTypes != DamageTypes.Nuke && damageTypes != DamageTypes.Decont && damageTypes != DamageTypes.Wall && damageTypes != DamageTypes.Tesla)
            {
                PlayerStats.HitInfo clinfo = ev.Info;

                //USPMultiplier
                if(damageTypes == DamageTypes.Usp)
                {
                    if(ev.Player.characterClassManager.IsAnyScp())
                    {
                        clinfo.Amount *= Configs.damage_usp_multiplier_scp;
                    }
                    else
                    {
                        clinfo.Amount *= Configs.damage_usp_multiplier_human;
                    }
                }

                //CuffedDivisor
                if(ev.Player.IsHandCuffed())
                {
                    clinfo.Amount /= Configs.damage_divisor_cuffed;
                }

                //SCPsDivisor
                if(damageTypes != DamageTypes.MicroHid 
                    && damageTypes != DamageTypes.Decont 
                    && damageTypes != DamageTypes.Nuke
                    && damageTypes != DamageTypes.Tesla)
                {
                    switch(ev.Player.GetRoleType())
                    {
                        case RoleType.Scp173:
                            clinfo.Amount /= Configs.damage_divisor_scp173;
                            break;
                        case RoleType.Scp106:
                            clinfo.Amount /= Configs.damage_divisor_scp106;
                            break;
                        case RoleType.Scp049:
                            clinfo.Amount /= Configs.damage_divisor_scp049;
                            break;
                        case RoleType.Scp096:
                            clinfo.Amount /= Configs.damage_divisor_scp096;
                            break;
                        case RoleType.Scp0492:
                            clinfo.Amount /= Configs.damage_divisor_scp0492;
                            break;
                        case RoleType.Scp93953:
                        case RoleType.Scp93989:
                            clinfo.Amount /= Configs.damage_divisor_scp939;
                            break;
                    }
                }

                //*****Final*****
                ev.Info = clinfo;
            }

            Log.Debug($"[OnPlayerHurt:After] {ev.Attacker?.GetName()} -{ev.Info.GetDamageName()}({ev.Info.Amount})-> {ev.Player?.GetName()}");
        }

        public void OnPlayerDeath(ref PlayerDeathEvent ev)
        {
            if(string.IsNullOrEmpty(ev.Player.GetIpAddress()) || ev.Player.characterClassManager.SpawnProtected) return;
            Log.Debug($"[OnPlayerDeath] {ev.Killer?.GetName()} -{ev.Info.GetDamageName()}-> {ev.Player?.GetName()}");

            if(Configs.data_enabled)
            {
                if(!string.IsNullOrEmpty(ev.Killer.GetUserId())
                    && ev.Player.GetUserId() != ev.Killer.GetUserId()
                    && PlayerDataManager.playersData.ContainsKey(ev.Killer.GetUserId()))
                {
                    PlayerDataManager.playersData[ev.Killer.GetUserId()].AddExp(Configs.level_exp_kill);
                }

                if(PlayerDataManager.playersData.ContainsKey(ev.Player.GetUserId()))
                {
                    PlayerDataManager.playersData[ev.Player.GetUserId()].AddExp(Configs.level_exp_death);
                }
            }

            if(ev.Info.GetDamageType() == DamageTypes.Scp173 && ev.Killer.GetRoleType() == RoleType.Scp173 && Configs.recovery_amount_scp173 > 0)
            {
                ev.Killer.playerStats.HealHPAmount(Configs.recovery_amount_scp173);
            }
            if(ev.Info.GetDamageType() == DamageTypes.Scp096 && ev.Killer.GetRoleType() == RoleType.Scp096 && Configs.recovery_amount_scp096 > 0)
            {
                ev.Killer.playerStats.HealHPAmount(Configs.recovery_amount_scp096);
            }
            if(ev.Info.GetDamageType() == DamageTypes.Scp939 && (ev.Killer.GetRoleType() == RoleType.Scp93953 || ev.Killer.GetRoleType() == RoleType.Scp93989) && Configs.recovery_amount_scp939 > 0)
            {
                ev.Killer.playerStats.HealHPAmount(Configs.recovery_amount_scp939);
            }
            if(ev.Info.GetDamageType() == DamageTypes.Scp0492 && ev.Killer.GetRoleType() == RoleType.Scp0492 && Configs.recovery_amount_scp0492 > 0)
            {
                ev.Killer.playerStats.HealHPAmount(Configs.recovery_amount_scp0492);
            }

            if(Configs.cassie_subtitle
                && ev.Player.GetTeam() == Team.SCP
                && ev.Player.GetRoleType() != RoleType.Scp0492)
            {
                string fullname = CharacterClassManager._staticClasses.Get(ev.Player.GetRoleType()).fullName;
                string str = string.Empty;

                if(ev.Info.GetDamageType() == DamageTypes.Tesla)
                {
                    str = Subtitles.SCPDeathTesla.Replace("{0}", fullname);
                }
                else if(ev.Info.GetDamageType() == DamageTypes.Nuke)
                {
                    str = Subtitles.SCPDeathWarhead.Replace("{0}", fullname);
                }
                else if(ev.Info.GetDamageType() == DamageTypes.Decont)
                {
                    str = Subtitles.SCPDeathDecont.Replace("{0}", fullname);
                }
                else
                {
                    Team killerTeam = ev.Killer.GetTeam();
                    foreach(var i in Player.GetHubs())
                    {
                        if(i.queryProcessor.PlayerId == ev.Info.PlyId)
                        {
                            killerTeam = i.GetTeam();
                        }
                    }
                    Log.Debug($"[CheckTeam] ply:{ev.Player.queryProcessor.PlayerId} kil:{ev.Killer.queryProcessor.PlayerId} plyid:{ev.Info.PlyId} killteam:{killerTeam}");
 
                    if(killerTeam == Team.CDP)
                    {
                        str = Subtitles.SCPDeathTerminated.Replace("{0}", fullname).Replace("{1}", "Dクラス職員").Replace("{2}", "Class-D Personnel");
                    }
                    else if(killerTeam == Team.CHI)
                    {
                        str = Subtitles.SCPDeathTerminated.Replace("{0}", fullname).Replace("{1}", "カオス・インサージェンシー").Replace("{2}", "Chaos Insurgency");
                    }
                    else if(killerTeam == Team.RSC)
                    {
                        str = Subtitles.SCPDeathTerminated.Replace("{0}", fullname).Replace("{1}", "科学者").Replace("{2}", "Science Personnel");
                    }
                    else if(killerTeam == Team.MTF)
                    {
                        string unit = NineTailedFoxUnits.host.list[ev.Killer.characterClassManager.NtfUnit];
                        str = Subtitles.SCPDeathContainedMTF.Replace("{0}", fullname).Replace("{1}", unit);
                    }
                    else
                    {
                        str = Subtitles.SCPDeathUnknown.Replace("{0}", fullname);
                    }
                }

                int count = 0;
                bool isFound079 = false;
                bool isForced = false;
                foreach(var i in Player.GetHubs())
                {
                    if(ev.Player.GetUserId() == i.GetUserId()) continue;
                    if(i.GetTeam() == Team.SCP) count++;
                    if(i.GetRoleType() == RoleType.Scp079) isFound079 = true;
                }

                Log.Debug($"[Check079] SCPs:{count} isFound079:{isFound079} totalvol:{Generator079.mainGenerator.totalVoltage} forced:{Generator079.mainGenerator.forcedOvercharge}");
                if(count == 1 
                    && isFound079 
                    && Generator079.mainGenerator.totalVoltage < 4 
                    && !Generator079.mainGenerator.forcedOvercharge
                    && ev.Info.GetDamageType() != DamageTypes.Nuke)
                {
                    isForced = true;
                    str = str.Replace("{-1}", "\n全てのSCPオブジェクトの安全が確保されました。SCP-079の再収用手順を開始します。\n重度収用区画は約一分後にオーバーチャージされます。").Replace("{-2}", "\nAll SCP subject has been secured. SCP-079 recontainment sequence commencing.\nHeavy containment zone will overcharge in t-minus 1 minutes.");
                }
                else
                {
                    str = str.Replace("{-1}", string.Empty).Replace("{-2}", string.Empty);
                }

                Methods.SendSubtitle(str, isForced ? 30u : 10u);
            }
        }

        public void OnPocketDimDeath(PocketDimDeathEvent ev)
        {
            Log.Debug($"[OnPocketDimDeath] {ev.Player.GetName()}");

            if(Configs.data_enabled)
            {
                foreach(ReferenceHub player in Player.GetHubs())
                {
                    if(player.GetRoleType() == RoleType.Scp106)
                    {
                        if(PlayerDataManager.playersData.ContainsKey(player.GetUserId()))
                        {
                            PlayerDataManager.playersData[player.GetUserId()].AddExp(Configs.level_exp_kill);
                        }
                    }
                }
            }

            if(Configs.recovery_amount_scp106 > 0)
            {
                foreach(ReferenceHub player in Player.GetHubs())
                {
                    if(player.GetRoleType() == RoleType.Scp106)
                    {
                        player.playerStats.HealHPAmount(Configs.recovery_amount_scp106);
                        player.GetComponent<Scp173PlayerScript>().TargetHitMarker(player.characterClassManager.connectionToClient);
                    }
                }
            }
        }

        public void OnPlayerTriggerTesla(ref TriggerTeslaEvent ev)
        {
            if(Configs.tesla_triggerable_teams.Count == 0
                || Configs.tesla_triggerable_teams.Contains((int)ev.Player.GetTeam()))
            {
                if(Configs.tesla_triggerable_disarmed || ev.Player.handcuffs.CufferId == -1)
                {
                    ev.Triggerable = true;
                }
                else
                {
                    ev.Triggerable = false;
                }
            }
            else
            {
                ev.Triggerable = false;
            }
        }

        public void OnPlayerDoorInteract(ref DoorInteractionEvent ev)
        {
            Log.Debug($"[OnPlayerDoorInteract] {ev.Player.GetName()}:{ev.Door.DoorName}:{ev.Door.permissionLevel}");

            if(Configs.inventory_keycard_act && ev.Player.GetTeam() != Team.SCP && !ev.Player.serverRoles.BypassMode && !ev.Door.locked)
            {
                foreach(var item in ev.Player.inventory.items)
                {
                    Log.Debug($"[OnPlayerDoorInteract] inv:{item.id} parm:{string.Join(",", ev.Player.inventory.GetItemByID(item.id).permissions)}");

                    if(ev.Player.inventory.GetItemByID(item.id).permissions.Contains(ev.Door.permissionLevel))
                    {
                        ev.Allow = true;
                    }
                }
            }
        }

        public void OnTeamRespawn(ref TeamRespawnEvent ev)
        {
            Log.Debug($"[OnTeamRespawn] Queues:{ev.ToRespawn.Count} IsCI:{ev.IsChaos} MaxAmount:{ev.MaxRespawnAmt}");
        }

        public void OnGeneratorUnlock(ref GeneratorUnlockEvent ev)
        {
            Log.Debug($"[OnGeneratorUnlock] {ev.Player.GetName()} -> {ev.Generator.curRoom}");
            if(Configs.inventory_keycard_act && !ev.Player.serverRoles.BypassMode)
            {
                foreach(var item in ev.Player.inventory.items)
                {
                    Log.Debug($"[OnGeneratorUnlock] inv:{item.id} parm:{string.Join(",", ev.Player.inventory.GetItemByID(item.id).permissions)}");

                    if(ev.Player.inventory.GetItemByID(item.id).permissions.Contains("ARMORY_LVL_2"))
                    {
                        ev.Allow = true;
                    }
                }
            }

            if(ev.Allow && Configs.generator_unlock_to_open)
            {
                ev.Generator.doorAnimationCooldown = 1.5f;
                ev.Generator.NetworkisDoorOpen = true;
                ev.Generator.RpcDoSound(true);
            }
        }

        public void OnGeneratorOpen(ref GeneratorOpenEvent ev)
        {
            Log.Debug($"[OnGeneratorOpen] {ev.Player.GetName()} -> {ev.Generator.curRoom}");
            if(ev.Generator.prevFinish && Configs.generator_finish_to_lock) ev.Allow = false;
        }

        public void OnGeneratorClose(ref GeneratorCloseEvent ev)
        {
            Log.Debug($"[OnGeneratorClose] {ev.Player.GetName()} -> {ev.Generator.curRoom}");
            if(ev.Allow && ev.Generator.isTabletConnected && Configs.generator_activating_opened) ev.Allow = false;
        }

        public void OnGeneratorFinish(ref GeneratorFinishEvent ev)
        {
            Log.Debug($"[OnGeneratorFinish] {ev.Generator.curRoom}");
            if(Configs.generator_finish_to_lock) ev.Generator.NetworkisDoorOpen = false;

            int curgen = Generator079.mainGenerator.NetworktotalVoltage + 1;
            if(Configs.cassie_subtitle && !Generator079.mainGenerator.forcedOvercharge)
            {
                if(curgen < 5)
                {
                    Methods.SendSubtitle(Subtitles.GeneratorFinish.Replace("{0}", curgen.ToString()), 10);
                }
                else
                {
                    Methods.SendSubtitle(Subtitles.GeneratorComplete, 20);
                }
            }

            if(eventmode == SANYA_GAME_MODE.NIGHT && curgen >= 3 && IsEnableBlackout)
            {
                IsEnableBlackout = false;
            }
        }

        public void On914Upgrade(ref SCP914UpgradeEvent ev)
        {
            Log.Debug($"[On914Upgrade] {ev.KnobSetting} Players:{ev.Players.Count} Items:{ev.Items.Count}");

            if(Configs.scp914_intake_death)
            {
                foreach(var player in ev.Players)
                {
                    var info = new PlayerStats.HitInfo(914914, "WORLD", DamageTypes.RagdollLess, 0);
                    player.playerStats.HurtPlayer(info, player.gameObject);
                }
            }
        }

        public void OnConsoleCommand(ConsoleCommandEvent ev)
        {
            Log.Debug($"[OnConsoleCommand] [Before] Called:{ev.Player.GetName()} Command:{ev.Command} Return:{ev.ReturnMessage}");

            //if(ev.ReturnMessage == "Command not found.")
            //{
            //    //switch(ev.Command)
            //    {
            //        case "attack":
            //            var scp049 = ev.Player.GetComponent<Scp049PlayerScript>();
            //            var wm = ev.Player.weaponManager;
            //            Vector3 forward = scp049.plyCam.transform.forward;
            //            Vector3 position = scp049.plyCam.transform.position;
            //            RaycastHit raycastHit;
            //            if(Physics.Raycast(new Ray(position, forward), out raycastHit, scp049.attackDistance, wm.raycastMask))
            //            {
            //                Log.Debug($"{Plugin.GetPlayer(raycastHit.transform.gameObject).GetName()}");
            //            }
            //            ev.ReturnMessage = "attacked.";
            //            break;
            //        default:
            //            break;
            //    }
            //}

            Log.Debug($"[OnConsoleCommand] [After] Called:{ev.Player.GetName()} Command:{ev.Command} Return:{ev.ReturnMessage}");
        }

        public void OnCommand(ref RACommandEvent ev)
        {
            Log.Debug($"[OnCommand] sender:{ev.Sender.SenderId} command:{ev.Command}");

            string[] args = ev.Command.Split(' ');
            string ReturnStr = string.Empty;
            bool isSuccess = true;
            if(args[0].ToLower() == "sanya")
            {
                if(args.Length > 1)
                {
                    switch(args[1].ToLower())
                    {
                        case "test":
                            {
                                ReturnStr = "test ok.";
                                break;
                            }
                        case "showconfig":
                            {
                                ReturnStr = Configs.GetConfigs();
                                break;
                            }
                        case "reload":
                            {
                                Configs.Reload();
                                ReturnStr = "reload ok";
                                break;
                            }
                        case "nukelock":
                            {
                                CancelWarheadPatch.Locked = !CancelWarheadPatch.Locked;
                                ReturnStr = $"nukelock:{CancelWarheadPatch.Locked}";
                                break;
                            }
                        case "blackout":
                            {
                                if(args.Length > 2 && args[2] == "hcz")
                                {
                                    Generator079.mainGenerator.RpcCustomOverchargeForOurBeautifulModCreators(10f, true);
                                    ReturnStr = "HCZ blackout!";
                                }
                                else
                                {
                                    Generator079.mainGenerator.RpcCustomOverchargeForOurBeautifulModCreators(10f, false);
                                    ReturnStr = "ALL blackout!";
                                }
                                break;
                            }
                        case "ev":
                            {
                                foreach(Lift lift in UnityEngine.Object.FindObjectsOfType<Lift>())
                                {
                                    lift.UseLift();
                                }
                                ReturnStr = "EV Used.";
                                break;
                            }
                        case "gen":
                            {
                                if(args.Length > 2)
                                {
                                    if(args[2] == "unlock")
                                    {
                                        foreach(var generator in Generator079.generators)
                                        {
                                            generator.NetworkisDoorUnlocked = true;
                                            generator.doorAnimationCooldown = 0.5f;
                                        }
                                        ReturnStr = "gen unlocked.";
                                    }
                                    else if(args[2] == "door")
                                    {
                                        foreach(var generator in Generator079.generators)
                                        {
                                            if(!generator.prevFinish)
                                            {
                                                bool now = !generator.NetworkisDoorOpen;
                                                generator.NetworkisDoorOpen = now;
                                                generator.CallRpcDoSound(now);
                                            }
                                        }
                                        ReturnStr = $"gen doors interacted.";
                                    }
                                    else if(args[2] == "set")
                                    {
                                        foreach(var generator in Generator079.generators)
                                        {
                                            if(!generator.prevFinish)
                                            {
                                                generator.NetworkisTabletConnected = true;
                                            }
                                        }
                                        ReturnStr = "gen set.";
                                    }
                                    else if(args[2] == "eject")
                                    {
                                        foreach(var generator in Generator079.generators)
                                        {
                                            if(generator.isTabletConnected)
                                            {
                                                generator.EjectTablet();
                                            }
                                        }
                                        ReturnStr = "gen ejected.";
                                    }
                                }
                                else
                                {
                                    isSuccess = false;
                                    ReturnStr = "[gen] Wrong Parameters.";
                                }
                                break;
                            }
                        case "spawn":
                            {
                                var mtfrespawn = PlayerManager.localPlayer.GetComponent<MTFRespawn>();
                                if(mtfrespawn.nextWaveIsCI)
                                {
                                    mtfrespawn.timeToNextRespawn = 14f;
                                }
                                else
                                {
                                    mtfrespawn.timeToNextRespawn = 18.5f;
                                }
                                ReturnStr = $"spawn soon. nextIsCI:{mtfrespawn.nextWaveIsCI}";
                                break;
                            }
                        case "next":
                            {
                                if(args.Length > 2)
                                {
                                    MTFRespawn mtfRespawn = PlayerManager.localPlayer.GetComponent<MTFRespawn>();
                                    if(args[2] == "ci")
                                    {
                                        mtfRespawn.nextWaveIsCI = true;
                                        ReturnStr = $"set NextIsCI:{mtfRespawn.nextWaveIsCI}";
                                    }
                                    else if(args[2] == "mtf" || args[2] == "ntf")
                                    {
                                        mtfRespawn.nextWaveIsCI = false;
                                        ReturnStr = $"set NextIsCI:{mtfRespawn.nextWaveIsCI}";
                                    }
                                    else
                                    {
                                        isSuccess = false;
                                        ReturnStr = "[next] Wrong Parameters.";
                                    }
                                }
                                else
                                {
                                    isSuccess = false;
                                    ReturnStr = "[next] Wrong Parameters.";
                                }
                                break;
                            }
                        default:
                            {
                                ReturnStr = "Wrong Parameters.";
                                isSuccess = false;
                                break;
                            }
                    }
                    ev.Allow = false;
                    ev.Sender.RAMessage(ReturnStr, isSuccess);
                }
                else
                {
                    ev.Allow = false;
                    ev.Sender.RAMessage("Usage : SANYA < RELOAD / SHOWCONFIG / NUKELOCK / BLACKOUT / EV / GEN / SPAWN / NEXT >", false);
                }
            }
        }
    }
}
