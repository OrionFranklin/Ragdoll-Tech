using System;
using NUnit.Framework;
using UnityEngine;

namespace RagdollTech.Tests
{
    public sealed class RagdollTests
    {
        ActiveRagdoll puppet;Transform actor;GameObject floor;SimulationMode previous;
        [SetUp] public void Setup(){previous=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;}
        [TearDown] public void Cleanup(){if(actor)UnityEngine.Object.DestroyImmediate(actor.gameObject);if(floor)UnityEngine.Object.DestroyImmediate(floor);Physics.simulationMode=previous;}
        void Rig(Vector3 up)
        {
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=-up*.25f;floor.transform.rotation=Quaternion.FromToRotation(Vector3.up,up);floor.transform.localScale=new Vector3(20,.5f,20);
            puppet=RagdollLab.CreateRig(up*.04f,Quaternion.FromToRotation(Vector3.up,up),out actor,false);puppet.Gravity=_=>-up*9.81f;Physics.SyncTransforms();
        }
        [Test] public void RigHasNormalizedMassLimitedJointsAndNoIdleCollision()
        {
            Rig(Vector3.up);float mass=0;foreach(var body in puppet.Bodies){mass+=body.mass;Assert.That(body.isKinematic);Assert.That(body.useGravity,Is.False);Assert.That(body.GetComponentInChildren<Collider>().enabled,Is.False);}
            Assert.That(puppet.BodyCount,Is.EqualTo(13));Assert.That(mass,Is.EqualTo(72).Within(.001));
            foreach(var body in puppet.Bodies)if(body.TryGetComponent<ConfigurableJoint>(out var joint)){Assert.That(joint.angularXMotion,Is.EqualTo(ConfigurableJointMotion.Limited));Assert.That(joint.xMotion,Is.EqualTo(ConfigurableJointMotion.Locked));}
        }
        [Test] public void JointSpacePreservesBindPoseAndTargetDirection()
        {
            var bind=Quaternion.Euler(12,38,-19);var basis=JointSpace.Basis(new Vector3(.4f,.6f,.1f),Vector3.up);
            Assert.That(Quaternion.Angle(JointSpace.Target(bind,bind,basis),Quaternion.identity),Is.LessThan(.001));
            var desired=Quaternion.Euler(28,-15,23);var target=JointSpace.Target(desired,bind,basis);
            var restored=bind*Quaternion.Inverse(basis*target*Quaternion.Inverse(basis));
            Assert.That(Quaternion.Angle(restored,desired),Is.LessThan(.05));
        }
        [Test] public void SmallAndLargeImpactsSelectDifferentModesAndCancelRestoresSafety()
        {
            Rig(Vector3.up);puppet.ApplyImpact(puppet.PelvisPosition,Vector3.forward*25);Assert.That(puppet.State,Is.EqualTo(RagdollState.Reacting));Assert.That(puppet.Bodies[0].isKinematic);
            puppet.ApplyImpact(puppet.PelvisPosition,Vector3.forward*100);Assert.That(puppet.State,Is.EqualTo(RagdollState.Falling));Assert.That(puppet.Bodies[0].isKinematic,Is.False);
            puppet.Cancel();Assert.That(puppet.Active,Is.False);foreach(var body in puppet.Bodies){Assert.That(body.isKinematic);Assert.That(body.GetComponentInChildren<Collider>().enabled,Is.False);}
        }
        [Test] public void InvalidImpulsesDoNotPoisonPhysics()
        {Rig(Vector3.up);puppet.ApplyImpact(Vector3.zero,new Vector3(float.NaN,0,0));Assert.That(puppet.Active,Is.False);}
        [Test] public void CancelToleratesProxyHierarchyDestroyedFirst()
        {
            Rig(Vector3.up);puppet.ApplyImpact(puppet.PelvisPosition,Vector3.forward*100);
            UnityEngine.Object.DestroyImmediate(puppet.Bodies[0].transform.parent.gameObject);
            Assert.DoesNotThrow(()=>puppet.Cancel());Assert.That(puppet.Active,Is.False);
        }
        [Test] public void BadGravityCancelsAndReportsFault()
        {Rig(Vector3.up);bool fault=false;puppet.SimulationFault+=_=>fault=true;puppet.Gravity=_=>new Vector3(float.NaN,0,0);puppet.ApplyImpact(puppet.PelvisPosition,Vector3.right*100);puppet.SimulateStep(.02f);Assert.That(fault);Assert.That(puppet.Active,Is.False);}
        [Test] public void RecoveryRejectsBlockedStandingVolume()
        {
            Rig(Vector3.up);Assert.That(puppet.TryFindRecoveryPose(Vector3.forward,out _));
            var ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try{ceiling.transform.position=Vector3.up*1.6f;ceiling.transform.localScale=new Vector3(4,.2f,4);Physics.SyncTransforms();Assert.That(puppet.TryFindRecoveryPose(Vector3.forward,out _),Is.False);}
            finally{UnityEngine.Object.DestroyImmediate(ceiling);}
        }
        [TestCase(0,1,0)][TestCase(1,0,0)][TestCase(0,-1,0)][TestCase(0,0,1)][TestCase(-1,0,0)][TestCase(0,0,-1)]
        public void KnockdownCollidesAndSettlesUnderEveryGravityAxis(float x,float y,float z)
        {
            var up=new Vector3(x,y,z);Rig(up);puppet.ApplyImpact(puppet.PelvisPosition,actor.forward*100,default,true);
            for(int i=0;i<600&&puppet.State!=RagdollState.Settled;i++){puppet.SimulateStep(.02f);Physics.Simulate(.02f);}
            Assert.That(puppet.State,Is.EqualTo(RagdollState.Settled));
            foreach(var body in puppet.Bodies){Assert.That(Vector3.Dot(body.worldCenterOfMass,up),Is.GreaterThan(-.15f));Assert.That((body.position-puppet.PelvisPosition).magnitude,Is.LessThan(2));}
            Assert.That(puppet.TryFindRecoveryPose(actor.forward,out var pose));Assert.That(Vector3.Dot(pose.rotation*Vector3.up,up),Is.GreaterThan(.999f));
        }
    }
}
