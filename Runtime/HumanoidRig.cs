using System;
using System.Collections.Generic;
using UnityEngine;

namespace RagdollTech
{
    public static class HumanoidRig
    {
        /// <summary>Build in a neutral pose, before the Animator has evaluated locomotion.</summary>
        public static BoneDefinition[] Describe(Animator animator)
        {
            if (!animator || !animator.isHuman || !animator.avatar || !animator.avatar.isValid)
                throw new ArgumentException("A valid Humanoid avatar is required.");
            Transform Bone(HumanBodyBones id) => animator.GetBoneTransform(id);
            var hips=Bone(HumanBodyBones.Hips); var head=Bone(HumanBodyBones.Head);
            var leftFoot=Bone(HumanBodyBones.LeftFoot); var rightFoot=Bone(HumanBodyBones.RightFoot);
            if (!hips || !head || !leftFoot || !rightFoot) throw new ArgumentException("Hips, head and feet are required.");
            float height=Mathf.Clamp(Vector3.Distance(head.position,(leftFoot.position+rightFoot.position)*.5f)+.2f,.8f,3.5f);
            var result=new List<BoneDefinition>();
            int Add(HumanBodyBones id, HumanBodyBones end, int parent, float radius, float mass, Vector3 limits)
            {
                var bone=Bone(id);var tip=Bone(end);
                if (!bone || !tip) throw new ArgumentException("Missing required bone: "+id+" or "+end);
                // Physics proxies have unit scale, so store metre offsets in the bone's rotational frame.
                var local=Quaternion.Inverse(bone.rotation)*(tip.position-bone.position);
                result.Add(new BoneDefinition(id.ToString(),bone,parent,local,height*radius,mass,limits));return result.Count-1;
            }
            int pelvis=Add(HumanBodyBones.Hips,HumanBodyBones.Spine,-1,.085f,.20f,Vector3.zero);
            var chestId=Bone(HumanBodyBones.Chest)?HumanBodyBones.Chest:HumanBodyBones.Spine;
            int chest=Add(chestId,Bone(HumanBodyBones.Neck)?HumanBodyBones.Neck:HumanBodyBones.Head,pelvis,.095f,.25f,new Vector3(30,35,25));
            result.Add(new BoneDefinition("Head",head,chest,Quaternion.Inverse(head.rotation)*(animator.transform.up*height*.09f),height*.067f,.07f,new Vector3(40,45,35)));
            foreach(bool left in new[]{true,false})
            {
                int upper=Add(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm,left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm,chest,.036f,.035f,new Vector3(75,85,85));
                Add(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm,left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand,upper,.029f,.025f,new Vector3(65,15,15));
                int thigh=Add(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg,left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg,pelvis,.053f,.095f,new Vector3(65,45,45));
                int shin=Add(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg,left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot,thigh,.037f,.045f,new Vector3(65,12,12));
                var foot=left?leftFoot:rightFoot;
                result.Add(new BoneDefinition(left?"LeftFoot":"RightFoot",foot,shin,Quaternion.Inverse(foot.rotation)*(animator.transform.forward*height*.075f),height*.035f,.02f,new Vector3(25,20,20)));
            }
            return result.ToArray();
        }
    }
}
