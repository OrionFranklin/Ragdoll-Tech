using UnityEngine;
namespace RagdollTech
{
    public sealed class RagdollContact : MonoBehaviour
    {
        public ActiveRagdoll Owner { get; internal set; }
        void OnCollisionEnter(Collision collision)
        {
            if (Owner) Owner.RecordCollision(collision.impulse.magnitude,collision.relativeVelocity.magnitude);
        }
    }
}
