using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using BepInEx;
using UnityEngine;

namespace GekiDrive
{
    internal static class VideoOutput
    {
        private sealed class CameraState { internal Camera Camera; internal RenderTexture Target; internal float Aspect; }
        private sealed class CanvasState { internal Canvas Canvas; internal RenderMode Mode; internal Camera Camera; internal float Distance; }
        private static readonly List<CameraState> captured = new List<CameraState>();
        private static readonly List<CanvasState> canvases = new List<CanvasState>();
        private static RenderTexture source, export;
        private static Texture2D pixels;
        private static Camera primary, secondary, freeCamera;
        private static Vector3 originalPosition, freePosition;
        private static Quaternion originalRotation, freeRotation;
        private static GameObject hiddenHud;
        private static bool hudWasActive;
        private static float nextScan;
        private static int originalWidth, originalHeight, imageCount;
        private static bool originalFullscreen, windowChanged;
        private static string exportPath;
        private static double exportBase, exportPosition;
        private static bool frameReady;
        private static int exportFps;
        internal static bool Exporting { get; private set; }
        internal static double ExportPosition { get { return exportPosition; } }
        private static readonly System.Reflection.FieldInfo battleUi = HarmonyLib.AccessTools.Field(typeof(MU3.Battle.GameEngine), "_battleUI");
        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
        private static IntPtr window;
        private static int savedStyle;
        internal static void Initialize() { originalWidth = Screen.width; originalHeight = Screen.height; originalFullscreen = Screen.fullScreen; }
        internal static void Configure()
        {
            RestoreLayout();
            if (window != IntPtr.Zero) { SetWindowLong(window, -16, savedStyle); SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0, 0x37); window = IntPtr.Zero; }
            if (ModuleConfig.WindowMode.Value != "Native")
            {
                Screen.SetResolution(ModuleConfig.OutputWidth.Value, ModuleConfig.OutputHeight.Value, false); windowChanged = true;
                if (ModuleConfig.WindowMode.Value == "Borderless")
                {
                    window = GetActiveWindow();
                    if (window != IntPtr.Zero) { savedStyle = GetWindowLong(window, -16); SetWindowLong(window, -16, savedStyle & ~0x00CF0000); SetWindowPos(window, IntPtr.Zero, 0, 0, ModuleConfig.OutputWidth.Value, ModuleConfig.OutputHeight.Value, 0x24); }
                }
            }
            else if (windowChanged) { Screen.SetResolution(originalWidth, originalHeight, originalFullscreen); windowChanged = false; }
            if (!ModuleConfig.Landscape.Value && !ModuleConfig.SecondDisplay.Value) return;
            source = new RenderTexture(1080, 1920, 24); source.Create();
            primary = OutputCamera(0, false);
            if (ModuleConfig.SecondDisplay.Value)
            {
                int index = ModuleConfig.DisplayIndex.Value;
                if (index < Display.displays.Length) { Display.displays[index].Activate(); secondary = OutputCamera(index, true); }
                else if (Plugin.SharedLog != null) Plugin.SharedLog.LogWarning("Second display index does not exist: " + index);
            }
            nextScan = 0;
        }
        private static Camera OutputCamera(int display, bool second)
        {
            var go = new GameObject("GEKIDRIVE Display " + display); UnityEngine.Object.DontDestroyOnLoad(go);
            var camera = go.AddComponent<Camera>(); camera.cullingMask = 0; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.depth = 1000000; camera.targetDisplay = display;
            go.AddComponent<DisplayCompositor>().Secondary = second;
            return camera;
        }
        internal static void Update()
        {
            if (source != null && Time.realtimeSinceStartup >= nextScan)
            {
                nextScan = Time.realtimeSinceStartup + 1;
                Camera highest = null;
                foreach (var camera in Camera.allCameras)
                {
                    if (camera == primary || camera == secondary || camera.targetDisplay != 0 || camera.targetTexture != null) continue;
                    captured.Add(new CameraState { Camera = camera, Target = camera.targetTexture, Aspect = camera.aspect });
                    camera.targetTexture = source;
                    camera.aspect = 1080f * camera.rect.width / (1920f * Mathf.Max(0.01f, camera.rect.height));
                }
                foreach (var item in captured) if (item.Camera != null && (highest == null || item.Camera.depth > highest.depth)) highest = item.Camera;
                if (highest != null)
                {
                    foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
                    {
                        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                        canvases.Add(new CanvasState { Canvas = canvas, Mode = canvas.renderMode, Camera = canvas.worldCamera, Distance = canvas.planeDistance });
                        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = highest; canvas.planeDistance = Mathf.Lerp(highest.nearClipPlane, highest.farClipPlane, 0.01f);
                    }
                }
            }
            if (!Training.Active || !Training.Controlled) { RestoreView(); return; }
            bool wantHidden = ModuleConfig.HideHud.Value || Exporting;
            if (wantHidden && hiddenHud == null && Training.Engine != null)
            {
                var ui = battleUi.GetValue(Training.Engine) as Component;
                if (ui != null) { hiddenHud = ui.gameObject; hudWasActive = hiddenHud.activeSelf; hiddenHud.SetActive(false); }
            }
            if (!wantHidden && hiddenHud != null) { hiddenHud.SetActive(hudWasActive); hiddenHud = null; }
            if (!ModuleConfig.FreeCam.Value) { RestoreCamera(); return; }
            if (freeCamera == null)
            {
                freeCamera = Training.NotesCamera;
                if (!string.IsNullOrEmpty(ModuleConfig.CameraName.Value)) foreach (var camera in Camera.allCameras) if (camera.name == ModuleConfig.CameraName.Value) freeCamera = camera;
                if (freeCamera == null) return;
                originalPosition = freePosition = freeCamera.transform.position; originalRotation = freeRotation = freeCamera.transform.rotation;
                if (freeCamera.GetComponent<FreeCameraDriver>() == null) freeCamera.gameObject.AddComponent<FreeCameraDriver>();
            }
            float delta = Time.unscaledDeltaTime * ModuleConfig.CameraMoveSpeed.Value;
            Vector3 move = new Vector3((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0), (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            freePosition += freeRotation * move * delta;
            if (Input.GetMouseButton(1)) freeRotation = Quaternion.Euler(-Input.GetAxis("Mouse Y") * 2, Input.GetAxis("Mouse X") * 2, 0) * freeRotation;
        }
        internal static void ApplyFreeCamera(Camera camera) { if (camera == freeCamera && ModuleConfig.FreeCam.Value) { camera.transform.position = freePosition; camera.transform.rotation = freeRotation; } }
        internal static void BeginSong()
        {
            EndExport();
            if (!ModuleConfig.ExportFrames.Value || !Training.Controlled) return;
            try
            {
                string directory = string.IsNullOrEmpty(ModuleConfig.ExportDirectory.Value) ? Path.Combine(Paths.BepInExRootPath, "GEKIDRIVE/exports") : ModuleConfig.ExportDirectory.Value;
                exportPath = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")); Directory.CreateDirectory(exportPath);
                export = new RenderTexture(ModuleConfig.ExportWidth.Value, ModuleConfig.ExportHeight.Value, 24); export.Create();
                pixels = new Texture2D(export.width, export.height, TextureFormat.RGB24, false);
                imageCount = 0; exportPosition = exportBase = Training.Position; exportFps = ModuleConfig.ExportFps.Value; frameReady = false; Exporting = true;
                if (PracticeAudio.Player != null) { PracticeAudio.Player.SetVolume(0); PracticeAudio.Player.UpdateAll(); }
                File.WriteAllText(Path.Combine(exportPath, "capture.txt"), "fps=" + ModuleConfig.ExportFps.Value + "\nspeed=" + ModuleConfig.Speed.Value.ToString(CultureInfo.InvariantCulture) + "\nsourceStartMs=" + exportBase.ToString(CultureInfo.InvariantCulture) + "\n");
            }
            catch (Exception e) { EndExport(); if (Plugin.SharedLog != null) Plugin.SharedLog.LogError("Export unavailable: " + e.Message); }
        }
        internal static void LateUpdate()
        {
            if (!Exporting || !frameReady || !Training.Active || Training.Paused || Training.NotesCamera == null) return;
            frameReady = false;
            Camera camera = freeCamera != null ? freeCamera : Training.NotesCamera;
            RenderTexture previous = camera.targetTexture, active = RenderTexture.active;
            float previousAspect = camera.aspect;
            try
            {
                camera.targetTexture = export; camera.aspect = (float)export.width / export.height; camera.Render(); RenderTexture.active = export;
                pixels.ReadPixels(new Rect(0, 0, export.width, export.height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(exportPath, "frame" + imageCount.ToString("D6") + ".png"), pixels.EncodeToPNG()); imageCount++;
                exportPosition += (1000.0 / exportFps) * ModuleConfig.Speed.Value;
            }
            catch (Exception e) { EndExport(); if (Plugin.SharedLog != null) Plugin.SharedLog.LogError("Export stopped: " + e.Message); }
            finally { camera.targetTexture = previous; camera.aspect = previousAspect; RenderTexture.active = active; }
        }
        internal static void EndSong() { EndExport(); RestoreView(); }
        internal static void MarkFrameReady()
        {
            if (Exporting && Training.Controlled) frameReady = true;
        }
        private static void EndExport()
        {
            if (Exporting && Plugin.SharedLog != null) Plugin.SharedLog.LogInfo("Chart frames exported: " + exportPath + " (" + imageCount + " frames)");
            bool wasExporting = Exporting; Exporting = false;
            if (wasExporting) { Training.ResumeClock(exportPosition); if (PracticeAudio.Player != null) { PracticeAudio.Player.SetVolume(1); PracticeAudio.Player.UpdateAll(); } }
            if (export != null) { export.Release(); UnityEngine.Object.Destroy(export); export = null; }
            if (pixels != null) { UnityEngine.Object.Destroy(pixels); pixels = null; }
        }
        private static void RestoreCamera() { if (freeCamera != null) { freeCamera.transform.position = originalPosition; freeCamera.transform.rotation = originalRotation; freeCamera = null; } }
        private static void RestoreView() { RestoreCamera(); if (hiddenHud != null) { hiddenHud.SetActive(hudWasActive); hiddenHud = null; } }
        private static void RestoreLayout()
        {
            foreach (var item in captured) if (item.Camera != null) { item.Camera.targetTexture = item.Target; item.Camera.aspect = item.Aspect; } captured.Clear();
            foreach (var item in canvases) if (item.Canvas != null) { item.Canvas.renderMode = item.Mode; item.Canvas.worldCamera = item.Camera; item.Canvas.planeDistance = item.Distance; } canvases.Clear();
            if (primary != null) UnityEngine.Object.Destroy(primary.gameObject); if (secondary != null) UnityEngine.Object.Destroy(secondary.gameObject); primary = secondary = null;
            if (source != null) { source.Release(); UnityEngine.Object.Destroy(source); source = null; }
        }
        internal static void Dispose() { EndSong(); RestoreLayout(); if (window != IntPtr.Zero) { SetWindowLong(window, -16, savedStyle); SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0, 0x37); } if (windowChanged) Screen.SetResolution(originalWidth, originalHeight, originalFullscreen); }
        internal static Rect Crop(string value)
        {
            string[] parts = value.Split(','); float x, y, w, h;
            if (parts.Length != 4 || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out w) || !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out h) || float.IsNaN(x + y + w + h) || x < 0 || y < 0 || w <= 0 || h <= 0 || x + w > 1 || y + h > 1) return new Rect(0, 0, 1, 1);
            return new Rect(x, y, w, h);
        }
        internal static void Composite(Material material, bool second)
        {
            if (source == null || material == null) return;
            material.mainTexture = source; material.SetPass(0); GL.PushMatrix(); GL.LoadOrtho();
            if (second) Quad(new Rect(0, 0, 1, 1), Crop(ModuleConfig.SecondaryCrop.Value));
            else if (ModuleConfig.Landscape.Value && Training.Active)
            {
                Quad(new Rect(0.28f, 0, 0.44f, 1), Crop(ModuleConfig.MainCrop.Value));
                Quad(new Rect(0.01f, 0.25f, 0.26f, 0.6f), Crop(ModuleConfig.LeftCrop.Value));
                Quad(new Rect(0.73f, 0.25f, 0.26f, 0.6f), Crop(ModuleConfig.RightCrop.Value));
            }
            else Quad(new Rect(0, 0, 1, 1), new Rect(0, 0, 1, 1));
            GL.PopMatrix();
        }
        private static void Quad(Rect destination, Rect crop)
        {
            float availableAspect = Camera.current.aspect * destination.width / destination.height;
            float cropAspect = 1080f * crop.width / (1920f * crop.height);
            if (availableAspect > cropAspect) { float width = destination.width * cropAspect / availableAspect; destination.x += (destination.width - width) / 2; destination.width = width; }
            else { float height = destination.height * availableAspect / cropAspect; destination.y += (destination.height - height) / 2; destination.height = height; }
            GL.Begin(GL.QUADS);
            GL.TexCoord2(crop.xMin, crop.yMin); GL.Vertex3(destination.xMin, destination.yMin, 0);
            GL.TexCoord2(crop.xMax, crop.yMin); GL.Vertex3(destination.xMax, destination.yMin, 0);
            GL.TexCoord2(crop.xMax, crop.yMax); GL.Vertex3(destination.xMax, destination.yMax, 0);
            GL.TexCoord2(crop.xMin, crop.yMax); GL.Vertex3(destination.xMin, destination.yMax, 0);
            GL.End();
        }
    }
    public sealed class DisplayCompositor : MonoBehaviour
    {
        internal bool Secondary;
        private Material material;
        private void OnPostRender() { if (material == null) { var shader = Shader.Find("Unlit/Texture"); if (shader == null) return; material = new Material(shader); } VideoOutput.Composite(material, Secondary); }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
    public sealed class FreeCameraDriver : MonoBehaviour
    {
        private void OnPreCull() { VideoOutput.ApplyFreeCamera(GetComponent<Camera>()); }
    }
}
