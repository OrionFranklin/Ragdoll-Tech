using System;
using UnityEngine;

namespace RagdollTech
{
    public enum RagdollState { Animated, Reacting, Falling, Settled, Recovering }

    [Serializable]
    public sealed class RagdollSettings
    {
        [Min(10)] public float totalMass = 72;
        [Min(0)] public float muscleSpring = 180;
        [Min(0)] public float muscleDamper = 18;
        [Min(0)] public float maximumTorque = 120;
        [Min(0)] public float knockdownImpulsePerMass = .9f;
        [Min(.1f)] public float reactionSeconds = .55f;
        [Min(.1f)] public float recoverySeconds = 1.1f;
        [Min(.1f)] public float settleSeconds = .55f;
        [Min(.01f)] public float settleSpeed = .65f;
        [Range(0, 31)] public int physicsLayer = 2;
        public LayerMask groundMask = Physics.DefaultRaycastLayers;
        public bool drawDebug;

        internal void Validate()
        {
            if (!Finite(totalMass) || totalMass < 10 || !Finite(muscleSpring) || muscleSpring < 0 ||
                !Finite(muscleDamper) || muscleDamper < 0 || !Finite(maximumTorque) || maximumTorque < 0 ||
                !Finite(knockdownImpulsePerMass) || knockdownImpulsePerMass <= 0 ||
                !Finite(reactionSeconds) || reactionSeconds <= 0 || !Finite(recoverySeconds) || recoverySeconds <= 0 ||
                !Finite(settleSeconds) || settleSeconds <= 0 || !Finite(settleSpeed) || settleSpeed <= 0 || physicsLayer < 0 || physicsLayer > 31)
                throw new ArgumentOutOfRangeException(nameof(RagdollSettings), "Mass, timing and solver settings must be finite and positive.");
        }
        internal static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        internal static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    }

    public sealed class BoneDefinition
    {
        public string name;
        public Transform bone;
        public int parent;
        public Vector3 localEnd;
        public float radius, massFraction;
        public Vector3 angularLimits;
        public float lowBend, highBend;
        public BoneDefinition(string name, Transform bone, int parent, Vector3 localEnd, float radius, float massFraction, Vector3 limits)
        {
            this.name=name; this.bone=bone; this.parent=parent; this.localEnd=localEnd; this.radius=radius; this.massFraction=massFraction; angularLimits=limits;
            lowBend=-limits.x;highBend=limits.x;
            if(name.Contains("LowerLeg")){lowBend=-5;highBend=125;}
            if(name.Contains("LowerArm")){lowBend=-125;highBend=5;}
        }
    }
}
