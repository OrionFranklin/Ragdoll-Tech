using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RagdollTech
{
    /// <summary>Asset-free playable test room; all models are primitive original geometry.</summary>
    public sealed class RagdollLab : MonoBehaviour
    {
        public Material surfaceMaterial;
        ActiveRagdoll puppet;Transform actor;Camera eye;Vector3 spawn;
        string status="Click a body to push it. K: knock down · R: reset · G: rotate gravity";
        Vector3 up=Vector3.up;readonly List<GameObject> room=new List<GameObject>();
        float recoveryRetry;bool smoke;
        static void Tint(Renderer renderer,Color color,Material surface)
        {
            if(surface)renderer.sharedMaterial=surface;
            var properties=new MaterialPropertyBlock();properties.SetColor("_Color",color);renderer.SetPropertyBlock(properties);
        }
        public static ActiveRagdoll CreateRig(Vector3 position,Quaternion rotation,out Transform actor,bool visible=true,Material surface=null)
        {
            actor=new GameObject("Test traveler").transform;actor.SetPositionAndRotation(position,rotation);
            var root=actor;
            var defs=new List<BoneDefinition>();
            int Add(string name,int parent,Vector3 point,Vector3 end,float radius,float mass,Vector3 limits)
            {
                var bone=new GameObject(name).transform;bone.SetParent(parent<0?root:defs[parent].bone);bone.position=root.TransformPoint(point);bone.rotation=root.rotation;
                var d=new BoneDefinition(name,bone,parent,end-point,radius,mass,limits);defs.Add(d);
                if(visible)
                {
                    var mesh=GameObject.CreatePrimitive(PrimitiveType.Capsule);mesh.name="Visible segment";var collider=mesh.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
                    mesh.transform.SetParent(bone,false);mesh.transform.localPosition=d.localEnd*.5f;mesh.transform.localRotation=Quaternion.FromToRotation(Vector3.up,d.localEnd.normalized);
                    mesh.transform.localScale=new Vector3(radius*2,Mathf.Max(radius,d.localEnd.magnitude*.5f),radius*2);
                    Tint(mesh.GetComponent<Renderer>(),parent<0?new Color(.85f,.61f,.3f):new Color(.3f,.65f,.72f),surface);
                }
                return defs.Count-1;
            }
            int hips=Add("Hips",-1,new Vector3(0,.98f,0),new Vector3(0,1.18f,0),.14f,.2f,Vector3.zero);
            int chest=Add("Chest",hips,new Vector3(0,1.18f,0),new Vector3(0,1.5f,0),.16f,.25f,new Vector3(30,35,25));
            Add("Head",chest,new Vector3(0,1.58f,0),new Vector3(0,1.77f,0),.12f,.07f,new Vector3(40,45,35));
            foreach(float s in new[]{-1f,1f})
            {
                string side=s<0?"Left":"Right";
                int arm=Add(side+"UpperArm",chest,new Vector3(s*.22f,1.45f,0),new Vector3(s*.39f,1.19f,.02f),.065f,.035f,new Vector3(75,85,85));
                Add(side+"LowerArm",arm,new Vector3(s*.39f,1.19f,.02f),new Vector3(s*.4f,.94f,.08f),.05f,.025f,new Vector3(65,15,15));
                int thigh=Add(side+"UpperLeg",hips,new Vector3(s*.1f,.94f,0),new Vector3(s*.13f,.52f,.025f),.085f,.095f,new Vector3(65,45,45));
                int shin=Add(side+"LowerLeg",thigh,new Vector3(s*.13f,.52f,.025f),new Vector3(s*.13f,.12f,0),.06f,.045f,new Vector3(65,12,12));
                Add(side+"Foot",shin,new Vector3(s*.13f,.12f,0),new Vector3(s*.13f,.09f,.18f),.055f,.02f,new Vector3(25,20,20));
            }
            var ragdoll=actor.gameObject.AddComponent<ActiveRagdoll>();ragdoll.Initialize(actor,defs.ToArray());return ragdoll;
        }
        void Start()
        {
            smoke=Array.IndexOf(Environment.GetCommandLineArgs(),"-ragdollSmoke")>=0;
            Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=90;
            var camera=new GameObject("Lab camera");eye=camera.AddComponent<Camera>();eye.backgroundColor=new Color(.07f,.1f,.14f);eye.clearFlags=CameraClearFlags.SolidColor;eye.fieldOfView=48;
            var sun=new GameObject("Lab light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.transform.rotation=Quaternion.Euler(42,-35,0);
            RenderSettings.ambientLight=new Color(.5f,.55f,.6f);
            BuildRoom();if(smoke){if(!surfaceMaterial||!surfaceMaterial.shader.isSupported||surfaceMaterial.shader.name!="Standard"){Fail("Lab surface shader missing or unsupported");return;}StartCoroutine(Smoke());}
        }
        void BuildRoom()
        {
            if(puppet)Destroy(puppet.gameObject);foreach(var item in room)Destroy(item);room.Clear();
            var frame=Quaternion.FromToRotation(Vector3.up,up);spawn=up*.04f;
            void Box(string name,Vector3 p,Vector3 size,Color color)
            {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetPositionAndRotation(frame*p,frame);go.transform.localScale=size;Tint(go.GetComponent<Renderer>(),color,surfaceMaterial);room.Add(go);}
            Box("Floor",new Vector3(0,-.25f,0),new Vector3(14,.5f,14),new Color(.2f,.27f,.31f));
            Box("Low obstacle",new Vector3(-1.8f,.3f,1),new Vector3(1,.6f,2),new Color(.5f,.4f,.3f));
            Box("Wall",new Vector3(0,1.4f,3),new Vector3(7,2.8f,.4f),new Color(.23f,.32f,.36f));
            puppet=CreateRig(spawn,frame,out actor,true,surfaceMaterial);puppet.Gravity=_=>-up*9.81f;puppet.RecoveryRequested+=TryRecover;puppet.SimulationFault+=message=>status=message;
            eye.transform.position=frame*new Vector3(4,3.4f,-6);eye.transform.LookAt(up*.9f,up);
        }
        void TryRecover()
        {
            if(puppet.TryFindRecoveryPose(actor.forward,out var pose))
            {actor.SetPositionAndRotation(pose.position,pose.rotation);puppet.BeginRecovery();}
        }
        void Update()
        {
            if(!puppet)return;
            if(puppet.State==RagdollState.Settled&&Time.time>recoveryRetry){recoveryRetry=Time.time+.5f;TryRecover();}
            if(smoke)return;
            if(Input.GetKeyDown(KeyCode.R)){puppet.Cancel();actor.SetPositionAndRotation(spawn,Quaternion.FromToRotation(Vector3.up,up));}
            if(Input.GetKeyDown(KeyCode.G)){up=up==Vector3.up?Vector3.right:up==Vector3.right?Vector3.down:Vector3.up;BuildRoom();}
            if(Input.GetKeyDown(KeyCode.K))puppet.ApplyImpact(puppet.PelvisPosition+up*.3f,(actor.forward+up*.15f)*110,default,true);
            if(Input.GetMouseButtonDown(0))
            {
                var ray=eye.ScreenPointToRay(Input.mousePosition);var point=puppet.PelvisPosition+up*.4f;
                if(Physics.Raycast(ray,out var hit,30,~0))point=hit.point;
                puppet.ApplyImpact(point,ray.direction*(Input.GetKey(KeyCode.LeftShift)?125:30));
            }
        }
        void OnGUI()
        {
            GUI.color=Color.white;GUI.Label(new Rect(24,20,1100,32),"RAGDOLL TECH · constrained reactions / arbitrary gravity / checked recovery");
            GUI.Label(new Rect(24,50,1100,32),status);if(puppet)GUI.Label(new Rect(24,80,1000,32),"State: "+puppet.State+" · "+puppet.BodyCount+" physics bodies · gravity up "+up);
        }
        IEnumerator Smoke()
        {
            yield return new WaitForSeconds(.4f);
            foreach(var axis in new[]{Vector3.up,Vector3.right,Vector3.down,Vector3.forward,Vector3.left,Vector3.back})
            {
                up=axis;BuildRoom();yield return new WaitForSeconds(.2f);
                puppet.ApplyImpact(puppet.PelvisPosition+up*.3f,actor.forward*30);
                if(puppet.State!=RagdollState.Reacting){Fail("Small impact did not react");yield break;}
                yield return new WaitForSeconds(2);
                if(puppet.Active){Fail("Small impact did not recover on "+axis);yield break;}
                puppet.ApplyImpact(puppet.PelvisPosition+up*.3f,actor.forward*110,default,true);
                yield return new WaitForSeconds(.7f);
                yield return new WaitForEndOfFrame();
                if(axis==Vector3.up)Capture("ragdoll-fall");
                float until=Time.time+12;while(puppet.Active&&Time.time<until)yield return null;
                if(puppet.Active){Fail("Knockdown did not recover on "+axis+" state="+puppet.State);yield break;}
                Debug.Log("RAGDOLL_PASS reactions, collision and recovery on "+axis);
            }
            File.WriteAllText(Path.Combine(Output(),"result.txt"),"PASS: six-axis impact and recovery lab");Application.Quit(0);
        }
        static string Output(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-ragdollOutput");return i>=0&&i+1<args.Length?args[i+1]:Application.persistentDataPath;}
        void Capture(string name)
        {
            Directory.CreateDirectory(Output());var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);var target=new RenderTexture(1600,900,24);
            var previous=eye.targetTexture;var active=RenderTexture.active;
            eye.targetTexture=target;eye.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();
            File.WriteAllBytes(Path.Combine(Output(),name+".png"),texture.EncodeToPNG());eye.targetTexture=previous;RenderTexture.active=active;
            target.Release();Destroy(target);Destroy(texture);
        }
        void Fail(string message){Directory.CreateDirectory(Output());File.WriteAllText(Path.Combine(Output(),"result.txt"),"FAIL: "+message);Debug.LogError(message);Application.Quit(1);}
    }
}
