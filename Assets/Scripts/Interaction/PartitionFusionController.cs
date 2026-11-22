using System;
using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Executes core POLYMULATOR Partition [1] and Fusion [2] spatial operations.
    /// Partition splits a heavy or fused block into two lighter non-overlapping halves;
    /// Fusion merges two nearby objects into one heavy composite block, with live link preview.
    /// </summary>
    public sealed class PartitionFusionController : MonoBehaviour
    {
        [Header("Partition & Fusion (grid units)")]
        [Min(0.8f)] public float fusionPairRange = 3.2f;
        [Min(0.5f)] public float minimumSplitMass = 1.15f;
        [Min(0.3f)] public float minimumHalfExtent = 0.40f;
        [Range(1, 3)] public int maxPartitionDepth = 2;

        [Header("References")]
        public SimulationModeController modeController;
        public ObjectInteractor interactor;
        public ObjectCarryController carryController;
        public TelekinesisController telekinesisController;

        private readonly Collider[] pairBuffer = new Collider[48];
        private LineRenderer linkLine;
        private Material linkMaterial;
        private InteractableObject cachedSource;
        private InteractableObject cachedPartner;

        public InteractableObject PreviewSource => cachedSource;
        public InteractableObject PreviewPartner => cachedPartner;
        public bool HasFusionPair => cachedSource != null && cachedPartner != null;

        public event Action<InteractableObject, InteractableObject> OnPartitionPerformed;
        public event Action<InteractableObject> OnFusionPerformed;

        private void Awake()
        {
            if (modeController == null) modeController = GetComponent<SimulationModeController>();
            if (interactor == null) interactor = GetComponent<ObjectInteractor>();
            if (carryController == null) carryController = GetComponent<ObjectCarryController>();
            if (telekinesisController == null) telekinesisController = GetComponent<TelekinesisController>();
            EnsureLinkVisual();
        }

        private void EnsureLinkVisual()
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");

            GameObject linkGo = new GameObject("Fusion Link Preview");
            linkGo.transform.SetParent(transform, false);
            linkLine = linkGo.AddComponent<LineRenderer>();
            linkLine.useWorldSpace = true;
            linkLine.positionCount = 2;
            linkLine.startWidth = GridSettings.ToWorld(0.036f);
            linkLine.endWidth = GridSettings.ToWorld(0.036f);
            linkLine.numCapVertices = 4;
            linkLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            linkLine.receiveShadows = false;

            linkMaterial = new Material(unlitShader) { hideFlags = HideFlags.HideAndDontSave };
            if (linkMaterial.HasProperty("_Cull")) linkMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            if (linkMaterial.HasProperty("_Surface")) linkMaterial.SetFloat("_Surface", 1f);
            linkMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            linkMaterial.SetOverrideTag("RenderType", "Transparent");
            linkMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            Color tint = PaletteSettings.Green * 1.25f;
            tint.a = 0.85f;
            linkMaterial.SetColor("_BaseColor", tint);
            linkMaterial.SetColor("_Color", tint);
            linkLine.sharedMaterial = linkMaterial;
            linkLine.enabled = false;
        }

        private void LateUpdate()
        {
            cachedSource = null;
            cachedPartner = null;

            bool fusionAllowed = modeController == null || modeController.CanUseFusion;
            if (!fusionAllowed || Time.timeScale <= 0f)
            {
                if (linkLine != null) linkLine.enabled = false;
                return;
            }

            InteractableObject src = ResolveActiveTarget();
            if (src != null && src.CanBePickedUp && src.canFuse)
            {
                InteractableObject partner = FindFusionPartner(src);
                if (partner != null)
                {
                    cachedSource = src;
                    cachedPartner = partner;
                    if (linkLine != null)
                    {
                        linkLine.enabled = true;
                        linkLine.SetPosition(0, src.transform.position);
                        linkLine.SetPosition(1, partner.transform.position);
                    }
                    return;
                }
            }

            if (linkLine != null) linkLine.enabled = false;
        }

        public bool TryPartition()
        {
            InteractableObject target = ResolveActiveTarget();
            if (target == null)
            {
                modeController?.NotifyBanner("Dekati atau pegang objek terlebih dulu untuk membelah [1]", PaletteSettings.Peach);
                return false;
            }

            if (!target.CanBePickedUp || !target.canPartition)
            {
                SimulationVFX.Ring(target.transform.position, PaletteSettings.Peach, 1.4f, 0.35f);
                modeController?.NotifyBanner(target.displayName + " tidak dapat dibelah", PaletteSettings.Peach);
                return false;
            }

            Rigidbody rb = target.Body;
            float sourceMass = rb != null ? rb.mass : 1f;
            float maxHoriz = Mathf.Max(target.transform.localScale.x, target.transform.localScale.z);
            if (target.partitionDepth >= maxPartitionDepth || sourceMass < minimumSplitMass || GridSettings.ToGrid(maxHoriz) < minimumHalfExtent * 1.5f)
            {
                SimulationVFX.Ring(target.transform.position, PaletteSettings.Yellow, 1.3f, 0.32f);
                modeController?.NotifyBanner(target.displayName + " sudah terlalu kecil untuk dibelah lagi", PaletteSettings.Yellow);
                return false;
            }

            Vector3 originPos = ReleaseFromHoldersAndPlates(target, out bool wasHeldByRect);
            Vector3 splitAxis = ComputeWorldSplitAxis(target);
            if (wasHeldByRect)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
                originPos = transform.position + fwd.normalized * GridSettings.ToWorld(1.25f) + Vector3.up * GridSettings.ToWorld(0.65f);
            }

            float halfMass = Mathf.Max(0.4f, Mathf.Round((sourceMass * 0.5f) * 100f) * 0.01f);
            Color baseColor = target.AssignedColor;
            Color colorA = PaletteSettings.NearestInteractiveAccent(Color.Lerp(baseColor, PaletteSettings.Peach, 0.25f));
            Color colorB = PaletteSettings.NearestInteractiveAccent(Color.Lerp(baseColor, PaletteSettings.Sky, 0.25f));

            Vector3 newScale = ComputeSplitScale(target, splitAxis);
            float separation = Mathf.Max(GridSettings.ToWorld(0.42f), EstimateExtentAlongAxis(target, newScale, splitAxis) * 1.25f);

            Vector3 posA = WorldBounds.ClampInside(originPos - splitAxis * (separation * 0.55f), newScale.x * 0.5f, newScale.z * 0.5f);
            Vector3 posB = WorldBounds.ClampInside(originPos + splitAxis * (separation * 0.55f), newScale.x * 0.5f, newScale.z * 0.5f);
            posA.y = Mathf.Max(posA.y, newScale.y * 0.52f);
            posB.y = Mathf.Max(posB.y, newScale.y * 0.52f);

            string baseLabel = TrimPartSuffix(target.displayName);
            InteractableObject partA = SpawnSplitHalf(target, baseLabel + "-A", posA, newScale, halfMass, colorA, -splitAxis);
            InteractableObject partB = SpawnSplitHalf(target, baseLabel + "-B", posB, newScale, halfMass, colorB, splitAxis);

            target.SetCollidersEnabled(false);
            Destroy(target.gameObject);

            SimulationAudio.PlayPartition(originPos);
            SimulationVFX.Burst(originPos, PaletteSettings.Peach, 24, 3.4f);
            SimulationVFX.Ring(originPos, PaletteSettings.Peach, 2.2f, 0.45f);
            SimulationVisualBridge.TriggerFloorRipple(originPos, PaletteSettings.Peach, 5.2f, 1.0f, 0.62f);
            RectVisualRig.NotifyActionStretch(transform, 0.16f);
            RectCameraController.AddImpulse(0.08f);

            modeController?.NotifyBanner(
                $"Partition: {baseLabel} dibelah menjadi {partA.displayName} & {partB.displayName} ({halfMass:0.0}t x 2)",
                PaletteSettings.Peach);
            OnPartitionPerformed?.Invoke(partA, partB);
            return true;
        }

        public bool TryFusion()
        {
            InteractableObject src = ResolveActiveTarget();
            if (src == null)
            {
                modeController?.NotifyBanner("Dekati atau pegang satu objek di dekat pasangannya untuk digabung [2]", PaletteSettings.Green);
                return false;
            }

            if (!src.CanBePickedUp || !src.canFuse)
            {
                SimulationVFX.Ring(src.transform.position, PaletteSettings.Peach, 1.4f, 0.35f);
                modeController?.NotifyBanner(src.displayName + " tidak dapat digabungkan", PaletteSettings.Peach);
                return false;
            }

            InteractableObject partner = FindFusionPartner(src);
            if (partner == null)
            {
                SimulationVFX.Ring(src.transform.position, PaletteSettings.Yellow, 1.8f, 0.40f);
                modeController?.NotifyBanner("Tidak ada objek lain di dekat " + src.displayName + " untuk digabung [2]", PaletteSettings.Yellow);
                return false;
            }

            Vector3 partnerPos = ReleaseFromHoldersAndPlates(partner, out _);
            Vector3 srcPos = ReleaseFromHoldersAndPlates(src, out bool srcWasHeld);

            Vector3 mergePos = srcWasHeld
                ? transform.position + transform.forward * GridSettings.ToWorld(1.3f) + Vector3.up * GridSettings.ToWorld(0.75f)
                : (srcPos + partnerPos) * 0.5f;

            float massA = src.Body != null ? src.Body.mass : 1f;
            float massB = partner.Body != null ? partner.Body.mass : 1f;
            float totalMass = Mathf.Round((massA + massB) * 100f) * 0.01f;

            Vector3 sA = src.transform.localScale;
            Vector3 sB = partner.transform.localScale;
            float maxY = Mathf.Clamp(Mathf.Max(sA.y, sB.y) * 1.06f, GridSettings.ToWorld(0.65f), GridSettings.ToWorld(1.9f));
            float maxX = Mathf.Clamp(Mathf.Max(sA.x, sB.x) * 1.32f, GridSettings.ToWorld(0.8f), GridSettings.ToWorld(2.2f));
            float maxZ = Mathf.Clamp(Mathf.Max(sA.z, sB.z) * 1.12f, GridSettings.ToWorld(0.8f), GridSettings.ToWorld(2.0f));
            Vector3 mergedScale = new Vector3(maxX, maxY, maxZ);

            mergePos = WorldBounds.ClampInside(mergePos, mergedScale.x * 0.55f, mergedScale.z * 0.55f);
            mergePos.y = Mathf.Max(mergePos.y, mergedScale.y * 0.54f);

            Color blendedColor = PaletteSettings.NearestInteractiveAccent(
                Color.Lerp(Color.Lerp(src.AssignedColor, partner.AssignedColor, 0.5f), PaletteSettings.Green, 0.35f));

            InteractableObject merged = Instantiate(src.gameObject, mergePos, Quaternion.Euler(0f, src.transform.eulerAngles.y, 0f)).GetComponent<InteractableObject>();
            EnsureChamferBlockColliderIfSphere(merged.gameObject);
            merged.transform.localScale = mergedScale;
            merged.displayName = "FUSED-" + totalMass.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "T";
            merged.partitionDepth = 0;
            merged.isFusedComposite = true;
            merged.canPartition = true;
            merged.canFuse = true;

            Rigidbody mergedRb = merged.Body;
            if (mergedRb != null)
            {
                mergedRb.isKinematic = false;
                mergedRb.detectCollisions = true;
                mergedRb.mass = totalMass;
                mergedRb.linearVelocity = Vector3.up * 1.1f;
                mergedRb.angularVelocity = Vector3.zero;
                mergedRb.WakeUp();
            }

            merged.SetCollidersEnabled(true);
            merged.SetSafeOrigin(mergePos, merged.transform.rotation);
            merged.ReinitializeClonedVisuals(blendedColor);
            merged.TriggerPulse(0.22f, 0.30f);

            src.SetCollidersEnabled(false);
            partner.SetCollidersEnabled(false);
            Destroy(src.gameObject);
            Destroy(partner.gameObject);

            if (linkLine != null) linkLine.enabled = false;
            SimulationAudio.PlayFusion(mergePos);
            SimulationVFX.Burst(mergePos, PaletteSettings.Green, 28, 3.8f);
            SimulationVFX.Ring(mergePos, PaletteSettings.Green, 2.5f, 0.52f);
            SimulationVisualBridge.TriggerFloorRipple(mergePos, PaletteSettings.Green, 6.2f, 1.15f, 0.68f);
            RectVisualRig.NotifyActionStretch(transform, 0.18f);
            RectCameraController.AddImpulse(0.09f);

            modeController?.NotifyBanner(
                $"Fusion: {src.displayName} + {partner.displayName} -> {merged.displayName}",
                PaletteSettings.Green);
            OnFusionPerformed?.Invoke(merged);
            return true;
        }

        private InteractableObject ResolveActiveTarget()
        {
            if (carryController != null && carryController.IsCarrying)
                return carryController.CarriedObject;
            if (telekinesisController != null && telekinesisController.HasTether)
                return telekinesisController.TetheredObject;
            return interactor != null ? interactor.CurrentTarget : null;
        }

        private InteractableObject FindFusionPartner(InteractableObject src)
        {
            float searchRadius = GridSettings.ToWorld(fusionPairRange);
            if (modeController != null && modeController.IsFlexible) searchRadius *= 1.25f;

            int count = Physics.OverlapSphereNonAlloc(src.transform.position, searchRadius, pairBuffer, ~0, QueryTriggerInteraction.Ignore);
            InteractableObject best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = pairBuffer[i];
                if (col == null) continue;
                InteractableObject cand = col.GetComponentInParent<InteractableObject>();
                if (cand == null || cand == src || !cand.isActiveAndEnabled) continue;
                if (!cand.CanBePickedUp || !cand.canFuse) continue;
                if (carryController != null && cand == carryController.CarriedObject) continue;
                if (telekinesisController != null && cand == telekinesisController.TetheredObject) continue;

                float d = Vector3.Distance(src.transform.position, cand.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = cand;
                }
            }
            return best;
        }

        private Vector3 ReleaseFromHoldersAndPlates(InteractableObject target, out bool wasCarriedByRect)
        {
            wasCarriedByRect = false;
            Vector3 pos = target.transform.position;

            if (carryController != null && carryController.CarriedObject == target)
            {
                wasCarriedByRect = true;
                carryController.ReleaseAndReturn();
            }
            if (telekinesisController != null && telekinesisController.TetheredObject == target)
                telekinesisController.Release(false);

            Rigidbody rb = target.Body;
            if (rb != null)
            {
                WeightedPressurePlate[] plates = FindObjectsByType<WeightedPressurePlate>(FindObjectsSortMode.None);
                for (int i = 0; i < plates.Length; i++)
                    plates[i].UnregisterBody(rb);
            }
            return pos;
        }

        private Vector3 ComputeWorldSplitAxis(InteractableObject target)
        {
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right.Normalize();

            Vector3 localX = Vector3.ProjectOnPlane(target.transform.right, Vector3.up).normalized;
            Vector3 localZ = Vector3.ProjectOnPlane(target.transform.forward, Vector3.up).normalized;
            if (localX.sqrMagnitude < 0.001f) localX = Vector3.right;
            if (localZ.sqrMagnitude < 0.001f) localZ = Vector3.forward;

            // Split perpendicular along whichever horizontal axis gives cleaner separation facing Rect
            if (target.transform.localScale.z > target.transform.localScale.x * 1.15f)
                return localZ;
            if (target.transform.localScale.x > target.transform.localScale.z * 1.15f)
                return localX;

            return Mathf.Abs(Vector3.Dot(right, localX)) >= Mathf.Abs(Vector3.Dot(right, localZ)) ? localX : localZ;
        }

        private static Vector3 ComputeSplitScale(InteractableObject target, Vector3 splitAxisWorld)
        {
            Vector3 s = target.transform.localScale;
            Vector3 localAxis = target.transform.InverseTransformDirection(splitAxisWorld);
            bool cutX = Mathf.Abs(localAxis.x) >= Mathf.Abs(localAxis.z);

            if (cutX)
                return new Vector3(Mathf.Max(0.45f, s.x * 0.54f), s.y * 0.96f, s.z * 0.96f);
            return new Vector3(s.x * 0.96f, s.y * 0.96f, Mathf.Max(0.45f, s.z * 0.54f));
        }

        private static float EstimateExtentAlongAxis(InteractableObject target, Vector3 scale, Vector3 axisWorld)
        {
            Vector3 localAxis = target.transform.InverseTransformDirection(axisWorld);
            return Mathf.Abs(localAxis.x) * scale.x + Mathf.Abs(localAxis.z) * scale.z;
        }

        private InteractableObject SpawnSplitHalf(InteractableObject template, string label, Vector3 pos,
            Vector3 scale, float mass, Color accentColor, Vector3 impulseDir)
        {
            GameObject go = Instantiate(template.gameObject, pos, template.transform.rotation);
            go.name = label;
            EnsureChamferBlockColliderIfSphere(go);
            go.transform.localScale = scale;

            InteractableObject info = go.GetComponent<InteractableObject>();
            info.displayName = label;
            info.partitionDepth = template.partitionDepth + 1;
            info.isFusedComposite = false;
            info.canPartition = info.partitionDepth < maxPartitionDepth && mass >= minimumSplitMass;
            info.canFuse = true;

            Rigidbody rb = info.Body;
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;
                rb.mass = mass;
                rb.linearVelocity = impulseDir * 1.15f + Vector3.up * 0.95f;
                rb.angularVelocity = Vector3.zero;
                rb.WakeUp();
            }

            info.SetCollidersEnabled(true);
            info.SetSafeOrigin(pos, go.transform.rotation);
            info.ReinitializeClonedVisuals(accentColor);
            info.TriggerPulse(0.16f, 0.22f);
            return info;
        }

        private static void EnsureChamferBlockColliderIfSphere(GameObject go)
        {
            SphereCollider sc = go.GetComponent<SphereCollider>();
            CapsuleCollider cc = go.GetComponent<CapsuleCollider>();
            if (sc == null && cc == null) return;

            if (sc != null) DestroyImmediate(sc);
            if (cc != null) DestroyImmediate(cc);
            if (go.GetComponent<BoxCollider>() == null)
                go.AddComponent<BoxCollider>();

            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf != null)
            {
                Mesh chamferCube = FindLoadedChamferCubeMesh();
                if (chamferCube != null) mf.sharedMesh = chamferCube;
            }
        }

        private static Mesh FindLoadedChamferCubeMesh()
        {
            Mesh[] meshes = Resources.FindObjectsOfTypeAll<Mesh>();
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] != null && meshes[i].name == "ChamferCube")
                    return meshes[i];
            }

            GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh fallback = tmp.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(tmp);
            return fallback;
        }

        private static string TrimPartSuffix(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "PART";
            if (raw.EndsWith("-A") || raw.EndsWith("-B"))
                return raw.Substring(0, raw.Length - 2);
            return raw;
        }

        private void OnDisable()
        {
            if (linkLine != null) linkLine.enabled = false;
        }

        private void OnDestroy()
        {
            if (linkMaterial != null) Destroy(linkMaterial);
        }
    }
}
