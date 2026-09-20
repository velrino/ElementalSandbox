using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalSandbox
{
    // Resolves the same tendril path, foliage and wither as
    // ArborBloomAbility.js / GrowthVineMaterial.js. Every stem, leaf and knot
    // is derived in the shader from the vine index and the cast seed, so the
    // only per-instance datum is that index and it is baked into the mesh.
    public sealed class SourceGrowth : IDisposable
    {
        const int MAX_VINES = 24;
        const int MAX_LEAVES = 220;

        readonly SpellVisual spell;
        readonly SandboxSettings settings;
        readonly Material vineMaterial, leafMaterial, sigilMaterial;
        readonly Mesh groundMesh;
        readonly Mesh vineMesh, leafMesh;
        readonly Light localLight;

        float seed, markTime;

        float F(string key, float fallback = 0) => spell.F(key, fallback);
        float G(string key) => settings.F("global." + key, 1);
        static float Sat(float x) => Mathf.Clamp01(x);
        static float OutCubic(float x) { float f = 1 - x; return 1 - f * f * f; }
        static float OutQuint(float x) { float f = 1 - x; return 1 - f * f * f * f * f; }

        public SourceGrowth(SpellVisual spell, SandboxSettings settings)
        {
            this.spell = spell;
            this.settings = settings;

            vineMaterial = new Material(Shader.Find("Elemental/GrowthVine"));
            leafMaterial = new Material(Shader.Find("Elemental/GrowthLeaf"));
            sigilMaterial = new Material(Shader.Find("Elemental/GrowthSigil"));

            vineMesh = Tubes(MAX_VINES, 56, 9);
            leafMesh = Blades(MAX_LEAVES, 7, 5);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            groundMesh = quad.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(quad);

            var lightObject = new GameObject("Growth light");
            lightObject.transform.SetParent(spell.Root.transform, false);
            localLight = lightObject.AddComponent<Light>();
            localLight.type = LightType.Point;
            localLight.shadows = LightShadows.None;
        }

        // Everything is placed in world space by the vertex stage.
        static readonly Bounds HUGE = new Bounds(Vector3.zero, Vector3.one * 2e4f);

        // One tube per vine, all in one mesh. position = (t along the stem,
        // a once around it, 0); uv.x carries the vine index. The seam column is
        // duplicated so a reaches a full 1.0 rather than wrapping.
        static Mesh Tubes(int count, int nodes, int sides)
        {
            int rows = Mathf.Max(2, nodes), facets = Mathf.Max(3, sides), cols = facets + 1;
            var p = new Vector3[count * rows * cols];
            var uv = new Vector2[p.Length];
            var ix = new int[count * (rows - 1) * (cols - 1) * 6];
            int v = 0, k = 0;
            for (int w = 0; w < count; w++)
            {
                int start = v;
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                    {
                        p[v] = new Vector3((float)i / (rows - 1), (float)j / facets, 0);
                        uv[v] = new Vector2(w, 0);
                        v++;
                    }
                for (int i = 0; i < rows - 1; i++)
                    for (int j = 0; j < cols - 1; j++)
                    {
                        int a = start + i * cols + j, b = a + cols;
                        ix[k++] = a; ix[k++] = b; ix[k++] = a + 1;
                        ix[k++] = b; ix[k++] = b + 1; ix[k++] = a + 1;
                    }
            }
            return new Mesh { name = "Growth vines", indexFormat = IndexFormat.UInt32, vertices = p, uv = uv, triangles = ix, bounds = HUGE };
        }

        // One blade per leaf. position = (u along the blade, v across it, 0),
        // uv.x carries the leaf index.
        static Mesh Blades(int count, int along, int across)
        {
            int rows = Mathf.Max(2, along), cols = Mathf.Max(2, across);
            var p = new Vector3[count * rows * cols];
            var uv = new Vector2[p.Length];
            var ix = new int[count * (rows - 1) * (cols - 1) * 6];
            int v = 0, k = 0;
            for (int w = 0; w < count; w++)
            {
                int start = v;
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                    {
                        p[v] = new Vector3((float)i / (rows - 1), (float)j / (cols - 1) * 2 - 1, 0);
                        uv[v] = new Vector2(w, 0);
                        v++;
                    }
                for (int i = 0; i < rows - 1; i++)
                    for (int j = 0; j < cols - 1; j++)
                    {
                        int a = start + i * cols + j, b = a + cols;
                        ix[k++] = a; ix[k++] = b; ix[k++] = a + 1;
                        ix[k++] = b; ix[k++] = b + 1; ix[k++] = a + 1;
                    }
            }
            return new Mesh { name = "Growth leaves", indexFormat = IndexFormat.UInt32, vertices = p, uv = uv, triangles = ix, bounds = HUGE };
        }

        public void Spawn()
        {
            seed = 311 + spell.Slot * 5.081f;
            markTime = 0;
        }

        // Feed every growth.* setting into any material declaring a property of
        // the same name, so the 359 source values stay authoritative.
        void BindSettings(Material m)
        {
            foreach (var e in settings.Entries)
            {
                const string prefix = "growth.";
                if (!e.key.StartsWith(prefix)) continue;
                string key = "_" + e.key.Substring(prefix.Length);
                if (!m.HasProperty(key)) continue;
                if (e.kind == "number") m.SetFloat(key, e.number);
                else if (e.kind == "string" && ColorUtility.TryParseHtmlString(e.text, out var color)) m.SetColor(key, color);
            }
            m.SetFloat("_GlobalGlow", G("glow"));
            m.SetFloat("_Seed", seed);
            m.SetFloat("_Radius", spell.Radius);
            m.SetVector("_Centre", new Vector3(spell.Target.x, 0, spell.Target.z));
        }

        public void Tick(float age, float travel, float dt, float retract)
        {
            markTime = Mathf.Max(0, age - travel);

            // The nest climbs on its own clock and is eaten back on the fade.
            float grow = OutCubic(Sat((markTime - F("vineDelay", .18f)) / Mathf.Max(.01f, F("vineTime", 1.15f))));
            float wither = Sat(retract);

            foreach (var m in new[] { vineMaterial, leafMaterial, sigilMaterial })
            {
                BindSettings(m);
                m.SetFloat("_Grow", grow);
                m.SetFloat("_Wither", wither);
            }

            /* ---- layer 1: the sigil ---- */
            // Cut outward as the growth races to the boundary; the leading edge
            // is live only while it is still cutting.
            float sigilTime = Mathf.Max(.01f, F("sigilTime", .42f));
            float quad = spell.Radius * F("sigilRailOuter", 1f) * 2 + 1.4f;
            sigilMaterial.SetFloat("_QuadSize", quad);
            sigilMaterial.SetFloat("_Grown", spell.Radius * OutQuint(Sat(markTime / sigilTime)));
            sigilMaterial.SetFloat("_Front", 1 - Sat(markTime / sigilTime));
            sigilMaterial.SetFloat("_Fade", 1 - retract);
            Graphics.DrawMesh(groundMesh,
                Matrix4x4.TRS(new Vector3(spell.Target.x, F("sigilHeight", .028f), spell.Target.z),
                              Quaternion.Euler(90, 0, 0), Vector3.one * quad),
                sigilMaterial, 0, null, 0, null, ShadowCastingMode.Off, false);

            if (grow > 0.002f)
            {
                Graphics.DrawMesh(vineMesh, Matrix4x4.identity, vineMaterial, 0, null, 0, null, ShadowCastingMode.On, true);
                Graphics.DrawMesh(leafMesh, Matrix4x4.identity, leafMaterial, 0, null, 0, null, ShadowCastingMode.Off, true);
            }

            localLight.transform.position = spell.Target + Vector3.up * F("lightHeight", 1.6f);
            localLight.color = spell.C("lightColor", "#4fe07a");
            localLight.range = F("lightRadius", 12);
            localLight.intensity = F("lightIntensity", 12) * G("lightIntensity") * (1 - retract);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(vineMaterial);
            UnityEngine.Object.Destroy(leafMaterial);
            UnityEngine.Object.Destroy(sigilMaterial);
            UnityEngine.Object.Destroy(vineMesh);
            UnityEngine.Object.Destroy(leafMesh);
        }
    }
}
