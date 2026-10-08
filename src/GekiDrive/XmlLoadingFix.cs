using System;
using System.Reflection;
using System.Xml.Serialization;
using BepInEx.Logging;

namespace GekiDrive
{
    internal sealed class XmlLoadingFix
    {
        private FieldInfo threshold;
        private int previous;

        internal bool Apply(ManualLogSource log)
        {
            // This game's Mono System.Xml recognizes -1 as interpreter-only mode.
            // Keep XML deserialization intact; avoid repeatedly invoking an absent compiler.
            threshold = typeof(XmlSerializer).GetField("generationThreshold", BindingFlags.Static | BindingFlags.NonPublic);
            if (threshold == null || threshold.FieldType != typeof(int))
            {
                threshold = null;
                log.LogWarning("XML optimization unavailable: Mono generationThreshold field not found.");
                return false;
            }
            try
            {
                previous = (int)threshold.GetValue(null);
                threshold.SetValue(null, -1);
                log.LogInfo("XML loading: interpreter mode enabled; unavailable external compiler attempts disabled.");
                return true;
            }
            catch (Exception e)
            {
                threshold = null;
                log.LogWarning("XML optimization skipped: " + e.Message);
                return false;
            }
        }

        internal void Restore()
        {
            if (threshold == null) return;
            // Do not overwrite a setting changed by another plugin after us.
            if ((int)threshold.GetValue(null) == -1) threshold.SetValue(null, previous);
            threshold = null;
        }
    }
}


