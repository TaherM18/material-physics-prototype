using Experiment001.Instrumentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Experiment001.EditorTools
{
    /// <summary>
    /// Builds the Experiment 001 sandbox scene and its Physics Material assets from scratch.
    /// <para>
    /// The scene is generated rather than hand-authored so that the experimental setup is
    /// reproducible: if a run produces a confusing number, the scene can be rebuilt to a known
    /// state in one menu click instead of being nursed back by hand. It also means the setup is
    /// reviewable as code rather than as a few hundred lines of scene YAML.
    /// </para>
    /// <para>
    /// Existing Physics Material and visual Material assets are never overwritten, so tuned values
    /// survive a rebuild. Delete the asset if a genuine reset is wanted.
    /// </para>
    /// </summary>
    public static class Experiment001SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Experiment001.unity";
        private const string RootFolder = "Assets/Experiments/001_RigidVsRubber";
        private const string PhysicsFolder = RootFolder + "/Physics";
        private const string MaterialsFolder = RootFolder + "/Materials";
        private const string InputAssetPath = "Assets/InputSystem_Actions.inputactions";

        private const float SubjectRadius = 0.5f;
        private const float GroundTopY = 0f;

        [MenuItem("Tools/Experiment 001/Build Sandbox Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolder(PhysicsFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder("Assets/Scenes");

            // The three Physics Materials. Neutral is applied to every piece of static geometry.
            // Leaving static colliders with no material would silently fall back to PhysX built-in
            // defaults, which are not recorded anywhere in the project, so a "rigid vs rubber"
            // comparison would actually be measuring an uncontrolled pair of materials.
            PhysicsMaterial rigidPhysics = EnsurePhysicsMaterial("Rigid", 0.2f, 0.25f, 0.05f);
            PhysicsMaterial rubberPhysics = EnsurePhysicsMaterial("Rubber", 0.9f, 1.0f, 0.85f);
            PhysicsMaterial neutralPhysics = EnsurePhysicsMaterial("Neutral", 0.4f, 0.4f, 0.0f);

            Material rigidVisual = EnsureVisualMaterial("RigidVisual", new Color(0.62f, 0.64f, 0.68f));
            Material rubberVisual = EnsureVisualMaterial("RubberVisual", new Color(0.78f, 0.16f, 0.16f));
            Material environmentVisual = EnsureVisualMaterial("EnvironmentVisual", new Color(0.42f, 0.44f, 0.46f));
            Material propVisual = EnsureVisualMaterial("PropVisual", new Color(0.30f, 0.46f, 0.62f));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            ConfigureCamera();
            BuildEnvironment(neutralPhysics, environmentVisual, out GameObject ramp);
            BuildHeightMarkers(propVisual);

            GameObject subject = BuildSubject(rigidPhysics, rubberPhysics, rigidVisual, rubberVisual);
            BuildProps(neutralPhysics, propVisual);

            GameObject startPoints = new GameObject("StartPoints");
            Transform dropStart = CreateStartPoint(startPoints.transform, "StartPoint_Drop",
                new Vector3(0f, GroundTopY + SubjectRadius + 5f, 0f));
            Transform wallStart = CreateStartPoint(startPoints.transform, "StartPoint_Wall",
                new Vector3(2f, GroundTopY + SubjectRadius + 1f, 0f));

            // Placed against the ramp's own surface, and parented to it, so the ramp angle can be
            // changed later without recomputing the start position by hand.
            Vector3 rampSurface = ramp.transform.TransformPoint(new Vector3(0.39f, 0.5f, 0f));
            Transform slopeStart = CreateStartPoint(ramp.transform, "StartPoint_Slope",
                rampSurface + ramp.transform.up * (SubjectRadius + 0.05f));

            GameObject runnerObject = new GameObject("ExperimentRunner");
            ExperimentRunner runner = runnerObject.AddComponent<ExperimentRunner>();

            WireSubject(subject, rigidPhysics, rubberPhysics, rigidVisual, rubberVisual, runner);
            WireRunner(runner, subject, dropStart, wallStart, slopeStart);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Experiment 001 sandbox built and saved to {ScenePath}.\n" +
                      "Press Play, then: 1 = drop test, 2 = wall test, 3 = slope test, " +
                      "R = repeat last, Tab = toggle rigid/rubber.");
        }

        private static void ConfigureCamera()
        {
            Camera camera = Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                return;
            }

            camera.transform.SetPositionAndRotation(
                new Vector3(0f, 7f, -20f),
                Quaternion.Euler(14f, 0f, 0f));
        }

        private static void BuildEnvironment(PhysicsMaterial neutral, Material visual, out GameObject ramp)
        {
            // Deliberately thick. A thin ground plate invites tunnelling at high impact speeds,
            // and tunnelling would be a measurement artefact rather than a finding.
            CreateBox("Ground", new Vector3(0f, GroundTopY - 1f, 0f), new Vector3(60f, 2f, 60f),
                Vector3.zero, neutral, visual);

            CreateBox("Wall", new Vector3(14f, 4f, 0f), new Vector3(1f, 8f, 24f),
                Vector3.zero, neutral, visual);

            ramp = CreateBox("Ramp", new Vector3(-11f, 2.2f, 0f), new Vector3(14f, 0.5f, 8f),
                new Vector3(0f, 0f, 16f), neutral, visual);
        }

        private static void BuildHeightMarkers(Material visual)
        {
            GameObject parent = new GameObject("HeightMarkers");
            float[] heights = { 0.25f, 0.5f, 1f, 2f, 4f };

            foreach (float height in heights)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = $"Marker_{height:0.##}m";
                marker.transform.SetParent(parent.transform);

                // The marker plane sits at the height the bottom of the sphere would reach, so an
                // apex can be read off by eye against it.
                marker.transform.position = new Vector3(2.2f, GroundTopY + height, 0f);
                marker.transform.localScale = new Vector3(1.4f, 0.02f, 1.4f);

                // Markers must never participate in the simulation.
                Object.DestroyImmediate(marker.GetComponent<Collider>());

                if (visual != null)
                {
                    marker.GetComponent<MeshRenderer>().sharedMaterial = visual;
                }
            }
        }

        private static GameObject BuildSubject(
            PhysicsMaterial rigidPhysics,
            PhysicsMaterial rubberPhysics,
            Material rigidVisual,
            Material rubberVisual)
        {
            GameObject subject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            subject.name = "Subject";
            subject.transform.position = new Vector3(0f, GroundTopY + SubjectRadius + 5f, 0f);

            Rigidbody body = subject.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.linearDamping = 0f;
            body.angularDamping = 0.05f;

            // Interpolation is a rendering concern only: it does not change the simulation, but
            // without it a 50 Hz simulation displayed at a higher frame rate looks stepped, and
            // any judgement about how the motion feels would be a judgement about the stepping.
            body.interpolation = RigidbodyInterpolation.Interpolate;

            // Left at the PhysX default deliberately. Switching to a continuous mode pre-emptively
            // would hide tunnelling rather than reveal it; the thick ground makes it unlikely, and
            // the logger records which mode was active in every summary.
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;

            subject.GetComponent<Collider>().sharedMaterial = rigidPhysics;
            if (rigidVisual != null)
            {
                subject.GetComponent<MeshRenderer>().sharedMaterial = rigidVisual;
            }

            subject.AddComponent<PhysicsStateSwitcher>();
            subject.AddComponent<TestSubjectController>();
            subject.AddComponent<BounceLogger>();

            return subject;
        }

        private static void BuildProps(PhysicsMaterial neutral, Material visual)
        {
            GameObject parent = new GameObject("TestObjects");

            // A stack, to observe how differently the two states transfer momentum into a
            // structure rather than into the ground.
            for (int i = 0; i < 3; i++)
            {
                GameObject box = CreateBox($"Stack_{i}", new Vector3(7f, 0.5f + i, 4f), Vector3.one,
                    Vector3.zero, neutral, visual);
                box.transform.SetParent(parent.transform);
                AddDynamicBody(box, 1f);
            }

            // A light box and a heavy sphere. Restitution against static geometry is mass
            // independent, but momentum exchange between two dynamic bodies is not, so these are
            // where mass should visibly matter.
            GameObject light = CreateBox("LightBox", new Vector3(4f, 0.5f, -4f), Vector3.one,
                Vector3.zero, neutral, visual);
            light.transform.SetParent(parent.transform);
            AddDynamicBody(light, 0.2f);

            GameObject heavy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            heavy.name = "HeavyBall";
            heavy.transform.position = new Vector3(9f, 0.5f, -4f);
            heavy.transform.SetParent(parent.transform);
            heavy.GetComponent<Collider>().sharedMaterial = neutral;
            if (visual != null)
            {
                heavy.GetComponent<MeshRenderer>().sharedMaterial = visual;
            }

            AddDynamicBody(heavy, 8f);
        }

        private static void AddDynamicBody(GameObject target, float mass)
        {
            Rigidbody body = target.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
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

        private static Transform CreateStartPoint(Transform parent, string name, Vector3 worldPosition)
        {
            GameObject point = new GameObject(name);
            point.transform.SetParent(parent);
            point.transform.position = worldPosition;
            return point.transform;
        }

        private static void WireSubject(
            GameObject subject,
            PhysicsMaterial rigidPhysics,
            PhysicsMaterial rubberPhysics,
            Material rigidVisual,
            Material rubberVisual,
            ExperimentRunner runner)
        {
            PhysicsStateSwitcher switcher = subject.GetComponent<PhysicsStateSwitcher>();
            SerializedObject switcherObject = new SerializedObject(switcher);

            SetStateConfig(switcherObject, "rigidState", rigidPhysics, rigidVisual);
            SetStateConfig(switcherObject, "rubberState", rubberPhysics, rubberVisual);
            switcherObject.FindProperty("startingState").enumValueIndex = (int)PhysicalState.Rigid;
            switcherObject.ApplyModifiedPropertiesWithoutUndo();

            TestSubjectController controller = subject.GetComponent<TestSubjectController>();
            InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            if (inputAsset == null)
            {
                Debug.LogWarning($"Could not find the input asset at {InputAssetPath}. " +
                                 "Manual control will be disabled until it is assigned by hand.");
            }

            SerializedObject controllerObject = new SerializedObject(controller);
            controllerObject.FindProperty("inputActions").objectReferenceValue = inputAsset;
            controllerObject.ApplyModifiedPropertiesWithoutUndo();

            BounceLogger logger = subject.GetComponent<BounceLogger>();
            SerializedObject loggerObject = new SerializedObject(logger);
            loggerObject.FindProperty("runner").objectReferenceValue = runner;
            loggerObject.FindProperty("stateSwitcher").objectReferenceValue = switcher;
            loggerObject.FindProperty("groundSurfaceY").floatValue = GroundTopY;
            loggerObject.ApplyModifiedPropertiesWithoutUndo();
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
            ExperimentRunner runner,
            GameObject subject,
            Transform dropStart,
            Transform wallStart,
            Transform slopeStart)
        {
            SerializedObject runnerObject = new SerializedObject(runner);

            runnerObject.FindProperty("subject").objectReferenceValue = subject.GetComponent<Rigidbody>();
            runnerObject.FindProperty("stateSwitcher").objectReferenceValue =
                subject.GetComponent<PhysicsStateSwitcher>();

            SerializedProperty scenarios = runnerObject.FindProperty("scenarios");
            scenarios.arraySize = 3;

            SetScenario(scenarios.GetArrayElementAtIndex(0), "Drop", dropStart, Vector3.zero);
            SetScenario(scenarios.GetArrayElementAtIndex(1), "Wall", wallStart, new Vector3(12f, 6f, 0f));
            SetScenario(scenarios.GetArrayElementAtIndex(2), "Slope", slopeStart, Vector3.zero);

            runnerObject.FindProperty("overrideBounceThreshold").boolValue = false;
            runnerObject.FindProperty("bounceThresholdOverride").floatValue = Physics.bounceThreshold;
            runnerObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetScenario(
            SerializedProperty scenario,
            string name,
            Transform startPoint,
            Vector3 initialVelocity)
        {
            scenario.FindPropertyRelative("name").stringValue = name;
            scenario.FindPropertyRelative("startPoint").objectReferenceValue = startPoint;
            scenario.FindPropertyRelative("initialVelocity").vector3Value = initialVelocity;
            // Deliberately false: a run uses whatever state is currently active, so the workflow
            // is "Tab to choose the state, then a number key to run". Forcing the state here would
            // snap the subject back to Rigid on every run and quietly make the rubber half of the
            // comparison impossible. The active state is printed on the HUD and in every summary.
            scenario.FindPropertyRelative("forceState").boolValue = false;
            scenario.FindPropertyRelative("stateOnStart").enumValueIndex = (int)PhysicalState.Rigid;
        }

        /// <summary>
        /// Creates the Physics Material asset, tolerating either spelling of the file extension.
        /// Unity 6 renamed the type from PhysicMaterial to PhysicsMaterial; the asset extension
        /// that ships with 6.6 could not be verified from outside the editor, so the preferred
        /// spelling is attempted first and the legacy one is used as a fallback.
        /// </summary>
        private static PhysicsMaterial EnsurePhysicsMaterial(
            string name,
            float dynamicFriction,
            float staticFriction,
            float bounciness)
        {
            string preferredPath = $"{PhysicsFolder}/{name}.physicsMaterial";
            string legacyPath = $"{PhysicsFolder}/{name}.physicMaterial";

            PhysicsMaterial existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(preferredPath);
            if (existing == null)
            {
                existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(legacyPath);
            }

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

                // Held at Average everywhere. When two materials disagree on combine mode, PhysX
                // does not average the modes: it applies whichever has the higher priority
                // (Average < Minimum < Multiply < Maximum). A single Maximum anywhere in the scene
                // would therefore override every other material and make results irreproducible.
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Average,
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
                Debug.LogWarning("URP Lit shader not found; skipping visual materials. " +
                                 "The experiment still runs, the states just will not be colour coded.");
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
