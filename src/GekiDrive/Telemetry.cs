using System;
using System.Globalization;
using System.Text;
using HarmonyLib;
using MU3.Battle;
using MU3.Notes;
using UnityEngine;

namespace GekiDrive
{
    internal static class Telemetry
    {
        private struct Hit { internal float Ms, At; internal bool Miss; }
        private static readonly Hit[] hits = new Hit[64];
        private static int cursor;
        internal static int Technical, Combo, Critical, Break, HitCount, Miss;
        internal static float LastMs, Actual, Center, Minimum, Maximum;
        internal static string Json = "{\"playing\":false}";
        private static float nextPublish;
        private static GUIStyle text;
        private delegate void FieldAt(NotesManager notes, float frame, ref NotesManager.FieldState state);
        private static readonly FieldAt future = (FieldAt)Delegate.CreateDelegate(typeof(FieldAt), AccessTools.Method(typeof(NotesManager), "createFieldState"));
        internal static void Reset() { cursor = 0; Array.Clear(hits, 0, hits.Length); Technical = Combo = Critical = Break = HitCount = Miss = 0; }
        internal static void Judge(Judge judge, Timing timing, float originalDiff, bool originalMiss, Lanes lanes)
        {
            if (!Training.Active || Training.Notes == null) return;
            // Native NotesManager uses 60 chart frames per second, regardless of display FPS.
            LastMs = originalDiff * (1000f / 60f);
            hits[cursor++ % hits.Length] = new Hit { Ms = LastMs, At = Time.realtimeSinceStartup, Miss = originalMiss };
            ReplayModule.Event(2, (float)Training.Notes.getCurrentMsec(), LastMs, (float)judge, (float)timing, (int)lanes);
        }
        internal static void Update()
        {
            if (Training.Active && Training.Engine != null)
            {
                var counter = Training.Engine.counters;
                Technical = counter.getScore(ScoreType.TechScore);
                Combo = counter.getCombo(ComboType.Note);
                Critical = counter.getScore(ScoreType.CriticalBreak); Break = counter.getScore(ScoreType.Break);
                HitCount = counter.getScore(ScoreType.Hit); Miss = counter.getScore(ScoreType.Miss);
                var area = Training.Notes.fieldState.area;
                Actual = Training.Notes.fieldState.playerJudge;
                Center = area.posInC;
                Minimum = Mathf.Min(area.placeAftL, area.placeAftR); Maximum = Mathf.Max(area.placeAftL, area.placeAftR);
            }
            if (Time.realtimeSinceStartup < nextPublish) return;
            nextPublish = Time.realtimeSinceStartup + 0.1f;
            Json = "{\"playing\":" + (Training.Active ? "true" : "false") + ",\"musicId\":" + (Training.Session.musicData == null ? 0 : Training.Session.musicData.id)
                + ",\"title\":" + Quote(Training.Session.musicData == null ? "" : Training.Session.musicData.name)
                + ",\"difficulty\":" + Quote(Training.Session.musicLevel.ToString()) + ",\"technicalScore\":" + Technical + ",\"combo\":" + Combo
                + ",\"critical\":" + Critical + ",\"break\":" + Break + ",\"hit\":" + HitCount + ",\"miss\":" + Miss
                + ",\"timingMs\":" + Number(LastMs) + ",\"lever\":" + Number(Actual) + ",\"trackCenter\":" + Number(Center)
                + ",\"practice\":" + (Training.Touched ? "true" : "false") + ",\"replay\":" + (ReplayModule.Playing ? "true" : "false") + "}";
        }
        internal static string Number(float value) { return float.IsNaN(value) || float.IsInfinity(value) ? "0" : value.ToString("0.###", CultureInfo.InvariantCulture); }
        internal static string Quote(string value)
        {
            var b = new StringBuilder("\"");
            foreach (char ch in value ?? "")
            {
                if (ch == '\\' || ch == '"') b.Append('\\').Append(ch);
                else if (ch < 32) b.Append("\\u").Append(((int)ch).ToString("x4"));
                else b.Append(ch);
            }
            return b.Append('"').ToString();
        }
        internal static void Draw()
        {
            if (!Training.Active) return;
            if (text == null) { text = new GUIStyle(GUI.skin.label); text.fontSize = 18; text.normal.textColor = Color.white; }
            if (ModuleConfig.TimingBar.Value)
            {
                float w = Mathf.Min(620, Screen.width - 24), x = (Screen.width - w) / 2, y = Screen.height - 100;
                GUI.Box(new Rect(x, y, w, 78), "");
                GUI.Label(new Rect(x + 8, y + 2, w - 16, 25), "EARLY    |    " + LastMs.ToString("+0.0;-0.0;0.0") + " ms    |    LATE", text);
                Color old = GUI.color;
                GUI.color = Color.gray; GUI.DrawTexture(new Rect(x + w / 2, y + 28, 2, 34), Texture2D.whiteTexture);
                foreach (var hit in hits)
                {
                    float age = Time.realtimeSinceStartup - hit.At;
                    if (hit.At <= 0 || age > 4 || hit.Miss) continue;
                    GUI.color = new Color(hit.Ms < 0 ? 0.3f : 1, 0.7f, hit.Ms < 0 ? 1 : 0.3f, 1 - age / 4);
                    float at = x + w / 2 + Mathf.Clamp(hit.Ms / ModuleConfig.TimingRange.Value, -1, 1) * (w / 2 - 10);
                    GUI.DrawTexture(new Rect(at, y + 30, 3, 30), Texture2D.whiteTexture);
                }
                GUI.color = old;
            }
            if (ModuleConfig.LeverAssist.Value)
            {
                float x = Screen.width * 0.25f, w = Screen.width * 0.5f, y = Screen.height - 190;
                GUI.Box(new Rect(x, y, w, 75), "Lever / track reference");
                float span = Mathf.Max(1, Maximum - Minimum), left = Minimum - span, range = span * 3;
                Color old = GUI.color;
                GUI.color = new Color(0.2f, 0.6f, 0.3f, 0.7f);
                GUI.DrawTexture(new Rect(x + w / 3, y + 30, w / 3, 25), Texture2D.whiteTexture);
                GUI.color = Color.cyan;
                GUI.DrawTexture(new Rect(x + (Actual - left) / range * w, y + 28, 4, 30), Texture2D.whiteTexture);
                GUI.color = Color.yellow;
                GUI.DrawTexture(new Rect(x + (Center - left) / range * w, y + 28, 3, 30), Texture2D.whiteTexture);
                for (int i = 1; i <= 20; i++)
                {
                    var state = new NotesManager.FieldState();
                    future(Training.Notes, Training.Notes.getCurrentFrame() + i * 6, ref state);
                    float point = Mathf.Clamp01((state.area.posInC - left) / range);
                    GUI.DrawTexture(new Rect(x + point * w, y + 29 + i, 2, 2), Texture2D.whiteTexture);
                }
                GUI.color = old;
                GUI.Label(new Rect(x + 8, y + 52, w - 16, 22), "Offset " + (Actual - Center).ToString("+0.00;-0.00;0.00") + " (cyan: player, yellow: center)", text);
            }
            if (ModuleConfig.Training.Value || ReplayModule.Playing)
            {
                GUI.Box(new Rect(12, 220, Mathf.Min(Screen.width - 24, 620), 125), "Training " + ModuleConfig.Speed.Value.ToString("F2") + "x  " + (Training.Paused ? "PAUSED" : ""));
                if (GUI.Button(new Rect(24, 252, 125, 28), "Retry")) Training.Request(0, false);
                if (GUI.Button(new Rect(159, 252, 125, 28), "Segment")) Training.Request(Math.Max(0, ModuleConfig.SegmentStart.Value - ModuleConfig.PreRoll.Value) * 1000, false);
                if (GUI.Button(new Rect(294, 252, 125, 28), "Reload chart")) Training.Request(Math.Max(0, ModuleConfig.SegmentStart.Value - ModuleConfig.PreRoll.Value) * 1000, true);
                GUI.Label(new Rect(24, 288, 580, 50), Training.Notice, text);
            }
        }
    }

    [HarmonyPatch(typeof(NotesManager), "setResultEffectAndScore", new Type[] { typeof(Judge), typeof(Timing), typeof(AttackNoteType), typeof(Lanes), typeof(Vector3), typeof(Vector3), typeof(float) })]
    internal static class TimingCapture
    {
        internal struct Raw { internal float Diff; internal bool Miss; }
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static void Prefix(Judge judge, float frameDiff, out Raw __state) { __state = new Raw { Diff = frameDiff, Miss = judge == Judge.Miss }; }
        [HarmonyPostfix]
        private static void Postfix(Judge judge, Timing timing, Lanes __3, Raw __state) { Telemetry.Judge(judge, timing, __state.Diff, __state.Miss, __3); }
    }
}
