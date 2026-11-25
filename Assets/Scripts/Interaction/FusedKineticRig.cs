using System.Collections.Generic;
using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Animates kinetic sub-parts, energy conduits, core lighting, and sparse emitter
    /// motes on welded Fusion assemblies. Respects SimulationSettings.ReduceMotion.
    /// </summary>
    public sealed class FusedKineticRig : MonoBehaviour
    {
        private struct SpinNode
        {
            public Transform target;
            public Vector3 localEulerDegPerSec;
        }

        private struct BobNode
        {
            public Transform target;
            public Vector3 restLocalPos;
            public Vector3 localAxis;
            public float amplitude;
            public float frequency;
            public float phaseOffset;
        }

        private struct PulseNode
        {
            public Transform target;
            public Vector3 baseScale;
            public float amplitude;
            public float frequency;
            public float phaseOffset;
        }

        private struct ArcLink
        {
            public LineRenderer line;
            public Transform anchorA;
            public Transform anchorB;
            public float jitterAmplitude;
            public float frequency;
            public float phaseOffset;
        }

        private struct MoteEmitter
        {
            public Transform origin;
            public Vector3 localDir;
            public float speedGrid;
            public float sizeGrid;
            public float lifetime;
            public float interval;
            public float nextTime;
            public Color tint;
        }

        private const int ArcSegments = 11;

        private readonly List<SpinNode> spinNodes = new List<SpinNode>(6);
        private readonly List<BobNode> bobNodes = new List<BobNode>(4);
        private readonly List<PulseNode> pulseNodes = new List<PulseNode>(4);
        private readonly List<ArcLink> arcLinks = new List<ArcLink>(2);
        private readonly List<MoteEmitter> moteEmitters = new List<MoteEmitter>(3);
        private readonly List<Material> ownedMaterials = new List<Material>(2);
        private readonly Vector3[] arcScratch = new Vector3[ArcSegments];

        private Light coreLight;
        private float baseLightIntensity;
        private float lightPulseAmp;
        private float lightPulseFreq = 2.6f;
        private float weldSurgeRemaining;
        private float weldSurgeDuration = 0.55f;
        private float phaseSeed;
        private Rigidbody cachedBody;

        private void Awake()
        {
            phaseSeed = Random.Range(0f, Mathf.PI * 2f);
            cachedBody = GetComponent<Rigidbody>();
        }

        public void TriggerWeldSurge(float duration = 0.55f)
        {
            weldSurgeDuration = Mathf.Max(0.15f, duration);
            weldSurgeRemaining = SimulationSettings.ReduceMotion ? 0f : weldSurgeDuration;
        }

        public void AddSpin(Transform target, Vector3 localEulerDegPerSec)
        {
            if (target == null) return;
            spinNodes.Add(new SpinNode
            {
                target = target,
                localEulerDegPerSec = localEulerDegPerSec
            });
        }

        public void AddBob(Transform target, Vector3 localAxis, float amplitudeGrid, float frequency, float phaseOffset = 0f)
        {
            if (target == null) return;
            bobNodes.Add(new BobNode
            {
                target = target,
                restLocalPos = target.localPosition,
                localAxis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.up,
                amplitude = GridSettings.ToWorld(amplitudeGrid),
                frequency = frequency,
                phaseOffset = phaseOffset
            });
        }

        public void AddPulse(Transform target, float amplitudeRatio, float frequency, float phaseOffset = 0f)
        {
            if (target == null) return;
            pulseNodes.Add(new PulseNode
            {
                target = target,
                baseScale = target.localScale,
                amplitude = amplitudeRatio,
                frequency = frequency,
                phaseOffset = phaseOffset
            });
        }

        public void SetCoreLight(Light lightComp, float intensity, float pulseAmplitude = 0.28f, float pulseFreq = 2.6f)
        {
            coreLight = lightComp;
            baseLightIntensity = intensity;
            lightPulseAmp = pulseAmplitude;
            lightPulseFreq = pulseFreq;
        }

        public void AddPlasmaArc(Transform parent, Transform anchorA, Transform anchorB, Color accentColor,
            float widthGrid = 0.044f, float jitterGrid = 0.062f, float frequency = 9f)
        {
            if (parent == null || anchorA == null || anchorB == null) return;

            GameObject arcGo = new GameObject("Fusion Plasma Arc");
            arcGo.transform.SetParent(parent, false);
            arcGo.AddComponent<ObjectTrim>();

            LineRenderer lr = arcGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = false;
            lr.positionCount = ArcSegments;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 2;
            float w = GridSettings.ToWorld(widthGrid);
            lr.startWidth = w * 0.65f;
            lr.endWidth = w * 0.65f;
            lr.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.55f),
                new Keyframe(0.5f, 1.05f),
                new Keyframe(1f, 0.55f));
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material mat = new Material(shader);
            mat.hideFlags = HideFlags.HideAndDontSave;
            Color hdr = Color.Lerp(accentColor, PaletteSettings.Text, 0.30f) * 1.75f;
            hdr.a = 0.92f;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", hdr);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", hdr);
            lr.sharedMaterial = mat;
            ownedMaterials.Add(mat);

            Gradient g = new Gradient();
            Color coreBright = Color.Lerp(accentColor, PaletteSettings.Text, 0.45f);
            g.SetKeys(
                new[] { new GradientColorKey(accentColor, 0f), new GradientColorKey(coreBright, 0.5f), new GradientColorKey(accentColor, 1f) },
                new[] { new GradientAlphaKey(0.80f, 0f), new GradientAlphaKey(0.96f, 0.5f), new GradientAlphaKey(0.80f, 1f) });
            lr.colorGradient = g;

            arcLinks.Add(new ArcLink
            {
                line = lr,
                anchorA = anchorA,
                anchorB = anchorB,
                jitterAmplitude = GridSettings.ToWorld(jitterGrid),
                frequency = frequency,
                phaseOffset = Random.Range(0f, 10f)
            });
        }

        public void AddMoteEmitter(Transform origin, Vector3 localDir, Color tint,
            float speedGrid = 0.85f, float sizeGrid = 0.10f, float lifetime = 0.42f, float interval = 0.52f)
        {
            if (origin == null) return;
            moteEmitters.Add(new MoteEmitter
            {
                origin = origin,
                localDir = localDir.sqrMagnitude > 0.0001f ? localDir.normalized : Vector3.up,
                speedGrid = speedGrid,
                sizeGrid = sizeGrid,
                lifetime = lifetime,
                interval = Mathf.Max(0.20f, interval),
                nextTime = Time.time + Random.Range(0.10f, interval),
                tint = tint
            });
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;

            bool reduceMotion = SimulationSettings.ReduceMotion;
            if (reduceMotion)
            {
                weldSurgeRemaining = 0f;
                ResetMotionNodesToRest();
                if (coreLight != null) coreLight.intensity = baseLightIntensity;
                UpdatePlasmaArcs(0f, 0f, staticConduit: true);
                return;
            }

            float dt = Time.deltaTime;
            float t = Time.time + phaseSeed;

            float surge01 = 0f;
            if (weldSurgeRemaining > 0f)
            {
                weldSurgeRemaining = Mathf.Max(0f, weldSurgeRemaining - dt);
                float u = weldSurgeRemaining / weldSurgeDuration;
                surge01 = u * u;
            }

            if (cachedBody == null) cachedBody = GetComponent<Rigidbody>();
            bool energized = cachedBody != null && (cachedBody.isKinematic || cachedBody.linearVelocity.sqrMagnitude > 0.25f);
            float speedMult = (energized ? 1.28f : 1.0f) + surge01 * 1.9f;

            for (int i = 0; i < spinNodes.Count; i++)
            {
                SpinNode s = spinNodes[i];
                if (s.target == null) continue;
                s.target.Rotate(s.localEulerDegPerSec * (dt * speedMult), Space.Self);
            }

            for (int i = 0; i < bobNodes.Count; i++)
            {
                BobNode b = bobNodes[i];
                if (b.target == null) continue;
                float wave = Mathf.Sin(t * b.frequency + b.phaseOffset);
                b.target.localPosition = b.restLocalPos + b.localAxis * (wave * b.amplitude);
            }

            for (int i = 0; i < pulseNodes.Count; i++)
            {
                PulseNode p = pulseNodes[i];
                if (p.target == null) continue;
                float wave = Mathf.Sin(t * p.frequency + p.phaseOffset);
                float scaleFactor = 1f + wave * p.amplitude + surge01 * 0.12f;
                p.target.localScale = p.baseScale * scaleFactor;
            }

            if (coreLight != null)
            {
                float wave = Mathf.Sin(t * lightPulseFreq);
                float boost = (energized ? 0.22f : 0f) + surge01 * 1.4f;
                coreLight.intensity = Mathf.Max(0.2f, baseLightIntensity + wave * lightPulseAmp + boost);
            }

            UpdatePlasmaArcs(t, surge01, staticConduit: false);
            UpdateMoteEmitters(energized || surge01 > 0.15f);
        }

        private void ResetMotionNodesToRest()
        {
            for (int i = 0; i < bobNodes.Count; i++)
            {
                if (bobNodes[i].target != null)
                    bobNodes[i].target.localPosition = bobNodes[i].restLocalPos;
            }
            for (int i = 0; i < pulseNodes.Count; i++)
            {
                if (pulseNodes[i].target != null)
                    pulseNodes[i].target.localScale = pulseNodes[i].baseScale;
            }
        }

        private void UpdatePlasmaArcs(float t, float surge01, bool staticConduit)
        {
            for (int i = 0; i < arcLinks.Count; i++)
            {
                ArcLink arc = arcLinks[i];
                if (arc.line == null || arc.anchorA == null || arc.anchorB == null) continue;

                Vector3 a = arc.anchorA.position;
                Vector3 b = arc.anchorB.position;
                Vector3 delta = b - a;
                float dist = delta.magnitude;
                if (dist < 0.001f)
                {
                    arc.line.enabled = false;
                    continue;
                }
                if (!arc.line.enabled) arc.line.enabled = true;

                if (staticConduit)
                {
                    for (int s = 0; s < ArcSegments; s++)
                        arcScratch[s] = Vector3.Lerp(a, b, s / (float)(ArcSegments - 1));
                    arc.line.SetPositions(arcScratch);
                    continue;
                }

                Vector3 dir = delta / dist;
                Vector3 sideA = Vector3.Cross(dir, Vector3.up);
                if (sideA.sqrMagnitude < 0.001f) sideA = Vector3.Cross(dir, Vector3.right);
                sideA.Normalize();
                Vector3 sideB = Vector3.Cross(dir, sideA).normalized;

                float amp = arc.jitterAmplitude * (1f + surge01 * 0.7f);
                for (int s = 0; s < ArcSegments; s++)
                {
                    float u = s / (float)(ArcSegments - 1);
                    Vector3 p = Vector3.Lerp(a, b, u);
                    float envelope = Mathf.Sin(u * Mathf.PI);
                    float phase = u * 8.5f - t * arc.frequency + arc.phaseOffset;
                    float j1 = Mathf.Sin(phase) * 0.65f + Mathf.Sin(phase * 2.2f + 1.4f) * 0.35f;
                    float j2 = Mathf.Cos(phase * 1.4f - 0.8f) * 0.6f + Mathf.Sin(phase * 2.7f) * 0.4f;
                    arcScratch[s] = p + (sideA * j1 + sideB * j2) * (amp * envelope);
                }
                arc.line.SetPositions(arcScratch);
            }
        }

        private void UpdateMoteEmitters(bool energized)
        {
            float now = Time.time;
            for (int i = 0; i < moteEmitters.Count; i++)
            {
                MoteEmitter e = moteEmitters[i];
                if (e.origin == null || now < e.nextTime) continue;

                e.nextTime = now + (energized ? e.interval * 0.72f : e.interval);
                moteEmitters[i] = e;

                Vector3 worldDir = e.origin.TransformDirection(e.localDir).normalized;
                Vector3 spread = Random.insideUnitSphere * 0.22f;
                Vector3 vel = (worldDir + spread).normalized * GridSettings.ToWorld(e.speedGrid);
                Color col = Color.Lerp(e.tint, PaletteSettings.Text, Random.Range(0.10f, 0.35f));
                SimulationVFX.EmitMote(e.origin.position, col, e.sizeGrid * Random.Range(0.85f, 1.15f), e.lifetime, vel);
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < ownedMaterials.Count; i++)
            {
                if (ownedMaterials[i] != null) Destroy(ownedMaterials[i]);
            }
            ownedMaterials.Clear();
        }
    }
}
