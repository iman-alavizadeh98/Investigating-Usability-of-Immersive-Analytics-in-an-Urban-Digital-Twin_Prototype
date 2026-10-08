using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;

using UrbanAnalytics.Interaction;
using UrbanAnalytics.Interaction.UI;
using UrbanAnalytics.Study;

namespace UrbanAnalytics.XR.Editor
{
    /// <summary>
    /// Builds the VR tabletop scene from the desktop scene, so the VR
    /// scene can be re-created whenever the desktop scene changes
    /// (UrbanAnalytics → VR → Build VR Scene).
    ///
    /// Steps: copy the desktop scene; remove the desktop camera; add
    /// the XRI rig (locomotion and teleport off, XRI ray visuals off);
    /// add TabletopRig, XRControllerPointer and XRSimulatorFallback;
    /// add the VR UI (XRHandMenu on the left wrist, XRHoverLabel,
    /// XRTableBoard, XRControllerShortcuts); floor tracking; the PC
    /// help line lists the facilitator's keys only;
    /// wire InteractionManager and StudySession to the XR camera;
    /// swap the UI input module for the XR one. The desktop scene is
    /// not modified.
    /// </summary>
    public static class VRSceneBuilder
    {
        private const string SourceScenePath =
            "Assets/Scenes/Helsingborg.unity";

        private const string TargetScenePath =
            "Assets/Scenes/Helsingborg_VR.unity";

        private const string XriSampleRoot =
            "Assets/Samples/XR Interaction Toolkit/3.6.1";

        private const string RigPrefabPath =
            XriSampleRoot + "/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        private const string InputActionsPath =
            XriSampleRoot + "/Starter Assets/XRI Default Input Actions.inputactions";

        private const string FacilitatorHelpText =
            "VR mode · PC keys for the facilitator: 1–9 views   0: clear view   " +
            "Esc: deselect   C: copy to compare   F2: study panel   " +
            "[ ]: panel size   H: hide this line   (the mouse works on these panels only)";

        private const string ControlsImagePath =
            "Assets/Textures/UrbanAnalytics/VR/vr_controls.png";

        private const string SimulatorPrefabPath =
            XriSampleRoot + "/XR Device Simulator/XR Device Simulator.prefab";

        private const string MaterialFolder =
            "Assets/Materials/UrbanAnalytics/VR";


        [MenuItem("UrbanAnalytics/VR/Build VR Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }


            Debug.Log(
                Build()
            );
        }


        /// <summary>Builds the scene and returns a report.</summary>
        public static string Build()
        {
            var report =
                new StringBuilder();


            GameObject rigPrefab =
                Require<GameObject>(RigPrefabPath);

            GameObject simulatorPrefab =
                Require<GameObject>(SimulatorPrefabPath);


            Dictionary<string, InputActionReference> actions =
                AssetDatabase
                    .LoadAllAssetsAtPath(InputActionsPath)
                    .OfType<InputActionReference>()
                    // The asset holds more than one reference per
                    // action (hidden ones); any of them works.
                    .GroupBy(
                        reference => reference.action.actionMap.name +
                                     "/" + reference.action.name
                    )
                    .ToDictionary(
                        group => group.Key,
                        group => group.First()
                    );


            // ----- copy the desktop scene -----

            Scene source =
                EditorSceneManager.OpenScene(
                    SourceScenePath,
                    OpenSceneMode.Single
                );

            EditorSceneManager.SaveScene(
                source,
                TargetScenePath,
                true
            );

            Scene scene =
                EditorSceneManager.OpenScene(
                    TargetScenePath,
                    OpenSceneMode.Single
                );

            report.AppendLine(
                $"Copied {SourceScenePath} → {TargetScenePath}."
            );


            // ----- desktop camera out -----

            foreach (DesktopCameraController controller in
                     Object.FindObjectsByType<DesktopCameraController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                report.AppendLine(
                    $"Removed desktop camera '{controller.name}'."
                );

                Object.DestroyImmediate(
                    controller.gameObject
                );
            }


            // ----- XR rig -----

            var rig =
                (GameObject)PrefabUtility.InstantiatePrefab(
                    rigPrefab,
                    scene
                );

            Camera xrCamera =
                rig.GetComponentInChildren<Camera>(true);


            // The table stands on the real floor, so the eye height must
            // be the user's real head height above it. The rig's default
            // (Not Specified + a fixed camera offset) lets the runtime
            // pick a head-relative origin instead.
            rig.GetComponent<XROrigin>().RequestedTrackingOriginMode =
                XROrigin.TrackingOriginMode.Floor;

            report.AppendLine(
                "XR Origin tracking origin: Floor."
            );


            // The table is fixed and users walk around it: no
            // artificial movement of any kind.
            foreach (Transform child in rig.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Locomotion" ||
                    child.name.Contains("Teleport"))
                {
                    child.gameObject.SetActive(false);

                    report.AppendLine(
                        $"Disabled rig object '{child.name}'."
                    );
                }
            }


            // XRI line visuals are sized in world units (wrong on
            // the scaled rig); XRControllerPointer draws instead.
            foreach (CurveVisualController visual in
                     rig.GetComponentsInChildren<CurveVisualController>(true))
            {
                visual.enabled =
                    false;
            }


            // The starter rig's poke-point affordances have no renderer
            // wired (m_Renderer is null), so they throw a
            // NullReferenceException on every tween. Visual feedback
            // only; poke interaction itself stays on.
            foreach (Transform child in rig.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Poke Point Affordances")
                {
                    child.gameObject.SetActive(false);

                    report.AppendLine(
                        $"Disabled rig object '{child.parent.parent.name}/{child.name}'."
                    );
                }
            }


            NearFarInteractor right =
                FindInteractor(rig, InteractorHandedness.Right);

            NearFarInteractor left =
                FindInteractor(rig, InteractorHandedness.Left);


            // ----- materials -----

            EnsureFolder(MaterialFolder);

            Material tableMaterial =
                EnsureMaterial(
                    "VRTable",
                    "Universal Render Pipeline/Lit",
                    new Color(0.16f, 0.17f, 0.19f)
                );

            Material floorMaterial =
                EnsureMaterial(
                    "VRFloor",
                    "Universal Render Pipeline/Lit",
                    new Color(0.32f, 0.33f, 0.35f)
                );

            Material rayMaterial =
                EnsureMaterial(
                    "VRPointerRay",
                    "UrbanAnalytics/VertexColorUnlit",
                    Color.white
                );

            Material reticleMaterial =
                EnsureMaterial(
                    "VRPointerReticle",
                    "Universal Render Pipeline/Unlit",
                    new Color(1.0f, 0.83f, 0.0f)
                );

            Material handMaterial =
                EnsureMaterial(
                    "VRHand",
                    "Universal Render Pipeline/Lit",
                    new Color(0.80f, 0.82f, 0.86f)
                );


            // ----- table -----

            Transform cityRoot =
                GameObject.Find("CityRoot")?.transform;

            var table =
                new GameObject("VRTable");

            TabletopRig tabletopRig =
                table.AddComponent<TabletopRig>();

            SetFields(
                tabletopRig,
                ("xrOrigin", rig.transform),
                ("xrCamera", xrCamera),
                ("cityRoot", cityRoot),
                ("tableMaterial", tableMaterial),
                ("floorMaterial", floorMaterial)
            );


            // ----- pointer -----

            var pointerObject =
                new GameObject("VRPointer");

            var pointer =
                pointerObject.AddComponent<XRControllerPointer>();

            var pointerSerialized =
                new SerializedObject(pointer);

            SerializedProperty handsProperty =
                pointerSerialized.FindProperty("hands");

            handsProperty.arraySize =
                2;

            SetHand(
                handsProperty.GetArrayElementAtIndex(0),
                "Right",
                right,
                actions["XRI Right Interaction/Activate"],
                actions["XRI Right Interaction/Select"]
            );

            SetHand(
                handsProperty.GetArrayElementAtIndex(1),
                "Left",
                left,
                actions["XRI Left Interaction/Activate"],
                actions["XRI Left Interaction/Select"]
            );

            pointerSerialized.FindProperty("rayMaterial").objectReferenceValue =
                rayMaterial;

            pointerSerialized.FindProperty("reticleMaterial").objectReferenceValue =
                reticleMaterial;

            pointerSerialized.ApplyModifiedPropertiesWithoutUndo();


            // ----- VR UI: wrist menu (left wrist), hover label, table
            //       board and the controller shortcuts -----

            Transform leftController =
                FindControllerRoot(left, "Left Controller");

            var vrUi =
                new GameObject("VRUI");

            Texture2D controlsImage =
                AssetDatabase.LoadAssetAtPath<Texture2D>(ControlsImagePath);

            if (controlsImage == null)
            {
                report.AppendLine(
                    $"WARNING: {ControlsImagePath} missing (run " +
                    "Tools/make_vr_controls_image.ps1); Help tab shows text only."
                );
            }

            SetFields(
                vrUi.AddComponent<XRHandMenu>(),
                ("anchor", leftController),
                ("eventCamera", xrCamera),
                ("pointer", pointer),
                ("controlsImage", controlsImage)
            );

            SetFields(
                vrUi.AddComponent<XRHoverLabel>(),
                ("viewCamera", xrCamera)
            );

            XRTableBoard tableBoard =
                vrUi.AddComponent<XRTableBoard>();

            SetFields(
                tableBoard,
                ("tabletopRig", tabletopRig),
                ("viewCamera", xrCamera)
            );

            SetFields(
                vrUi.AddComponent<XRControllerShortcuts>(),
                ("tabletopRig", tabletopRig),
                ("pointer", pointer),
                ("tableBoard", tableBoard)
            );

            SetFields(
                vrUi.AddComponent<XRTableMover>(),
                ("controller", leftController),
                ("viewCamera", xrCamera),
                ("tabletopRig", tabletopRig),
                ("pointer", pointer)
            );

            report.AppendLine(
                $"Wrist menu anchored to '{leftController?.name}'; " +
                "table board, controller shortcuts and table mover added."
            );


            // ----- virtual hands (replace the controller models) -----

            Transform rightController =
                FindControllerRoot(right, "Right Controller");

            XRVirtualHands virtualHands =
                vrUi.AddComponent<XRVirtualHands>();

            SetFields(
                virtualHands,
                ("leftController", leftController),
                ("rightController", rightController),
                ("handMaterial", handMaterial)
            );

            var handsSerialized =
                new SerializedObject(virtualHands);

            SerializedProperty visuals =
                handsSerialized.FindProperty("controllerVisuals");

            Transform[] visualRoots =
            {
                leftController != null ? leftController.Find("Left Controller Visual") : null,
                rightController != null ? rightController.Find("Right Controller Visual") : null
            };

            visuals.arraySize =
                visualRoots.Length;

            for (int i = 0; i < visualRoots.Length; i++)
            {
                visuals.GetArrayElementAtIndex(i).objectReferenceValue =
                    visualRoots[i] != null
                        ? visualRoots[i].gameObject
                        : null;
            }

            handsSerialized.ApplyModifiedPropertiesWithoutUndo();

            report.AppendLine(
                $"Virtual hands on '{leftController?.name}' and " +
                $"'{rightController?.name}'; controller models hidden."
            );


            // The PC monitor's help line: only the facilitator's keys
            // (no mouse camera or mouse picking in VR).
            DesktopInteractionUI desktopUi =
                Object.FindFirstObjectByType<DesktopInteractionUI>();

            if (desktopUi != null)
            {
                var desktopSerialized =
                    new SerializedObject(desktopUi);

                desktopSerialized.FindProperty("helpTextOverride").stringValue =
                    FacilitatorHelpText;

                desktopSerialized.ApplyModifiedPropertiesWithoutUndo();
            }


            // ----- simulator fallback (Editor only) -----

            var devTools =
                new GameObject("VRDevTools");

            SetFields(
                devTools.AddComponent<XRSimulatorFallback>(),
                ("simulatorPrefab", simulatorPrefab)
            );


            // ----- wire the existing systems -----

            InteractionManager interaction =
                Object.FindFirstObjectByType<InteractionManager>();

            SetFields(
                interaction,
                ("interactionCamera", xrCamera),
                ("cameraController", null),
                ("pointerSource", pointer)
            );


            StudySession study =
                Object.FindFirstObjectByType<StudySession>();

            if (study != null)
            {
                SetFields(
                    study,
                    ("viewCamera", xrCamera)
                );

                var studySerialized =
                    new SerializedObject(study);

                studySerialized.FindProperty("condition").stringValue =
                    "vr";

                studySerialized.ApplyModifiedPropertiesWithoutUndo();
            }


            // UI: XR input module (also keeps mouse input, so the
            // facilitator can use the desktop panels on the monitor).
            EventSystem eventSystem =
                Object.FindFirstObjectByType<EventSystem>();

            if (eventSystem != null)
            {
                InputSystemUIInputModule desktopModule =
                    eventSystem.GetComponent<InputSystemUIInputModule>();

                if (desktopModule != null)
                {
                    Object.DestroyImmediate(desktopModule);
                }


                if (eventSystem.GetComponent<XRUIInputModule>() == null)
                {
                    eventSystem.gameObject.AddComponent<XRUIInputModule>();
                }
            }


            EditorSceneManager.MarkSceneDirty(scene);

            EditorSceneManager.SaveScene(scene);


            AddToBuildSettings(TargetScenePath);


            report.AppendLine(
                $"Rig: camera '{xrCamera?.name}', right interactor " +
                $"'{right?.name}', left interactor '{left?.name}'."
            );

            report.AppendLine(
                $"InteractionManager pointer → VRPointer; " +
                $"StudySession condition = vr. Saved {TargetScenePath}."
            );


            return report.ToString();
        }


        // =========================================================
        // HELPERS
        // =========================================================

        private static T Require<T>(
            string path
        )
            where T : Object
        {
            T asset =
                AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
            {
                throw new FileNotFoundException(
                    $"VRSceneBuilder: '{path}' not found. Import the " +
                    "XRI samples 'Starter Assets' and 'XR Device " +
                    "Simulator' (Package Manager → XR Interaction " +
                    "Toolkit → Samples).",
                    path
                );
            }

            return asset;
        }


        private static NearFarInteractor FindInteractor(
            GameObject rig,
            InteractorHandedness handedness
        )
        {
            return rig
                .GetComponentsInChildren<NearFarInteractor>(true)
                .FirstOrDefault(
                    interactor => interactor.handedness == handedness
                );
        }


        /// <summary>
        /// The tracked controller object above an interactor (named
        /// "Left Controller" / "Right Controller" in the XRI rig).
        /// </summary>
        private static Transform FindControllerRoot(
            NearFarInteractor interactor,
            string name
        )
        {
            if (interactor == null)
            {
                return null;
            }

            for (Transform current = interactor.transform;
                 current != null;
                 current = current.parent)
            {
                if (current.name == name)
                {
                    return current;
                }
            }

            return interactor.transform.parent;
        }


        private static void SetHand(
            SerializedProperty hand,
            string name,
            NearFarInteractor interactor,
            InputActionReference select,
            InputActionReference modifier
        )
        {
            hand.FindPropertyRelative("name").stringValue =
                name;

            hand.FindPropertyRelative("interactor").objectReferenceValue =
                interactor;

            hand.FindPropertyRelative("rayOrigin").objectReferenceValue =
                interactor != null
                    ? interactor.transform
                    : null;

            SetActionReference(
                hand.FindPropertyRelative("selectAction"),
                select
            );

            SetActionReference(
                hand.FindPropertyRelative("blockModifierAction"),
                modifier
            );
        }


        private static void SetActionReference(
            SerializedProperty property,
            InputActionReference reference
        )
        {
            property.FindPropertyRelative("m_UseReference").boolValue =
                true;

            property.FindPropertyRelative("m_Reference").objectReferenceValue =
                reference;
        }


        private static void SetFields(
            Object target,
            params (string field, Object value)[] fields
        )
        {
            var serialized =
                new SerializedObject(target);

            foreach ((string field, Object value) in fields)
            {
                SerializedProperty property =
                    serialized.FindProperty(field);

                if (property == null)
                {
                    throw new System.MissingFieldException(
                        $"VRSceneBuilder: {target.GetType().Name} " +
                        $"has no serialized field '{field}'."
                    );
                }

                property.objectReferenceValue =
                    value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }


        private static void EnsureFolder(
            string folder
        )
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent =
                Path.GetDirectoryName(folder)?.Replace('\\', '/');

            EnsureFolder(parent);

            AssetDatabase.CreateFolder(
                parent,
                Path.GetFileName(folder)
            );
        }


        private static Material EnsureMaterial(
            string name,
            string shaderName,
            Color color
        )
        {
            string path =
                $"{MaterialFolder}/{name}.mat";

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material != null)
            {
                return material;
            }

            Shader shader =
                Shader.Find(shaderName) ??
                throw new MissingReferenceException(
                    $"VRSceneBuilder: shader '{shaderName}' not found."
                );

            material =
                new Material(shader)
                {
                    color = color
                };

            if (material.HasProperty("_Tint"))
            {
                material.SetColor("_Tint", color);
            }

            AssetDatabase.CreateAsset(
                material,
                path
            );

            return material;
        }


        private static void AddToBuildSettings(
            string path
        )
        {
            List<EditorBuildSettingsScene> scenes =
                EditorBuildSettings.scenes.ToList();

            if (scenes.Any(scene => scene.path == path))
            {
                return;
            }

            // Added but disabled: enable it for a VR player build.
            scenes.Add(
                new EditorBuildSettingsScene(
                    path,
                    false
                )
            );

            EditorBuildSettings.scenes =
                scenes.ToArray();
        }
    }
}
