using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

//This work is licensed under the Creative Commons Attribution-NonCommercial 2.0 License. 
//To view a copy of the license, visit https://creativecommons.org/licenses/by-nc/2.0/legalcode

//Made by Dreadrith#3238
//Discord: https://discord.gg/ZsPfrGn
//Github: https://github.com/Dreadrith/DreadScripts
//Gumroad: https://gumroad.com/dreadrith
//Ko-fi: https://ko-fi.com/dreadrith

namespace DreadScripts.PhysBoneConverter
{
    public class PhysBoneConverter : EditorWindow
    {
        #region Automated
        private static readonly Dictionary<VRCPhysBoneCollider, DynamicBoneColliderBase> colliderDictionary = new Dictionary<VRCPhysBoneCollider, DynamicBoneColliderBase>();
        private static readonly AnimationCurve AngleToStiffnessCurve = SmoothCurveTangents(new AnimationCurve(
            new Keyframe(0, 1),
            new Keyframe(60, 0.5f),
            new Keyframe(105, 0.2f),
            new Keyframe(130, 0.1f),
            new Keyframe(180, 0)));

        private static readonly GUIContent[] names =
        {
            new GUIContent("Damping","How much the bones get slowed down."),
            new GUIContent("Elasticity","How much force is applied to return each bone to its original orientation."),
            new GUIContent("Stiffness", "How much the bone's original orientation gets preserved."),
            new GUIContent("Inert", "How much the character's position change is ignored in physics simulation."),
            new GUIContent("Friction", "How much the bones get slowed down when colliding.")
        };

        private static int optionsCount;
        private static FieldInfo frictionField;
        private static FieldInfo frictionDistribField;
        #endregion

        #region Input
        public static GameObject targetRoot;
        public static bool makeBackup = true;
        public static bool[] bools = {true,true,true,true,true};
        public static float[] floats = {0.2f, 0.2f, 0, 0, 0};
        public static AnimationCurve[] curves = {new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve()};
        #endregion

        #region GUI
        [MenuItem("DreadTools/Utility/PhysBone Converter")]
        public static void ShowWindow() => GetWindow<PhysBoneConverter>("PhysBone Converter");
        

        public void OnGUI()
        {
            EditorGUIUtility.labelWidth = 90;

            using (new GUILayout.HorizontalScope(GUI.skin.box))
                targetRoot = (GameObject) EditorGUILayout.ObjectField(Helper.Content.root, targetRoot, typeof(GameObject), true);

            for (int i = 0; i < optionsCount; i++)
                DrawFloatOption(i);

            using (new EditorGUI.DisabledScope(!targetRoot))
                if (GUILayout.Button("Convert", Helper.Styles.toolbarButton))
                    Helper.ReportCall(StartConversion);


            EditorGUIUtility.labelWidth = 0;

            Helper.Credit();
        }

        private static void DrawFloatOption(int index)
        {
            using (new GUILayout.HorizontalScope(GUI.skin.box))
            {
                EditorGUILayout.PrefixLabel(names[index]);
                if (!bools[index])
                {
                    floats[index] = EditorGUILayout.Slider(floats[index], 0f, 1f);
                    curves[index] = EditorGUILayout.CurveField(curves[index], GUILayout.Width(70));
                }
                else GUILayout.FlexibleSpace();

                var ogColor = GUI.backgroundColor;
                GUI.backgroundColor = bools[index] ? Color.green : Color.red;
                bools[index] = GUILayout.Toggle(bools[index], "Auto", GUI.skin.button, GUILayout.ExpandWidth(false));
                GUI.backgroundColor = ogColor;
            }
        }

        private void OnEnable()
        {
            targetRoot = targetRoot ?? FindObjectOfType<VRCAvatarDescriptor>()?.gameObject ?? FindObjectOfType<Animator>()?.gameObject;
            frictionField = typeof(DynamicBone).GetField("m_Friction");
            frictionDistribField = typeof(DynamicBone).GetField("m_FrictionDistrib");
            optionsCount = frictionField == null ? 4 : 5;
        }
        #endregion

        #region Main
        public static void StartConversion()
        {
            if (!targetRoot) return;
            colliderDictionary.Clear();
            var pbBones = targetRoot.GetComponentsInChildren<VRCPhysBone>(true);
            var pbColliders = targetRoot.GetComponentsInChildren<VRCPhysBoneCollider>(true);
            if (pbBones.Length == 0 && pbColliders.Length == 0)
            {
                Helper.GreenLog("No Physbones or colliders to convert.");
                return;
            }

            if (makeBackup)
            {
                var t = targetRoot.transform;
                var backup = Instantiate(targetRoot, t.position, t.rotation, t.parent);
                backup.name = backup.name.Replace(" (Backup)", string.Empty).Replace("(Clone)", " (Backup)");
                backup.SetActive(false);
            }

            foreach (var c in pbColliders) ConvertCollider(c);

            //1 to 1 Replacement is rarely incorrect.
            //In cases where there are multiple children of the PB and it's set to ignore, then each child needs its own DBone.
            foreach (var pb in pbBones) ConvertPhysBone(pb);


            foreach (var c in pbColliders) DestroyImmediate(c);
            foreach (var pb in pbBones) DestroyImmediate(pb);

            Helper.GreenLog($"Finished conversion! Bones: {pbBones.Length} | Colliders: {pbColliders.Length}");
        }

        private static void ConvertCollider(VRCPhysBoneCollider pbc)
        {
            Transform rootTransform = pbc.GetRootTransform();
            string collidersName = $"{rootTransform.name} Colliders";
            GameObject colliderParent = rootTransform.Find(collidersName)?.gameObject;
            if (!colliderParent)
            {
                colliderParent = new GameObject(collidersName)
                {
                    transform =
                    {
                        parent = rootTransform,
                        localPosition = Vector3.zero,
                        localRotation = Quaternion.identity,
                        localScale = Vector3.one
                    }
                };
            }

            GameObject colliderTarget = new GameObject("Collider")
            {
                transform =
                {
                    parent = colliderParent.transform,
                    localPosition = pbc.position,
                    localRotation = pbc.rotation,
                    localScale = Vector3.one
                }
            };
            GameObjectUtility.EnsureUniqueNameForSibling(colliderTarget);

            bool isPlane = pbc.shapeType == VRC.Dynamics.VRCPhysBoneColliderBase.ShapeType.Plane;

            DynamicBoneColliderBase baseCollider;
            if (isPlane) baseCollider = colliderTarget.AddComponent<DynamicBonePlaneCollider>();
                else baseCollider = colliderTarget.AddComponent<DynamicBoneCollider>();


            baseCollider.m_Bound = (DynamicBoneColliderBase.Bound) (pbc.insideBounds ? 1 : 0);
            baseCollider.m_Center = Vector3.zero;
            baseCollider.m_Direction = DynamicBoneColliderBase.Direction.Y;

            if (!isPlane)
            {
                var collider = (DynamicBoneCollider)baseCollider;
                collider.m_Radius = pbc.radius;
                collider.m_Height = pbc.height;
            }

            colliderDictionary.Add(pbc, baseCollider);
        }

        private static void ConvertPhysBone(VRCPhysBone pb)
        {
            var dbone = pb.gameObject.AddComponent<DynamicBone>();
            dbone.m_Root = pb.GetRootTransform();
            float scaleFactor = (Mathf.Abs(dbone.transform.lossyScale.x / dbone.m_Root.lossyScale.x));

            
            dbone.m_Damping = bools[0] ? 1 - pb.spring : floats[0];
            dbone.m_DampingDistrib = bools[0] ? InvertCurve(pb.springCurve) : curves[0];
            dbone.m_Elasticity = bools[1] ? pb.pull : floats[1];
            dbone.m_ElasticityDistrib = bools[1] ? pb.pullCurve : curves[1];
            dbone.m_Inert = bools[3] ? pb.immobile : floats[3];
            dbone.m_InertDistrib = bools[3] ? pb.immobileCurve : curves[3];
            if (!bools[4])
            {
                frictionField.SetValue(dbone, floats[4]);
                frictionDistribField.SetValue(dbone, curves[4]);
            }

            dbone.m_Radius = pb.radius / scaleFactor;
            dbone.m_RadiusDistrib = pb.radiusCurve;
            dbone.m_Gravity = new Vector3(0, -pb.gravity / 10f, 0);


            //Better Limit conversion, such as Hinge to Freeze Axis, can be done, but not all possibilities with PhysBone limits can be achieved.
           if (bools[2])
           {
               dbone.m_Stiffness = 0;
               if (pb.limitType == VRC.Dynamics.VRCPhysBoneBase.LimitType.Angle)
               {
                   bool hasAnglecurve = pb.maxAngleXCurve != null && pb.maxAngleXCurve.keys.Length > 0;
                   dbone.m_Stiffness = hasAnglecurve ? 1 : AngleToStiffnessCurve.Evaluate(pb.maxAngleX);

                   dbone.m_StiffnessDistrib = SmoothCurveTangents(DeriveCurve(SubdivideCurve(pb.maxAngleXCurve, 10, false), k =>
                   {
                       k.value = AngleToStiffnessCurve.Evaluate(k.value * 180);
                       return k;
                   }));
               }
           }
           else
           {
               dbone.m_Stiffness = floats[2];
               dbone.m_StiffnessDistrib = curves[2];
           }

            dbone.m_Exclusions = pb.ignoreTransforms;
            dbone.m_Colliders = pb.colliders.Cast<VRCPhysBoneCollider>()
                .Where(pbc => pbc && colliderDictionary.ContainsKey(pbc))
                .Select(pbc => colliderDictionary[pbc]).ToList();


            if (pb.endpointPosition != Vector3.zero)
            {
                foreach (var t in dbone.m_Root.GetComponentsInChildren<Transform>().Where(t => t.childCount == 0 && !IsExcluded(t, dbone.m_Exclusions)))
                {
                    GameObjectUtility.EnsureUniqueNameForSibling(new GameObject($"{t.name} EndBone")
                    {
                        transform =
                        {
                            parent = t,
                            position = t.TransformPoint(pb.endpointPosition),
                            localRotation = Quaternion.identity,
                            localScale = Vector3.one,
                        }
                    });
                }
            }

            //Dunno if this optimizes anything in-game but aye better than nothing
            dbone.m_DistantDisable = true;
            dbone.m_ReferenceObject = targetRoot.transform;
        }

        private static bool IsExcluded(Transform t, IEnumerable<Transform> exclusions) => exclusions.Any(e => e != null && t.IsChildOf(e));
        #endregion

        #region Curve Functions
        private static AnimationCurve SmoothCurveTangents(AnimationCurve curve)
        {
            if (curve == null) return null;
            for (int i = 0; i < curve.keys.Length; i++)
                curve.SmoothTangents(i, 0);
            return curve;
        }
        private static AnimationCurve DeriveCurve(AnimationCurve curve, Func<Keyframe, Keyframe> KeyFunc)
        {
            if (curve == null) return null;
            int length = curve.keys.Length;

            var newKeys = new Keyframe[length];
            for (int i = 0; i < length; i++)
                newKeys[i] = KeyFunc(curve.keys[i]);

            return new AnimationCurve(newKeys);
        }
        private static AnimationCurve InvertCurve(AnimationCurve curve) =>
            DeriveCurve(curve, k =>
            {
                k.value = -k.value;
                k.inTangent = -k.inTangent;
                k.outTangent = -k.outTangent;
                return k;
            });
        private static AnimationCurve SubdivideCurve(AnimationCurve curve, int keyLimit = 10, bool smoothCurve = true)
        {
            if (curve == null) return null;
            if (curve.length <= 1) return curve;
            List<Keyframe> keys = new List<Keyframe>(curve.keys);
            if (keys.Count > 1)
            {
                while (keys.Count < keyLimit)
                {
                    var currentKeys = keys.OrderBy(k => k.time).ToList();
                    for (int i = 0; i < currentKeys.Count - 1 && keys.Count < keyLimit; i++)
                    {
                        var time = (currentKeys[i].time + currentKeys[i + 1].time) / 2;
                        keys.Add(new Keyframe(time, curve.Evaluate(time)));
                    }
                }
            }


            var newCurve = new AnimationCurve(keys.ToArray());
            if (smoothCurve) SmoothCurveTangents(newCurve);
            return newCurve;
        }
        private static void LogCurve(AnimationCurve curve)
        {
           Debug.Log(string.Join("\n", curve.keys.Select(k => k.time).Zip(curve.keys.Select(k => k.value), (t, v) => $"Time: {t} | Value: {v}")));
        }
        #endregion
    }
    public static class Helper
    {
        
        internal static void ReportCall(Action action)
        {
            try { action(); }
            catch (Exception e)
            {
                string errorMSG =  $"{e.Message}\n\n{e.StackTrace}";
                if (EditorUtility.DisplayDialog("Error!", $"An unexpected error has occured and execution was halted. Please Press Copy and report this stack trace to Dreadrith#3238\n~~~~~~~~~~~~\n{errorMSG}", "Copy", "Nah"))
                    EditorGUIUtility.systemCopyBuffer = e.StackTrace;
                throw;
            }
        }

        internal static void Credit()
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Made By Dreadrith#3238", "boldlabel"))
                    Application.OpenURL("https://linktr.ee/Dreadrith");
            }
        }

        private const string logName = "PBConverter";
        internal static void GreenLog(string msg) => 
            Debug.Log($"<color=green>[{logName}]</color> {msg}");

        public static class Styles
        {
            public static readonly GUIStyle toolbarButton = GUI.skin.GetStyle("toolbarbutton");
            public static readonly GUIStyle box = GUI.skin.GetStyle("box");
        }

        public static class Content
        {
            public static readonly GUIContent root = new GUIContent("Root", "The Root. Will convert all the PhysBones and Colliders under this root.");
        }

    }

}
