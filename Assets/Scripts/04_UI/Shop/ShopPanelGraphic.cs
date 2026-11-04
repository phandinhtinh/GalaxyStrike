using UnityEngine;
using UnityEngine.UI;

namespace GalaxyStrike.UI.Shop
{
    /// <summary>Resolution-independent, cut-corner neon frame for the store UI.</summary>
    [AddComponentMenu("Galaxy Strike/UI/Shop Panel Graphic")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ShopPanelGraphic : MaskableGraphic
    {
        public Color topColor = new Color32(16, 39, 83, 250);
        public Color bottomColor = new Color32(3, 15, 41, 250);
        public Color borderColor = new Color32(0, 190, 255, 255);
        public float cornerCut = 20f;
        public float borderWidth = 2f;
        public float glowWidth = 7f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float cut = Mathf.Min(cornerCut, Mathf.Min(r.width, r.height) * 0.3f);
            Vector2[] outer = Polygon(r, cut);
            float inset = Mathf.Max(0.1f, borderWidth);
            Vector2[] inner = Polygon(Inset(r, inset), Mathf.Max(0, cut - inset * 0.6f));
            if (glowWidth > 0)
            {
                Vector2[] glow = Polygon(Inset(r, -glowWidth), cut + glowWidth * 0.6f);
                Ring(vh, glow, outer, new Color(borderColor.r, borderColor.g, borderColor.b, 0),
                    new Color(borderColor.r, borderColor.g, borderColor.b, 0.2f));
            }
            Ring(vh, outer, inner, borderColor, borderColor * new Color(0.6f, 0.75f, 0.9f, 1));
            int start = vh.currentVertCount;
            Vector2 center = r.center;
            Add(vh, center, Color.Lerp(bottomColor, topColor, 0.5f));
            foreach (Vector2 p in inner)
                Add(vh, p, Color.Lerp(bottomColor, topColor, Mathf.InverseLerp(r.yMin, r.yMax, p.y)));
            for (int i = 0; i < 8; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 8);
        }

        private static Rect Inset(Rect r, float d) => new Rect(r.x + d, r.y + d, Mathf.Max(1, r.width - 2*d), Mathf.Max(1, r.height - 2*d));
        private static Vector2[] Polygon(Rect r, float c) => new[] {
            new Vector2(r.xMin+c,r.yMax), new Vector2(r.xMax-c,r.yMax),
            new Vector2(r.xMax,r.yMax-c), new Vector2(r.xMax,r.yMin+c),
            new Vector2(r.xMax-c,r.yMin), new Vector2(r.xMin+c,r.yMin),
            new Vector2(r.xMin,r.yMin+c), new Vector2(r.xMin,r.yMax-c) };
        private void Add(VertexHelper vh, Vector2 p, Color tint) => vh.AddVert(p, tint * color, Vector2.zero);
        private void Ring(VertexHelper vh, Vector2[] a, Vector2[] b, Color ca, Color cb)
        {
            int start = vh.currentVertCount;
            for (int i=0;i<8;i++) { Add(vh,a[i],ca); Add(vh,b[i],cb); }
            for (int i=0;i<8;i++) { int n=(i+1)%8; vh.AddTriangle(start+i*2,start+n*2,start+i*2+1); vh.AddTriangle(start+n*2,start+n*2+1,start+i*2+1); }
        }
    }
}
