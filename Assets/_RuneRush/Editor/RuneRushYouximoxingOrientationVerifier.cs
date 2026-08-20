using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RuneRush.Editor
{
    internal static class RuneRushYouximoxingOrientationVerifier
    {
        private const string PrefabRoot = "Assets/_RuneRush/Settings/YouximoxingGenerated/Prefabs";
        private static readonly Quaternion VehicleForwardRotation = Quaternion.Euler(0f, 90f, 0f);
        private static readonly Quaternion FenceForwardRotation = Quaternion.Euler(0f, 90f, 0f);
        private static readonly string[] VehicleIds =
        {
            "0d34fcdb-f6bd-45fc-aad2-1308addc08b8",
            "2e3548e9-e8d3-425c-be31-357627402c9a",
            "3ae5f651-78c4-4c37-949c-b3ccc0d32cb0",
            "42f6e9cc-49e5-44e4-8746-b9ba38b4b8f9",
            "6144d475-05fc-4dde-a6d0-bc1de5444136",
            "c22ac981-38d0-49eb-8647-d14055d8e740",
            "d098abc5-58b3-44cf-b686-e66986d10017",
            "d4275677-62be-4d89-b960-4c7360fc1366"
        };
        private static readonly HashSet<string> FenceIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "19036be9-b920-47cd-9195-fdb45b801a91",
            "482b83bb-5ee8-4ee2-922e-faa64abda184",
            "7db38d40-facd-4765-8cb5-2142c6a45549"
        };

        [MenuItem("Rune Rush/Scene Editing/Verify Youximoxing Model Orientation")]
        internal static void VerifyFromMenu()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot });
            int checkedCount = 0;
            int failedCount = 0;

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null) continue;

                try
                {
                    Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                    if (!TryCalculateBounds(renderers, out Bounds bounds))
                    {
                        Debug.LogError("Rune Rush orientation verification: no renderable bounds in " + path);
                        failedCount += 1;
                        continue;
                    }

                    checkedCount += 1;
                    float tolerance = Mathf.Max(0.01f, bounds.size.magnitude * 0.0025f);
                    if (Mathf.Abs(bounds.min.y) > tolerance)
                    {
                        Debug.LogError($"Rune Rush orientation verification: {path} is not grounded. minY={bounds.min.y:F4}, tolerance={tolerance:F4}");
                        failedCount += 1;
                    }

                    Transform model = FindDeepChild(instance.transform, "Model");
                    if (model == null)
                    {
                        // Runtime_Oil is intentionally procedural and has no FBX Model child.
                        if (!path.EndsWith("/Runtime_Oil.prefab", StringComparison.Ordinal))
                        {
                            Debug.LogError($"Rune Rush orientation verification: {path} has no FBX Model transform.");
                            failedCount += 1;
                        }
                        continue;
                    }

                    Transform sourceModel = PrefabUtility.GetCorrespondingObjectFromOriginalSource(model);
                    if (sourceModel == null)
                    {
                        Debug.LogError($"Rune Rush orientation verification: {path} has no corresponding FBX source transform.");
                        failedCount += 1;
                        continue;
                    }

                    if (!TryBuildExpectedReference(path, sourceModel, out GameObject reference, out Quaternion expectedRotation))
                    {
                        Debug.LogError($"Rune Rush orientation verification: {path} could not build an FBX orientation reference.");
                        failedCount += 1;
                        continue;
                    }

                    try
                    {
                        if (Quaternion.Angle(model.localRotation, expectedRotation) > 0.1f)
                        {
                            Debug.LogError(
                                $"Rune Rush orientation verification: {path} changed its required FBX/model rotation. "
                                + $"actual={model.localEulerAngles}, expected={expectedRotation.eulerAngles}");
                            failedCount += 1;
                        }

                        if (!TryCalculateBounds(reference.GetComponentsInChildren<Renderer>(true), out Bounds expectedBounds))
                        {
                            Debug.LogError($"Rune Rush orientation verification: {path} has no renderable FBX reference bounds.");
                            failedCount += 1;
                        }
                        else if (!SameNormalizedDimensions(bounds.size, expectedBounds.size))
                        {
                            Debug.LogError(
                                $"Rune Rush orientation verification: {path} render geometry no longer matches its upright FBX reference. "
                                + $"actual={bounds.size}, expected={expectedBounds.size}");
                            failedCount += 1;
                        }

                        if (IsVehiclePrefab(path) && bounds.size.y > Mathf.Min(bounds.size.x, bounds.size.z) * 1.2f)
                        {
                            Debug.LogError(
                                $"Rune Rush orientation verification: {path} has vehicle height on a horizontal axis. size={bounds.size}");
                            failedCount += 1;
                        }

                        if (IsFencePrefab(path) && bounds.size.y >= Mathf.Max(bounds.size.x, bounds.size.z))
                        {
                            Debug.LogError(
                                $"Rune Rush orientation verification: {path} has fence length on Unity Y and is lying on its side. size={bounds.size}");
                            failedCount += 1;
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(reference);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            if (failedCount == 0)
            {
                Debug.Log(
                    $"RUNE_RUSH_YOUXIMOXING_ORIENTATION_OK: {checkedCount} generated prefabs match their upright FBX geometry, "
                    + "preserve exact axis conversion, face Unity forward where applicable, and rest on the Y-up ground plane.");
            }
            else
            {
                Debug.LogError($"RUNE_RUSH_YOUXIMOXING_ORIENTATION_FAILED: {failedCount} of {checkedCount} generated prefabs failed grounding verification.");
            }
        }

        private static bool IsVehiclePrefab(string path)
        {
            if (path.IndexOf("/Runtime_Racer_", StringComparison.Ordinal) >= 0) return true;
            foreach (string id in VehicleIds)
            {
                if (path.IndexOf("/Scene_" + id + ".prefab", StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }

        private static bool IsFencePrefab(string path)
        {
            foreach (string id in FenceIds)
            {
                if (path.IndexOf("/Scene_" + id + ".prefab", StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }

        private static bool TryBuildExpectedReference(
            string prefabPath,
            Transform sourceModel,
            out GameObject reference,
            out Quaternion expectedRotation)
        {
            reference = null;
            expectedRotation = sourceModel.localRotation;
            string sourcePath = AssetDatabase.GetAssetPath(sourceModel);
            GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (sourceAsset == null) return false;

            reference = PrefabUtility.InstantiatePrefab(sourceAsset) as GameObject;
            if (reference == null) return false;

            reference.hideFlags = HideFlags.HideAndDontSave;
            reference.transform.SetPositionAndRotation(Vector3.zero, sourceModel.localRotation);
            reference.transform.localScale = Vector3.one;

            if (IsVehiclePrefab(prefabPath))
            {
                expectedRotation = VehicleForwardRotation * sourceModel.localRotation;
            }
            else if (IsFencePrefab(prefabPath)
                && TryCalculateBounds(reference.GetComponentsInChildren<Renderer>(true), out Bounds fenceBounds)
                && fenceBounds.size.x > fenceBounds.size.z)
            {
                expectedRotation = FenceForwardRotation * sourceModel.localRotation;
            }

            reference.transform.rotation = expectedRotation;
            return true;
        }

        private static bool SameNormalizedDimensions(Vector3 actual, Vector3 expected)
        {
            actual = NormalizeDimensions(actual);
            expected = NormalizeDimensions(expected);
            return Mathf.Abs(actual.x - expected.x) < 0.01f
                && Mathf.Abs(actual.y - expected.y) < 0.01f
                && Mathf.Abs(actual.z - expected.z) < 0.01f;
        }

        private static Vector3 NormalizeDimensions(Vector3 size)
        {
            float largest = Mathf.Max(size.x, size.y, size.z);
            return largest > 0.0001f ? size / largest : Vector3.zero;
        }

        private static bool TryCalculateBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                Transform nested = FindDeepChild(child, name);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
