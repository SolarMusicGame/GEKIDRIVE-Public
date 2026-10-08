using System;
using System.Reflection;
using HarmonyLib;
using MU3.Battle;
using MU3.Client;
using MU3.User;

namespace GekiDrive
{
    // Public build policy is unconditional, and installed independently of optional feature patches.
    internal static class PublicScoreGuard
    {
        internal static bool Ready { get; private set; }
        private static Harmony protection;
        private static readonly FieldInfo state = typeof(Packet).GetField("state_", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo status = typeof(Packet).GetField("status_", BindingFlags.Instance | BindingFlags.NonPublic);
        private static bool resultLogged, uploadLogged;
        internal static void Install()
        {
            protection = new Harmony("org.gekidrive.ongeki.public.guard");
            try
            {
                if (state == null || status == null) throw new MissingFieldException("Packet completion fields missing.");
                Patch(typeof(GameEngine), "applyResultToUserData", "BlockResult");
                Patch(typeof(UserLocal), "addPlayLog", "BlockPlayLog");
                Patch(typeof(PacketUpsertUserAll), "create", "BlockUploadCreate");
                Patch(typeof(PacketUpsertUserAll), "proc", "BlockUploadProcess");
                Patch(typeof(Packet), "create", "BlockGenericCreate");
                Patch(typeof(Packet), "proc", "BlockGenericProcess");
                Ready = true;
                Plugin.SharedLog.LogInfo("PUBLIC SCORE GUARD READY: result persistence and UpsertUserAll submission permanently blocked.");
            }
            catch { protection.UnpatchSelf(); Ready = false; throw; }
        }
        private static void Patch(Type type, string method, string prefix)
        {
            var target = AccessTools.Method(type, method);
            if (target == null) throw new MissingMethodException(type.FullName, method);
            protection.Patch(target, prefix: new HarmonyMethod(typeof(PublicScoreGuard), prefix) { priority = Priority.First });
        }
        private static bool BlockResult()
        {
            if (!resultLogged) { resultLogged = true; Plugin.SharedLog.LogInfo("PUBLIC: result shown locally; player result update skipped."); }
            return false;
        }
        private static bool BlockPlayLog() { return false; }
        private static void Done(Packet packet)
        {
            state.SetValue(packet, Packet.State.Done); status.SetValue(packet, Packet.Status.OK);
            if (!uploadLogged) { uploadLogged = true; Plugin.SharedLog.LogInfo("PUBLIC: UpsertUserAll skipped locally; no user-save request created."); }
        }
        private static bool BlockUploadCreate(PacketUpsertUserAll __instance, ref bool __result)
        { Done(__instance); __result = false; return false; }
        private static bool BlockUploadProcess(PacketUpsertUserAll __instance, ref Packet.State __result)
        { Done(__instance); __result = Packet.State.Done; return false; }
        private static bool BlockGenericCreate(Packet __instance, INetQuery __0, ref bool __result)
        {
            if (!(__instance is PacketUpsertUserAll) && !(__0 is UpsertUserAll)) return true;
            Done(__instance); __result = false; return false;
        }
        private static bool BlockGenericProcess(Packet __instance, ref Packet.State __result)
        {
            if (!(__instance is PacketUpsertUserAll) && !(__instance.Query is UpsertUserAll)) return true;
            Done(__instance); __result = Packet.State.Done; return false;
        }
        internal static void Dispose() { Ready = false; if (protection != null) protection.UnpatchSelf(); }
    }
}
