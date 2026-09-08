using Experiment001;
using Experiment002.Instrumentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Experiment002.EditorTools
{
    /// <summary>
    /// Builds the Experiment 002 traversal chamber.
    /// <para>
    /// The chamber is a three-stage zigzag. Each stage is a 30 degree ramp that converts
    /// horizontal speed into height, and each target platform is placed where an idealised
    /// frictionless launch at the ground speed cap of 12 m/s would land. Every platform is above
    /// the 1.83 m ceiling of a normal jump, so no target is reachable without using a ramp.
    /// </para>
    /// <para>
    /// The platform positions are a first draft computed from a frictionless reflection model.
    /// Real PhysX contacts lose tangential speed to friction and convert some of it into spin, so
    /// the real landing points will fall short of these. If a stage proves unreachable, the
    /// correction is to move the platform, which is level design; it is not to raise bounciness,
    /// which would destroy the comparison this experiment exists to make.
    /// </para>
    /// </summary>
    public static class Experiment002ChamberBuilder
    {
        private const string ScenePath = "Assets/Scenes/Experiment002.unity";
        private const string RootFolder = "Assets/Experiments/002_BounceTraversal";
        private const string PhysicsFolder = RootFolder + "/Physics";
        private const string MaterialsFolder = RootFolder + "/Materials";
        private const string InputAssetPath = "Assets/InputSystem_Actions.inputactions";

        private const float SubjectRadius = 0.5f;
        private const float FloorY = 0f;

        [MenuItem("Tools/Experiment 002/Build Traversal Chamber")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolder(PhysicsFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder("Assets/Scenes");

            PhysicsMaterial neutral = EnsurePhysicsMaterial("E2_Neutral", 0.4f, 0.4f, 0f,
                PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Average);

            PhysicsMaterial rigid = EnsurePhysicsMaterial("E2_Rigid", 0.2f, 0.25f, 0.05f,
                PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Average);

            // The two variants differ in exactly one field: bounceCombine. Against the bounciness-0
            // environment, Average yields an effective restitution of (0.85 + 0) / 2 = 0.425, which
            // is what Experiment 001 actually ran at. Maximum yields the full 0.85.
            PhysicsMaterial rubberAverage = EnsurePhysicsMaterial("E2_Rubber_AverageCombine",
                0.9f, 1.0f, 0.85f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Average);

            PhysicsMaterial rubberMaximum = EnsurePhysicsMaterial("E2_Rubber_MaximumCombine",
                0.9f, 1.0f, 0.85f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Maximum);

            Material environmentVisual = EnsureVisualMaterial("E2_Environment", new Color(0.40f, 0.42f, 0.45f));
            Material rampVisual = EnsureVisualMaterial("E2_Ramp", new Color(0.85f, 0.52f, 0.15f));
            Material targetVisual = EnsureVisualMaterial("E2_TargetPlatform", new Color(0.20f, 0.66f, 0.36f));
            Material rigidVisual = EnsureVisualMaterial("E2_RigidBall", new Color(0.62f, 0.64f, 0.68f));
            Material rubberVisual = EnsureVisualMaterial("E2_RubberBall", new Color(0.80f, 0.15f, 0.15f));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            ConfigureCamera();
            BuildShell(neutral, environmentVisual);

            // Stage 1: run east along the floor into Ramp_A, land on Platform_A.
            CreateBox("Ramp_A", new Vector3(0f, 1.1f, 0f), new Vector3(5f, 0.5f, 8f),
                new Vector3(0f, 0f, 30f), neutral, rampVisual);
            CreateBox("Platform_A", new Vector3(13f, 4.8f, 0f), new Vector3(8f, 0.4f, 8f),
                Vector3.zero, neutral, targetVisual);

            // Stage 2: run back west along Platform_A into Ramp_C, land on Platform_B.
            CreateBox("Ramp_C", new Vector3(7.5f, 6.1f, 0f), new Vector3(5f, 0.5f, 8f),
                new Vector3(0f, 0f, -30f), neutral, rampVisual);
            CreateBox("Platform_B", new Vector3(-7f, 9.3f, 0f), new Vector3(8f, 0.4f, 8f),
                Vector3.zero, neutral, targetVisual);

            // Stage 3: run east again along Platform_B into Ramp_D, land on Platform_C.
            CreateBox("Ramp_D", new Vector3(-4.5f, 10.6f, 0f), new Vector3(5f, 0.5f, 8f),
                new Vector3(0f, 0f, 30f), neutral, rampVisual);
            CreateBox("Platform_C", new Vector3(10f, 13.3f, 0f), new Vector3(7f, 0.4f, 8f),
                Vector3.zero, neutral, targetVisual);

            BounceTarget targetA = CreateTarget("Target_A", new Vector3(13f, 5.9f, 0f),
                new Vector3(8f, 1.6f, 8f), 5.0f);
            BounceTarget targetB = CreateTarget("Target_B", new Vector3(-7f, 10.4f, 0f),
                new Vector3(8f, 1.6f, 8f), 9.5f);
            BounceTarget targetC = CreateTarget("Target_C", new Vector3(10f, 14.4f, 0f),
                new Vector3(7f, 1.6f, 8f), 13.5f);

            BounceTarget[] targets = { targetA, targetB, targetC };

            GameObject subject = BuildSubject(rigid, rubberAverage, rigidVisual, rubberVisual);

            GameObject startPointObject = new GameObject("StartPoint");
            startPointObject.transform.position = new Vector3(-15f, FloorY + SubjectRadius + 0.1f, 0f);

            GameObject runnerObject = new GameObject("TraversalRunner");
            TraversalRunner runner = runnerObject.AddComponent<TraversalRunner>();

            WireRunner(runner, subject, startPointObject.transform, targets, rubberAverage, rubberMaximum);
            WireSubject(subject, runner, targets);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Experiment 002 chamber built and saved to {ScenePath}.\n" +
                      "Play, then: WASD to run, HOLD Left Shift for rubber, Space to jump, " +
                      "R to reset, C to swap the bounce-combine variant.");
        }

        private static void ConfigureCamera()
        {
            Camera camera = Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                return;
            }

            // The whole chamber is in the XY plane at z = 0, so a straight side-on view shows every
            // stage at once. Reading a trajectory is much easier without perspective foreshortening.
            camera.transform.SetPositionAndRotation(new Vector3(0f, 8f, -34f), Quaternion.identity);
            camera.farClipPlane = 200f;
        }

        private static void BuildShell(PhysicsMaterial neutral, Material visual)
        {
            CreateBox("Floor", new Vector3(0f, FloorY - 0.5f, 0f), new Vector3(38f, 1f, 12f),
                Vector3.zero, neutral, visual);

            CreateBox("Wall_West", new Vector3(-18.5f, 10f, 0f), new Vector3(1f, 20f, 12f),
                Vector3.zero, neutral, visual);

            CreateBox("Wall_East", new Vector3(18.5f, 10f, 0f), new Vector3(1f, 20f, 12f),
                Vector3.zero, neutral, visual);

            // Back wall only. The front is left open so the camera looks straight into the chamber.
            CreateBox("Wall_Back", new Vector3(0f, 10f, 6.5f), new Vector3(38f, 20f, 1f),
                Vector3.zero, neutral, visual);
        }

        private static BounceTarget CreateTarget(string name, Vector3 position, Vector3 scale, float height)
        {
            GameObject targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.name = name;
            targetObject.transform.position = position;
            targetObject.transform.localScale = scale;

            // Invisible: the coloured platform underneath is the thing the player aims at, and a
            // solid-looking box that the ball passes through would misrepresent the collision.
            Object.DestroyImmediate(targetObject.GetComponent<MeshRenderer>());
            targetObject.GetComponent<Collider>().isTrigger = true;

            BounceTarget target = targetObject.AddComponent<BounceTarget>();

            SerializedObject targetSerialized = new SerializedObject(target);
            targetSerialized.FindProperty("targetName").stringValue = name;
            targetSerialized.FindProperty("heightAboveFloor").floatValue = height;
            targetSerialized.ApplyModifiedPropertiesWithoutUndo();

            return target;
        }

        private static GameObject BuildSubject(
            PhysicsMaterial rigid,
            PhysicsMaterial rubber,
            Material rigidVisual,
            Material rubberVisual)
        {
            GameObject subject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            subject.name = "Subject";
            subject.transform.position = new Vector3(-15f, FloorY + SubjectRadius + 0.1f, 0f);

            Rigidbody body = subject.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.linearDamping = 0f;
            body.angularDamping = 0.05f;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            // Continuous here, unlike Experiment 001. Launch speeds off a ramp reach roughly
            // 12 m/s, which is 24 cm per 0.02 s step, and the platforms are only 0.4 units thick.
            // Discrete detection would risk passing straight through a platform, and that would be
            // an artefact rather than a result.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            subject.GetComponent<Collider>().sharedMaterial = rigid;
            subject.GetComponent<MeshRenderer>().sharedMaterial = rigidVisual;

            PhysicsStateSwitcher switcher = subject.AddComponent<PhysicsStateSwitcher>();
            SerializedObject switcherSerialized = new SerializedObject(switcher);
            SetStateConfig(switcherSerialized, "rigidState", rigid, rigidVisual);
            SetStateConfig(switcherSerialized, "rubberState", rubber, rubberVisual);
            switcherSerialized.FindProperty("startingState").enumValueIndex = (int)PhysicalState.Rigid;
            switcherSerialized.ApplyModifiedPropertiesWithoutUndo();

            subject.AddComponent<BounceTraversalController>();
            subject.AddComponent<TraversalLogger>();

            return subject;
        }

        private static void SetStateConfig(
            SerializedObject target,
            string fieldName,
            PhysicsMaterial physicsMaterial,
            Material visual)
        {
            target.FindProperty($"{fieldName}.physicsMaterial").objectReferenceValue = physicsMaterial;
            target.FindProperty($"{fieldName}.mass").floatValue = 1f;
            target.FindProperty($"{fieldName}.linearDamping").floatValue = 0f;
            target.FindProperty($"{fieldName}.angularDamping").floatValue = 0.05f;
            target.FindProperty($"{fieldName}.visualMaterial").objectReferenceValue = visual;
        }

        private static void WireRunner(
            TraversalRunner runner,
            GameObject subject,
            Transform startPoint,
            BounceTarget[] targets,
            PhysicsMaterial rubberAverage,
            PhysicsMaterial rubberMaximum)
        {
            SerializedObject serialized = new SerializedObject(runner);

            serialized.FindProperty("subject").objectReferenceValue = subject.GetComponent<Rigidbody>();
            serialized.FindProperty("stateSwitcher").objectReferenceValue =
                subject.GetComponent<PhysicsStateSwitcher>();
            serialized.FindProperty("startPoint").objectReferenceValue = startPoint;

            SerializedProperty targetArray = serialized.FindProperty("targets");
            targetArray.arraySize = targets.Length;
            for (int i = 0; i < targets.Length; i++)
            {
                targetArray.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
            }

            // Starts on the variant Experiment 001 actually ran, so the first attempt reproduces
            // the known baseline before anything is changed.
            serialized.FindProperty("variant").enumValueIndex = (int)RestitutionVariant.AverageCombine;
            serialized.FindProperty("rubberAverageCombine").objectReferenceValue = rubberAverage;
            serialized.FindProperty("rubberMaximumCombine").objectReferenceValue = rubberMaximum;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireSubject(GameObject subject, TraversalRunner runner, BounceTarget[] targets)
        {
            InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            if (inputAsset == null)
            {
                Debug.LogWarning($"Could not find the input asset at {InputAssetPath}.");
            }

            SerializedObject controller = new SerializedObject(subject.GetComponent<BounceTraversalController>());
            controller.FindProperty("inputActions").objectReferenceValue = inputAsset;
            controller.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject logger = new SerializedObject(subject.GetComponent<TraversalLogger>());
            logger.FindProperty("runner").objectReferenceValue = runner;
            logger.FindProperty("stateSwitcher").objectReferenceValue = subject.GetComponent<PhysicsStateSwitcher>();
            logger.FindProperty("floorY").floatValue = FloorY;

            SerializedProperty loggerTargets = logger.FindProperty("targets");
            loggerTargets.arraySize = targets.Length;
            for (int i = 0; i < targets.Length; i++)
            {
                loggerTargets.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
            }

            logger.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateBox(
            string name,
            Vector3 position,
            Vector3 scale,
            Vector3 euler,
            PhysicsMaterial physicsMaterial,
            Material visual)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
            box.transform.localScale = scale;
            box.GetComponent<Collider>().sharedMaterial = physicsMaterial;

            if (visual != null)
            {
                box.GetComponent<MeshRenderer>().sharedMaterial = visual;
            }

            return box;
        }

        private static PhysicsMaterial EnsurePhysicsMaterial(
            string name,
            float dynamicFriction,
            float staticFriction,
            float bounciness,
            PhysicsMaterialCombine frictionCombine,
            PhysicsMaterialCombine bounceCombine)
        {
            string preferredPath = $"{PhysicsFolder}/{name}.physicsMaterial";
            string legacyPath = $"{PhysicsFolder}/{name}.physicMaterial";

            PhysicsMaterial existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(preferredPath)
                                       ?? AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(legacyPath);
            if (existing != null)
            {
                return existing;
            }

            AssetDatabase.CreateAsset(CreateInstance(), preferredPath);
            AssetDatabase.SaveAssets();

            PhysicsMaterial created = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(preferredPath);
            if (created != null)
            {
                return created;
            }

            AssetDatabase.DeleteAsset(preferredPath);
            AssetDatabase.CreateAsset(CreateInstance(), legacyPath);
            AssetDatabase.SaveAssets();

            return AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(legacyPath);

            PhysicsMaterial CreateInstance() => new PhysicsMaterial(name)
            {
                dynamicFriction = dynamicFriction,
                staticFriction = staticFriction,
                bounciness = bounciness,
                frictionCombine = frictionCombine,
                bounceCombine = bounceCombine,
            };
        }

        private static Material EnsureVisualMaterial(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("URP Lit shader not found; skipping visual materials.");
                return null;
            }

            Material material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else
            {
                material.color = color;
            }

            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();

            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
