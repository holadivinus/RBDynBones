#if UNITY_EDITOR
using NoodledEvents;
using NoodledEvents.Assets.Noodled_Events;
using SLZ.Bonelab;
using SLZ.Marrow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UltEvents;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UI;

public class PhysBoneMaker : MonoBehaviour
{
    public static string BaseFolder => InPackage() ? "Packages/com.holadivinus.rbdynbones/" : "Assets/RBDynBones/";
    public static string EditorFolder => BaseFolder + "Editor/";
    private static bool InPackage()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
        return packageInfo != null;
    }


    [Header("Maker Settings")]
    public int MaxSubJoints = -1;
    public float ColliderScale = 1;
    public AnimationCurve ColliderCurve = AnimationCurve.Linear(0,0,1,1);
    public float Elasticity = .05f;
    public float VisualSmoothing = .05f;
    public MirrorSyncMode MirrorRotationSyncMode = MirrorSyncMode.Tweened;

    [Header("Physics Settings")]
    public float Drag = 3f;
    public float Mass = .01f;
    public Vector3 ConstantForce = Vector3.zero;

    public RigSegment RigInheritance = RigSegment.Nope;
    public float RigInheritanceFactor = 10f;


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        List<Transform> bones = GetBones();
        for (int i = 0; i < bones.Count-1; i++)
        {
            Transform boneA = bones[i];
            Transform boneB = bones[i+1];
            Gizmos.DrawLine(boneA.position, boneB.position);

        }
        Gizmos.color = Color.green;
        for (int i = 1; i < bones.Count; i++)
        {
            Transform bone = bones[i];
            Gizmos.DrawWireSphere(bone.position, ColliderCurve.Evaluate((i-1) / (float)(bones.Count - 1)) * ColliderScale);
        }
    }

    private List<Transform> GetBones()
    {
        List<Transform> o = new() { transform };
        Transform cur = transform;
        int sj = MaxSubJoints;
        while (sj > 0 || sj == -1)
        {
            if (sj != -1)
                sj--;
            if (cur.childCount == 0) break;
            Transform nexChil = cur.GetChild(0);
            cur = nexChil;
            o.Add(cur);
        }
        return o;
    }

    [ContextMenu("Finalize for Bonelab")]
    public void MakeIntoPhys()
    {
        foreach (var item in GetComponentsInChildren<ParentConstraint>())
            DestroyImmediate(item);

        List<Transform> bones = GetBones();
        if (bones.Count <= 1) return;

        

        // Section for Physics Root with chain/bone

        var oldPhys = bones[0].transform.parent.Find(bones[0].gameObject.name + "_PhysicsChain");
        if (oldPhys)
            DestroyImmediate(oldPhys.gameObject);

        GameObject physicsRoot = new GameObject(bones[0].gameObject.name + "_PhysicsChain");
        physicsRoot.transform.SetParent(bones[0].transform.parent, false);
        physicsRoot.transform.localPosition = bones[0].transform.localPosition;
        physicsRoot.transform.localRotation = bones[0].transform.localRotation;
        physicsRoot.transform.localScale = bones[0].transform.localScale;

        Rigidbody bonePhysParent = physicsRoot.gameObject.AddComponent<Rigidbody>();
        bonePhysParent.isKinematic = true;
        bonePhysParent.useGravity = false;

        var avi = GetComponentInParent<SLZ.VRMK.Avatar>();
        bool isOnHead = false;
        if (avi)
        {
            var headTransf = avi.animator.GetBoneTransform(HumanBodyBones.Head);
            if (headTransf.GetComponentsInChildren<PhysBoneMaker>().Contains(this))
            {
                // we are on head :(
                isOnHead = true; // disables smoothfollow
                
                // add logic to push bones back to where head would've kept them
                var fixxer = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BaseFolder + "BoneLocalPosFixxer.prefab"));
                fixxer.transform.SetParent(physicsRoot.transform);

                var ht = fixxer.transform.Find("VARS/gobj_Object_var_HeadTransform").GetComponents<Component>().First(c => c is not Transform);
                var blxrProp = ht.GetType().GetProperty("interactorSource", (BindingFlags)60);
                blxrProp.SetValue(ht, headTransf);

                var bt = fixxer.transform.Find("VARS/gobj_Object_var_BoneTransform").GetComponents<Component>().First(c => c is not Transform);
                blxrProp.SetValue(bt, physicsRoot.transform); 

                var lp = fixxer.transform.Find("VARS/gobj_Vector3_var_HeadOrigLocPos").GetComponent<PositionConstraint>();
                lp.translationOffset = headTransf.localPosition;

                var bp = fixxer.transform.Find("VARS/gobj_Vector3_var_BoneOrigLocPos").GetComponent<PositionConstraint>();
                bp.translationOffset = physicsRoot.transform.localPosition;
            }
        }


        // remove in mirror logic
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(BaseFolder + "RemoveInMirror.prefab").GetComponent<LifeCycleEvents>();
            var lce = physicsRoot.AddComponent<LifeCycleEvents>();
            lce.EnableEvent.CopyFrom(template.EnableEvent);
            lce.EnableEvent.PersistentCallsList[0].PersistentArguments[0].Object = lce.gameObject;
            lce.EnableEvent.PersistentCallsList[3].FSetTarget(lce.gameObject);
        }

        // Section for Anims Root; aka where the phys items tryto goto

        var oldAnimRig = bones[0].transform.parent.Find(bones[0].gameObject.name + "_AnimRig");
        if (oldAnimRig)
            DestroyImmediate(oldAnimRig.gameObject);

        GameObject animsRoot = new GameObject(bones[0].gameObject.name + "_AnimRig");
        animsRoot.transform.SetParent(bones[0].transform.parent, false);
        animsRoot.transform.localPosition = bones[0].transform.localPosition;
        animsRoot.transform.localRotation = bones[0].transform.localRotation;
        animsRoot.transform.localScale = bones[0].transform.localScale;

        var animsRootEndPoint = new GameObject(bones[0].gameObject.name + "_animer_endpoint");
        animsRootEndPoint.transform.SetParent(animsRoot.transform);
        animsRootEndPoint.transform.position = bones[1].transform.position;
        animsRootEndPoint.transform.localRotation = Quaternion.identity;
        animsRootEndPoint.transform.localScale = Vector3.one;

        List<Transform> animBones = new List<Transform>() { animsRoot.transform };
        List<Transform> animerEndpoints = new List<Transform>() { animsRootEndPoint.transform };

        for (int i = 1; i < bones.Count - 1; i++)
        {
            Transform boneA = bones[i];
            Transform boneB = bones[i + 1];

            Transform lastAnimBone = animBones[animBones.Count - 1];
            var animBone = new GameObject(boneA.gameObject.name + "_animer");
            animBone.transform.SetParent(lastAnimBone);
            animBone.transform.localPosition = boneA.transform.localPosition;
            animBone.transform.localRotation = boneA.transform.localRotation;
            animBone.transform.localScale = boneA.transform.localScale;
            animBones.Add(animBone.transform);

            var farOutPos = new GameObject(boneA.gameObject.name + "_animer_endpoint");
            farOutPos.transform.SetParent(animBone.transform);
            farOutPos.transform.position = boneB.transform.position;
            farOutPos.transform.localRotation = Quaternion.identity;
            farOutPos.transform.localScale = Vector3.one;
            animerEndpoints.Add(farOutPos.transform);
        }

        // actually make RB's and joints loop
        List<CharacterJoint> physBalls = new List<CharacterJoint>();
        for (int i = 0; i < bones.Count - 1; i++)
        {
            Transform boneA = bones[i];
            Transform boneB = bones[i + 1];

            GameObject newPhyser = new GameObject(boneA.name + "_PhysObj");
            newPhyser.transform.SetParent(boneA.transform, false);
            newPhyser.transform.position = boneB.transform.position;
            newPhyser.transform.localRotation = Quaternion.identity;
            newPhyser.transform.SetParent(physicsRoot.transform, true);
            newPhyser.transform.localScale = Vector3.one;
            var newRB = newPhyser.AddComponent<Rigidbody>();

            float colScale = ColliderCurve.Evaluate(i / (float)(bones.Count - 1)) * ColliderScale;
            colScale = newPhyser.transform.InverseTransformPoint((newPhyser.transform.position + (Vector3.up * colScale))).magnitude;
            newPhyser.AddComponent<SphereCollider>().radius = colScale;
            var newJ = newPhyser.AddComponent<CharacterJoint>();
            newJ.connectedBody = bonePhysParent;
            newJ.anchor = newPhyser.transform.InverseTransformPoint(bonePhysParent.transform.position);
            physBalls.Add(newJ);

            {
                var lim = newJ.lowTwistLimit;
                lim.limit = 0;
                newJ.lowTwistLimit = lim;
            }
            {
                var lim = newJ.highTwistLimit;
                lim.limit = 0;
                newJ.highTwistLimit = lim;
            }
            {
                var lim = newJ.swing1Limit;
                lim.limit = 0;
                newJ.swing1Limit = lim;
            }
            {
                var lim = newJ.swing2Limit;
                lim.limit = 0;
                newJ.swing2Limit = lim;
            }
            {
                var spr = newJ.twistLimitSpring;
                spr.spring = 3;
                spr.damper = 1;
                newJ.twistLimitSpring = spr;
            }
            {
                var spr = newJ.swingLimitSpring;
                spr.spring = 3;
                spr.damper = 1;
                newJ.swingLimitSpring = spr;
            }
            newJ.enableProjection = true;

            // setup of elasticity enforcement
            if (RigInheritance != RigSegment.Nope)
            {
                var attachedEnforcer = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BaseFolder + "RBAttachedElasticityEnforcer.prefab"));
                attachedEnforcer.transform.SetParent(newPhyser.transform);

                var man = attachedEnforcer.GetComponent<VarMan>();
                man.Vars.First(v => v.Name == "MyRigidBody").DefaultObject = newRB;
                man.Vars.First(v => v.Name == "MyTransform").DefaultObject = newRB.transform;
                man.Vars.First(v => v.Name == "MyAnimTransform").DefaultObject = animerEndpoints[i];
                man.Vars.First(v => v.Name == "Elasticity").DefaultFloatValue = Elasticity;
                man.Vars.First(v => v.Name == "DerivedMotionFactor").DefaultFloatValue = -RigInheritanceFactor;
                man.Vars.First(v => v.Name == "AviRootTransform").DefaultObject = avi.transform;
                man.Vars.First(v => v.Name == "AttachedRBPath").DefaultStringValue = $"../PhysicsRig/{GetColName(RigInheritance)}";
                man.Vars.First(v => v.Name == "ParentBoneTransform").DefaultObject = bonePhysParent.transform;
                man.Vars.First(v => v.Name == "PhysBoneMaker").DefaultObject = this;
                
                attachedEnforcer.GetComponent<SerializedBowl>().NodeDatas.First(nd => nd.Name == "Vector3.op_Addition Marked").DataInputs[1].DefaultVector3Value = ConstantForce;
                attachedEnforcer.GetComponent<SerializedBowl>().NodeDatas.First(nd => nd.Name == "Transform.TransformPoint Marked").DataInputs[1].DefaultVector3Value = newJ.anchor;
                attachedEnforcer.GetComponent<SerializedBowl>().NodeDatas.First(nd => nd.Name == "Transform.TransformPoint EditorMarked").DataInputs[1].DefaultVector3Value = newJ.anchor;
                attachedEnforcer.GetComponent<SerializedBowl>().NodeDatas.First(nd => nd.Name == "Vector3.op_Addition EditorMarked").DataInputs[1].DefaultVector3Value = ConstantForce;
                
                ApplyVars(man);
            }
            else
            {
                var attachedEnforcer = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BaseFolder + "RBElasticityEnforcer.prefab"));
                attachedEnforcer.transform.SetParent(newPhyser.transform);

                var man = attachedEnforcer.GetComponent<VarMan>();
                man.Vars.First(v => v.Name == "MyRigidBody").DefaultObject = newRB;
                man.Vars.First(v => v.Name == "MyTransform").DefaultObject = newRB.transform;
                man.Vars.First(v => v.Name == "MyAnimTransform").DefaultObject = animerEndpoints[i];
                man.Vars.First(v => v.Name == "Elasticity").DefaultFloatValue = Elasticity;
                man.Vars.First(v => v.Name == "ParentBoneTransform").DefaultObject = bonePhysParent.transform;

                attachedEnforcer.GetComponent<SerializedBowl>().NodeDatas.First(nd => nd.Name == "Transform.TransformPoint Marked").DataInputs[1].DefaultVector3Value = newJ.anchor;

                ApplyVars(man);
            }

            // add on-disable pos reset
            var posReset = newPhyser.AddComponent<LifeCycleEvents>();
            posReset.DisableEvent.EnsurePCallList();
            var posrs = new PersistentCall();
            posrs.SetMethod(typeof(Transform).GetProperty("localPosition").SetMethod, newPhyser.transform);
            posrs.PersistentArguments[0].Vector3 = newPhyser.transform.localPosition;
            posReset.DisableEvent.PersistentCallsList.Add(posrs);
            var rotsrs = new PersistentCall();
            rotsrs.SetMethod(typeof(Transform).GetProperty("localRotation").SetMethod, newPhyser.transform);
            rotsrs.PersistentArguments[0].Quaternion = newPhyser.transform.localRotation;
            posReset.DisableEvent.PersistentCallsList.Add(rotsrs);


            bonePhysParent = newRB;
        }
        // then also add mirror mitigation
        /*{
            var physLCE = physicsRoot.AddComponent<LifeCycleEvents>();
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(BaseFolder + "MirrorDetector.prefab").GetComponent<LifeCycleEvents>();
            physLCE.EnableEvent.CopyFrom(template.EnableEvent);
            physLCE.EnableEvent.PersistentCallsList[0].PersistentArguments[0].Object = physLCE.transform;
            // pcall 13 => Axis
            // pcall 14 => Swing Axis
            foreach (var item in physBalls)
            {
                // Oughggh optimization be damned
                // all this shit just to man-handle a PersistentArgument
                var s_PersistentArgumentTypeGetSet = typeof(PersistentArgument).GetField("_Type", UltEventUtils.AnyAccessBindings);
                var s_PersistentArgumentStringGetSet = typeof(PersistentArgument).GetField("_String", UltEventUtils.AnyAccessBindings);
                var s_PersistentArgumentIntGetSet = typeof(PersistentArgument).GetField("_Int", UltEventUtils.AnyAccessBindings);
                PersistentArgument FSetType(PersistentArgument arg, PersistentArgumentType t)
                { s_PersistentArgumentTypeGetSet.SetValue(arg, t); return arg; }
                PersistentArgument FSetString(PersistentArgument arg, string s)
                { s_PersistentArgumentStringGetSet.SetValue(arg, s); return arg; }
                PersistentArgument FSetInt(PersistentArgument arg, int i) 
                { s_PersistentArgumentIntGetSet.SetValue(arg, i); return arg;}

                var axisSetter = new PersistentCall();
                axisSetter.SetMethod(typeof(Joint).GetProperty("axis", (BindingFlags)60).SetMethod, item);
                FSetType(axisSetter.PersistentArguments[0], PersistentArgumentType.ReturnValue);
                FSetString(axisSetter.PersistentArguments[0], typeof(Vector3).AssemblyQualifiedName);
                FSetInt(axisSetter.PersistentArguments[0], 13);
                physLCE.EnableEvent.PersistentCallsList.Add(axisSetter);

                var swingAxisSetter = new PersistentCall();
                swingAxisSetter.SetMethod(typeof(CharacterJoint).GetProperty("swingAxis", (BindingFlags)60).SetMethod, item);
                FSetType(swingAxisSetter.PersistentArguments[0], PersistentArgumentType.ReturnValue);
                FSetString(swingAxisSetter.PersistentArguments[0], typeof(Vector3).AssemblyQualifiedName);
                FSetInt(swingAxisSetter.PersistentArguments[0], 14);
                physLCE.EnableEvent.PersistentCallsList.Add(swingAxisSetter);
            }
        }*/ //psyche

        // Section for Smoothing Root; Where the visuals get smoothened between places
        var oldSmoothRig = bones[0].transform.parent.Find(bones[0].gameObject.name + "_Smoothers");
        if (oldSmoothRig)
            DestroyImmediate(oldSmoothRig.gameObject);
        List<Transform> smoothers = new List<Transform>();
        var smoothingRoot = new GameObject(bones[0].gameObject.name + "_Smoothers"); ;
        smoothingRoot.transform.SetParent(animsRoot.transform.parent);
        smoothingRoot.transform.localPosition = animsRoot.transform.localPosition;
        smoothingRoot.transform.localRotation = animsRoot.transform.localRotation;
        smoothingRoot.transform.localScale = animsRoot.transform.localScale;
        for (int i = 0; i < bones.Count - 1; i++)
        {
            Transform boneA = bones[i];
            Transform boneB = bones[i + 1];

            var template = AssetDatabase.LoadAssetAtPath<GameObject>(BaseFolder + "Smoother.prefab");
            var newSmooth = UnityEngine.Object.Instantiate(template);
            newSmooth.gameObject.name = (boneA.gameObject.name + "_smoother");
            newSmooth.transform.SetParent(smoothingRoot.transform);
            newSmooth.transform.rotation = boneA.transform.rotation;
            newSmooth.transform.position = boneB.transform.position;
            newSmooth.transform.localScale = Vector3.one;

            var normalSmoother = newSmooth.GetComponent<SmoothFollower>();
            normalSmoother.targetTransform = physBalls[i].transform;

            foreach (var smth in newSmooth.GetComponentsInChildren<SmoothFollower>(true))
            {
                smth.TranslationSmoothTime = (.5f / VisualSmoothing);
                smth.RotationalSmoothTime = (.5f / VisualSmoothing);
            }

            
            var trg = newSmooth.transform.Find("VARS/gobj_Object_var_TargetGobj").GetComponents<Component>().First(c => c is not Transform);
            var blxrProp = trg.GetType().GetProperty("interactorSource", (BindingFlags)60);
            blxrProp.SetValue(trg, physBalls[i].gameObject);

            var man = newSmooth.GetComponent<VarMan>();
            man.Vars.First(v => v.Name == "AvatarRootTransform").DefaultObject = avi.transform;
            man.Vars.First(v => v.Name == "NotOnHead").DefaultBoolValue = !isOnHead;
            man.Vars.First(v => v.Name == "LocalTargetGobj").DefaultObject = physBalls[i].gameObject;
            man.Vars.First(v => v.Name == "DirectRotate").DefaultBoolValue = MirrorRotationSyncMode == MirrorSyncMode.Direct;
            man.Vars.First(v => v.Name == "DirectInstant").DefaultBoolValue = Mathf.Clamp01((1/120f) * VisualSmoothing * 20) == 1;

            // lock visuals to smoother
            var pa = boneA.AddComponent<ParentConstraint>();
            pa.AddSource(new ConstraintSource() { sourceTransform = newSmooth.transform, weight = 1 });
            var method = typeof(ParentConstraint).GetMethod("ActivateAndPreserveOffset", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null)
                method.Invoke(pa, null);

            ApplyVars(man);


            foreach (var item in newSmooth.GetComponentsInChildren<SerializedBowl>(true))
                DestroyImmediate(item);

            smoothers.Add(newSmooth.transform);
        }
        lastMadeSmoothers = smoothers;
        lastMadePhysers = physBalls;

        UpdatePhysicsSettings();
    }

    public enum MirrorSyncMode
    {
        Tweened, Direct
    }

    [Header("ignore these")]
    public List<Transform> lastMadeSmoothers;
    public List<CharacterJoint> lastMadePhysers;
    private void Update()
    {
        if (lastMadePhysers != null && lastMadeSmoothers != null)
            if (lastMadePhysers.Count == lastMadeSmoothers.Count)
            for (int i = 0; i < lastMadeSmoothers.Count; i++)
            {
                Transform smthr = lastMadeSmoothers[i];
                Transform targ = lastMadePhysers[i].transform;
                smthr.transform.localPosition = Vector3.Lerp(smthr.transform.localPosition, targ.transform.localPosition, Mathf.Clamp01(Time.deltaTime * VisualSmoothing * 20));
                smthr.transform.localRotation = Quaternion.Lerp(smthr.transform.localRotation, targ.transform.localRotation, Mathf.Clamp01(Time.deltaTime * VisualSmoothing * 20));
            }
    }

    [ContextMenu("Update Physics of Bones")]  
    public void UpdatePhysicsSettings()
    {
        List<Transform> bones = GetBones();
        if (bones.Count <= 1) return;

        var oldPhys = bones[0].transform.parent.Find(bones[0].gameObject.name + "_PhysicsChain");
        foreach (var rb in oldPhys.GetComponentsInChildren<Rigidbody>())
        {
            rb.mass = Mass;
            if (rb.transform == oldPhys) continue;
            rb.drag = Drag;
            rb.useGravity = false;
        }

        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null)
        {
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
        }
    }
    [MenuItem("CONTEXT/Transform/Finalize all PhysBones")]
    static void UpdateAllBones(MenuCommand mc)
    {
        var go = (Transform)mc.context;
        foreach (var item in go.GetComponentsInChildren<PhysBoneMaker>())
            item.MakeIntoPhys();
    }

    [ContextMenu("Remove Generated Objects")]
    public void RemoveGeneratedObjects()
    {
        foreach (var item in GetComponentsInChildren<ParentConstraint>())
            DestroyImmediate(item);
        List<Transform> bones = GetBones();
        if (bones.Count == 0) return;
        var oldAnimRig = bones[0].transform.parent.Find(bones[0].gameObject.name + "_AnimRig");
        if (oldAnimRig)
            DestroyImmediate(oldAnimRig.gameObject);
        var oldPhys = bones[0].transform.parent.Find(bones[0].gameObject.name + "_PhysicsChain");
        if (oldPhys)
            DestroyImmediate(oldPhys.gameObject);
        var oldSmoothRig = bones[0].transform.parent.Find(bones[0].gameObject.name + "_Smoothers");
        if (oldSmoothRig)
            DestroyImmediate(oldSmoothRig.gameObject);
    }

    private string GetColName(RigSegment rs)
    {
        switch (rs)
        {
            case RigSegment.FootLf:
            case RigSegment.HandLf:
                return rs.ToString() + " (left)";
            case RigSegment.FootRf:
            case RigSegment.HandRf:
                return rs.ToString() + " (right)";
            default:
                return rs.ToString();
        }
    }
    public enum RigSegment
    {
        Nope,
        Head,
        Neck,
        Chest,
        ShoulderLf,
        ElbowLf,
        HandLf,// (left),
        ShoulderRt,
        ElbowRt,
        HandRf,// (right),
        Spine,
        Pelvis,
        HipLf,
        KneeLf,
        FootLf,// (left),
        HipRt,
        KneeRt,
        FootRf,// (right),
    }


    [MenuItem("RBDynBones/Save Selected Joint Props")]
    static void SaveProps(MenuCommand cmd)
    {
        SavedLimits.Clear();
        foreach (var selected in Selection.gameObjects)
        {
            var j = selected.GetComponent<CharacterJoint>();
            SavedLimits.Add(selected.name + "_lowTwistLimit", j.lowTwistLimit);
            SavedLimits.Add(selected.name + "_highTwistLimit", j.highTwistLimit);
            SavedLimits.Add(selected.name + "_swing1Limit", j.swing1Limit);
            SavedLimits.Add(selected.name + "_swing2Limit", j.swing2Limit);
        }
    }
    private static Dictionary<string, SoftJointLimit> SavedLimits = new Dictionary<string, SoftJointLimit>();

    [MenuItem("RBDynBones/Load Selected Joint Props")]
    static void LoadProps() 
    {
        foreach (var selected in Selection.gameObjects)
        {
            var j = selected.GetComponent<CharacterJoint>();
            if (!j) continue;
            if (SavedLimits.ContainsKey(selected.name + "_lowTwistLimit"))
                j.lowTwistLimit = SavedLimits[selected.name + "_lowTwistLimit"];
            if (SavedLimits.ContainsKey(selected.name + "_highTwistLimit"))
                j.highTwistLimit = SavedLimits[selected.name + "_highTwistLimit"];
            if (SavedLimits.ContainsKey(selected.name + "_swing1Limit"))
                j.swing1Limit = SavedLimits[selected.name + "_swing1Limit"];
            if (SavedLimits.ContainsKey(selected.name + "_swing2Limit"))
                j.swing2Limit = SavedLimits[selected.name + "_swing2Limit"];
        }
    }

    private static void ApplyVars(VarMan myMan)
    {
        Dictionary<PersistentArgumentType, Type> Typz = new Dictionary<PersistentArgumentType, Type>()
        {
            { PersistentArgumentType.Int, typeof(int)},
            { PersistentArgumentType.Float, typeof(float)},
            { PersistentArgumentType.Bool, typeof(bool) },
            { PersistentArgumentType.String, typeof(string) },
            { PersistentArgumentType.Object, typeof(UnityEngine.Object) }
        };

        foreach (var varrr in myMan.Vars)
        {
            var SData = varrr;
            SData.Type = Typz[varrr.ConstInput];

            foreach (var bowl in myMan.GetComponentsInChildren<SerializedBowl>(true))
                foreach (var node in bowl.NodeDatas)
                    foreach (var input in node.DataInputs)
                    {
                        if (input.Source == null && input.EditorConstName == SData.Name && SData.Type.Type.IsAssignableFrom(input.Type.Type))
                        {
                            input.ValDefs = SData.ValDefs;
                            input.DefaultStringValue = SData.DefaultStringValue;
                            input.DefaultObject = SData.DefaultObject;
                            EditorUtility.SetDirty(bowl);
                            PrefabUtility.RecordPrefabInstancePropertyModifications(bowl);
                        }
                    }
        }
        foreach (var bowl in myMan.GetComponentsInChildren<SerializedBowl>(true))
        {
            bowl.Compile();
        }
    }
}
#endif