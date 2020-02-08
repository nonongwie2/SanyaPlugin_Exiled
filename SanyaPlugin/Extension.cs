﻿using EXILED;

namespace SanyaPlugin
{
    public static class Extensions
    {
        public static string GetName(this ReferenceHub player) => player.nicknameSync.MyNick;
        public static string GetIpAddress(this ReferenceHub player) => player.characterClassManager.RequestIp;
        public static string GetUserId(this ReferenceHub player) => player.characterClassManager.UserId;
        public static RoleType GetRoleType(this ReferenceHub player) => player.characterClassManager.CurClass;
        public static Team GetTeam(this ReferenceHub player) => Plugin.GetTeam(player.GetRoleType());
        public static bool isHandCuffed(this ReferenceHub player) => player.handcuffs.CufferId != -1;
    }
}