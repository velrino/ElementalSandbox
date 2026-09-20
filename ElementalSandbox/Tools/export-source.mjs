import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL,fileURLToPath} from 'node:url';
const source=process.argv[2], destination=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../Assets/ElementalSandbox/Resources/Elemental');
const mod=async p=>import(pathToFileURL(path.join(source,p)));
const {settings,ELEMENT_META,ELEMENTS}=await mod('src/config/settings.js');
const entries=[];
function flatten(o,p=''){for(const [k,v] of Object.entries(o)){const key=p?`${p}.${k}`:k;if(v&&typeof v==='object')flatten(v,key);else entries.push({key,kind:typeof v,number:typeof v==='number'?v:0,text:typeof v==='string'?v:'',flag:v===true});}}
flatten(settings);
fs.mkdirSync(destination,{recursive:true});
fs.writeFileSync(path.join(destination,'Defaults.json'),JSON.stringify({entries,abilities:ELEMENTS.map(id=>({id,...ELEMENT_META[id],zone:ELEMENT_META[id].cast==='zone'}))},null,2));
fs.writeFileSync(path.join(destination,'OriginalSettings.json'),JSON.stringify(settings,null,2));
const {createCrystalGeometry,createShardGeometry}=await mod('src/assets/ProceduralGeometry.js');
const {createMonolithGeometry,createDebrisGeometry,monolithVariantOptions}=await mod('src/assets/MonolithGeometry.js');
const meshes=[];
function add(name,g){const p=g.attributes.position,n=g.attributes.normal,uv=g.attributes.uv;const index=g.index?Array.from(g.index.array):Array.from({length:p.count},(_,i)=>i); meshes.push({name,positions:Array.from(p.array),normals:n?Array.from(n.array):[],uv:uv?Array.from(uv.array):[],indices:index,faces:g.attributes.aFace?Array.from(g.attributes.aFace.array):[],cells:g.attributes.aCell?Array.from(g.attributes.aCell.array):[],rands:g.attributes.aRand?Array.from(g.attributes.aRand.array):[]});}
for(let i=0;i<6;i++){add(`crystal${i}`,createCrystalGeometry({seed:4.1+i*17.3,sides:settings.venom.facets,taper:settings.venom.taper,roughness:settings.venom.gemRough,bend:settings.venom.bend}));add(`monolith${i}`,createMonolithGeometry(monolithVariantOptions(Math.min(i,4),settings.quake)));add(`debris${i}`,createDebrisGeometry(i*7+1));add(`shard${i}`,createShardGeometry(i*7+1));}
const {createShatterPlateGeometry}=await mod('src/assets/ShatterGeometry.js');
for(const id of ['venom','quake']){const c=settings[id],rock=id==='quake';add(id+'Plate',createShatterPlateGeometry({seed:17.3,cells:rock?c.plateCells:c.slabCount,depth:rock?c.plateDepth:c.slabDepth,bias:rock?c.plateBias:c.slabBias,ragged:rock?c.plateRagged:c.slabRagged}));}
// Load the original GLB and apply the same canonical serpent conversion as Three.js.
globalThis.ProgressEvent=class{constructor(type,args){Object.assign(this,args);this.type=type;}};
const {GLTFLoader}=await mod('node_modules/three/examples/jsm/loaders/GLTFLoader.js');
const {buildSerpentGeometry}=await mod('src/assets/SerpentGeometry.js');
const bytes=fs.readFileSync(path.join(source,'public/models/snake.glb'));
const jsonSize=bytes.readUInt32LE(12);const document=JSON.parse(bytes.subarray(20,20+jsonSize).toString());const binStart=20+jsonSize+8;document.buffers[0].uri='data:application/octet-stream;base64,'+bytes.subarray(binStart).toString('base64');delete document.materials;delete document.images;delete document.textures;for(const m of document.meshes)for(const p of m.primitives)delete p.material;const gltf=await new GLTFLoader().parseAsync(JSON.stringify(document),'');
const serpent=buildSerpentGeometry(gltf.scene,{ghosts:1});
console.log('Serpent export keys',Object.keys(serpent));
add('serpent',serpent.body??serpent.geometry??serpent);
fs.writeFileSync(path.join(destination,'Geometry.json'),JSON.stringify({meshes}));
console.log(`Exported ${entries.length} settings, ${meshes.length} source geometries and ${ELEMENTS.length} abilities.`);
