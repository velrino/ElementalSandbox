using UnityEngine;

namespace ElementalSandbox
{
    // The four populations BalefulCascadeAbility.js emits, with the source
    // palettes, rates, sizes, lifetimes and gravity. Motes drift and rise off
    // the mark, sparks are fast and fall, chips are solid and fall harder, and
    // the mist is the aerosol seated in the ring.
    public sealed class SourceCascadeParticles : System.IDisposable
    {
        readonly SpellVisual spell;
        readonly SandboxSettings settings;
        readonly ParticleSystem motes, sparks, chips, mist;
        readonly Material moteMaterial, sparkMaterial, chipMaterial, mistMaterial;
        float moteCarry, mistCarry;
        bool crowned;

        float F(string key, float fallback = 0) => spell.F(key, fallback);
        float G(string key) => settings.F("global." + key, 1);
        int Count(float n) => Mathf.Clamp(Mathf.RoundToInt(n * G("particleCount")), 0, 1200);

        public SourceCascadeParticles(SpellVisual spell, SandboxSettings settings)
        {
            this.spell = spell;
            this.settings = settings;

            mistMaterial = new Material(Shader.Find("Elemental/SourceSmoke"));
            moteMaterial = Blob(1.0f);
            sparkMaterial = Blob(2.2f);
            chipMaterial = Blob(0.8f);

            mist = Make("Cascade mist", mistMaterial, "colorMist", 1400, true);
            motes = Make("Cascade motes", moteMaterial, "colorMote", 1600, false);
            sparks = Make("Cascade sparks", sparkMaterial, "colorSpark", 1600, false);
            chips = Make("Cascade chips", chipMaterial, "colorChip", 600, false);
        }

        // The shared point sprite. Mode 5 is the radial gaussian.
        static Material Blob(float glow)
        {
            var m = new Material(Shader.Find("Elemental/Energy"));
            m.SetFloat("_Mode", 5);
            m.SetFloat("_Opacity", 1);
            m.SetFloat("_Glow", glow);
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_HotColor", Color.white);
            return m;
        }

        ParticleSystem Make(string name, Material material, string palette, int capacity, bool aerosol)
        {
            var go = new GameObject(name);
            go.transform.SetParent(spell.Root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false; main.loop = false; main.duration = 30;
            main.maxParticles = capacity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;

            // The source palettes are four stops; alpha opens fast and closes
            // over the tail, and the aerosol opens from nothing.
            var color = ps.colorOverLifetime; color.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(spell.C(palette + "A"), 0),
                    new GradientColorKey(spell.C(palette + "B"), .25f),
                    new GradientColorKey(spell.C(palette + "C"), .65f),
                    new GradientColorKey(spell.C(palette + "D"), 1)
                },
                new[] {
                    new GradientAlphaKey(aerosol ? 0 : 1, 0),
                    new GradientAlphaKey(1, .12f),
                    new GradientAlphaKey(.7f, .55f),
                    new GradientAlphaKey(0, 1)
                });
            color.color = grad;

            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = aerosol
                ? new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .35f, 1, 1.6f))
                : new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .3f), new Keyframe(.12f, 1), new Keyframe(1, 0)));

            if (aerosol)
            {
                var noise = ps.noise; noise.enabled = true;
                noise.strength = F("moteTurbulence", .65f) * G("turbulence");
                noise.frequency = .5f; noise.scrollSpeed = .25f;
                noise.quality = ParticleSystemNoiseQuality.Medium;
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        public void Reset()
        {
            motes.Clear(); sparks.Clear(); chips.Clear(); mist.Clear();
            moteCarry = mistCarry = 0; crowned = false;
        }

        /* ------------------------------------------------------------------ */
        /* emitters                                                            */
        /* ------------------------------------------------------------------ */

        public void Motes(Vector3 p, int count, float spread, float speed)
        {
            for (int i = 0, n = Count(count); i < n; i++)
            {
                Vector2 v = Random.insideUnitCircle;
                motes.Emit(new ParticleSystem.EmitParams
                {
                    position = p + new Vector3(v.x, Random.value * .3f, v.y) * spread,
                    velocity = new Vector3(v.x, F("moteRise", .5f) + Random.value * .4f, v.y) * speed * G("particleSpeed"),
                    startLifetime = F("moteLifetime", 2.6f) * Random.Range(.7f, 1.3f) * G("particleLifetime"),
                    startSize = F("moteSize", .055f) * Random.Range(.6f, 1.5f) * G("particleSize")
                }, 1);
            }
        }

        public void Sparks(Vector3 p, int count, float speed)
        {
            for (int i = 0, n = Count(count); i < n; i++)
            {
                Vector3 v = Random.onUnitSphere;
                v.y = Mathf.Abs(v.y) * .8f + .2f;
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = p,
                    velocity = v * speed * Random.Range(.4f, 1.2f) * G("particleSpeed"),
                    startLifetime = F("sparkLifetime", .9f) * Random.Range(.6f, 1.3f) * G("particleLifetime"),
                    startSize = F("sparkSize", .07f) * Random.Range(.5f, 1.4f) * G("particleSize")
                }, 1);
            }
        }

        public void Chips(Vector3 p, int count)
        {
            for (int i = 0, n = Count(count); i < n; i++)
            {
                Vector3 v = Random.onUnitSphere;
                v.y = Mathf.Abs(v.y) * .9f + .3f;
                chips.Emit(new ParticleSystem.EmitParams
                {
                    position = p,
                    velocity = v * F("chipSpeed", 3.2f) * Random.Range(.4f, 1.2f) * G("particleSpeed"),
                    startLifetime = F("chipLifetime", 2.4f) * Random.Range(.7f, 1.2f) * G("particleLifetime"),
                    startSize = F("chipSize", .07f) * Random.Range(.6f, 1.4f) * G("particleSize"),
                    rotation = Random.value * 360,
                    angularVelocity = F("chipSpin", 5) * Random.Range(-60f, 60f)
                }, 1);
            }
        }

        void Mist(Vector3 p, int count, float radius)
        {
            for (int i = 0, n = Count(count); i < n; i++)
            {
                Vector2 v = Random.insideUnitCircle;
                mist.Emit(new ParticleSystem.EmitParams
                {
                    position = p + new Vector3(v.x, .06f, v.y) * radius,
                    velocity = new Vector3(v.x, F("mistRise", .16f), v.y) * F("mistSpeed", .5f) * G("particleSpeed"),
                    startLifetime = F("mistLifetime", 3.6f) * Random.Range(.7f, 1.3f) * G("particleLifetime"),
                    startSize = F("mistSize", .95f) * Random.Range(.7f, 1.4f) * G("particleSize"),
                    rotation = Random.value * 360
                }, 1);
            }
        }

        /* ------------------------------------------------------------------ */
        /* events                                                              */
        /* ------------------------------------------------------------------ */

        // The mark lands: the source throws motes, sparks and mist at the floor.
        public void Land(Vector3 target, float radius)
        {
            Motes(target + Vector3.up * .2f, (int)F("landMotes", 120), radius * .6f, 1.2f);
            Sparks(target + Vector3.up * .2f, (int)F("landSparks", 70), F("sparkSpeed", 6));
            Mist(target, (int)F("landMist", 16), radius * F("mistSeat", .85f));
        }

        // A blade leaves the crown.
        public void Launch(Vector3 tip)
        {
            Sparks(tip, (int)F("launchSparks", 26), F("sparkSpeed", 6) * .7f);
            Chips(tip, (int)F("launchChips", 6));
        }

        // A blade goes through a body.
        public void Cut(Vector3 at)
        {
            Sparks(at, (int)F("cutSparks", 110), F("cutSpeed", 5.2f));
            Motes(at, (int)F("cutMotes", 70), .35f, 1.4f);
            Chips(at, (int)F("cutChips", 26));
        }

        public void Tick(float markTime, float dt, float retract, Vector3 target, Vector3 crownAt, float radius, bool crownFormed)
        {
            mistMaterial.SetFloat("_Opacity", F("mistOpacity", .13f) * G("opacity") * (1 - retract));

            var main = motes.main; main.gravityModifier = 0;
            main = sparks.main; main.gravityModifier = -F("sparkGravity", -1.6f) / 9.81f;
            main = chips.main; main.gravityModifier = -F("chipGravity", -5.5f) / 9.81f;
            main = mist.main; main.gravityModifier = 0;

            // The crown finishes assembling: the one big throw of this ability.
            if (crownFormed && !crowned)
            {
                crowned = true;
                Motes(crownAt, (int)F("crownMotes", 140), .5f, 1.1f);
                Sparks(crownAt, (int)F("crownSparks", 120), F("sparkSpeed", 6));
                Chips(crownAt, (int)F("crownChips", 22));
            }

            // Standing: motes creeping up off the mark and mist seated in it.
            if (dt > 0 && retract <= 0 && markTime > 0)
            {
                moteCarry += dt * F("creepRate", 26);
                int n = (int)moteCarry; moteCarry -= n;
                if (n > 0) Motes(target, n, radius * F("moteSeat", .9f), F("moteSpeed", .9f));

                mistCarry += dt * F("mistRate", 5);
                int m = (int)mistCarry; mistCarry -= m;
                if (m > 0) Mist(target, m, radius * F("mistSeat", .85f));
            }

            if (dt > 0)
            {
                motes.Simulate(dt, false, false);
                sparks.Simulate(dt, false, false);
                chips.Simulate(dt, false, false);
                mist.Simulate(dt, false, false);
            }
        }

        public void Dispose()
        {
            Object.Destroy(moteMaterial);
            Object.Destroy(sparkMaterial);
            Object.Destroy(chipMaterial);
            Object.Destroy(mistMaterial);
        }
    }
}
