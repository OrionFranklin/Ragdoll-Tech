using System;
using System.Collections.Generic;
using UnityEngine;

namespace RagdollTech
{
    /// <summary>Separate unit-scale physics proxies. Animation supplies targets; physics supplies the visible pose.</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class ActiveRagdoll : MonoBehaviour
    {
        sealed class Link
        {
            public BoneDefinition definition;
            public Rigidbody body;
            public Collider collider;
            public ConfigurableJoint joint;
            public Quaternion bindRelative,basis,targetRotation,recoveryRotation;
            public Vector3 targetPosition,recoveryPosition;
        }
        struct LocalPose { public Transform bone;public Vector3 position;public Quaternion rotation; }
        readonly List<Link> links=new List<Link>();
        LocalPose[] animationPose;
        GameObject physicsRoot;
        PhysicsMaterial material;
        bool poseOverridden;
        float stateAge,quietTime;
        readonly Collider[] overlaps=new Collider[64];
        public RagdollSettings Settings { get; private set; }
        public Func<Vector3,Vector3> Gravity { get; set; } = _=>Physics.gravity;
        public RagdollState State { get; private set; } = RagdollState.Animated;
        public bool Active => State!=RagdollState.Animated;
        public bool Simulating => State==RagdollState.Reacting||State==RagdollState.Falling||State==RagdollState.Settled;
        public bool ControlsRoot => State==RagdollState.Falling||State==RagdollState.Settled||State==RagdollState.Recovering;
        public int BodyCount => links.Count;
        public Vector3 PelvisPosition => links.Count==0?transform.position:links[0].body.position;
        public event Action<RagdollState> StateChanged;
        public event Action RecoveryRequested;
        public event Action<string> SimulationFault;
        public IReadOnlyList<Rigidbody> Bodies => bodies;
        readonly List<Rigidbody> bodies=new List<Rigidbody>();

        public void Initialize(Transform animatedRoot, BoneDefinition[] rig, RagdollSettings settings=null)
        {
            if (links.Count>0) throw new InvalidOperationException("Already initialized.");
            Settings=settings??new RagdollSettings();Settings.Validate();
            if (!animatedRoot||rig==null||rig.Length<2) throw new ArgumentException("At least two connected bones are required.");
            float total=0;
            for(int i=0;i<rig.Length;i++)
            {
                var d=rig[i];
                if(d==null||!d.bone||d.parent>=i||d.parent< -1||(i>0&&d.parent<0)||
                    !RagdollSettings.Finite(d.localEnd)||!RagdollSettings.Finite(d.radius)||d.radius<=0||
                    !RagdollSettings.Finite(d.massFraction)||d.massFraction<=0||
                    !RagdollSettings.Finite(d.angularLimits)||!RagdollSettings.Finite(d.lowBend)||!RagdollSettings.Finite(d.highBend)||
                    d.lowBend>0||d.highBend<0||d.lowBend< -177||d.highBend>177||d.angularLimits.y<0||d.angularLimits.y>177||d.angularLimits.z<0||d.angularLimits.z>177||
                    !d.bone.IsChildOf(animatedRoot)) throw new ArgumentException("Invalid bone at index "+i);
                total+=d.massFraction;
            }
            if(!RagdollSettings.Finite(total))throw new ArgumentException("Total mass fraction must be finite.");
            var transforms=animatedRoot.GetComponentsInChildren<Transform>(true);
            animationPose=new LocalPose[transforms.Length];
            for(int i=0;i<transforms.Length;i++)animationPose[i].bone=transforms[i];
            physicsRoot=new GameObject(name+" · ragdoll physics");
            material=new PhysicsMaterial("Ragdoll contact"){dynamicFriction=.6f,staticFriction=.7f,bounciness=0,frictionCombine=PhysicsMaterialCombine.Average};
            for(int i=0;i<rig.Length;i++)
            {
                var d=rig[i];var obj=new GameObject(d.name);obj.layer=Settings.physicsLayer;obj.transform.SetParent(physicsRoot.transform);
                obj.transform.SetPositionAndRotation(d.bone.position,d.bone.rotation);
                var body=obj.AddComponent<Rigidbody>();body.mass=Settings.totalMass*d.massFraction/total;body.useGravity=false;body.isKinematic=true;
                body.linearDamping=.15f;body.angularDamping=1.5f;body.maxAngularVelocity=15;body.maxLinearVelocity=25;
                body.maxDepenetrationVelocity=2;body.solverIterations=16;body.solverVelocityIterations=8;
                body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;body.interpolation=RigidbodyInterpolation.Interpolate;
                var shape=new GameObject("Contact shape");shape.layer=Settings.physicsLayer;shape.transform.SetParent(obj.transform,false);
                float length=d.localEnd.magnitude;shape.transform.localPosition=d.localEnd*.5f;
                shape.transform.localRotation=length>.001f?Quaternion.FromToRotation(Vector3.up,d.localEnd/length):Quaternion.identity;
                var capsule=shape.AddComponent<CapsuleCollider>();capsule.radius=d.radius;capsule.height=Mathf.Max(d.radius*2,length+d.radius*.6f);capsule.sharedMaterial=material;capsule.enabled=false;
                obj.AddComponent<RagdollContact>().Owner=this;
                var link=new Link{definition=d,body=body,collider=capsule};links.Add(link);bodies.Add(body);
                if(i==0)continue;
                var parent=links[d.parent];var joint=obj.AddComponent<ConfigurableJoint>();link.joint=joint;joint.connectedBody=parent.body;
                joint.autoConfigureConnectedAnchor=false;joint.anchor=Vector3.zero;joint.connectedAnchor=parent.body.transform.InverseTransformPoint(obj.transform.position);
                // X is the anatomical bend axis; Y/Z allow limited twist and swing.
                joint.axis=Quaternion.Inverse(d.bone.rotation)*animatedRoot.right;
                joint.secondaryAxis=Quaternion.Inverse(d.bone.rotation)*animatedRoot.up;
                joint.xMotion=joint.yMotion=joint.zMotion=ConfigurableJointMotion.Locked;
                joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Limited;
                joint.lowAngularXLimit=new SoftJointLimit{limit=d.lowBend};joint.highAngularXLimit=new SoftJointLimit{limit=d.highBend};
                joint.angularYLimit=new SoftJointLimit{limit=d.angularLimits.y};joint.angularZLimit=new SoftJointLimit{limit=d.angularLimits.z};
                joint.rotationDriveMode=RotationDriveMode.Slerp;joint.enablePreprocessing=false;joint.enableCollision=false;
                joint.projectionMode=JointProjectionMode.PositionAndRotation;joint.projectionDistance=.08f;joint.projectionAngle=15;
                link.bindRelative=Quaternion.Inverse(parent.body.rotation)*body.rotation;
                link.basis=JointSpace.Basis(joint.axis,joint.secondaryAxis);
            }
            // Adjacent/overlapping body shapes cannot launch one another on activation.
            for(int i=0;i<links.Count;i++)for(int j=i+1;j<links.Count;j++)Physics.IgnoreCollision(links[i].collider,links[j].collider);
            CaptureTargets();SyncKinematic();
        }

        public void ApplyImpact(Vector3 point,Vector3 impulse,Vector3 inheritedVelocity=default,bool forceKnockdown=false)
        {
            if(links.Count==0||!RagdollSettings.Finite(point)||!RagdollSettings.Finite(impulse)||!RagdollSettings.Finite(inheritedVelocity)||impulse.sqrMagnitude<.0001f)return;
            bool falling=forceKnockdown||impulse.magnitude/Settings.totalMass>=Settings.knockdownImpulsePerMass||ControlsRoot;
            if(!Simulating)
            {
                RestoreAnimation();CaptureTargets();SyncKinematic();
                foreach(var link in links){link.body.isKinematic=false;link.collider.enabled=true;link.body.linearVelocity=Vector3.ClampMagnitude(inheritedVelocity,18);link.body.angularVelocity=Vector3.zero;}
                for(int i=0;i<links.Count;i++)for(int j=i+1;j<links.Count;j++)Physics.IgnoreCollision(links[i].collider,links[j].collider);
            }
            ChangeState(falling?RagdollState.Falling:RagdollState.Reacting);
            links[0].body.isKinematic=!falling;
            impulse=Vector3.ClampMagnitude(impulse,Settings.totalMass*5);
            int nearest=0;float distance=float.PositiveInfinity;
            for(int i=0;i<links.Count;i++){float d=(links[i].body.worldCenterOfMass-point).sqrMagnitude;if(d<distance){distance=d;nearest=i;}}
            foreach(var link in links)if(!link.body.isKinematic)link.body.AddForce(impulse*.65f*(link.body.mass/Settings.totalMass),ForceMode.Impulse);
            if(!links[nearest].body.isKinematic)links[nearest].body.AddForceAtPosition(Vector3.ClampMagnitude(impulse*.35f,links[nearest].body.mass*6),links[nearest].collider.ClosestPoint(point),ForceMode.Impulse);
        }

        internal void RecordCollision(float impulse,float speed)
        {
            if(State==RagdollState.Reacting&&stateAge>.08f&&speed>3&&impulse/Settings.totalMass>Settings.knockdownImpulsePerMass)
            {links[0].body.isKinematic=false;ChangeState(RagdollState.Falling);}
        }
        void ChangeState(RagdollState next)
        {State=next;stateAge=quietTime=0;StateChanged?.Invoke(next);}

        void Update(){RestoreAnimation();}
        void FixedUpdate(){SimulateStep(Time.fixedDeltaTime);}
        /// <summary>Applies forces/state changes; exposed for deterministic PhysicsScene acceptance tests.</summary>
        public void SimulateStep(float dt)
        {
            if(links.Count==0||!Simulating||dt<=0)return;
            stateAge+=dt;float speed=0;
            var down=Gravity(PelvisPosition).normalized;
            float brace=0;
            if(State==RagdollState.Falling&&down.sqrMagnitude>.5f&&Physics.Raycast(PelvisPosition,down,out var landing,1.8f,Settings.groundMask,QueryTriggerInteraction.Ignore))brace=Mathf.Clamp01((1.8f-landing.distance)*1.5f);
            foreach(var link in links)
            {
                if(!RagdollSettings.Finite(link.body.position)||!RagdollSettings.Finite(link.body.linearVelocity)||
                    (link.body.position-links[0].body.position).sqrMagnitude>36)
                {Cancel();SimulationFault?.Invoke("Ragdoll exceeded finite pose bounds.");return;}
                var gravity=Gravity(link.body.worldCenterOfMass);
                if(!RagdollSettings.Finite(gravity)){Cancel();SimulationFault?.Invoke("Gravity provider returned a non-finite vector.");return;}
                if(!link.body.isKinematic)link.body.AddForce(Vector3.ClampMagnitude(gravity,50),ForceMode.Acceleration);
                speed+=link.body.linearVelocity.sqrMagnitude;
                if(link.joint)
                {
                    var parent=links[link.definition.parent];var desired=Quaternion.Inverse(parent.targetRotation)*link.targetRotation;
                    bool arm=link.definition.name.Contains("Arm");float strength=State==RagdollState.Reacting?1:State==RagdollState.Settled?.04f:arm?.28f:.09f;
                    // A bounded reflex bends the arms toward the chest as support approaches.
                    // This is a heuristic brace, not a locomotion or balance solver.
                    if(arm&&brace>0)desired*=Quaternion.AngleAxis((link.definition.name.Contains("Lower")?-55:-20)*brace,link.joint.axis);
                    link.joint.targetRotation=JointSpace.Target(desired,link.bindRelative,link.basis);
                    link.joint.slerpDrive=new JointDrive{positionSpring=Settings.muscleSpring*strength,positionDamper=Settings.muscleDamper*Mathf.Sqrt(strength),maximumForce=Settings.maximumTorque*strength};
                }
            }
            if(State==RagdollState.Reacting)
            {
                links[0].body.MovePosition(links[0].targetPosition);links[0].body.MoveRotation(links[0].targetRotation);
                if(stateAge>=Settings.reactionSeconds)BeginRecovery();
                return;
            }
            float rms=Mathf.Sqrt(speed/links.Count);
            bool supported=down.sqrMagnitude>.5f&&Physics.Raycast(PelvisPosition-down*.1f,down,1.15f,Settings.groundMask,QueryTriggerInteraction.Ignore);
            quietTime=supported&&rms<Settings.settleSpeed?quietTime+dt:0;
            if(State==RagdollState.Falling&&stateAge>.5f&&quietTime>=Settings.settleSeconds)
            {ChangeState(RagdollState.Settled);RecoveryRequested?.Invoke();}
            else if(State==RagdollState.Settled&&(!supported||rms>Settings.settleSpeed*2))ChangeState(RagdollState.Falling);
        }

        public bool TryFindRecoveryPose(Vector3 forward,out Pose pose,float standingHeight=1.9f,float radius=.32f)
        {
            pose=default;if(links.Count==0||!RagdollSettings.Finite(forward)||!RagdollSettings.Finite(standingHeight)||!RagdollSettings.Finite(radius)||radius<=0||standingHeight<radius*2+.02f)return false;
            var gravity=Gravity(PelvisPosition);if(!RagdollSettings.Finite(gravity)||gravity.sqrMagnitude<.01f)return false;
            var up=-gravity.normalized;forward=Vector3.ProjectOnPlane(forward,up).normalized;
            if(forward.sqrMagnitude<.1f)forward=Vector3.Cross(up,Mathf.Abs(up.y)<.9f?Vector3.up:Vector3.right).normalized;
            var right=Vector3.Cross(up,forward);
            for(int attempt=0;attempt<9;attempt++)
            {
                float angle=(attempt-1)*Mathf.PI/4;var offset=attempt==0?Vector3.zero:(right*Mathf.Cos(angle)+forward*Mathf.Sin(angle))*.65f;
                if(!Physics.Raycast(PelvisPosition+up*.8f+offset,-up,out var floor,3,Settings.groundMask,QueryTriggerInteraction.Ignore)||Vector3.Dot(floor.normal,up)<.7f)continue;
                if(Vector3.Dot(floor.point-PelvisPosition,up)>.35f)continue;
                var feet=floor.point+up*.035f;
                int count=Physics.OverlapCapsuleNonAlloc(feet+up*(radius+.02f),feet+up*(standingHeight-radius),radius,overlaps,Settings.groundMask,QueryTriggerInteraction.Ignore);
                if(count==overlaps.Length)continue;bool blocked=false;
                for(int i=0;i<count;i++){var contact=overlaps[i].GetComponentInParent<RagdollContact>();if(!contact||contact.Owner!=this){blocked=true;break;}}
                if(blocked)continue;
                pose=new Pose(feet,Quaternion.LookRotation(forward,up));return true;
            }
            return false;
        }
        /// <summary>Host must validate/reposition its motor before requesting full knockdown recovery.</summary>
        public void BeginRecovery()
        {
            if(!Simulating)return;
            foreach(var link in links)
            {
                link.recoveryPosition=link.body.position;link.recoveryRotation=link.body.rotation;
                link.body.isKinematic=true;link.collider.enabled=false;
            }
            ChangeState(RagdollState.Recovering);
        }
        public void Cancel()
        {
            RestoreAnimation();
            // Scene teardown can destroy the separate proxy hierarchy before this component.
            foreach(var link in links){if(link.collider)link.collider.enabled=false;if(link.body)link.body.isKinematic=true;}
            ChangeState(RagdollState.Animated);CaptureTargets();SyncKinematic();
        }
        void CaptureTargets()
        {
            if(animationPose==null)return;
            for(int i=0;i<animationPose.Length;i++){var bone=animationPose[i].bone;if(!bone)continue;animationPose[i].position=bone.localPosition;animationPose[i].rotation=bone.localRotation;}
            foreach(var link in links)if(link.definition.bone){link.targetPosition=link.definition.bone.position;link.targetRotation=link.definition.bone.rotation;}
        }
        void RestoreAnimation()
        {
            if(!poseOverridden||animationPose==null)return;
            foreach(var p in animationPose)if(p.bone){p.bone.localPosition=p.position;p.bone.localRotation=p.rotation;}
            poseOverridden=false;
        }
        void SyncKinematic()
        {foreach(var link in links)if(link.body){link.body.position=link.targetPosition;link.body.rotation=link.targetRotation;}}
        void LateUpdate()
        {
            if(links.Count==0)return;
            CaptureTargets();
            if(!Active){SyncKinematic();return;}
            if(State==RagdollState.Recovering)
            {
                stateAge+=Time.deltaTime;float blend=Mathf.SmoothStep(0,1,stateAge/Settings.recoverySeconds);
                foreach(var link in links)link.definition.bone.SetPositionAndRotation(Vector3.Lerp(link.recoveryPosition,link.targetPosition,blend),Quaternion.Slerp(link.recoveryRotation,link.targetRotation,blend));
                poseOverridden=true;if(blend>=1){RestoreAnimation();ChangeState(RagdollState.Animated);SyncKinematic();}return;
            }
            foreach(var link in links)link.definition.bone.SetPositionAndRotation(link.body.position,link.body.rotation);
            poseOverridden=true;
        }
        void OnDisable(){if(Active)Cancel();}
        void OnDestroy()
        {
            if(Application.isPlaying){if(physicsRoot)Destroy(physicsRoot);if(material)Destroy(material);}
            else{if(physicsRoot)DestroyImmediate(physicsRoot);if(material)DestroyImmediate(material);}
        }
        void OnDrawGizmos()
        {
            if(Settings==null||!Settings.drawDebug)return;
            Gizmos.color=State==RagdollState.Reacting?Color.yellow:Color.cyan;
            foreach(var link in links)if(link.body){Gizmos.DrawWireSphere(link.body.worldCenterOfMass,link.definition.radius);if(link.joint)Gizmos.DrawLine(link.body.position,links[link.definition.parent].body.position);}
        }
    }
}
