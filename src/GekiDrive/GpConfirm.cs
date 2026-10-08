using HarmonyLib;
using MU3;
using UnityEngine;
using System.Collections.Generic;
using System.Text;
using MU3.AM;

namespace GekiDrive
{
    [HarmonyPatch(typeof(UIGPDialog), "Update")]
    internal static class GpConfirm
    {
        private static UIGPDialog active;
        private static string status;
        private static GUIStyle style;

        [HarmonyPrefix]
        private static void Prefix(UIGPDialog __instance, int ___state_, UISelector ___uiSelector_, UIGPDialogContent ___content_, List<Product> ___productList_, bool ___enableCancel_)
        {
            active = null;
            if (Plugin.GpConfirmEnabled.Value && ___state_ <= 1 && ___uiSelector_ != null)
            {
                active = __instance;
                var text = new StringBuilder("GP: Left / Right to select, Enter to confirm\n");
                int selected = ___uiSelector_.selectIndexRaw;
                for (int i = 0; i < ___uiSelector_.elementList.Count; i++)
                {
                    int productIndex = i - (___enableCancel_ ? 1 : 0);
                    text.Append(i == selected ? "> " : "  ");
                    if (productIndex < 0) text.Append("Cancel");
                    else if (___productList_ != null && productIndex < ___productList_.Count && ___productList_[productIndex].isValid())
                    {
                        var product = ___productList_[productIndex];
                        text.Append(product.getCredit()).Append(" CREDIT -> ").Append(product.getGp()).Append(" GP");
                    }
                    else text.Append("No valid GP product");
                    if (___uiSelector_.elementList[i].enable != 1) text.Append(" [unavailable]");
                    text.Append('\n');
                }
                if (___uiSelector_.elementList.Count == 0) text.Append("No purchase options created\n");
                if (___state_ == 0) text.Append("Waiting for purchase screen...");
                status = text.ToString();
            }
            if (!Plugin.GpConfirmEnabled.Value || ___state_ != 1 || ___uiSelector_ == null || ___content_ == null || !___content_.IsReady) return;
            int direction = Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : Input.GetKeyDown(KeyCode.RightArrow) ? 1 : 0;
            if (direction != 0)
            {
                int count = ___uiSelector_.elementList.Count;
                int current = ___uiSelector_.selectIndexRaw;
                for (int step = 1; step <= count; step++)
                {
                    int next = ((current + direction * step) % count + count) % count;
                    if (___uiSelector_.elementList[next].enable != 1) continue;
                    ___uiSelector_.setSelectIndexRaw(next, true, false);
                    break;
                }
                return; // Selection requests take effect in the selector's next update.
            }
            if (!Input.GetKeyDown(Plugin.GpConfirmKey.Value) && !Input.GetKeyDown(KeyCode.KeypadEnter)) return;
            int index = ___uiSelector_.selectIndexRaw;
            if (index < 0 || index >= ___uiSelector_.elementList.Count || ___uiSelector_.elementList[index].enable != 1)
            {
                if (Plugin.SharedLog != null) Plugin.SharedLog.LogWarning("GP confirm blocked: raw selection=" + index + "; " + status);
                return;
            }
            ___uiSelector_.triggerDecide();
            if (Plugin.SharedLog != null) Plugin.SharedLog.LogInfo("GP selection confirmed by keyboard: " + index);
        }

        internal static void Draw()
        {
            if (active == null || !Plugin.GameHooksReady || !Plugin.GpConfirmEnabled.Value || string.IsNullOrEmpty(status)) return;
            if (style == null) { style = new GUIStyle(GUI.skin.box); style.fontSize = 20; style.alignment = TextAnchor.UpperLeft; }
            int lines = status.Split('\n').Length;
            GUI.Box(new Rect(12, 64, Mathf.Min(620, Screen.width - 24), lines * 26 + 16), status, style);
        }
    }

    [HarmonyPatch(typeof(UIGPDialog), "Start")]
    internal static class GpDiagnostics
    {
        [HarmonyPostfix]
        private static void Postfix(UISelector ___uiSelector_, List<Product> ___productList_, List<UIGPButton> ___buttons_, bool ___enableCancel_)
        {
            if (Plugin.SharedLog == null) return;
            var text = new StringBuilder("GP dialog: cancel=").Append(___enableCancel_);
            text.Append(", products=").Append(___productList_ == null ? -1 : ___productList_.Count);
            text.Append(", buttons=").Append(___buttons_ == null ? -1 : ___buttons_.Count);
            if (___uiSelector_ != null)
            {
                text.Append(", rawSelection=").Append(___uiSelector_.selectIndexRaw).Append(", options=");
                foreach (var element in ___uiSelector_.elementList) text.Append(element.enable).Append(' ');
            }
            Plugin.SharedLog.LogInfo(text.ToString());
        }
    }
}

