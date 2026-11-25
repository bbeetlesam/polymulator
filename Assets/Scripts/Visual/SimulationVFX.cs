using System.Collections.Generic;
using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Centralized pooled Catppuccin Mocha VFX service: procedural soft-glow bokeh motes,
    /// directional pickup/drop/rotation/throw/portal bursts, hover puffs, beam sparkles,
    /// and expanding horizontal pulse rings. Zero Instantiate/Destroy per action.
    /// </summary>
    public sealed class SimulationVFX : MonoBehaviour
    {
        public static SimulationVFX Instance { get; private set; }

        [Header("Particle Budget")]
        public int ambientParticleCount = 95;
        public float hoverEmitInterval = 0.08f;

        private ParticleSystem ambientParticles;
        private ParticleSystem burstParticles;
        private ParticleSystem hoverParticles;
        private Material particleMaterial;
        private Material ringMaterial;
        private Texture2D softOrbTexture;

        private readonly List<RingSlot> ringPool = new List<RingSlot>(10);
        private float nextHoverEmitTime;
        private RectController rectController;

        private sealed class RingSlot
        {
            public LineRenderer line;
            public Material material;
            public bool active;
            public float elapsed;
            public float duration;
            public float maxRadius;
            public Vector3 center;
            public Color color;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            rectController = Object.FindFirstObjectByType<RectController>();
            CreateSoftOrbTexture();
            CreateMaterials();
            BuildAmbientSystem();
            BuildBurstSystem();
            BuildHoverSystem();
            BuildRingPool();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (softOrbTexture != null) Destroy(softOrbTexture);
        }

        private void CreateSoftOrbTexture()
        {
            const int size = 64;
            softOrbTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float soft = Mathf.Pow(Mathf.Clamp01(1f - r), 2.15f);
                    float hotCore = Mathf.Exp(-r * r * 14f) * 0.65f;
                    float a = Mathf.Clamp01(soft + hotCore);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            softOrbTexture.SetPixels(pixels);
            softOrbTexture.Apply();
        }

        private void CreateMaterials()
        {
            Material resourceMat = Resources.Load<Material>("Sim_ParticleGlow");
            if (resourceMat != null)
            {
                particleMaterial = new Material(resourceMat) { hideFlags = HideFlags.HideAndDontSave };
            }
            else
            {
                Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (particleShader == null) particleShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (particleShader == null) particleShader = Shader.Find("Sprites/Default");
                particleMaterial = new Material(particleShader) { hideFlags = HideFlags.HideAndDontSave };
            }

            if (particleMaterial.HasProperty("_Surface")) particleMaterial.SetFloat("_Surface", 1f);
            if (particleMaterial.HasProperty("_Blend")) particleMaterial.SetFloat("_Blend", 2f); // Additive
            if (particleMaterial.HasProperty("_SrcBlend")) particleMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (particleMaterial.HasProperty("_DstBlend")) particleMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (particleMaterial.HasProperty("_ZWrite")) particleMaterial.SetFloat("_ZWrite", 0f);
            if (softOrbTexture != null)
            {
                particleMaterial.mainTexture = softOrbTexture;
                if (particleMaterial.HasProperty("_BaseMap")) particleMaterial.SetTexture("_BaseMap", softOrbTexture);
            }
            particleMaterial.SetColor("_BaseColor", Color.white);
            particleMaterial.SetColor("_Color", Color.white);

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");
            ringMaterial = new Material(unlitShader) { hideFlags = HideFlags.HideAndDontSave };
            if (ringMaterial.HasProperty("_Surface")) ringMaterial.SetFloat("_Surface", 1f);
            if (ringMaterial.HasProperty("_SrcBlend")) ringMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (ringMaterial.HasProperty("_DstBlend")) ringMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (ringMaterial.HasProperty("_ZWrite")) ringMaterial.SetFloat("_ZWrite", 0f);
            ringMaterial.SetColor("_BaseColor", PaletteSettings.Sapphire);
        }

        private void BuildAmbientSystem()
        {
            GameObject go = new GameObject("VFX_AmbientDust");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, GridSettings.ToWorld(2.2f), 0f);

            ambientParticles = go.AddComponent<ParticleSystem>();
            var main = ambientParticles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.maxParticles = ambientParticleCount;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(GridSettings.ToWorld(0.06f), GridSettings.ToWorld(0.22f));
            main.startSize = new ParticleSystem.MinMaxCurve(GridSettings.ToWorld(0.07f), GridSettings.ToWorld(0.16f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            Color c1 = PaletteSettings.Sapphire; c1.a = 0.34f;
            Color c2 = PaletteSettings.Lavender; c2.a = 0.28f;
            main.startColor = new ParticleSystem.MinMaxGradient(c1, c2);

            var emission = ambientParticles.emission;
            emission.rateOverTime = 14f;

            var shape = ambientParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = GridSettings.ToWorld(new Vector3(26f, 4f, 26f));

            var vel = ambientParticles.velocityOverLifetime;
            vel.enabled = true;
            vel.y = new ParticleSystem.MinMaxCurve(GridSettings.ToWorld(0.08f));

            var col = ambientParticles.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(PaletteSettings.Sapphire, 0f), new GradientColorKey(PaletteSettings.Lavender, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.45f, 0.25f), new GradientAlphaKey(0.4f, 0.75f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = g;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void BuildBurstSystem()
        {
            GameObject go = new GameObject("VFX_BurstPool");
            go.transform.SetParent(transform, false);

            burstParticles = go.AddComponent<ParticleSystem>();
            var main = burstParticles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 320;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.65f);
            main.startSize = new ParticleSystem.MinMaxCurve(GridSettings.ToWorld(0.10f), GridSettings.ToWorld(0.24f));
            main.gravityModifier = 0.10f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;

            var emission = burstParticles.emission;
            emission.enabled = false;

            var sizeLife = burstParticles.sizeOverLifetime;
            sizeLife.enabled = true;
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void BuildHoverSystem()
        {
            GameObject go = new GameObject("VFX_HoverPuffs");
            go.transform.SetParent(transform, false);

            hoverParticles = go.AddComponent<ParticleSystem>();
            var main = hoverParticles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 80;
            main.startLifetime = 0.38f;
            main.startSize = new ParticleSystem.MinMaxCurve(GridSettings.ToWorld(0.09f), GridSettings.ToWorld(0.18f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = hoverParticles.emission;
            emission.enabled = false;

            var sizeLife = hoverParticles.sizeOverLifetime;
            sizeLife.enabled = true;
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.9f, 1f, 0f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void BuildRingPool()
        {
            const int poolCount = 10;
            const int segments = 40;

            for (int i = 0; i < poolCount; i++)
            {
                GameObject go = new GameObject("VFX_Ring_" + i);
                go.transform.SetParent(transform, false);

                LineRenderer lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.loop = true;
                lr.positionCount = segments;
                lr.numCornerVertices = 2;
                Material slotMat = new Material(ringMaterial);
                slotMat.hideFlags = HideFlags.HideAndDontSave;
                lr.sharedMaterial = slotMat;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;

                ringPool.Add(new RingSlot { line = lr, material = slotMat });
            }
        }

        public static void Burst(Vector3 worldPos, Color color, int count = 16, float speedGrid = 2.2f)
        {
            if (Instance != null) Instance.EmitBurstInternal(worldPos, color, count, speedGrid);
        }

        public static void EmitMote(Vector3 worldPos, Color color, float sizeGrid = 0.14f, float lifetime = 0.35f, Vector3 velocity = default)
        {
            if (Instance == null || Instance.burstParticles == null) return;
            color.a = 0.9f;
            var ep = new ParticleSystem.EmitParams
            {
                position = worldPos,
                velocity = velocity,
                startColor = color,
                startSize = GridSettings.ToWorld(sizeGrid),
                startLifetime = lifetime
            };
            Instance.burstParticles.Emit(ep, 1);
        }

        public static void PlaySpawnAperture(Vector3 worldPos, Color color)
        {
            if (Instance == null) return;
            Instance.SpawnRingInternal(worldPos, color, 1.9f, 0.55f);
            Instance.SpawnRingInternal(worldPos + Vector3.up * 0.25f, PaletteSettings.Teal, 1.25f, 0.42f);
            for (int i = 0; i < 18; i++)
            {
                float ang = (Mathf.PI * 2f / 18f) * i;
                Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 vel = radial * GridSettings.ToWorld(0.6f) + Vector3.up * GridSettings.ToWorld(Random.Range(0.9f, 2.2f));
                EmitMote(worldPos + radial * GridSettings.ToWorld(0.45f), Color.Lerp(color, PaletteSettings.Text, 0.35f),
                    Random.Range(0.11f, 0.20f), Random.Range(0.35f, 0.65f), vel);
            }
        }

        public static void Ring(Vector3 worldPos, Color color, float maxRadiusGrid = 1.6f, float duration = 0.42f)
        {
            if (Instance != null) Instance.SpawnRingInternal(worldPos, color, maxRadiusGrid, duration);
        }

        private void EmitBurstInternal(Vector3 worldPos, Color color, int count, float speedGrid)
        {
            if (burstParticles == null) return;

            float speed = GridSettings.ToWorld(speedGrid);
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.15f;
                dir.Normalize();

                Color c = Color.Lerp(color, PaletteSettings.Text, Random.Range(0f, 0.32f));
                c.a = 0.94f;
                var emitParams = new ParticleSystem.EmitParams
                {
                    position = worldPos + dir * GridSettings.ToWorld(0.10f),
                    velocity = dir * Random.Range(speed * 0.45f, speed),
                    startColor = c,
                    startSize = Random.Range(GridSettings.ToWorld(0.10f), GridSettings.ToWorld(0.24f)),
                    startLifetime = Random.Range(0.30f, 0.62f)
                };
                burstParticles.Emit(emitParams, 1);
            }
        }

        private void SpawnRingInternal(Vector3 worldPos, Color color, float maxRadiusGrid, float duration)
        {
            RingSlot slot = null;
            for (int i = 0; i < ringPool.Count; i++)
            {
                if (!ringPool[i].active)
                {
                    slot = ringPool[i];
                    break;
                }
            }
            if (slot == null && ringPool.Count > 0) slot = ringPool[0];
            if (slot == null) return;

            slot.active = true;
            slot.elapsed = 0f;
            slot.duration = Mathf.Max(0.1f, duration);
            slot.maxRadius = GridSettings.ToWorld(maxRadiusGrid);
            slot.center = worldPos + Vector3.up * GridSettings.ToWorld(0.04f);
            slot.color = color;
            slot.line.enabled = true;
            UpdateRingVisual(slot, 0.05f);
        }

        private void Update()
        {
            UpdateHoverEmission();
            UpdateActiveRings();
        }

        private void UpdateHoverEmission()
        {
            if (rectController == null || hoverParticles == null || Time.timeScale <= 0f) return;
            if (!rectController.IsGrounded || rectController.CurrentSpeed < GridSettings.ToWorld(1.5f)) return;
            if (Time.time < nextHoverEmitTime) return;

            nextHoverEmitTime = Time.time + hoverEmitInterval;
            Color tint = PaletteSettings.Sapphire;
            SimulationModeController modeCtrl = SimulationModeController.Instance;
            if (modeCtrl != null) tint = modeCtrl.ModeColor;
            tint.a = 0.45f;

            Vector3 foot = rectController.transform.position + Vector3.up * GridSettings.ToWorld(0.05f);
            Vector3 back = -rectController.transform.forward * GridSettings.ToWorld(0.32f);
            var emitParams = new ParticleSystem.EmitParams
            {
                position = foot + back + new Vector3(Random.Range(-0.15f, 0.15f), 0f, Random.Range(-0.15f, 0.15f)),
                velocity = (Vector3.up * 0.4f - rectController.transform.forward * 0.5f) * GridSettings.Unit,
                startColor = tint
            };
            hoverParticles.Emit(emitParams, 1);
        }

        private void UpdateActiveRings()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < ringPool.Count; i++)
            {
                RingSlot slot = ringPool[i];
                if (!slot.active) continue;

                slot.elapsed += dt;
                float t = Mathf.Clamp01(slot.elapsed / slot.duration);
                if (t >= 1f)
                {
                    slot.active = false;
                    slot.line.enabled = false;
                    continue;
                }
                UpdateRingVisual(slot, t);
            }
        }

        private static void UpdateRingVisual(RingSlot slot, float t)
        {
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float radius = Mathf.Lerp(GridSettings.ToWorld(0.22f), slot.maxRadius, eased);
            float width = GridSettings.ToWorld(0.065f) * (1f - t * 0.72f);
            slot.line.startWidth = width;
            slot.line.endWidth = width;

            Color c = slot.color;
            c.a = (1f - t) * 0.88f;
            slot.material.SetColor("_BaseColor", c);

            int count = slot.line.positionCount;
            float step = Mathf.PI * 2f / count;
            for (int i = 0; i < count; i++)
            {
                float angle = step * i;
                slot.line.SetPosition(i, slot.center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }
    }
}
