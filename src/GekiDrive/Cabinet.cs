using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using MU3.AM;
using MU3.User;
using UnityEngine;

namespace GekiDrive
{
    internal static class Cabinet
    {
        private static CreditLedger ledger;
        private static readonly object sync = new object();
        private static readonly Queue<KeyValuePair<string, int>> signals = new Queue<KeyValuePair<string, int>>();
        internal static void Initialize()
        {
            ledger = new CreditLedger(Path.Combine(Paths.BepInExRootPath, "GEKIDRIVE/credit-ledger.bin"), ModuleConfig.CreditValue.Value);
        }
        internal static int Balance { get { int balance = ledger == null ? 0 : ledger.Balance; return ModuleConfig.FreePlay.Value ? Math.Max(3, balance) : balance; } }
        internal static bool Spend(int count, bool apply)
        {
            if (count < 0) return false;
            if (ModuleConfig.FreePlay.Value) return true;
            if (ledger == null || ledger.Balance < count) return false;
            if (!apply) return true;
            try { return ledger.Spend(count); }
            catch (Exception e) { if (Plugin.SharedLog != null) Plugin.SharedLog.LogError("Virtual credit persistence failed: " + e.Message); return false; }
        }
        internal static bool QueueSignal(string token, string id, int count)
        {
            if (!ModuleConfig.PaymentBridge.Value || !ModuleConfig.VirtualCredits.Value || ModuleConfig.PaymentToken.Value.Length < 24 || !EqualToken(token, ModuleConfig.PaymentToken.Value) || count < 1 || count > 9999 || string.IsNullOrEmpty(id) || id.Length > 128) return false;
            foreach (char ch in id) if (!(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')) return false;
            lock (sync) { if (signals.Count >= 100) return false; signals.Enqueue(new KeyValuePair<string, int>(id, count)); }
            return true;
        }
        private static bool EqualToken(string a, string b) { if (a == null || a.Length != b.Length) return false; int value = 0; for (int i = 0; i < a.Length; i++) value |= a[i] ^ b[i]; return value == 0; }
        internal static void Update()
        {
            if (ModuleConfig.VirtualCredits.Value && Input.GetKeyDown(ModuleConfig.AddCoin.Value)) AddCoin();
            lock (sync)
            {
                while (signals.Count > 0)
                {
                    var item = signals.Dequeue();
                    try
                    {
                        if (ledger == null || !ledger.Add(item.Key, item.Value)) continue;
                        if (Plugin.SharedLog != null) Plugin.SharedLog.LogInfo("Virtual credit signal applied: " + item.Key + ", credits=" + item.Value);
                    }
                    catch (Exception e) { if (Plugin.SharedLog != null) Plugin.SharedLog.LogError("Credit signal ledger failed; credit not added: " + e.Message); }
                }
            }
        }
        internal static void AddCoin()
        {
            if (!ModuleConfig.VirtualCredits.Value || ledger == null) return;
            try { ledger.Add("coin-" + Guid.NewGuid().ToString("N"), ModuleConfig.CoinUnits.Value); }
            catch (Exception e) { if (Plugin.SharedLog != null) Plugin.SharedLog.LogError("Virtual coin not added: " + e.Message); }
        }
    }
    [HarmonyPatch(typeof(UserManager), "get_GP")]
    internal static class GpLockRead
    {
        [HarmonyPostfix] private static void Postfix(ref int __result) { if (ModuleConfig.LockGp.Value) __result = ModuleConfig.GpValue.Value; }
    }
    [HarmonyPatch(typeof(UserManager), "set_GP")]
    internal static class GpLockWrite
    {
        [HarmonyPrefix] private static void Prefix(ref int __0) { if (ModuleConfig.LockGp.Value) __0 = ModuleConfig.GpValue.Value; }
    }
    [HarmonyPatch(typeof(Credit), "get_credit")]
    internal static class CabinetCredits
    {
        [HarmonyPrefix] private static bool Prefix(ref int __result) { if (!ModuleConfig.FreePlay.Value && !ModuleConfig.VirtualCredits.Value) return true; __result = Cabinet.Balance; return false; }
    }
    [HarmonyPatch(typeof(Credit), "isGameCostEnough")]
    internal static class CabinetEnough
    {
        [HarmonyPrefix] private static bool Prefix(int __1, ref bool __result) { if (!ModuleConfig.FreePlay.Value && !ModuleConfig.VirtualCredits.Value) return true; __result = Cabinet.Spend(__1, false); return false; }
    }
    [HarmonyPatch(typeof(Credit), "payGameCost")]
    internal static class CabinetPay
    {
        [HarmonyPrefix] private static bool Prefix(int __1, ref bool __result) { if (!ModuleConfig.FreePlay.Value && !ModuleConfig.VirtualCredits.Value) return true; __result = Cabinet.Spend(__1, true); return false; }
    }
    [HarmonyPatch(typeof(Credit), "onCoinIn")]
    internal static class CabinetCoin
    {
        [HarmonyPostfix] private static void Postfix(bool __result) { if (__result) Cabinet.AddCoin(); }
    }
}
