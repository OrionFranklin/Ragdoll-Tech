using UnityEngine;

namespace RagdollTech
{
    public static class JointSpace
    {
        public static Quaternion Basis(Vector3 axis, Vector3 secondary)
        {
            var right=axis.normalized;
            var forward=Vector3.Cross(right,secondary).normalized;
            return Quaternion.LookRotation(forward,Vector3.Cross(forward,right));
        }
        /// <summary>Convert a desired child/connected-body rotation into joint constraint space.</summary>
        public static Quaternion Target(Quaternion desiredRelative, Quaternion bindRelative, Quaternion basis)
            => Quaternion.Inverse(basis)*Quaternion.Inverse(desiredRelative)*bindRelative*basis;
    }
}
