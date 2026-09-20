using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalSandbox
{
    // Resolves the same crown, wisp, heart, halo and volley equations as
    // BalefulCascadeAbility.js / CascadeBladeMaterial.js / CascadeWispMaterial.js.
    // Nothing is captured at spawn: every heading, length, roll and tone is
    // resolved from the live settings on the frame it is read, so dragging a
    // slider re-cuts a crown that is already standing.
    public sealed class SourceCascade : IDisposable
    {
        const int MAX_BLADES = 56;
        const int MAX_SHOTS = 8;
        const float TAU = Mathf.PI * 2;
        // The angle a Fibonacci spiral advances by, radians.
        const float GOLDEN_ANGLE = 2.39996322972865332f; // pi * (3 - sqrt 5)

        readonly SpellVisual spell;
        readonly SandboxSettings settings;
        readonly Material crownMaterial, volleyMaterial, wispMaterial, heartMaterial, haloMaterial;
        readonly Material markMaterial, glowMaterial;
        readonly Mesh bladeMesh, wispMesh, heartMesh, haloMesh, groundMesh;
        readonly Light localLight;
        readonly SourceCascadeParticles particles;

        readonly Matrix4x4[] crownMatrices = new Matrix4x4[MAX_BLADES];
        readonly Vector4[] crownDir = new Vector4[MAX_BLADES];
        readonly Vector4[] crownShape = new Vector4[MAX_BLADES];
        readonly float[] present = new float[MAX_BLADES];
        readonly float[] regrow = new float[MAX_BLADES];
        readonly MaterialPropertyBlock crownBlock = new MaterialPropertyBlock();

        readonly Matrix4x4[] volleyMatrices = new Matrix4x4[MAX_SHOTS];
        readonly Vector4[] volleyFrom = new Vector4[MAX_SHOTS];
        readonly Vector4[] volleyTo = new Vector4[MAX_SHOTS];
        readonly Vector4[] volleyState = new Vector4[MAX_SHOTS];
        readonly MaterialPropertyBlock volleyBlock = new MaterialPropertyBlock();

        struct Shot { public Vector3 from, to; public float life, seed, curve; public bool live, struck; public SandboxDummy target; }
        readonly Shot[] shots = new Shot[MAX_SHOTS];

        float seed, markTime, pulse, charge, chargeTimer, nextThrow, flare;
        bool landed;
        Vector3 crownAt;
        readonly List<SandboxDummy> struck = new List<SandboxDummy>();

        float F(string key, float fallback = 0) => spell.F(key, fallback);
        float G(string key) => settings.F("global." + key, 1);

        public SourceCascade(SpellVisual spell, SandboxSettings settings)
        {
            this.spell = spell;
            this.settings = settings;

            crownMaterial = new Material(Shader.Find("Elemental/CascadeCrown")) { enableInstancing = true };
            volleyMaterial = new Material(Shader.Find("Elemental/CascadeVolley")) { enableInstancing = true };
            wispMaterial = new Material(Shader.Find("Elemental/CascadeWisp"));
            heartMaterial = new Material(Shader.Find("Elemental/CascadeHeart"));
            haloMaterial = new Material(Shader.Find("Elemental/CascadeHalo"));
            markMaterial = new Material(Shader.Find("Elemental/CascadeMark"));
            glowMaterial = new Material(Shader.Find("Elemental/CascadeGlow"));

            bladeMesh = BladeGrid(20, 8);
            wispMesh = WispRibbons(MAX_BLADES, 40, 3);
            heartMesh = BuiltIn(PrimitiveType.Sphere);
            haloMesh = BuiltIn(PrimitiveType.Quad);
            groundMesh = haloMesh;

            var lightObject = new GameObject("Cascade light");
            lightObject.transform.SetParent(spell.Root.transform, false);
            localLight = lightObject.AddComponent<Light>();
            localLight.type = LightType.Point;
            localLight.shadows = LightShadows.None;

            particles = new SourceCascadeParticles(spell, settings);
        }

        /* ------------------------------------------------------------------ */
        /* geometry                                                            */
        /* ------------------------------------------------------------------ */

        // Everything below is placed in world space by a shader, so the bounds
        // are meaningless — make them big enough that Unity never culls a blade
        // that is fifteen metres from the mesh's nominal origin.
        static readonly Bounds HUGE = new Bounds(Vector3.zero, Vector3.one * 2e4f);

        // A grid of quads in parameter space. position = (t, a, 0) with t
        // running 0 -> 1 root to point and a running 0 -> 1 once around. The
        // seam column is duplicated so a reaches a full 1.0 rather than wrapping.
        static Mesh BladeGrid(int nodes, int sides)
        {
            int rows = Mathf.Max(2, nodes), facets = Mathf.Max(3, sides), cols = facets + 1;
            var p = new Vector3[rows * cols];
            var ix = new int[(rows - 1) * (cols - 1) * 6];
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    p[i * cols + j] = new Vector3((float)i / (rows - 1), (float)j / facets, 0);
            int k = 0;
            for (int i = 0; i < rows - 1; i++)
                for (int j = 0; j < cols - 1; j++)
                {
                    int a = i * cols + j, b = a + cols;
                    ix[k++] = a; ix[k++] = b; ix[k++] = a + 1;
                    ix[k++] = b; ix[k++] = b + 1; ix[k++] = a + 1;
                }
            var m = new Mesh { name = "Cascade blade grid", vertices = p, triangles = ix, bounds = HUGE };
            return m;
        }

        // Every wisp in one mesh. The only per-wisp datum is its index, and the
        // shader derives the rest from dice, so there is nothing to instance.
        // position = (t along the climb, v across the ribbon, 0), uv.x = index.
        static Mesh WispRibbons(int count, int nodes, int across)
        {
            int rows = Mathf.Max(2, nodes), cols = Mathf.Max(2, across);
            var p = new Vector3[count * rows * cols];
            var uv = new Vector2[p.Length];
            var ix = new int[count * (rows - 1) * (cols - 1) * 6];
            int v = 0, k = 0;
            for (int w = 0; w < count; w++)
            {
                int baseIndex = v;
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
                        int a = baseIndex + i * cols + j, b = a + cols;
                        ix[k++] = a; ix[k++] = b; ix[k++] = a + 1;
                        ix[k++] = b; ix[k++] = b + 1; ix[k++] = a + 1;
                    }
            }
            var m = new Mesh { name = "Cascade wisps", indexFormat = IndexFormat.UInt32, vertices = p, uv = uv, triangles = ix, bounds = HUGE };
            return m;
        }

        static Mesh BuiltIn(PrimitiveType type)
        {
            var g = GameObject.CreatePrimitive(type);
            var mesh = g.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(g);
            return mesh;
        }

        /* ------------------------------------------------------------------ */
        /* envelopes                                                           */
        /* ------------------------------------------------------------------ */

        static float Sat(float x) => Mathf.Clamp01(x);
        static float OutCubic(float x) { float f = 1 - x; return 1 - f * f * f; }
        static float OutQuint(float x) { float f = 1 - x; return 1 - f * f * f * f * f; }
        static float InQuad(float x) => x * x;
        // Overshoot on the way out: a blade that eases to a stop reads as
        // inflating, and one that overshoots reads as *thrown* out of the middle.
        static float OutBack(float x) { const float s = 1.70158f; float f = x - 1; return f * f * ((s + 1) * f + s) + 1; }

        // The breath. Two sines a fifth apart, so their sum has no period inside
        // the seconds a mark stands. Weighted low by the cube: the mark spends
        // most of its time banked and briefly comes up. A thing that glows
        // evenly is friendly. This one should not be.
        static float BaleEnvelope(float t)
        {
            float a = Mathf.Sin(t), b = Mathf.Sin(t * 1.5f + 2.31f);
            float s = Sat((a + b * 0.62f) / 1.62f * 0.5f + 0.5f);
            return s * s * (3 - 2 * s) * s;
        }

        static float Hash11(float p)
        {
            float x = Mathf.Repeat(p * 0.1031f, 1f);
            x *= x + 33.33f;
            x *= x + x;
            return Mathf.Repeat(x, 1f);
        }

        float CrownLift() => OutCubic(Sat((markTime - F("crownDelay", .5f) * 0.6f) / Mathf.Max(.01f, F("crownTime", .8f) * 1.3f)));
        float CrownGrow() => OutCubic(Sat((markTime - F("crownDelay", .5f)) / Mathf.Max(.01f, F("crownTime", .8f))));
        // How far the mark has cut itself out to the boundary, metres.
        float MarkGrown() => spell.Radius * OutQuint(Sat(markTime / Mathf.Max(.01f, F("markTime", .52f))));
        // How far the glow has opened out, metres.
        float GlowGrown() => spell.Radius * F("glowSpillReach", 1.35f) * OutQuint(Sat(markTime / Mathf.Max(.01f, F("glowTime", .42f))));
        float WispGrow() => OutCubic(Sat((markTime - F("wispDelay", .22f)) / Mathf.Max(.01f, F("wispTime", .9f))));

        int BladeCount()
        {
            int total = Mathf.RoundToInt(F("crownSpears", 10)) + Mathf.RoundToInt(F("crownBlades", 16)) + Mathf.RoundToInt(F("crownShards", 14));
            return Mathf.Clamp(total, 1, MAX_BLADES);
        }

        // A step that walks every index exactly once, in an order nowhere near
        // sequential. Consecutive Fibonacci indices sit at nearly the same
        // latitude, so dealing the three populations as three blocks would put
        // every spear round one pole.
        static int Stride(int count)
        {
            int stride = Mathf.Max(1, Mathf.RoundToInt(count * 0.618f));
            for (int guard = 0; guard < count; guard++)
            {
                int a = stride, b = count;
                while (b != 0) { int t = a % b; a = b; b = t; }
                if (a == 1) return stride;
                stride = stride % count + 1;
            }
            return 1;
        }

        public void Spawn()
        {
            seed = 127 + spell.Slot * 7.919f;
            markTime = 0; charge = 0; chargeTimer = 0; nextThrow = 0; flare = 1;
            struck.Clear();
            for (int i = 0; i < MAX_BLADES; i++) { present[i] = 1; regrow[i] = 0; }
            for (int i = 0; i < MAX_SHOTS; i++) shots[i].live = false;
            landed = false;
            particles.Reset();
        }

        /* ------------------------------------------------------------------ */
        /* the crown                                                           */
        /* ------------------------------------------------------------------ */

        void DealCrown(float dt, float grow, float collapse)
        {
            int count = BladeCount();
            int spears = Mathf.RoundToInt(F("crownSpears", 10));
            int blades = Mathf.RoundToInt(F("crownBlades", 16));
            float scale = Mathf.Max(.01f, F("crownScale", 1.65f));
            float jitter = F("crownJitter", .16f) * G("randomness");
            float flatten = Mathf.Max(.05f, F("crownFlatten", .72f));
            float stagger = Sat(F("crownStagger", .4f));
            int stride = Stride(count);

            float time = spell.Age;
            float spin = time * F("crownSpin", .03f) * TAU + seed;
            float tilt = F("crownTilt", .22f) * Mathf.Sin(time * F("crownTiltSpeed", .05f) * TAU + seed);
            float cs = Mathf.Cos(spin), ss = Mathf.Sin(spin), ct = Mathf.Cos(tilt), st = Mathf.Sin(tilt);
            float regrowRate = 1 / Mathf.Max(.02f, F("crownRegrow", .9f));
            float violet = F("crownViolet", .42f);
            float lengthJitter = F("crownLengthJitter", .28f) * G("randomness");

            for (int i = 0; i < count; i++)
            {
                /* ---- which way it points ---- */
                int slot = i * stride % count;
                float k = (slot + 0.5f) / count;
                float y = 1 - 2 * k;
                float ring = Mathf.Sqrt(Mathf.Max(0, 1 - y * y));
                float phi = slot * GOLDEN_ANGLE + seed;
                float x = ring * Mathf.Cos(phi);
                float z = ring * Mathf.Sin(phi);

                // Squashed toward the equator: a crown dealt evenly over a
                // sphere reads as a ball of spines from every angle, and the
                // panel is a *star*.
                y *= flatten;
                x += (Hash11(i * 1.7f + seed) - .5f) * jitter;
                y += (Hash11(i * 2.3f + seed) - .5f) * jitter;
                z += (Hash11(i * 3.1f + seed) - .5f) * jitter;

                // Tipped, then turned. The tilt is a slow nod rather than a
                // fixed lean, so the crown never settles into one silhouette.
                float y1 = y * ct - z * st, z1 = y * st + z * ct;
                float x2 = x * cs + z1 * ss, z2 = -x * ss + z1 * cs;
                float inv = 1 / Mathf.Max(1e-4f, Mathf.Sqrt(x2 * x2 + y1 * y1 + z2 * z2));
                crownDir[i] = new Vector4(x2 * inv, y1 * inv, z2 * inv, 0);

                /* ---- how long it is ---- */
                float dice = Hash11(i * 4.7f + seed * 1.3f);
                float baseLength = i < spears ? F("spearLength", 1.85f)
                                 : i < spears + blades ? F("bladeLength", 1f)
                                 : F("shardLength", .55f);
                float length = baseLength * scale * (1 + (dice - .5f) * 2 * lengthJitter);

                /* ---- how much of it is standing ---- */
                if (regrow[i] > 0) regrow[i] = Mathf.Max(0, regrow[i] - dt);
                else if (present[i] < 1) present[i] = Sat(present[i] + regrowRate * dt);

                float lag = stagger * Hash11(i * 5.9f + seed * 2.1f);
                float opened = Sat((grow - lag) / Mathf.Max(.05f, 1 - lag));
                float punch = Mathf.Max(0, OutBack(opened));
                // Going the other way it retracts, on the same stagger. The
                // fragment stage can eat a blade back from its point, but on
                // its own that leaves a flat white cut face standing in the
                // air. The length is what takes the blade away; the cull only
                // finishes it.
                float dying = Sat((collapse - lag * .4f) / Mathf.Max(.15f, 1 - lag * .4f));
                float live = punch * present[i] * (1 - InQuad(dying));

                // Two stones dealt across the crown: the violet ones are the
                // barbs the reference sheet puts under the teal, and they are
                // dealt rather than grouped so no wedge is all one colour.
                float tone = Hash11(i * 7.1f + seed * 4.3f) < violet
                    ? .65f + .35f * Hash11(i * 8.9f + seed)
                    : .3f * Hash11(i * 9.7f + seed);

                crownShape[i] = new Vector4(length, Hash11(i * 6.3f + seed * 3.7f) * TAU, live, tone);
                crownMatrices[i] = Matrix4x4.identity;
            }

            crownBlock.SetVectorArray("_CrownDir", crownDir);
            crownBlock.SetVectorArray("_CrownShape", crownShape);
            Graphics.DrawMeshInstanced(bladeMesh, 0, crownMaterial, crownMatrices, count, crownBlock, ShadowCastingMode.On, true);
        }

        // The tip of blade i, in world space — the point a throw leaves from.
        // Read straight back out of the buffers DealCrown just wrote, so it is
        // the position that will actually be drawn this frame.
        Vector3 BladeTip(int i)
        {
            float seat = Mathf.Max(.01f, F("crownInner", .16f) * Mathf.Max(.01f, F("crownScale", 1.65f)));
            float reach = seat + crownShape[i].x * crownShape[i].z;
            return crownAt + new Vector3(crownDir[i].x, crownDir[i].y, crownDir[i].z) * reach;
        }

        /* ------------------------------------------------------------------ */
        /* the volley                                                          */
        /* ------------------------------------------------------------------ */

        bool Armed => settings.B("cascade.throwEnabled", true)
                   && markTime >= F("crownDelay", .5f) + F("crownTime", .8f) + F("fireDelay", .3f);

        // The blade best placed to be thrown at the target — the one already
        // pointing that way, and grown enough to leave.
        int PickBlade(Vector3 toward)
        {
            int count = BladeCount(), best = -1;
            float bestDot = -2;
            Vector3 want = toward.normalized;
            for (int i = 0; i < count; i++)
            {
                if (crownShape[i].z < .6f) continue;
                Vector3 d = new Vector3(crownDir[i].x, crownDir[i].y, crownDir[i].z);
                float dot = Vector3.Dot(d, want);
                if (dot > bestDot) { bestDot = dot; best = i; }
            }
            return best;
        }

        void ThrowAt(SandboxDummy dummy)
        {
            int flight = Mathf.Clamp(Mathf.RoundToInt(F("throwBlades", 3)), 1, MAX_SHOTS);
            float aim = F("throwAim", .62f);
            Vector3 body = dummy.Position + Vector3.up * aim;
            for (int n = 0; n < flight; n++)
            {
                int free = -1;
                for (int s = 0; s < MAX_SHOTS; s++) if (!shots[s].live) { free = s; break; }
                if (free < 0) return;

                int blade = PickBlade(body - crownAt);
                if (blade < 0) return;
                // The blade leaves the crown, so the gap it leaves has to regrow.
                present[blade] = 0;
                regrow[blade] = F("crownRegrowDelay", .35f);

                shots[free] = new Shot
                {
                    from = BladeTip(blade),
                    to = body,
                    life = -n * F("throwStagger", .07f),
                    seed = Hash11(free * 3.3f + markTime * 7.7f),
                    curve = Hash11(free * 5.1f + markTime * 3.1f) * 2 - 1,
                    live = true,
                    struck = false,
                    target = dummy
                };
                particles.Launch(shots[free].from);
            }
            spell.App.Shake(F("throwShake", .14f));
        }

        void StepVolley(float dt)
        {
            int count = 0;
            float life = Mathf.Max(.05f, F("throwLife", .55f));
            float strike = F("throwStrike", .34f);
            for (int s = 0; s < MAX_SHOTS; s++)
            {
                if (!shots[s].live) continue;
                shots[s].life += dt / life;
                if (shots[s].life >= 1) { shots[s].live = false; continue; }
                float q = shots[s].life;
                // The frame it reaches the strike is the frame the body parts.
                if (!shots[s].struck && q >= strike)
                {
                    shots[s].struck = true;
                    var d = shots[s].target;
                    if (d != null && d.Alive)
                    {
                        Vector3 dir = (shots[s].to - shots[s].from).normalized;
                        d.Cut(dir * settings.F("cascade.cutHit.impulse", 3.6f) + Vector3.up * settings.F("cascade.cutHit.lift", 2.6f));
                        spell.App.Shake(F("cutShake", .18f));
                        particles.Cut(shots[s].to);
                        flare = Mathf.Max(flare, 1);
                    }
                }
                if (q < 0) continue;
                volleyFrom[count] = shots[s].from;
                volleyTo[count] = shots[s].to;
                volleyState[count] = new Vector4(q, shots[s].seed, shots[s].curve, 1);
                volleyMatrices[count] = Matrix4x4.identity;
                count++;
            }
            if (count == 0) return;
            volleyBlock.SetVectorArray("_VolleyFrom", volleyFrom);
            volleyBlock.SetVectorArray("_VolleyTo", volleyTo);
            volleyBlock.SetVectorArray("_VolleyState", volleyState);
            Graphics.DrawMeshInstanced(bladeMesh, 0, volleyMaterial, volleyMatrices, count, volleyBlock, ShadowCastingMode.Off, false);
        }

        /* ------------------------------------------------------------------ */
        /* the frame                                                           */
        /* ------------------------------------------------------------------ */

        // Feed every cascade.* setting into any material that declares a
        // property of the same name. Keeps the 299 source values authoritative
        // without a hand-written binding table.
        void BindSettings(Material m)
        {
            foreach (var e in settings.Entries)
            {
                const string prefix = "cascade.";
                if (!e.key.StartsWith(prefix)) continue;
                string key = "_" + e.key.Substring(prefix.Length);
                if (!m.HasProperty(key)) continue;
                if (e.kind == "number") m.SetFloat(key, e.number);
                else if (e.kind == "string" && ColorUtility.TryParseHtmlString(e.text, out var color)) m.SetColor(key, color);
            }
            m.SetFloat("_GlobalGlow", G("glow"));
        }

        public void Tick(float age, float travel, float dt, float retract)
        {
            markTime = Mathf.Max(0, age - travel);
            float fade = 1 - retract;
            float collapse = Sat((retract - .45f) / .55f);

            pulse = BaleEnvelope(markTime * F("pulseRate", 1.05f)) * F("pulseDepth", .6f);
            flare = Mathf.Max(0, flare - flare * 7 * dt - 0.4f * dt);

            /* ---- where the crown hangs ---- */
            float lift = CrownLift();
            crownAt = spell.Target;
            crownAt.y = F("crownHeight", 3.1f)
                      - F("crownRise", .9f) * (1 - lift)
                      + Mathf.Sin(age * F("crownBobSpeed", .55f) * TAU) * F("crownBob", .05f) * lift;

            /* ---- the throw clock ---- */
            if (Armed && retract < .5f)
            {
                var target = spell.App.Nearest(spell.Target, F("throwRange", 12));
                if (target != null)
                {
                    chargeTimer += dt;
                    charge = Sat(chargeTimer / Mathf.Max(.02f, F("throwWarmup", .3f)));
                    if (markTime >= nextThrow && charge >= 1)
                    {
                        ThrowAt(target);
                        nextThrow = markTime + F("throwInterval", .75f);
                        chargeTimer = 0;
                        charge = 1;
                    }
                }
                else { chargeTimer = 0; charge = Mathf.Max(0, charge - dt * 2.4f); }
            }
            else charge = Mathf.Max(0, charge - dt * 3.2f);

            /* ---- shared uniforms ---- */
            foreach (var m in new[] { crownMaterial, volleyMaterial, wispMaterial, heartMaterial, haloMaterial, markMaterial, glowMaterial })
            {
                BindSettings(m);
                if (m.HasProperty("_Charge")) m.SetFloat("_Charge", charge);
                if (m.HasProperty("_Fade")) m.SetFloat("_Fade", fade * G("opacity"));
                if (m.HasProperty("_Pulse")) m.SetFloat("_Pulse", pulse);
                if (m.HasProperty("_Seed")) m.SetFloat("_Seed", seed);
                if (m.HasProperty("_Flare")) m.SetFloat("_Flare", flare);
            }
            // The blades hang off the heart; the wisps climb out of the mark on
            // the floor. One centre each — sharing the crown's puts the wisps in
            // the air with nothing under them.
            Vector3 markAt = new Vector3(spell.Target.x, 0, spell.Target.z);
            crownMaterial.SetVector("_Centre", crownAt);
            volleyMaterial.SetVector("_Centre", crownAt);
            heartMaterial.SetVector("_Centre", crownAt);
            haloMaterial.SetVector("_Centre", crownAt);
            wispMaterial.SetVector("_Centre", markAt);
            crownMaterial.SetFloat("_Inner", Mathf.Max(.01f, F("crownInner", .16f) * Mathf.Max(.01f, F("crownScale", 1.65f))));
            crownMaterial.SetFloat("_Collapse", collapse);

            /* ---- the particle layer ---- */
            if (!landed && markTime > 0)
            {
                landed = true;
                particles.Land(spell.Target, spell.Radius);
                spell.App.Shake(F("landShake", .3f));
            }
            bool crownFormed = markTime >= F("crownDelay", .5f) + F("crownTime", .8f);
            particles.Tick(markTime, dt, retract, spell.Target, crownAt, spell.Radius, crownFormed);

            /* ---- layer 0: the ground glow ---- */
            var groundRot = Quaternion.Euler(90, 0, 0);
            float glowQuad = spell.Radius * F("glowSpillReach", 1.35f) * 2 + 1.6f;
            glowMaterial.SetFloat("_QuadSize", glowQuad);
            glowMaterial.SetFloat("_Radius", spell.Radius);
            glowMaterial.SetFloat("_Grown", GlowGrown());
            Graphics.DrawMesh(groundMesh, Matrix4x4.TRS(markAt + Vector3.up * F("glowHeight", .02f), groundRot, Vector3.one * glowQuad),
                              glowMaterial, 0, null, 0, null, ShadowCastingMode.Off, false);

            /* ---- layer 1: the mark ---- */
            float markQuad = spell.Radius * F("markStarOuter", .94f) * 2 + 1.4f;
            markMaterial.SetFloat("_QuadSize", markQuad);
            markMaterial.SetFloat("_Radius", spell.Radius);
            markMaterial.SetFloat("_Grown", MarkGrown());
            // The leading edge is only live while the mark is still cutting itself on.
            markMaterial.SetFloat("_Front", 1 - Sat(markTime / Mathf.Max(.01f, F("markTime", .52f))));
            Graphics.DrawMesh(groundMesh, Matrix4x4.TRS(markAt + Vector3.up * F("markHeight", .03f), groundRot, Vector3.one * markQuad),
                              markMaterial, 0, null, 0, null, ShadowCastingMode.Off, false);

            /* ---- layer 3a: the crown ---- */
            DealCrown(dt, CrownGrow(), collapse);

            /* ---- layer 3b: the blades in the air ---- */
            StepVolley(dt);

            /* ---- layer 2: the wisps ---- */
            float wispGrow = WispGrow() * fade;
            if (wispGrow > 0.002f)
            {
                wispMaterial.SetFloat("_Radius", spell.Radius);
                wispMaterial.SetFloat("_Grow", wispGrow);
                Graphics.DrawMesh(wispMesh, Matrix4x4.identity, wispMaterial, 0, null, 0, null, ShadowCastingMode.Off, false);
            }

            /* ---- layer 3c: the heart ---- */
            float scale = Mathf.Max(.01f, F("crownScale", 1.65f));
            float heartRadius = Mathf.Max(.02f, F("heartSize", .24f) * scale) * (1 + charge * F("heartSwell", .35f));
            Graphics.DrawMesh(heartMesh, Matrix4x4.TRS(crownAt, Quaternion.identity, Vector3.one * heartRadius * 2),
                              heartMaterial, 0, null, 0, null, ShadowCastingMode.Off, false);

            /* ---- layer 3d: the halo ---- */
            haloMaterial.SetFloat("_Size", Mathf.Max(.05f, F("haloSize", 2.3f) * scale));
            Graphics.DrawMesh(haloMesh, Matrix4x4.TRS(crownAt, Quaternion.identity, Vector3.one),
                              haloMaterial, 0, null, 0, null, ShadowCastingMode.Off, false);

            /* ---- the light it throws ---- */
            localLight.transform.position = crownAt + Vector3.up * F("lightHeight", .5f);
            localLight.color = spell.C("lightColor", "#3ff0e0");
            localLight.range = F("lightRadius", 12);
            localLight.intensity = F("lightIntensity", 12) * G("lightIntensity") * fade
                                 * (1 - F("lightPulse", .45f) * .5f + F("lightPulse", .45f) * pulse + charge * .3f);
        }

        public void Dispose()
        {
            particles.Dispose();
            foreach (var m in new[] { crownMaterial, volleyMaterial, wispMaterial, heartMaterial, haloMaterial, markMaterial, glowMaterial })
                UnityEngine.Object.Destroy(m);
            UnityEngine.Object.Destroy(bladeMesh);
            UnityEngine.Object.Destroy(wispMesh);
        }
    }
}
