using RuneRush;
using UnityEngine;

namespace RuneRush.UnityRuntime
{
    public static class ProceduralAssetFactory
    {
        private static Shader _litShader;
        private static Shader _unlitShader;

        public static Material Material(string name, Color color, float metallic = 0f, float smoothness = 0.25f,
            bool emission = false)
        {
            _litShader ??= Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(_litShader) { name = name, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                Color glow = color * 2.6f;
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", glow);
            }
            return material;
        }

        public static Material UnlitMaterial(string name, Color color)
        {
            _unlitShader ??= Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(_unlitShader) { name = name, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            return material;
        }

        public static GameObject CreateVehicle(string name, Color heraldry, bool player)
        {
            var root = new GameObject(name);
            var visual = new GameObject("VisualSlot");
            visual.transform.SetParent(root.transform, false);

            Material wood = Material("Dark oak", new Color(0.16f, 0.075f, 0.035f), 0.05f, 0.28f);
            Material iron = Material("Blackened iron", new Color(0.11f, 0.13f, 0.14f), 0.78f, 0.48f);
            Material color = Material("Heraldry", heraldry, 0.28f, 0.42f);
            Material rune = Material("Rune crystal", Color.Lerp(heraldry, Color.white, 0.24f), 0.1f, 0.8f, true);

            AddPrimitive(visual.transform, PrimitiveType.Cube, "Chassis", new Vector3(0f, 0.6f, 0f),
                new Vector3(2.6f, 0.55f, 4.4f), wood);
            AddPrimitive(visual.transform, PrimitiveType.Cube, "Armor", new Vector3(0f, 1.05f, -0.2f),
                new Vector3(2.1f, 0.55f, 2.35f), iron);
            AddPrimitive(visual.transform, PrimitiveType.Cube, "HeraldicPanel", new Vector3(0f, 1.05f, 1.05f),
                new Vector3(1.55f, 0.62f, 0.18f), color);
            AddPrimitive(visual.transform, PrimitiveType.Cylinder, "RuneCore", new Vector3(0f, 1.76f, 0.48f),
                new Vector3(0.35f, 0.62f, 0.35f), rune, new Vector3(0f, 0f, 45f));

            for (int side = -1; side <= 1; side += 2)
            {
                for (int axle = -1; axle <= 1; axle += 2)
                {
                    AddPrimitive(visual.transform, PrimitiveType.Cylinder, $"Wheel_{side}_{axle}",
                        new Vector3(side * 1.52f, 0.42f, axle * 1.35f), new Vector3(0.62f, 0.26f, 0.62f),
                        iron, new Vector3(0f, 0f, 90f));
                }
            }

            AddPrimitive(visual.transform, PrimitiveType.Cube, "Ram", new Vector3(0f, 0.58f, 2.48f),
                new Vector3(1.65f, 0.22f, 0.9f), iron);
            CreateAnchor(root.transform, "CockpitAnchor", new Vector3(0f, 1.68f, 0.15f));
            CreateAnchor(root.transform, "ShieldVfxAnchor", new Vector3(0f, 1f, 0f));
            CreateAnchor(root.transform, "TailVfxAnchor", new Vector3(0f, 0.55f, -2.45f));
            CreateAnchor(root.transform, "NameplateAnchor", new Vector3(0f, 2.6f, 0f));
            CreateAnchor(root.transform, "ColliderRoot", Vector3.zero);

            if (player)
            {
                var cockpit = new GameObject("CockpitVisualSlot");
                cockpit.transform.SetParent(root.transform.Find("CockpitAnchor"), false);
                AddPrimitive(cockpit.transform, PrimitiveType.Cube, "Dashboard", new Vector3(0f, -0.22f, 0.55f),
                    new Vector3(1.8f, 0.22f, 0.5f), wood);
            }

            return root;
        }

        public static GameObject CreatePickup()
        {
            var root = new GameObject("RunePickup");
            Material crystal = Material("Pickup rune", new Color(0.16f, 0.72f, 0.94f), 0.15f, 0.82f, true);
            AddPrimitive(root.transform, PrimitiveType.Cylinder, "Crystal", Vector3.zero,
                new Vector3(0.62f, 1.05f, 0.62f), crystal, new Vector3(0f, 0f, 45f));
            AddPrimitive(root.transform, PrimitiveType.Cylinder, "Halo", new Vector3(0f, -0.38f, 0f),
                new Vector3(1.35f, 0.035f, 1.35f), crystal);
            return root;
        }

        public static GameObject CreateCockpit()
        {
            var root = new GameObject("Runic Cockpit");
            Material wood = Material("Cockpit oak", new Color(0.13f, 0.055f, 0.025f), 0.04f, 0.28f);
            Material iron = Material("Cockpit iron", new Color(0.09f, 0.12f, 0.13f), 0.82f, 0.42f);
            Material rune = Material("Cockpit rune", new Color(0.12f, 0.72f, 0.94f), 0.12f, 0.86f, true);

            AddPrimitive(root.transform, PrimitiveType.Cube, "Dashboard", new Vector3(0f, 0.72f, 1.05f),
                new Vector3(2.25f, 0.18f, 0.6f), wood, new Vector3(-9f, 0f, 0f));
            AddPrimitive(root.transform, PrimitiveType.Cube, "Left guard", new Vector3(-1.12f, 0.86f, 0.28f),
                new Vector3(0.16f, 0.38f, 1.75f), iron, new Vector3(0f, 0f, -8f));
            AddPrimitive(root.transform, PrimitiveType.Cube, "Right guard", new Vector3(1.12f, 0.86f, 0.28f),
                new Vector3(0.16f, 0.38f, 1.75f), iron, new Vector3(0f, 0f, 8f));
            AddPrimitive(root.transform, PrimitiveType.Cylinder, "Speed rune", new Vector3(0f, 0.9f, 0.82f),
                new Vector3(0.3f, 0.035f, 0.3f), rune, new Vector3(90f, 0f, 0f));
            return root;
        }

        public static GameObject CreateMine()
        {
            var root = new GameObject("CurseMine");
            Material curse = Material("Curse stone", new Color(0.35f, 0.06f, 0.48f), 0.25f, 0.6f, true);
            AddPrimitive(root.transform, PrimitiveType.Sphere, "Stone", Vector3.zero, Vector3.one * 1.15f, curse);
            AddPrimitive(root.transform, PrimitiveType.Cylinder, "WarningRing", new Vector3(0f, -0.35f, 0f),
                new Vector3(1.6f, 0.035f, 1.6f), curse);
            return root;
        }

        public static GameObject CreateObstacle(ObstacleKind kind)
        {
            var root = new GameObject(kind.ToString());
            Material stone = Material("Highcrag stone", new Color(0.29f, 0.32f, 0.32f), 0.08f, 0.22f);
            Material wood = Material("Barrier oak", new Color(0.27f, 0.11f, 0.045f), 0.05f, 0.2f);
            Material curse = Material("Cursed mire", new Color(0.13f, 0.19f, 0.11f), 0f, 0.72f, true);
            Material iron = Material("Gate iron", new Color(0.16f, 0.17f, 0.18f), 0.7f, 0.35f);
            switch (kind)
            {
                case ObstacleKind.Crate:
                    AddPrimitive(root.transform, PrimitiveType.Cube, "StoneCrate", new Vector3(0f, 0.8f, 0f),
                        new Vector3(1.7f, 1.6f, 1.7f), stone);
                    break;
                case ObstacleKind.Barricade:
                    AddPrimitive(root.transform, PrimitiveType.Cube, "Beam", new Vector3(0f, 1f, 0f),
                        new Vector3(4.2f, 0.45f, 0.5f), wood);
                    AddPrimitive(root.transform, PrimitiveType.Cube, "LegL", new Vector3(-1.5f, 0.55f, 0f),
                        new Vector3(0.35f, 1.1f, 0.55f), iron);
                    AddPrimitive(root.transform, PrimitiveType.Cube, "LegR", new Vector3(1.5f, 0.55f, 0f),
                        new Vector3(0.35f, 1.1f, 0.55f), iron);
                    break;
                case ObstacleKind.Oil:
                    AddPrimitive(root.transform, PrimitiveType.Cylinder, "Mire", new Vector3(0f, 0.08f, 0f),
                        new Vector3(2.25f, 0.08f, 2.25f), curse);
                    break;
                case ObstacleKind.Spinner:
                    AddPrimitive(root.transform, PrimitiveType.Cylinder, "Pillar", new Vector3(0f, 1f, 0f),
                        new Vector3(0.45f, 1f, 0.45f), stone);
                    AddPrimitive(root.transform, PrimitiveType.Cube, "MotionRoot", new Vector3(0f, 1.25f, 0f),
                        new Vector3(5.2f, 0.42f, 0.52f), iron);
                    break;
                case ObstacleKind.Gate:
                    AddPrimitive(root.transform, PrimitiveType.Cube, "MotionRoot", new Vector3(0f, 1.1f, 0f),
                        new Vector3(4.4f, 2.2f, 0.4f), iron);
                    break;
            }
            return root;
        }

        public static GameObject CreatePad(PadKind kind)
        {
            Color color = kind == PadKind.Boost ? new Color(0.08f, 0.7f, 0.94f) : new Color(0.2f, 0.82f, 0.52f);
            var root = new GameObject(kind == PadKind.Boost ? "WindRunePad" : "HealingGrovePad");
            Material material = Material(root.name, color, 0.2f, 0.72f, true);
            AddPrimitive(root.transform, PrimitiveType.Cube, "RuneSlab", new Vector3(0f, 0.08f, 0f),
                new Vector3(3.4f, 0.12f, 5.4f), material);
            return root;
        }

        public static GameObject AddPrimitive(Transform parent, PrimitiveType type, string name, Vector3 position,
            Vector3 scale, Material material, Vector3? rotation = null)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localRotation = Quaternion.Euler(rotation ?? Vector3.zero);
            primitive.transform.localScale = scale;
            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            return primitive;
        }

        private static Transform CreateAnchor(Transform parent, string name, Vector3 position)
        {
            var anchor = new GameObject(name);
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = position;
            return anchor.transform;
        }
    }
}
