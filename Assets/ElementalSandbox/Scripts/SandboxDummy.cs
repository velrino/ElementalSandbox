using System.Collections.Generic;
using UnityEngine;
namespace ElementalSandbox
{
    public sealed class SandboxDummy : MonoBehaviour
    {
        public bool Alive {get;private set;}=true;
        public int SliceParts=>fragments.Count;
        // Longest edge of any severed piece, in metres. A body cut at the waist
        // cannot exceed its own height; anything far above that means the slice
        // has picked the rig's scale up twice again. See ElementalMeshSlice.Half.
        public float LargestFragment{get{float largest=0;foreach(var m in fragmentMeshes)if(m!=null){var size=m.bounds.size;largest=Mathf.Max(largest,Mathf.Max(size.x,Mathf.Max(size.y,size.z)));}return largest;}}
        public Vector3 Position=>fragments.Count>0?new Vector3(fragments[0].position.x,transform.position.y,fragments[0].position.z):hip!=null?new Vector3(hip.position.x,transform.position.y,hip.position.z):transform.position;
        ElementalApp app;Transform model,hip;int index,count;float age;bool consumed;
        readonly List<Rigidbody> bodies=new List<Rigidbody>();
        readonly List<Rigidbody> fragments=new List<Rigidbody>();
        readonly List<Mesh> fragmentMeshes=new List<Mesh>();
        readonly List<Transform> bones=new List<Transform>();
        readonly List<Vector3> restPositions=new List<Vector3>();readonly List<Quaternion> restRotations=new List<Quaternion>();
        Material material;
        public void Initialize(ElementalApp owner,int i,int n)
        {
            app=owner;index=i;count=n;Place();var prefab=Resources.Load<GameObject>("Elemental/Models/dummy");if(prefab==null)throw new System.InvalidOperationException("Original dummy FBX missing.");model=Instantiate(prefab,transform).transform;ElementalApp.NormalizeModel(model,app.Settings.F("dummies.height",1.78f));foreach(var a in model.GetComponentsInChildren<Animator>())a.enabled=false;foreach(var a in model.GetComponentsInChildren<Animation>())a.enabled=false;
            material=new Material(Shader.Find("Elemental/Surface"));material.SetColor("_BaseColor",app.Settings.C("dummies.look.color","#1b2029"));material.SetColor("_EdgeColor",app.Settings.C("dummies.look.rimColor","#6fd2ff"));material.SetColor("_HotColor",new Color(.1f,.2f,.3f));material.SetFloat("_Mode",4);material.SetFloat("_Glow",app.Settings.F("dummies.look.rimEmissive",1.5f));material.SetFloat("_Roughness",.78f);material.SetFloat("_Metallic",.15f);material.SetFloat("_Flat",0);material.SetFloat("_Opacity",1);
            foreach(var r in model.GetComponentsInChildren<Renderer>()){var mats=r.sharedMaterials;for(int j=0;j<mats.Length;j++)mats[j]=material;r.sharedMaterials=mats;}
            foreach(var t in model.GetComponentsInChildren<Transform>()){bones.Add(t);restPositions.Add(t.localPosition);restRotations.Add(t.localRotation);string name=t.name.ToLowerInvariant();if(name.Contains("hips"))hip=t;if(!IsBody(name))continue;var rb=t.gameObject.AddComponent<Rigidbody>();rb.mass=name.Contains("hips")?5:1;rb.isKinematic=true;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.linearDamping=.15f;rb.angularDamping=.8f;var c=t.gameObject.AddComponent<SphereCollider>();float worldRadius=name.Contains("head")?.11f:name.Contains("hips")?.13f:.075f;c.radius=worldRadius/Mathf.Max(.001f,t.lossyScale.x);bodies.Add(rb);}
            foreach(var rb in bodies){var p=rb.transform.parent;Rigidbody parent=null;while(p!=null&&p!=transform){parent=p.GetComponent<Rigidbody>();if(parent!=null)break;p=p.parent;}if(parent==null)continue;var joint=rb.gameObject.AddComponent<CharacterJoint>();joint.connectedBody=parent;joint.enableProjection=true;joint.lowTwistLimit=new SoftJointLimit{limit=-35};joint.highTwistLimit=new SoftJointLimit{limit=35};joint.swing1Limit=new SoftJointLimit{limit=45};joint.swing2Limit=new SoftJointLimit{limit=30};}
            for(int a=0;a<bodies.Count;a++)for(int b=a+1;b<bodies.Count;b++)Physics.IgnoreCollision(bodies[a].GetComponent<Collider>(),bodies[b].GetComponent<Collider>());
        }
        static bool IsBody(string n)=>!n.Contains("end")&&(n.EndsWith("hips")||n.EndsWith("spine")||n.EndsWith("spine2")||n.EndsWith("head")||n.EndsWith("leftarm")||n.EndsWith("rightarm")||n.EndsWith("leftforearm")||n.EndsWith("rightforearm")||n.EndsWith("leftupleg")||n.EndsWith("rightupleg")||n.EndsWith("leftleg")||n.EndsWith("rightleg"));
        void Place(){float angle=(index+.35f)*Mathf.PI*2/Mathf.Max(1,count);float radius=Mathf.Lerp(app.Settings.F("dummies.minRadius",5),app.Settings.F("dummies.radius",13),.25f+(index%3)*.22f);transform.position=new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);transform.rotation=Quaternion.LookRotation(-transform.position);}
        public void Hit(Vector3 impulse){if(!Alive||consumed)return;Alive=false;age=0;foreach(var rb in bodies){rb.isKinematic=false;rb.linearVelocity=impulse;rb.angularVelocity=new Vector3(impulse.z,1,-impulse.x)*.5f;}}
        public void Cut(Vector3 impulse)
        {
            if(!Alive)return;float height=transform.position.y+app.Settings.F("dummies.height",1.78f)*.51f;
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>()){
                var baked=new Mesh();skin.BakeMesh(baked);for(int side=0;side<2;side++){Vector3 center=skin.bounds.center;Mesh mesh=ElementalMeshSlice.Half(baked,skin.transform,height,side==0,center);if(mesh.vertexCount<3){Destroy(mesh);continue;}fragmentMeshes.Add(mesh);var g=new GameObject(side==0?"Upper body":"Lower body",typeof(MeshFilter),typeof(MeshRenderer),typeof(BoxCollider),typeof(Rigidbody));g.transform.position=center;g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=material;var collider=g.GetComponent<BoxCollider>();collider.center=mesh.bounds.center;collider.size=mesh.bounds.size;var rb=g.GetComponent<Rigidbody>();rb.mass=3;rb.linearVelocity=impulse+Vector3.up*(side==0?1.3f:-.2f);rb.angularVelocity=new Vector3(side==0?2:-2,1,0);fragments.Add(rb);}Destroy(baked);
            }
            if(fragments.Count==0){Hit(impulse);return;}Alive=false;age=0;model.gameObject.SetActive(false);
        }
        public void Pull(Vector3 center,float dt,float strength,bool swallow)
        {
            if(consumed)return;if(Alive)Hit(Vector3.up*2);foreach(var rb in fragments.Count>0?fragments:bodies){Vector3 delta=center-rb.position;Vector3 swirl=Vector3.Cross(Vector3.up,delta.normalized);rb.AddForce((delta*strength+swirl*strength+Vector3.up*9.81f-rb.linearVelocity*2),ForceMode.Acceleration);}
            if(hip!=null&&Vector3.Distance(fragments.Count>0?fragments[0].position:hip.position,center)<(swallow?.8f:.6f)){consumed=true;model.gameObject.SetActive(false);age=app.Settings.F("dummies.corpseTime",4.5f)+app.Settings.F("dummies.dissolveTime",1.3f);foreach(var rb in bodies)rb.isKinematic=true;foreach(var rb in fragments)rb.gameObject.SetActive(false);}
        }
        public void Tick(float dt)
        {
            if(Alive)return;age+=dt;float corpse=app.Settings.F("dummies.corpseTime",4.5f),dissolve=app.Settings.F("dummies.dissolveTime",1.3f),respawn=app.Settings.F("dummies.respawnDelay",2);material.SetFloat("_Opacity",1-Mathf.Clamp01((age-corpse)/Mathf.Max(.01f,dissolve)));if(age>corpse+dissolve+respawn)ResetBody();
        }
        void ClearFragments(){foreach(var rb in fragments)if(rb!=null)Destroy(rb.gameObject);foreach(var m in fragmentMeshes)Destroy(m);fragments.Clear();fragmentMeshes.Clear();}
        public void ResetBody(){ClearFragments();foreach(var rb in bodies){if(!rb.isKinematic){rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;}rb.isKinematic=true;}for(int i=0;i<bones.Count;i++){bones[i].localPosition=restPositions[i];bones[i].localRotation=restRotations[i];}Place();Alive=true;consumed=false;age=0;model.gameObject.SetActive(true);material.SetFloat("_Opacity",1);}
        void OnDestroy(){ClearFragments();if(material!=null)Destroy(material);}
    }
}
