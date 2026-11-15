using System;
using Apollo.OrbitalSim;
using Apollo.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace Apollo.MissionControl
{
    /// <summary>
    /// Mission Control telemetry readout (uGUI, legacy Text). Every value is computed from the shared
    /// SimulationHost state each frame; nothing is stored or animated here.
    /// </summary>
    public sealed class TelemetryPanel : MonoBehaviour
    {
        [SerializeField] private SimulationHost host;
        [SerializeField] private bool compact; // two-line strip at the bottom of the screen (used over the flat map)

        private Text label;

        private void Start()
        {
            if (host == null) host = FindFirstObjectByType<SimulationHost>();
            Build();
        }

        private void Update()
        {
            if (host == null || host.Orbit == null || label == null) return;
            label.text = compact ? FormatCompact(host.CurrentState, host.Clock) : Format(host.CurrentState, host.Orbit, host.Clock, host);
        }

        private static string Format(OrbitalState s, KeplerOrbit orbit, SimulationClock clock, SimulationHost host)
        {
            TimeSpan met = TimeSpan.FromSeconds(s.MissionElapsedTime);
            int orbitNumber = (int)Math.Floor(host.Revolutions) + 1;
            string warning = host.PeriapsisIsLow ? $"\n!! LOW PERIAPSIS - restart below {host.RestartAltitudeMeters / 1000.0:F0} km" : "";
            GeoPoint ground = EarthRotation.SubPoint(s.Position, s.MissionElapsedTime);
            string timeScale = clock.IsPaused ? "PAUSED" : $"x{clock.TimeScale:0.#}";

            return
                "MISSION CONTROL TELEMETRY\n" +
                "-------------------------\n" +
                $"MET            {(int)met.TotalHours:000}:{met.Minutes:00}:{met.Seconds:00}   ({timeScale})\n" +
                $"Orbit #        {orbitNumber}\n" +
                $"Altitude       {s.Altitude / 1000.0,10:F3} km\n" +
                $"Speed          {s.Speed,10:F2} m/s\n" +
                $"Period         {s.Period / 60.0,10:F2} min\n" +
                $"Inclination    {orbit.Inclination * 180.0 / Math.PI,10:F2} deg\n" +
                $"Eccentricity   {orbit.Eccentricity,10:F5}\n" +
                $"Periapsis alt  {orbit.PeriapsisAltitude / 1000.0,10:F1} km\n" +
                $"Apoapsis alt   {orbit.ApoapsisAltitude / 1000.0,10:F1} km\n" +
                $"Semi-major ax  {orbit.SemiMajorAxis / 1000.0,10:F1} km\n" +
                $"Latitude       {ground.LatitudeDeg,10:F2} deg\n" +
                $"Longitude      {ground.LongitudeDeg,10:F2} deg\n" +
                $"Pos ECI (km)   {s.Position.X / 1000.0,9:F0} {s.Position.Y / 1000.0,9:F0} {s.Position.Z / 1000.0,9:F0}\n" +
                $"Vel ECI (m/s)  {s.Velocity.X,9:F0} {s.Velocity.Y,9:F0} {s.Velocity.Z,9:F0}" + warning;
        }


        private static string FormatCompact(OrbitalState s, SimulationClock clock)
        {
            TimeSpan met = TimeSpan.FromSeconds(s.MissionElapsedTime);
            GeoPoint ground = EarthRotation.SubPoint(s.Position, s.MissionElapsedTime);
            string timeScale = clock.IsPaused ? "PAUSED" : $"x{clock.TimeScale:0.#}";
            return
                $"MET {(int)met.TotalHours:000}:{met.Minutes:00}:{met.Seconds:00} ({timeScale})   Altitude {s.Altitude / 1000.0:F3} km   Speed {s.Speed:F1} m/s\n" +
                $"Ground point: lat {ground.LatitudeDeg,7:F2} deg   lon {ground.LongitudeDeg,8:F2} deg   | cyan: past track, yellow: predicted, red: spacecraft";
        }

        private void Build()
        {
            var canvasObject = new GameObject("TelemetryCanvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);
            var panelRect = (RectTransform)panel.transform;
            if (compact)
            {
                panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0f);
                panelRect.anchoredPosition = new Vector2(0f, 20f);
                panelRect.sizeDelta = new Vector2(1860f, 110f);
            }
            else
            {
                panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0f, 1f);
                panelRect.anchoredPosition = new Vector2(20f, -20f);
                panelRect.sizeDelta = new Vector2(640f, 560f);
            }
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panel.transform, false);
            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 16f);
            textRect.offsetMax = new Vector2(-16f, -16f);

            label = textObject.GetComponent<Text>();
            label.font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Menlo", "Courier New", "Liberation Mono", "Arial" }, 24);
            label.fontSize = compact ? 26 : 24;
            label.color = new Color(0.6f, 1f, 0.7f);
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
        }
    }
}
