using System.Collections.Generic;
using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Builds sculpted Catppuccin Mocha silhouettes and attaches FusedKineticRig animations
    /// per Fusion preset. All generated meshes carry ObjectTrim with no colliders so compound
    /// physics, pressure-plate mass, and PlacementGhost 2D footprints stay exact.
    /// </summary>
    public static class FusionPresetVisualBuilder
    {
        private static Mesh torusThinMesh;
        private static Mesh torusThickMesh;
        private static Mesh sweptFinMesh;
        private static Mesh crystalMesh;
        private static Mesh nozzleBellMesh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticMeshes()
        {
            DestroyCachedMesh(ref torusThinMesh);
            DestroyCachedMesh(ref torusThickMesh);
            DestroyCachedMesh(ref sweptFinMesh);
            DestroyCachedMesh(ref crystalMesh);
            DestroyCachedMesh(ref nozzleBellMesh);
        }

        private static void DestroyCachedMesh(ref Mesh mesh)
        {
            if (mesh != null)
            {
                Object.Destroy(mesh);
                mesh = null;
            }
        }

        public static FusedKineticRig BuildPresetAssembly(
            Transform compoundRoot,
            InteractableObject partner,
            InteractableObject attached,
            Vector3 partnerWorldSize,
            Vector3 attachedWorldSize,
            Vector3 seamCenter,
            Vector3 seamNormal,
            bool isTop,
            FusionShapeKind partnerShape,
            FusionShapeKind attachedShape,
            FusionPresetDefinition preset,
            Mesh chamferCube,
            Mesh octagonPrism)
        {
            Vector3 upHint = Mathf.Abs(seamNormal.y) > 0.9f ? compoundRoot.forward : Vector3.up;
            Quaternion seamRot = Quaternion.LookRotation(seamNormal, upHint);

            GameObject joineryGo = new GameObject("Fusion Joinery");
            joineryGo.transform.SetPositionAndRotation(seamCenter, seamRot);
            joineryGo.transform.SetParent(compoundRoot, true);
            Transform root = joineryGo.transform;

            FusedKineticRig rig = compoundRoot.GetComponent<FusedKineticRig>();
            if (rig == null) rig = compoundRoot.gameObject.AddComponent<FusedKineticRig>();

            Color accent = preset != null ? preset.accentTint : PaletteSettings.Teal;
            FusionJoineryStyle style = preset != null ? preset.joineryStyle : FusionJoineryStyle.ModularBracket;

            // Keep HDR emission restrained to 1-2 focal parts while dark metallic framing holds the silhouette.
            Material darkChassis = InteractableObject.GetSharedMetallicMaterial(PaletteSettings.Crust, 0.72f, 0.74f);
            Material midChassis = InteractableObject.GetSharedMetallicMaterial(PaletteSettings.Surface0, 0.62f, 0.68f);
            Material lightChassis = InteractableObject.GetSharedMetallicMaterial(PaletteSettings.Surface2, 0.56f, 0.74f);
            Material glowPrimary = InteractableObject.GetSharedEmissiveMaterial(accent, 1.85f);
            Material glowCore = InteractableObject.GetSharedEmissiveMaterial(Color.Lerp(accent, PaletteSettings.Text, 0.35f), 2.25f);

            Vector3 localPartner = root.InverseTransformPoint(partner.transform.position);
            Vector3 localAttached = root.InverseTransformPoint(attached.transform.position);
            float centerDist = Vector3.Distance(partner.transform.position, attached.transform.position);

            Vector3 right = seamRot * Vector3.right;
            Vector3 up = seamRot * Vector3.up;
            float spanW = Mathf.Max(GridSettings.ToWorld(0.46f), Mathf.Min(
                ProjectWorldSizeAlong(attachedWorldSize, attached.transform, right),
                ProjectWorldSizeAlong(partnerWorldSize, partner.transform, right)));
            float spanH = Mathf.Max(GridSettings.ToWorld(0.46f), Mathf.Min(
                ProjectWorldSizeAlong(attachedWorldSize, attached.transform, up),
                ProjectWorldSizeAlong(partnerWorldSize, partner.transform, up)));
            float minSpan = Mathf.Min(spanW, spanH);

            switch (style)
            {
                case FusionJoineryStyle.TwinTurbineBridge:
                    BuildTwinTurbinePreset(root, rig, partnerWorldSize, attachedWorldSize,
                        localPartner, localAttached, centerDist, spanH, isTop, accent,
                        darkChassis, midChassis, lightChassis, glowPrimary, glowCore, chamferCube, octagonPrism);
                    break;

                case FusionJoineryStyle.AxleFlangeNozzle:
                    BuildAxleNozzlePreset(root, rig, partner, attached, partnerWorldSize, attachedWorldSize,
                        attachedShape, localPartner, localAttached, minSpan, isTop, accent,
                        darkChassis, midChassis, lightChassis, glowPrimary, glowCore, octagonPrism);
                    break;

                case FusionJoineryStyle.MagneticClawSocket:
                    BuildMagneticSocketPreset(root, rig, partnerWorldSize, attachedWorldSize,
                        attachedShape, localPartner, localAttached, minSpan, isTop, accent,
                        darkChassis, lightChassis, glowPrimary, glowCore, octagonPrism);
                    break;

                case FusionJoineryStyle.DumbbellAxle:
                    BuildDumbbellOrbPreset(root, rig, partnerWorldSize, attachedWorldSize,
                        localPartner, localAttached, centerDist, isTop, accent,
                        darkChassis, midChassis, lightChassis, glowPrimary, glowCore, octagonPrism);
                    break;

                case FusionJoineryStyle.EmitterCrown:
                    BuildEmitterCrownPreset(root, rig, partnerWorldSize, attachedWorldSize,
                        attachedShape, localPartner, localAttached, minSpan, isTop, accent,
                        darkChassis, midChassis, lightChassis, glowPrimary, glowCore);
                    break;

                case FusionJoineryStyle.ClampRails:
                    BuildExoskeletonGirderPreset(root, rig, attachedWorldSize,
                        localAttached, centerDist, spanW, spanH, isTop, accent,
                        darkChassis, midChassis, glowPrimary, glowCore, chamferCube);
                    break;

                default:
                    BuildModularBracketPreset(root, rig, minSpan, spanW, spanH, isTop, accent,
                        darkChassis, lightChassis, glowPrimary, glowCore, chamferCube);
                    break;
            }

            Light coreLight = AddCorePointLight(root, Vector3.up * GridSettings.ToWorld(0.10f), accent,
                rangeGrid: 3.2f, intensity: 1.45f);
            rig.SetCoreLight(coreLight, intensity: 1.45f, pulseAmplitude: 0.28f, pulseFreq: 2.6f);
            rig.TriggerWeldSurge(0.55f);
            return rig;
        }

        // Tabung + Tabung: TURBIN-GANDA (side) or PISTON-MENARA (top).
        private static void BuildTwinTurbinePreset(
            Transform root, FusedKineticRig rig,
            Vector3 sizeB, Vector3 sizeA,
            Vector3 localB, Vector3 localA,
            float centerDist, float spanH, bool isTop, Color accent,
            Material darkChassis, Material midChassis, Material lightChassis,
            Material glowPrimary, Material glowCore,
            Mesh chamferCube, Mesh octagonPrism)
        {
            if (!isTop)
            {
                float bridgeZ = Mathf.Max(GridSettings.ToWorld(0.54f), centerDist * 0.64f);
                SpawnMesh(root, "Turbine Bridge Spine", chamferCube, Vector3.zero, Quaternion.identity,
                    new Vector3(GridSettings.ToWorld(0.28f), GridSettings.ToWorld(0.22f), bridgeZ), darkChassis);
                SpawnMesh(root, "Turbine Upper Bridge", chamferCube, new Vector3(0f, spanH * 0.36f, 0f), Quaternion.identity,
                    new Vector3(GridSettings.ToWorld(0.22f), GridSettings.ToWorld(0.10f), bridgeZ * 1.08f), midChassis);
                SpawnMesh(root, "Turbine Lower Bridge", chamferCube, new Vector3(0f, -spanH * 0.36f, 0f), Quaternion.identity,
                    new Vector3(GridSettings.ToWorld(0.22f), GridSettings.ToWorld(0.10f), bridgeZ * 1.08f), midChassis);

                Transform coreCrystal = SpawnMesh(root, "Turbine Reactor Crystal", GetCrystalMesh(),
                    Vector3.zero, Quaternion.identity, Vector3.one * GridSettings.ToWorld(0.32f), glowCore);
                rig.AddSpin(coreCrystal, new Vector3(0f, 95f, 0f));

                BuildCylinderTurbineCap(root, rig, localB, sizeB, -1f, +148f, darkChassis, lightChassis, glowPrimary);
                BuildCylinderTurbineCap(root, rig, localA, sizeA, +1f, -148f, darkChassis, lightChassis, glowPrimary);

                Transform arcA = CreatePivot(root, "ArcAnchor_B", localB + new Vector3(0f, sizeB.y * 0.12f, 0f));
                Transform arcB = CreatePivot(root, "ArcAnchor_A", localA + new Vector3(0f, sizeA.y * 0.12f, 0f));
                rig.AddPlasmaArc(root, arcA, arcB, accent, 0.044f, 0.060f, 10f);
                rig.AddMoteEmitter(coreCrystal, Vector3.up, accent, 0.95f, 0.10f, 0.44f, 0.52f);
                return;
            }

            float colDiam = Mathf.Max(sizeB.x, sizeA.x);
            float totalH = centerDist + (sizeA.y + sizeB.y) * 0.42f;
            float rodR = colDiam * 0.56f;

            SpawnMesh(root, "Piston Flange Base", octagonPrism, Vector3.zero, Quaternion.Euler(90f, 0f, 0f),
                new Vector3(colDiam * 1.18f, GridSettings.ToWorld(0.18f), colDiam * 1.18f), darkChassis);

            for (int i = 0; i < 3; i++)
            {
                float deg = i * 120f + 30f;
                float rad = deg * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(rad) * rodR, Mathf.Sin(rad) * rodR, 0f);
                SpawnMesh(root, "Hydraulic Rod " + i, octagonPrism, offset, Quaternion.Euler(90f, 0f, 0f),
                    new Vector3(GridSettings.ToWorld(0.11f), totalH * 0.72f, GridSettings.ToWorld(0.11f)), midChassis);
                SpawnMesh(root, "Column Buttress Fin " + i, GetSweptFinMesh(),
                    offset * 1.15f, Quaternion.Euler(0f, 0f, deg - 90f) * Quaternion.Euler(-90f, 0f, 0f),
                    new Vector3(GridSettings.ToWorld(0.92f), GridSettings.ToWorld(0.46f), GridSettings.ToWorld(0.30f)), darkChassis);
            }

            Transform pistonRing = SpawnMesh(root, "Reciprocating Piston Ring", GetThickTorusMesh(),
                Vector3.zero, Quaternion.Euler(90f, 0f, 0f),
                new Vector3(colDiam * 1.30f, GridSettings.ToWorld(1.05f), colDiam * 1.30f), glowPrimary);
            rig.AddBob(pistonRing, Vector3.forward, 0.22f, 3.6f);

            Vector3 summitPos = localA + new Vector3(0f, 0f, sizeA.y * 0.50f);
            SpawnMesh(root, "Column Crown Bell", GetNozzleBellMesh(), summitPos, Quaternion.Euler(90f, 0f, 0f),
                new Vector3(colDiam * 0.78f, GridSettings.ToWorld(0.22f), colDiam * 0.78f), darkChassis);
            Transform summitCrystal = SpawnMesh(root, "Column Beacon Crystal", GetCrystalMesh(),
                summitPos + new Vector3(0f, 0f, GridSettings.ToWorld(0.32f)), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(GridSettings.ToWorld(0.24f), GridSettings.ToWorld(0.38f), GridSettings.ToWorld(0.24f)), glowCore);
            rig.AddSpin(summitCrystal, new Vector3(0f, 110f, 0f));
            rig.AddMoteEmitter(summitCrystal, Vector3.up, accent, 1.05f, 0.11f, 0.48f, 0.52f);
        }

        private static void BuildCylinderTurbineCap(
            Transform root, FusedKineticRig rig, Vector3 cylLocalPos, Vector3 cylSize,
            float outerSideSign, float rotorSpeedDeg,
            Material darkChassis, Material lightChassis, Material glowPrimary)
        {
            float diam = Mathf.Max(cylSize.x, cylSize.z);
            Vector3 topCap = cylLocalPos + new Vector3(0f, cylSize.y * 0.50f + GridSettings.ToWorld(0.05f), 0f);

            SpawnMesh(root, "Turbine Intake Cowl", GetThickTorusMesh(), topCap, Quaternion.identity,
                new Vector3(diam * 1.18f, GridSettings.ToWorld(0.92f), diam * 1.18f), darkChassis);
            SpawnMesh(root, "Turbine Intake Rim", GetTorusMesh(), topCap + Vector3.up * GridSettings.ToWorld(0.045f), Quaternion.identity,
                new Vector3(diam * 1.05f, GridSettings.ToWorld(0.70f), diam * 1.05f), glowPrimary);

            Transform rotorHub = CreatePivot(root, "Turbine Rotor Hub", topCap + Vector3.up * GridSettings.ToWorld(0.06f));
            SpawnMesh(rotorHub, "Rotor Nose Cone", GetCrystalMesh(), Vector3.zero, Quaternion.identity,
                Vector3.one * GridSettings.ToWorld(0.20f), lightChassis);
            for (int b = 0; b < 4; b++)
            {
                Transform bladePivot = CreatePivot(rotorHub, "BladePivot_" + b, Vector3.zero, Quaternion.Euler(0f, b * 90f, 0f));
                SpawnMesh(bladePivot, "Turbine Blade", GetSweptFinMesh(),
                    new Vector3(0f, 0f, diam * 0.24f), Quaternion.Euler(22f, 0f, 90f),
                    new Vector3(GridSettings.ToWorld(0.55f), diam * 0.44f, GridSettings.ToWorld(0.18f)),
                    b % 2 == 0 ? lightChassis : darkChassis);
            }
            rig.AddSpin(rotorHub, new Vector3(0f, rotorSpeedDeg, 0f));

            Vector3 wingPos = cylLocalPos + new Vector3(0f, 0f, outerSideSign * (diam * 0.56f));
            Quaternion wingRot = outerSideSign > 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            SpawnMesh(root, "Turbine Aero Wing", GetSweptFinMesh(), wingPos, wingRot,
                new Vector3(GridSettings.ToWorld(1.12f), cylSize.y * 0.72f, GridSettings.ToWorld(0.46f)), darkChassis);
            SpawnMesh(root, "Turbine Wing Trim", GetSweptFinMesh(),
                wingPos + new Vector3(0f, 0f, outerSideSign * GridSettings.ToWorld(0.07f)), wingRot,
                new Vector3(GridSettings.ToWorld(0.50f), cylSize.y * 0.30f, GridSettings.ToWorld(0.30f)), lightChassis);
        }

        // Tabung + Balok: MODUL-POROS (side) or TURRET-SILINDER (top).
        private static void BuildAxleNozzlePreset(
            Transform root, FusedKineticRig rig,
            InteractableObject partner, InteractableObject attached,
            Vector3 sizeB, Vector3 sizeA,
            FusionShapeKind shapeA,
            Vector3 localB, Vector3 localA,
            float minSpan, bool isTop, Color accent,
            Material darkChassis, Material midChassis, Material lightChassis,
            Material glowPrimary, Material glowCore,
            Mesh octagonPrism)
        {
            bool attachedIsCyl = shapeA == FusionShapeKind.Cylinder;
            InteractableObject cylObj = attachedIsCyl ? attached : partner;
            Vector3 cylLocal = attachedIsCyl ? localA : localB;
            Vector3 cylSize = attachedIsCyl ? sizeA : sizeB;
            Vector3 boxLocal = attachedIsCyl ? localB : localA;
            float cylDirSign = attachedIsCyl ? 1f : -1f;

            // Align barrel hardware along the cylinder's real up axis regardless of horizontal or vertical orientation.
            Vector3 cylAxis = root.InverseTransformDirection(cylObj.transform.up).normalized;
            if (Vector3.Dot(cylAxis, (cylLocal - boxLocal)) < 0f) cylAxis = -cylAxis;

            float cylDiam = Mathf.Min(cylSize.x, cylSize.z);
            if (cylDiam < GridSettings.ToWorld(0.35f)) cylDiam = minSpan * 0.85f;
            float cylLen = cylSize.y;

            float flangeD = minSpan * 1.10f;
            SpawnMesh(root, "Armored Mount Flange", octagonPrism, Vector3.zero, Quaternion.Euler(90f, 0f, 0f),
                new Vector3(flangeD, GridSettings.ToWorld(0.18f), flangeD), darkChassis);
            SpawnMesh(root, "Flange Trim Gasket", GetTorusMesh(),
                new Vector3(0f, 0f, cylDirSign * GridSettings.ToWorld(0.07f)), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(flangeD * 1.04f, GridSettings.ToWorld(0.80f), flangeD * 1.04f), lightChassis);

            if (!isTop)
            {
                Quaternion barrelRot = Quaternion.FromToRotation(Vector3.up, cylAxis);

                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 finPos = new Vector3(s * (flangeD * 0.56f), GridSettings.ToWorld(0.06f), cylDirSign * GridSettings.ToWorld(0.14f));
                    Quaternion finRot = Quaternion.Euler(0f, s > 0 ? 90f : -90f, s * -16f);
                    SpawnMesh(root, s > 0 ? "Thruster Wing R" : "Thruster Wing L", GetSweptFinMesh(),
                        finPos, finRot,
                        new Vector3(GridSettings.ToWorld(1.08f), GridSettings.ToWorld(0.56f), GridSettings.ToWorld(0.48f)), darkChassis);
                }

                Transform rotaryPivot = CreatePivot(root, "Barrel Rotary Accelerator", cylLocal, barrelRot);
                SpawnMesh(rotaryPivot, "Rotary Ring Frame", GetThickTorusMesh(), Vector3.zero, Quaternion.identity,
                    new Vector3(cylDiam * 1.32f, GridSettings.ToWorld(0.92f), cylDiam * 1.32f), midChassis);
                for (int f = 0; f < 3; f++)
                {
                    Transform fPivot = CreatePivot(rotaryPivot, "RotaryFin_" + f, Vector3.zero, Quaternion.Euler(0f, f * 120f, 0f));
                    SpawnMesh(fPivot, "Accelerator Fin", GetSweptFinMesh(),
                        new Vector3(0f, 0f, cylDiam * 0.72f), Quaternion.identity,
                        new Vector3(GridSettings.ToWorld(0.75f), GridSettings.ToWorld(0.34f), GridSettings.ToWorld(0.26f)), lightChassis);
                }
                rig.AddSpin(rotaryPivot, new Vector3(0f, 98f, 0f));

                Vector3 muzzlePos = cylLocal + cylAxis * (cylLen * 0.50f + GridSettings.ToWorld(0.08f));
                Transform nozzleAnchor = CreatePivot(root, "Nozzle Muzzle", muzzlePos, barrelRot);
                SpawnMesh(nozzleAnchor, "Thruster Nozzle Bell", GetNozzleBellMesh(), Vector3.zero, Quaternion.identity,
                    new Vector3(cylDiam * 0.96f, GridSettings.ToWorld(0.28f), cylDiam * 0.96f), darkChassis);
                Transform muzzleGlow = SpawnMesh(nozzleAnchor, "Nozzle Muzzle Ring", GetThickTorusMesh(),
                    new Vector3(0f, GridSettings.ToWorld(0.11f), 0f), Quaternion.identity,
                    new Vector3(cylDiam * 0.94f, GridSettings.ToWorld(0.90f), cylDiam * 0.94f), glowCore);
                rig.AddPulse(muzzleGlow, 0.06f, 5.2f);
                rig.AddMoteEmitter(nozzleAnchor, Vector3.up, accent, 1.55f, 0.12f, 0.38f, 0.38f);
                return;
            }

            for (int c = 0; c < 4; c++)
            {
                float deg = 45f + c * 90f;
                float rad = deg * Mathf.Deg2Rad;
                Vector3 strutPos = new Vector3(Mathf.Cos(rad) * flangeD * 0.48f, Mathf.Sin(rad) * flangeD * 0.48f, 0f);
                SpawnMesh(root, "Turret Anchor Strut " + c, GetSweptFinMesh(),
                    strutPos, Quaternion.Euler(0f, 0f, deg - 90f) * Quaternion.Euler(90f, 0f, 0f),
                    new Vector3(GridSettings.ToWorld(0.88f), GridSettings.ToWorld(0.36f), GridSettings.ToWorld(0.28f)), midChassis);
            }

            Vector3 topMount = localA + new Vector3(0f, 0f, sizeA.y * 0.50f + GridSettings.ToWorld(0.08f));
            Transform radarPivot = CreatePivot(root, "Turret Radar Array", topMount, Quaternion.Euler(90f, 0f, 0f));
            SpawnMesh(radarPivot, "Radar Hub", octagonPrism, Vector3.zero, Quaternion.identity,
                new Vector3(GridSettings.ToWorld(0.32f), GridSettings.ToWorld(0.22f), GridSettings.ToWorld(0.32f)), darkChassis);
            SpawnMesh(radarPivot, "Radar Halo Ring", GetTorusMesh(), new Vector3(0f, GridSettings.ToWorld(0.08f), 0f), Quaternion.identity,
                new Vector3(minSpan * 1.16f, GridSettings.ToWorld(0.82f), minSpan * 1.16f), glowPrimary);
            for (int w = -1; w <= 1; w += 2)
            {
                SpawnMesh(radarPivot, w > 0 ? "Radar Wing R" : "Radar Wing L", GetSweptFinMesh(),
                    new Vector3(w * GridSettings.ToWorld(0.38f), GridSettings.ToWorld(0.16f), 0f),
                    Quaternion.Euler(0f, w > 0 ? 90f : -90f, w * -24f),
                    new Vector3(GridSettings.ToWorld(0.95f), GridSettings.ToWorld(0.38f), GridSettings.ToWorld(0.42f)), darkChassis);
            }
            Transform spire = SpawnMesh(radarPivot, "Turret Beacon Spire", GetCrystalMesh(),
                new Vector3(0f, GridSettings.ToWorld(0.34f), 0f), Quaternion.identity,
                new Vector3(GridSettings.ToWorld(0.19f), GridSettings.ToWorld(0.40f), GridSettings.ToWorld(0.19f)), glowCore);
            rig.AddSpin(radarPivot, new Vector3(0f, 52f, 0f));
            rig.AddMoteEmitter(spire, Vector3.up, accent, 0.95f, 0.10f, 0.44f, 0.55f);
        }

        // Balok + Bola: SOKET-ORB (side) or PEDESTAL-ORB (top).
        private static void BuildMagneticSocketPreset(
            Transform root, FusedKineticRig rig,
            Vector3 sizeB, Vector3 sizeA,
            FusionShapeKind shapeA,
            Vector3 localB, Vector3 localA,
            float minSpan, bool isTop, Color accent,
            Material darkChassis, Material lightChassis, Material glowPrimary, Material glowCore,
            Mesh octagonPrism)
        {
            bool attachedIsSphere = shapeA == FusionShapeKind.Sphere;
            Vector3 sphereLocal = attachedIsSphere ? localA : localB;
            Vector3 sphereSize = attachedIsSphere ? sizeA : sizeB;
            float sphereDir = attachedIsSphere ? 1f : -1f;
            float sphereDiam = Mathf.Max(sphereSize.x, Mathf.Max(sphereSize.y, sphereSize.z));

            float collarD = minSpan * 1.02f;
            SpawnMesh(root, "Socket Emitter Collar", octagonPrism, Vector3.zero, Quaternion.Euler(90f, 0f, 0f),
                new Vector3(collarD, GridSettings.ToWorld(0.16f), collarD), darkChassis);
            SpawnMesh(root, "Socket Emitter Rim", GetThickTorusMesh(),
                new Vector3(0f, 0f, sphereDir * GridSettings.ToWorld(0.05f)), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(collarD * 0.96f, GridSettings.ToWorld(0.82f), collarD * 0.96f), glowPrimary);

            float clawR = collarD * 0.50f;
            for (int c = 0; c < 4; c++)
            {
                float deg = 45f + c * 90f;
                float rad = deg * Mathf.Deg2Rad;
                Vector3 clawPos = new Vector3(
                    Mathf.Cos(rad) * clawR,
                    Mathf.Sin(rad) * clawR,
                    sphereDir * GridSettings.ToWorld(0.26f));
                Quaternion clawRot = Quaternion.Euler(0f, 0f, deg) *
                                     Quaternion.Euler(0f, sphereDir > 0f ? -14f : 194f, 0f);
                SpawnMesh(root, "Containment Claw " + c, GetSweptFinMesh(), clawPos, clawRot,
                    new Vector3(GridSettings.ToWorld(0.95f), GridSettings.ToWorld(0.24f), GridSettings.ToWorld(0.56f)), darkChassis);
            }

            // Pair one anodized outer gimbal hoop with one luminous inner ring for clear silhouette contrast.
            Transform gyroCenter = CreatePivot(root, "Orb Gyro Center", sphereLocal);

            Transform gyroOuter = CreatePivot(gyroCenter, "Gyro Outer Pivot", Vector3.zero, Quaternion.Euler(32f, 0f, 24f));
            SpawnMesh(gyroOuter, "Gyro Gimbal Outer", GetThickTorusMesh(), Vector3.zero, Quaternion.identity,
                new Vector3(sphereDiam * 1.48f, GridSettings.ToWorld(0.82f), sphereDiam * 1.48f), lightChassis);
            rig.AddSpin(gyroOuter, new Vector3(22f, 68f, 14f));

            Transform gyroInner = CreatePivot(gyroCenter, "Gyro Inner Pivot", Vector3.zero, Quaternion.Euler(-44f, 30f, -26f));
            SpawnMesh(gyroInner, "Gyro Halo Inner", GetTorusMesh(), Vector3.zero, Quaternion.identity,
                new Vector3(sphereDiam * 1.28f, GridSettings.ToWorld(0.85f), sphereDiam * 1.28f), glowCore);
            rig.AddSpin(gyroInner, new Vector3(-32f, -94f, 22f));

            Transform emitterAnchor = CreatePivot(root, "Socket Arc Origin", new Vector3(0f, 0f, sphereDir * GridSettings.ToWorld(0.05f)));
            rig.AddPlasmaArc(root, emitterAnchor, gyroCenter, accent, 0.046f, 0.068f, 11f);
            rig.AddMoteEmitter(gyroCenter, isTop ? Vector3.forward : Vector3.up, accent, 0.90f, 0.10f, 0.48f, 0.55f);
        }

        // Bola + Bola: BARBEL-ORB (side) or INTI-KEMBAR (top).
        private static void BuildDumbbellOrbPreset(
            Transform root, FusedKineticRig rig,
            Vector3 sizeB, Vector3 sizeA,
            Vector3 localB, Vector3 localA,
            float centerDist, bool isTop, Color accent,
            Material darkChassis, Material midChassis, Material lightChassis,
            Material glowPrimary, Material glowCore,
            Mesh octagonPrism)
        {
            float diamB = Mathf.Max(sizeB.x, Mathf.Max(sizeB.y, sizeB.z));
            float diamA = Mathf.Max(sizeA.x, Mathf.Max(sizeA.y, sizeA.z));
            float avgDiam = (diamA + diamB) * 0.5f;

            Transform anchorB = CreatePivot(root, "OrbCore_B", localB);
            Transform anchorA = CreatePivot(root, "OrbCore_A", localA);

            if (!isTop)
            {
                float spindleLen = Mathf.Max(GridSettings.ToWorld(0.54f), centerDist * 0.72f);
                SpawnMesh(root, "Reactor Spindle", octagonPrism, Vector3.zero, Quaternion.Euler(90f, 0f, 0f),
                    new Vector3(GridSettings.ToWorld(0.22f), spindleLen, GridSettings.ToWorld(0.22f)), darkChassis);

                Transform flywheel = CreatePivot(root, "Central Energy Flywheel", Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
                SpawnMesh(flywheel, "Flywheel Hub", octagonPrism, Vector3.zero, Quaternion.identity,
                    new Vector3(avgDiam * 0.60f, GridSettings.ToWorld(0.18f), avgDiam * 0.60f), midChassis);
                SpawnMesh(flywheel, "Flywheel Core Rim", GetThickTorusMesh(), Vector3.zero, Quaternion.identity,
                    new Vector3(avgDiam * 1.16f, GridSettings.ToWorld(0.92f), avgDiam * 1.16f), glowPrimary);
                for (int b = 0; b < 3; b++)
                {
                    Transform bladeP = CreatePivot(flywheel, "FlywheelBlade_" + b, Vector3.zero, Quaternion.Euler(0f, b * 120f, 0f));
                    SpawnMesh(bladeP, "Flywheel Fin", GetSweptFinMesh(),
                        new Vector3(0f, 0f, avgDiam * 0.44f), Quaternion.identity,
                        new Vector3(GridSettings.ToWorld(0.75f), GridSettings.ToWorld(0.22f), GridSettings.ToWorld(0.30f)), lightChassis);
                }
                rig.AddSpin(flywheel, new Vector3(0f, 138f, 0f));

                Transform haloB = CreatePivot(anchorB, "OrbHaloB", Vector3.zero, Quaternion.Euler(35f, 0f, 28f));
                SpawnMesh(haloB, "Orb Gimbal B", GetTorusMesh(), Vector3.zero, Quaternion.identity,
                    new Vector3(diamB * 1.34f, GridSettings.ToWorld(0.80f), diamB * 1.34f), lightChassis);
                rig.AddSpin(haloB, new Vector3(16f, 76f, -22f));

                Transform haloA = CreatePivot(anchorA, "OrbHaloA", Vector3.zero, Quaternion.Euler(-35f, 0f, -28f));
                SpawnMesh(haloA, "Orb Gimbal A", GetTorusMesh(), Vector3.zero, Quaternion.identity,
                    new Vector3(diamA * 1.34f, GridSettings.ToWorld(0.80f), diamA * 1.34f), lightChassis);
                rig.AddSpin(haloA, new Vector3(-16f, -76f, 22f));

                rig.AddPlasmaArc(root, anchorB, anchorA, accent, 0.048f, 0.072f, 11f);
                rig.AddMoteEmitter(flywheel, Vector3.right, accent, 0.90f, 0.10f, 0.44f, 0.52f);
                return;
            }

            float cageR = avgDiam * 0.66f;
            float cageSpan = centerDist * 0.92f;
            for (int r = 0; r < 3; r++)
            {
                float deg = r * 120f;
                float rad = deg * Mathf.Deg2Rad;
                Vector3 ribPos = new Vector3(Mathf.Cos(rad) * cageR, Mathf.Sin(rad) * cageR, 0f);
                SpawnMesh(root, "Hourglass Rib " + r, octagonPrism, ribPos, Quaternion.Euler(90f, 0f, 0f),
                    new Vector3(GridSettings.ToWorld(0.095f), cageSpan, GridSettings.ToWorld(0.095f)), darkChassis);
                SpawnMesh(root, "Hourglass Rib Fin " + r, GetSweptFinMesh(),
                    ribPos * 1.12f, Quaternion.Euler(0f, 0f, deg - 90f) * Quaternion.Euler(90f, 0f, 0f),
                    new Vector3(GridSettings.ToWorld(0.85f), GridSettings.ToWorld(0.38f), GridSettings.ToWorld(0.28f)), midChassis);
            }

            Transform coreDiamond = SpawnMesh(root, "Hourglass Core Diamond", GetCrystalMesh(),
                Vector3.zero, Quaternion.Euler(90f, 0f, 0f),
                new Vector3(GridSettings.ToWorld(0.30f), GridSettings.ToWorld(0.40f), GridSettings.ToWorld(0.30f)), glowCore);
            rig.AddSpin(coreDiamond, new Vector3(0f, 110f, 0f));

            Transform waistOuter = CreatePivot(root, "Hourglass Waist Outer", Vector3.zero, Quaternion.Euler(90f + 26f, 0f, 18f));
            SpawnMesh(waistOuter, "Waist Gimbal", GetThickTorusMesh(), Vector3.zero, Quaternion.identity,
                new Vector3(avgDiam * 1.44f, GridSettings.ToWorld(0.85f), avgDiam * 1.44f), lightChassis);
            rig.AddSpin(waistOuter, new Vector3(18f, 84f, 12f));

            rig.AddPlasmaArc(root, anchorB, anchorA, accent, 0.046f, 0.062f, 10f);
            rig.AddMoteEmitter(coreDiamond, Vector3.up, accent, 0.90f, 0.10f, 0.48f, 0.55f);
        }

        // Tabung + Bola: PISTON-ORB (side) or MERCU-ORB (top).
        private static void BuildEmitterCrownPreset(
            Transform root, FusedKineticRig rig,
            Vector3 sizeB, Vector3 sizeA,
            FusionShapeKind shapeA,
            Vector3 localB, Vector3 localA,
            float minSpan, bool isTop, Color accent,
            Material darkChassis, Material midChassis, Material lightChassis,
            Material glowPrimary, Material glowCore)
        {
            bool attachedIsSphere = shapeA == FusionShapeKind.Sphere;
            Vector3 sphereLocal = attachedIsSphere ? localA : localB;
            Vector3 cylLocal = attachedIsSphere ? localB : localA;
            Vector3 sphereSize = attachedIsSphere ? sizeA : sizeB;
            Vector3 cylSize = attachedIsSphere ? sizeB : sizeA;
            float sphereDir = attachedIsSphere ? 1f : -1f;
            float sphereDiam = Mathf.Max(sphereSize.x, Mathf.Max(sphereSize.y, sphereSize.z));

            float crownD = minSpan * 1.08f;
            Quaternion bellRot = sphereDir > 0f ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(-90f, 0f, 0f);
            SpawnMesh(root, "Solar Emitter Bell", GetNozzleBellMesh(),
                new Vector3(0f, 0f, sphereDir * GridSettings.ToWorld(0.02f)), bellRot,
                new Vector3(crownD, GridSettings.ToWorld(0.28f), crownD), darkChassis);
            SpawnMesh(root, "Solar Emitter Ring", GetThickTorusMesh(),
                new Vector3(0f, 0f, sphereDir * GridSettings.ToWorld(0.11f)), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(crownD * 1.06f, GridSettings.ToWorld(0.84f), crownD * 1.06f), glowPrimary);

            Transform focusRing = CreatePivot(root, "Emitter Focus Ring",
                new Vector3(0f, 0f, sphereDir * GridSettings.ToWorld(0.14f)), Quaternion.Euler(90f, 0f, 0f));
            for (int p = 0; p < 3; p++)
            {
                Transform pPivot = CreatePivot(focusRing, "FocusProng_" + p, Vector3.zero, Quaternion.Euler(0f, p * 120f, 0f));
                SpawnMesh(pPivot, "Focus Wing", GetSweptFinMesh(),
                    new Vector3(0f, GridSettings.ToWorld(0.08f), crownD * 0.56f), Quaternion.Euler(-20f, 0f, 0f),
                    new Vector3(GridSettings.ToWorld(0.82f), GridSettings.ToWorld(0.36f), GridSettings.ToWorld(0.36f)),
                    midChassis);
            }
            rig.AddSpin(focusRing, new Vector3(0f, isTop ? 64f : 110f, 0f));

            Transform orbPivot = CreatePivot(root, "Solar Orb Pivot", sphereLocal);
            Transform halo1 = CreatePivot(orbPivot, "Solar Halo 1", Vector3.zero, Quaternion.Euler(34f, 0f, 22f));
            SpawnMesh(halo1, "Solar Halo Ring", GetTorusMesh(), Vector3.zero, Quaternion.identity,
                new Vector3(sphereDiam * 1.42f, GridSettings.ToWorld(0.88f), sphereDiam * 1.42f), glowCore);
            rig.AddSpin(halo1, new Vector3(24f, 88f, -18f));

            if (isTop)
            {
                Transform halo2 = CreatePivot(orbPivot, "Solar Gimbal 2", Vector3.zero, Quaternion.Euler(-44f, 20f, -30f));
                SpawnMesh(halo2, "Solar Gimbal Ring", GetTorusMesh(), Vector3.zero, Quaternion.identity,
                    new Vector3(sphereDiam * 1.24f, GridSettings.ToWorld(0.75f), sphereDiam * 1.24f), lightChassis);
                rig.AddSpin(halo2, new Vector3(-28f, -96f, 20f));

                Vector3 apexPos = sphereLocal + new Vector3(0f, 0f, sphereDir * (sphereDiam * 0.62f));
                Transform apexCrystal = SpawnMesh(root, "Lighthouse Apex Crystal", GetCrystalMesh(),
                    apexPos, Quaternion.Euler(90f, 0f, 0f),
                    new Vector3(GridSettings.ToWorld(0.22f), GridSettings.ToWorld(0.38f), GridSettings.ToWorld(0.22f)), glowCore);
                rig.AddSpin(apexCrystal, new Vector3(0f, 115f, 0f));
                rig.AddMoteEmitter(apexCrystal, Vector3.up, accent, 1.05f, 0.11f, 0.50f, 0.48f);
            }
            else
            {
                Vector3 tailPos = cylLocal + new Vector3(0f, 0f, -sphereDir * (cylSize.y * 0.36f));
                for (int s = -1; s <= 1; s += 2)
                {
                    SpawnMesh(root, s > 0 ? "Lance Tail Fin R" : "Lance Tail Fin L", GetSweptFinMesh(),
                        tailPos + new Vector3(s * (minSpan * 0.56f), 0f, 0f),
                        Quaternion.Euler(0f, s > 0 ? 90f : -90f, 0f),
                        new Vector3(GridSettings.ToWorld(0.92f), GridSettings.ToWorld(0.46f), GridSettings.ToWorld(0.42f)), darkChassis);
                }
                rig.AddMoteEmitter(orbPivot, Vector3.up, accent, 0.95f, 0.10f, 0.44f, 0.50f);
            }

            Transform beamOrigin = CreatePivot(root, "Solar Beam Origin", new Vector3(0f, 0f, sphereDir * GridSettings.ToWorld(0.06f)));
            rig.AddPlasmaArc(root, beamOrigin, orbPivot, accent, 0.048f, 0.062f, 10f);
        }

        // Balok + Balok: BALOK-GANDA (side) or PILAR-GANDA (top).
        private static void BuildExoskeletonGirderPreset(
            Transform root, FusedKineticRig rig,
            Vector3 sizeA,
            Vector3 localA,
            float centerDist, float spanW, float spanH, bool isTop, Color accent,
            Material darkChassis, Material midChassis,
            Material glowPrimary, Material glowCore,
            Mesh chamferCube)
        {
            if (!isTop)
            {
                float trussSpanZ = Mathf.Max(GridSettings.ToWorld(0.76f), centerDist * 1.18f);

                Transform seamCore = SpawnMesh(root, "Girder Energy Seam", chamferCube, Vector3.zero, Quaternion.identity,
                    new Vector3(spanW * 1.04f, spanH * 1.03f, GridSettings.ToWorld(0.10f)), glowPrimary);
                rig.AddPulse(seamCore, 0.03f, 3.0f);

                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sx = -1; sx <= 1; sx += 2)
                    {
                        Vector3 railPos = new Vector3(sx * spanW * 0.44f, sy * spanH * 0.50f, 0f);
                        SpawnMesh(root, "Exo Truss Rail", chamferCube, railPos, Quaternion.identity,
                            new Vector3(GridSettings.ToWorld(0.12f), GridSettings.ToWorld(0.11f), trussSpanZ), darkChassis);
                    }
                    SpawnMesh(root, sy > 0 ? "Exo Bridge Top" : "Exo Bridge Bot", chamferCube,
                        new Vector3(0f, sy * spanH * 0.52f, 0f), Quaternion.identity,
                        new Vector3(spanW * 1.04f, GridSettings.ToWorld(0.13f), GridSettings.ToWorld(0.34f)), midChassis);
                }

                for (int sx = -1; sx <= 1; sx += 2)
                {
                    SpawnMesh(root, sx > 0 ? "Girder Gusset R" : "Girder Gusset L", GetSweptFinMesh(),
                        new Vector3(sx * (spanW * 0.56f), 0f, 0f), Quaternion.Euler(0f, sx > 0 ? 90f : -90f, 0f),
                        new Vector3(GridSettings.ToWorld(1.02f), spanH * 0.68f, GridSettings.ToWorld(0.32f)), darkChassis);
                }

                // Single ridge conduit arc linking two solid end anchors across the girder spine.
                Transform topA = CreatePivot(root, "GirderArc_A", new Vector3(0f, spanH * 0.58f, centerDist * 0.34f));
                Transform topB = CreatePivot(root, "GirderArc_B", new Vector3(0f, spanH * 0.58f, -centerDist * 0.34f));
                SpawnMesh(topA, "Anchor Post A", chamferCube, Vector3.zero, Quaternion.identity, Vector3.one * GridSettings.ToWorld(0.14f), darkChassis);
                SpawnMesh(topB, "Anchor Post B", chamferCube, Vector3.zero, Quaternion.identity, Vector3.one * GridSettings.ToWorld(0.14f), darkChassis);
                rig.AddPlasmaArc(root, topA, topB, accent, 0.040f, 0.045f, 8f);
                return;
            }

            float colSpanZ = Mathf.Max(GridSettings.ToWorld(0.95f), centerDist * 1.24f);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    Vector3 cornerPos = new Vector3(sx * spanW * 0.50f, sy * spanH * 0.50f, 0f);
                    SpawnMesh(root, "Monolith Exo Column", chamferCube, cornerPos, Quaternion.identity,
                        new Vector3(GridSettings.ToWorld(0.13f), GridSettings.ToWorld(0.13f), colSpanZ), darkChassis);
                }
            }

            SpawnMesh(root, "Monolith Seam Core", chamferCube, Vector3.zero, Quaternion.identity,
                new Vector3(spanW * 1.05f, spanH * 1.05f, GridSettings.ToWorld(0.09f)), glowPrimary);

            float haloD = Mathf.Max(spanW, spanH) * 1.52f;
            Transform haloPivot = CreatePivot(root, "Monolith Resonator Halo", Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            SpawnMesh(haloPivot, "Monolith Halo Ring", GetThickTorusMesh(), Vector3.zero, Quaternion.identity,
                new Vector3(haloD, GridSettings.ToWorld(0.80f), haloD), glowPrimary);
            rig.AddSpin(haloPivot, new Vector3(0f, 54f, 0f));
            rig.AddBob(haloPivot, Vector3.forward, 0.12f, 2.4f);

            Vector3 summit = localA + new Vector3(0f, 0f, sizeA.y * 0.50f + GridSettings.ToWorld(0.22f));
            Transform crown = SpawnMesh(root, "Monolith Crown Crystal", GetCrystalMesh(),
                summit, Quaternion.Euler(90f, 0f, 0f),
                new Vector3(GridSettings.ToWorld(0.22f), GridSettings.ToWorld(0.36f), GridSettings.ToWorld(0.22f)), glowCore);
            rig.AddSpin(crown, new Vector3(0f, 95f, 0f));
            rig.AddMoteEmitter(crown, Vector3.up, accent, 0.90f, 0.10f, 0.46f, 0.55f);
        }

        // Compound 3-4 parts: RAKITAN-MODULAR (side) or MENARA-MODULAR (top).
        private static void BuildModularBracketPreset(
            Transform root, FusedKineticRig rig,
            float minSpan, float spanW, float spanH, bool isTop, Color accent,
            Material darkChassis, Material lightChassis, Material glowPrimary, Material glowCore,
            Mesh chamferCube)
        {
            SpawnMesh(root, "Modular Core Seam", chamferCube, Vector3.zero, Quaternion.identity,
                new Vector3(spanW * 1.02f, spanH * 1.02f, GridSettings.ToWorld(0.09f)), glowPrimary);
            SpawnMesh(root, "Modular Heavy Band", chamferCube, Vector3.zero, Quaternion.identity,
                new Vector3(spanW * 1.08f, GridSettings.ToWorld(0.14f), GridSettings.ToWorld(0.28f)), darkChassis);

            Transform modRing = CreatePivot(root, "Modular Kinetic Ring", Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            SpawnMesh(modRing, "Modular Gimbal Ring", GetTorusMesh(), Vector3.zero, Quaternion.identity,
                new Vector3(minSpan * 1.36f, GridSettings.ToWorld(0.82f), minSpan * 1.36f), lightChassis);
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? 1f : -1f;
                SpawnMesh(modRing, "Modular Node " + i, GetCrystalMesh(),
                    new Vector3(s * minSpan * 0.66f, 0f, 0f), Quaternion.identity,
                    Vector3.one * GridSettings.ToWorld(0.15f), glowCore);
            }
            rig.AddSpin(modRing, new Vector3(0f, 76f, 0f));
            rig.AddMoteEmitter(root, isTop ? Vector3.forward : Vector3.up, accent, 0.85f, 0.10f, 0.42f, 0.58f);
        }

        private static Transform CreatePivot(Transform parent, string name, Vector3 localPos, Quaternion? localRot = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private static Transform SpawnMesh(Transform parent, string name, Mesh mesh,
            Vector3 localPos, Quaternion localRot, Vector3 localScale, Material mat)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;

            // Unity built-in Cylinder fallback has height 2.0 whereas OctagonPrism and procedural meshes have height 1.0.
            float sy = localScale.y;
            if (mesh != null && mesh.name == "Cylinder" && mesh.bounds.size.y > 1.5f)
                sy *= 0.5f;
            go.transform.localScale = new Vector3(localScale.x, sy, localScale.z);
            go.AddComponent<ObjectTrim>();

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        private static Light AddCorePointLight(Transform parent, Vector3 localPos, Color color, float rangeGrid, float intensity)
        {
            GameObject go = new GameObject("Fusion Core Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Color.Lerp(color, PaletteSettings.Text, 0.22f);
            l.range = GridSettings.ToWorld(rangeGrid);
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        private static float ProjectWorldSizeAlong(Vector3 worldSize, Transform targetTransform, Vector3 worldDir)
        {
            Vector3 localDir = targetTransform.InverseTransformDirection(worldDir.normalized);
            return Mathf.Abs(localDir.x) * worldSize.x +
                   Mathf.Abs(localDir.y) * worldSize.y +
                   Mathf.Abs(localDir.z) * worldSize.z;
        }

        private static Mesh GetTorusMesh()
        {
            if (torusThinMesh == null)
                torusThinMesh = BuildTorusMesh("Fusion_TorusThin", 0.46f, 0.058f, 28, 12);
            return torusThinMesh;
        }

        private static Mesh GetThickTorusMesh()
        {
            if (torusThickMesh == null)
                torusThickMesh = BuildTorusMesh("Fusion_TorusThick", 0.44f, 0.092f, 28, 12);
            return torusThickMesh;
        }

        private static Mesh BuildTorusMesh(string meshName, float majorR, float minorR, int radialSegs, int tubeSegs)
        {
            int vertCount = (radialSegs + 1) * (tubeSegs + 1);
            Vector3[] verts = new Vector3[vertCount];
            Vector3[] norms = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            int[] tris = new int[radialSegs * tubeSegs * 6];

            int vi = 0;
            for (int i = 0; i <= radialSegs; i++)
            {
                float u = i / (float)radialSegs;
                float theta = u * Mathf.PI * 2f;
                float cosT = Mathf.Cos(theta);
                float sinT = Mathf.Sin(theta);
                Vector3 ringCenter = new Vector3(cosT * majorR, 0f, sinT * majorR);

                for (int j = 0; j <= tubeSegs; j++)
                {
                    float v = j / (float)tubeSegs;
                    float phi = v * Mathf.PI * 2f;
                    float cosP = Mathf.Cos(phi);
                    float sinP = Mathf.Sin(phi);

                    Vector3 normal = new Vector3(cosT * cosP, sinP, sinT * cosP).normalized;
                    verts[vi] = ringCenter + normal * minorR;
                    norms[vi] = normal;
                    uvs[vi] = new Vector2(u * 4f, v);
                    vi++;
                }
            }

            int ti = 0;
            int stride = tubeSegs + 1;
            for (int i = 0; i < radialSegs; i++)
            {
                for (int j = 0; j < tubeSegs; j++)
                {
                    int a = i * stride + j;
                    int b = (i + 1) * stride + j;
                    int c = (i + 1) * stride + (j + 1);
                    int d = i * stride + (j + 1);

                    tris[ti++] = a;
                    tris[ti++] = c;
                    tris[ti++] = b;

                    tris[ti++] = a;
                    tris[ti++] = d;
                    tris[ti++] = c;
                }
            }

            Mesh m = new Mesh { name = meshName, hideFlags = HideFlags.HideAndDontSave };
            m.vertices = verts;
            m.normals = norms;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        private static Mesh GetSweptFinMesh()
        {
            if (sweptFinMesh != null) return sweptFinMesh;

            Vector3 r0 = new Vector3(-0.065f, -0.50f, -0.45f);
            Vector3 r1 = new Vector3( 0.065f, -0.50f, -0.45f);
            Vector3 r2 = new Vector3( 0.065f,  0.50f, -0.45f);
            Vector3 r3 = new Vector3(-0.065f,  0.50f, -0.45f);

            Vector3 t0 = new Vector3(-0.032f, -0.34f,  0.55f);
            Vector3 t1 = new Vector3( 0.032f, -0.34f,  0.55f);
            Vector3 t2 = new Vector3( 0.032f,  0.14f,  0.55f);
            Vector3 t3 = new Vector3(-0.032f,  0.14f,  0.55f);

            List<Vector3> verts = new List<Vector3>(24);
            List<Vector3> norms = new List<Vector3>(24);
            List<Vector2> uvs = new List<Vector2>(24);
            List<int> tris = new List<int>(36);

            AddFlatQuad(verts, norms, uvs, tris, r0, r3, r2, r1);
            AddFlatQuad(verts, norms, uvs, tris, t1, t2, t3, t0);
            AddFlatQuad(verts, norms, uvs, tris, r3, t3, t2, r2);
            AddFlatQuad(verts, norms, uvs, tris, r0, r1, t1, t0);
            AddFlatQuad(verts, norms, uvs, tris, r1, r2, t2, t1);
            AddFlatQuad(verts, norms, uvs, tris, r3, r0, t0, t3);

            Mesh m = new Mesh { name = "Fusion_SweptFin", hideFlags = HideFlags.HideAndDontSave };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            sweptFinMesh = m;
            return sweptFinMesh;
        }

        private static Mesh GetCrystalMesh()
        {
            if (crystalMesh != null) return crystalMesh;

            const int sides = 8;
            const float radius = 0.42f;
            Vector3 topApex = new Vector3(0f, 0.50f, 0f);
            Vector3 botApex = new Vector3(0f, -0.50f, 0f);

            List<Vector3> verts = new List<Vector3>(sides * 6);
            List<Vector3> norms = new List<Vector3>(sides * 6);
            List<Vector2> uvs = new List<Vector2>(sides * 6);
            List<int> tris = new List<int>(sides * 6);

            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);

                AddFlatTri(verts, norms, uvs, tris, topApex, p1, p0);
                AddFlatTri(verts, norms, uvs, tris, botApex, p0, p1);
            }

            Mesh m = new Mesh { name = "Fusion_Crystal", hideFlags = HideFlags.HideAndDontSave };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            crystalMesh = m;
            return crystalMesh;
        }

        private static Mesh GetNozzleBellMesh()
        {
            if (nozzleBellMesh != null) return nozzleBellMesh;

            const int sides = 16;
            const float rBot = 0.30f;
            const float rTop = 0.52f;
            const float yBot = -0.50f;
            const float yTop = 0.50f;

            List<Vector3> verts = new List<Vector3>(sides * 10);
            List<Vector3> norms = new List<Vector3>(sides * 10);
            List<Vector2> uvs = new List<Vector2>(sides * 10);
            List<int> tris = new List<int>(sides * 12);

            Vector3 topCenter = new Vector3(0f, yTop - 0.16f, 0f);
            Vector3 botCenter = new Vector3(0f, yBot, 0f);

            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f;

                Vector3 b0 = new Vector3(Mathf.Cos(a0) * rBot, yBot, Mathf.Sin(a0) * rBot);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * rBot, yBot, Mathf.Sin(a1) * rBot);
                Vector3 t0 = new Vector3(Mathf.Cos(a0) * rTop, yTop, Mathf.Sin(a0) * rTop);
                Vector3 t1 = new Vector3(Mathf.Cos(a1) * rTop, yTop, Mathf.Sin(a1) * rTop);

                AddFlatQuad(verts, norms, uvs, tris, b0, t0, t1, b1);
                AddFlatTri(verts, norms, uvs, tris, topCenter, t1, t0);
                AddFlatTri(verts, norms, uvs, tris, botCenter, b0, b1);
            }

            Mesh m = new Mesh { name = "Fusion_NozzleBell", hideFlags = HideFlags.HideAndDontSave };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            nozzleBellMesh = m;
            return nozzleBellMesh;
        }

        private static void AddFlatTri(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
            Vector3 a, Vector3 b, Vector3 c)
        {
            int idx = verts.Count;
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            verts.Add(a); verts.Add(b); verts.Add(c);
            norms.Add(n); norms.Add(n); norms.Add(n);
            uvs.Add(new Vector2(0.5f, 1f)); uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
            tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
        }

        private static void AddFlatQuad(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int idx = verts.Count;
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            norms.Add(n); norms.Add(n); norms.Add(n); norms.Add(n);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(1f, 0f));
            tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
            tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 3);
        }
    }
}
