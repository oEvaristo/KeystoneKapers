using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KeystoneKapers
{
    /// <summary>
    /// Minimapa lateral: três andares + terraço, escadas rolantes (subida em ciano, descida exclusiva do fugitivo em laranja),
    /// poço/cabine do elevador, saída do terraço, policial e fugitivo. Gerado a partir do LevelLayout real.
    /// </summary>
    public class MinimapController : MonoBehaviour
    {
        public RectTransform area;
        public PoliceController police;
        public FugitiveAI fugitive;
        public Color floorColor = new Color(1f, 1f, 1f, 0.55f);
        public Color upColor = new Color(0.3f, 0.9f, 1f, 0.9f);
        public Color downColor = new Color(1f, 0.55f, 0.15f, 0.8f);
        public Color shaftColor = new Color(1f, 0.9f, 0.3f, 0.25f);
        public Color elevatorColor = new Color(1f, 0.85f, 0.2f, 1f);
        public Color policeColor = new Color(0.25f, 0.55f, 1f, 1f);
        public Color fugitiveColor = new Color(1f, 0.2f, 0.2f, 1f);
        public Color escapeColor = new Color(0.2f, 1f, 0.3f, 1f);

        RectTransform policeDot, fugitiveDot, elevatorDot;
        float minX, maxX, minY, maxY;
        readonly List<Graphic> built = new List<Graphic>();

        LevelLayout Layout => LevelLayout.Instance;

        void Start() { Build(); }

        Vector2 Map(Vector3 world)
        {
            var r = area.rect;
            float u = Mathf.InverseLerp(minX, maxX, world.x);
            float v = Mathf.InverseLerp(minY, maxY, world.y);
            return new Vector2(Mathf.Lerp(r.xMin + 6f, r.xMax - 6f, u), Mathf.Lerp(r.yMin + 8f, r.yMax - 14f, v));
        }

        Image MakeImage(string n, Color c)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(area, false);
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            built.Add(img);
            return img;
        }

        void Line(string n, Vector2 a, Vector2 b, float thickness, Color c)
        {
            var img = MakeImage(n, c);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            Vector2 d = b - a;
            rt.sizeDelta = new Vector2(d.magnitude, thickness);
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        RectTransform Dot(string n, Vector2 size, Color c)
        {
            var img = MakeImage(n, c);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = size;
            return rt;
        }

        public void Build()
        {
            foreach (var g in built) if (g) Destroy(g.gameObject);
            built.Clear();
            if (!Layout || !area) return;
            minX = float.MaxValue; maxX = float.MinValue;
            foreach (var f in Layout.floors) { minX = Mathf.Min(minX, f.minX); maxX = Mathf.Max(maxX, f.maxX); }
            minY = Layout.FloorY(0);
            maxY = Layout.FloorY(Layout.TerraceIndex);

            for (int i = 0; i < Layout.FloorCount; i++)
            {
                var f = Layout.floors[i];
                Line("Andar" + i, Map(new Vector3(f.minX, f.y)), Map(new Vector3(f.maxX, f.y)), 2f, floorColor);
            }

            var el = Layout.elevator;
            if (el)
            {
                float ex = el.cabin.position.x;
                Vector2 a = Map(new Vector3(ex, Layout.FloorY(0))), b = Map(new Vector3(ex, Layout.FloorY(el.TopFloor)));
                var shaft = Dot("Poco", new Vector2(10f, b.y - a.y + 12f), shaftColor);
                shaft.anchoredPosition = a;
                elevatorDot = Dot("Elevador", new Vector2(10f, 12f), elevatorColor);
            }

            foreach (var c in Layout.connections)
            {
                if (!c) continue;
                Vector2 a = Map(c.path[1].position), b = Map(c.path[c.path.Length - 2].position);
                Vector2 off = c.policeCanUse ? Vector2.zero : new Vector2(0f, -5f);
                Line(c.policeCanUse ? "EscadaSobe" : "EscadaFugitivo", a + off, b + off, c.policeCanUse ? 3f : 2f, c.policeCanUse ? upColor : downColor);
            }

            if (Layout.escapePoint)
            {
                var flag = Dot("Saida", new Vector2(6f, 12f), escapeColor);
                flag.anchoredPosition = Map(Layout.escapePoint.position);
            }

            fugitiveDot = Dot("Fugitivo", new Vector2(7f, 11f), fugitiveColor);
            policeDot = Dot("Policial", new Vector2(7f, 11f), policeColor);
        }

        void LateUpdate()
        {
            if (!Layout || !policeDot) return;
            if (police) policeDot.anchoredPosition = Map(police.transform.position);
            if (fugitive)
            {
                fugitiveDot.anchoredPosition = Map(fugitive.transform.position);
                fugitiveDot.gameObject.SetActive(fugitive.CurrentMode != FugitiveAI.Mode.Escaped);
            }
            if (elevatorDot && Layout.elevator)
            {
                elevatorDot.anchoredPosition = Map(new Vector3(Layout.elevator.cabin.position.x, Layout.elevator.CabinFloorY));
                var img = elevatorDot.GetComponent<Image>();
                img.color = Layout.elevator.OpenFloor >= 0 ? elevatorColor : elevatorColor * 0.7f;
            }
        }
    }
}
