#define MONO_CAIRO_DEBUG_DISPOSE


using System;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

using System.Diagnostics;

//using GGL;
using Crow;
using System.Threading;
using GGL;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using Tetra;
using Tetra.DynamicShading;

namespace Ottd3D
{
	public class Ottd3DWindow : OpenTKGameWindow, IBindable
	{
		#region IBindable implementation
		List<Binding> bindings = new List<Binding> ();
		public List<Binding> Bindings {
			get { return bindings; }
		}
		#endregion

		public enum GameState
		{
			Playing,
			RailTrackEdition,
		}
		public enum ShadingPass { Normal, Shadow };

		[StructLayout(LayoutKind.Sequential)]
		public struct UBOSharedData
		{
			public Matrix4 projection;
			public Matrix4 view;
			public Matrix4 normal;
			public Matrix4 shadowTexMat;
			public Vector4 LightPosition;
			public Vector4 Color;
			public Vector4 Shared;		//x ScreenGamma;
										//y pass => 0 normal pass
										//		 => 1 Shadow pass
			public void SetPass(ShadingPass pass){
				Shared.Y = (float)pass;
			}
		}
		[StructLayout(LayoutKind.Explicit, Size=48)]
		public struct UBOMaterialData
		{
			[FieldOffset(0)]public Vector3 Diffuse;
			[FieldOffset(16)]public Vector3 Ambient;
			[FieldOffset(32)]public Vector3 Specular;
			[FieldOffset(44)]public float Shininess;

			public UBOMaterialData(Vector3 kd, Vector3 ka, Vector3 ks, float shininess = 1.0f){
				Diffuse = kd;
				Ambient = ka;
				Specular = ks;
				Shininess = shininess;
			}
		}
		[StructLayout(LayoutKind.Sequential)]
		public struct UBOFogData
		{
			public Vector4 fogColor;
			public Vector4 fog;

			public static UBOFogData CreateUBOFogData()
			{
				UBOFogData tmp;
				tmp.fogColor = new Vector4(0.7f,0.7f,0.7f,1.0f);
				tmp.fog = new Vector4(
					100.0f, // This is only for linear fog
					300.0f, // This is only for linear fog
					0.008f, // For exp and exp2 equation   
					1f); // 0 = linear, 1 = exp, 2 = exp2
				return tmp;
			}
		}



		public GameState CurrentState = GameState.Playing;
		public Track RailTrack = new Track();

		#region  scene matrix and vectors
		public static Matrix4 modelview;
		public static Matrix4 projection;

		public float EyeDist { 
			get { return eyeDist; } 
			set { 
				eyeDist = value; 
				UpdateViewMatrix ();
			} 
		}
		public Vector3 vEyeTarget = new Vector3(32, 32, 0f);
		public Vector3 vLook = Vector3.Normalize(new Vector3(-1f, -1f, 1f));  // Camera vLook Vector
		public float zFar = 400.0f;
		public float zNear = 0.8f;
		public float fovY = (float)Math.PI / 4;

		float eyeDist = 100;
		float eyeDistTarget = 100f;
		float MoveSpeed = 0.004f;
		float ZoomSpeed = 2.0f;
		float RotationSpeed = 0.005f;

		public Vector4 vLight = new Vector4 (0.5f, 0.5f, -1.0f, 0f);
		public static Matrix4 lightProjection, lightView;
		int lightZFar = 20;
		int lightZNear = -20;
		float lightProjSize = 10f;


		UBOMaterialData terrainMat = new UBOMaterialData(
			new Vector3 (0.8f, 0.8f, 0.8f),
			new Vector3 (0.1f, 0.1f, 0.1f),
			new Vector3 (0.0f,0.0f,0.0f),
			1f
		);
		UBOMaterialData heolMat = new UBOMaterialData(
			new Vector3 (1.0f, 1.0f, 1.0f),
			new Vector3 (0.01f, 0.01f, 0.01f),
			new Vector3 (1.0f,1.0f,1.0f),
			1.0f
		);
		UBOMaterialData carMat = new UBOMaterialData(
			new Vector3 (1.0f, 1.0f, 1.0f),
			new Vector3 (0.3f, 0.3f, 0.3f),
			new Vector3 (1.0f, 1.0f, 1.0f),
			128.0f
		);

		UniformBufferObject<UBOMaterialData> material;
		UniformBufferObject<UBOSharedData> shaderSharedData;
		UniformBufferObject<UBOFogData> fogData;

		#endregion

		public Vector2 MousePos {
			get { return new Vector2 (Mouse.X, Mouse.Y); }
		}


		VertexArrayObject<WeightedMeshData, WeightedInstancedData> vaoDeformables, transparentItemsVao;
		VertexArrayObject<MeshData, InstancedData> vaoObjects;
		Terrain terrain;

		void initGL(){
			GL.Enable(EnableCap.CullFace);
			GL.CullFace(CullFaceMode.Back);
			GL.Enable(EnableCap.DepthTest);
			GL.DepthFunc (DepthFunction.Lequal);
			//			GL.Enable(EnableCap.CullFace);
			GL.PrimitiveRestartIndex (int.MaxValue);
			GL.Enable (EnableCap.PrimitiveRestart);

			GL.Enable (EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
		}
		float[] heolBones = new float[12];
		float[] pawnBones = new float[12];

		VAOItem<WeightedInstancedData> heoliennes, heollow, pawn, trees, treesLeave;
		VAOItem<InstancedData> car;

		void initScene(){
			heolBones[5] = 10f;
			pawnBones [2] = 0.299f;
			pawnBones [5] = 0.90812f;

			int nbHeol = 5;

			terrain = new Terrain (ClientRectangle.Size);
			terrain.gridShader.ShadowMap = shadowMap;

			vaoDeformables = new VertexArrayObject<WeightedMeshData, WeightedInstancedData> ();
			vaoObjects = new VertexArrayObject<MeshData, InstancedData> ();

			car = (VAOItem<InstancedData>)vaoObjects.Add (OBJMeshLoader.Load ("Meshes/car.obj"));
			car.DiffuseTexture = Tetra.Texture.Load ("Meshes/0000.png");
			car.InstancedDatas = new InstancedData[nbHeol];
			for (int i = 0; i < nbHeol; i++) {
				Vector2 pos = new Vector2 ((float)rnd.Next(0,terrain.GridSize), (float)rnd.Next(0,terrain.GridSize));
				car.InstancedDatas[i].modelMats = Matrix4.CreateScale(0.2f) * Matrix4.CreateTranslation (pos.X-(pos.X % 4f) + 0.5f, pos.Y-(pos.Y % 4f) + 0.5f, 0.1f);
			}
			car.UpdateInstancesData();


			nbHeol = 50;
//			trees = (VAOItem<WeightedInstancedData>)vaoDeformables.Add (OBJMeshLoader.Load ("Meshes/trees/treesTrunk.obj"));
//			treesLeave = (VAOItem<WeightedInstancedData>)vaoDeformables.Add (OBJMeshLoader.Load ("Meshes/trees/treesLeaves.obj"));
//			trees.DiffuseTexture = Tetra.Texture.Load ("Meshes/trees/treeTrunk.jpg");
//			treesLeave.DiffuseTexture = Tetra.Texture.Load ("Meshes/trees/treeLeaves.png");
//			trees.InstancedDatas = new Tetra.WeightedInstancedData[nbHeol];
//			treesLeave.InstancedDatas = new Tetra.WeightedInstancedData[nbHeol];
//			for (int i = 0; i < nbHeol; i++) {
//				Vector2 pos = new Vector2 ((float)rnd.Next(0,terrain.GridSize), (float)rnd.Next(0,terrain.GridSize));
//				float angle = (float)(rnd.NextDouble() * Math.PI);
//				trees.InstancedDatas[i].modelMats = Matrix4.CreateRotationZ(angle) * Matrix4.CreateScale(4f) * Matrix4.CreateTranslation (pos.X-(pos.X % 4f) + 0.5f, pos.Y-(pos.Y % 4f) + 0.5f, 0f);
//				treesLeave.InstancedDatas [i].modelMats = trees.InstancedDatas [i].modelMats;
//			}
//			trees.UpdateInstancesData();
//
//			treesLeave.UpdateInstancesData();
			//HEOLIENNES
			nbHeol = 5;
//			heoliennes = (VAOItem<WeightedInstancedData>)vaoDeformables.Add (OBJMeshLoader.Load ("Meshes/heolienne.obj"));
//			heoliennes.DiffuseTexture = Tetra.Texture.Load ("Meshes/heolienne.png");
//			heoliennes.InstancedDatas = new Tetra.WeightedInstancedData[nbHeol];
//			for (int i = 0; i < nbHeol; i++) {
//				Vector2 pos = new Vector2 ((float)rnd.Next(0,terrain.GridSize), (float)rnd.Next(0,terrain.GridSize));
//				heoliennes.InstancedDatas[i].modelMats = Matrix4.CreateScale(0.1f) * Matrix4.CreateTranslation (pos.X-(pos.X % 4f) + 0.5f, pos.Y-(pos.Y % 4f) + 0.5f, 0f);
//				heoliennes.InstancedDatas [i].quat0 = Quaternion.Identity;
//				heoliennes.InstancedDatas [i].quat1 = Quaternion.Identity;
//				heoliennes.InstancedDatas [i].quat2 = Quaternion.Identity;
//				heoliennes.InstancedDatas [i].quat3 = Quaternion.Identity;
////				heoliennes.InstancedDatas [i].bpos0 = new Vector4 (0f, 0f, 0f, 0f);
////				heoliennes.InstancedDatas [i].bpos1 = new Vector4 (0f, 0f, 0f, 0f);
////				heoliennes.InstancedDatas [i].bpos2 = new Vector4 (0f, 0f, 0f, 0f);
////				heoliennes.InstancedDatas [i].bpos3 = new Vector4 (0f, 0f, 0f, 0f);
//			}
//			heoliennes.UpdateInstancesData();
			nbHeol = 5;
			heollow = (VAOItem<WeightedInstancedData>)vaoDeformables.Add (OBJMeshLoader.Load ("Meshes/heolienne_lod0.obj"));
			heollow.DiffuseTexture = Tetra.Texture.Load ("Meshes/heollow.png");
			heollow.InstancedDatas = new Tetra.WeightedInstancedData[nbHeol];
			for (int i = 0; i < nbHeol; i++) {
				Vector2 pos = new Vector2 ((float)rnd.Next(0,terrain.GridSize), (float)rnd.Next(0,terrain.GridSize));
				heollow.InstancedDatas[i].modelMats = Matrix4.CreateTranslation (pos.X-(pos.X % 4f) + 0.5f, pos.Y-(pos.Y % 4f) + 0.5f, 0f);
				heollow.InstancedDatas [i].quat0 = Quaternion.Identity;
				heollow.InstancedDatas [i].quat1 = Quaternion.Identity;
				heollow.InstancedDatas [i].quat2 = Quaternion.Identity;
				heollow.InstancedDatas [i].quat3 = Quaternion.Identity;
//				heollow.InstancedDatas [i].bpos0 = new Vector4 (0f, 0f, 0f, 0f);
//				heollow.InstancedDatas [i].bpos1 = new Vector4 (0f, 0f, 0f, 0f);
//				heollow.InstancedDatas [i].bpos2 = new Vector4 (0f, 0f, 0f, 0f);
//				heollow.InstancedDatas [i].bpos3 = new Vector4 (0f, 0f, 0f, 0f);
			}
			heollow.UpdateInstancesData();

//			pawn = (VAOItem<WeightedInstancedData>)vaoDeformables.Add (OBJMeshLoader.Load ("Meshes/pawn.obj"));
//			pawn.DiffuseTexture = Tetra.Texture.Load ("Meshes/pawn.png");
//			pawn.InstancedDatas = new Tetra.WeightedInstancedData[nbHeol];
//			for (int i = 0; i < nbHeol; i++) {
//				Vector2 pos = new Vector2 ((float)rnd.Next(0,terrain.GridSize), (float)rnd.Next(0,terrain.GridSize));
//				pawn.InstancedDatas[i].modelMats = Matrix4.CreateTranslation (pos.X-(pos.X % 4f) + 0.5f, pos.Y-(pos.Y % 4f) + 0.5f, 0f);
//				pawn.InstancedDatas [i].quat0 = Quaternion.Identity;
//				pawn.InstancedDatas [i].quat1 = Quaternion.Identity;
//				pawn.InstancedDatas [i].quat2 = Quaternion.Identity;
//				pawn.InstancedDatas [i].quat3 = Quaternion.Identity;
//			}
//			pawn.UpdateInstancesData();

			//landItemsVao.ComputeTangents();
			vaoDeformables.BuildBuffers ();
			vaoObjects.BuildBuffers ();

//			const float treezone = 32;
//			const int treeCount = 50;
//			transparentItemsVao = new VertexArrayObject<MeshData, VAOInstancedData> ();
//
//			//====TREE1====
//			//			vaoi = transparentItemsVao.Add (Tetra.OBJMeshLoader.Load ("#Ottd3D.images.trees.obj__pinet1.obj"));
//			//			vaoi.DiffuseTexture = Tetra.Texture.Load("#Ottd3D.images.trees.pinet1.png");
//			//			vaoi.modelMats = new Matrix4[treeCount];
//			//			for (int i = 0; i < treeCount; i++) {				
//			//				Vector2 pos = new Vector2 ((float)rnd.NextDouble() * treezone, (float)rnd.NextDouble() * treezone);
//			//				float scale = (float)rnd.NextDouble () * 0.002f + 0.004f;
//			//				vaoi.modelMats[i] =treeRot * Matrix4.CreateScale (scale)* Matrix4.CreateTranslation(pos.X, pos.Y, 0f);
//			//			}
//			//			vaoi.UpdateInstancesData ();
//
//			//====TREE2====
//			//			addRandomTrees (transparentItemsVao, treeCount,
//			//				"#Ottd3D.images.trees.simple.obj",
//			//				"#Ottd3D.images.trees.birch_tree_small_20131230_2041956203.png",400f);
//
//			//			addRandomTrees (transparentItemsVao, treeCount,
//			//				"#Ottd3D.images.trees.obj__pinet1.obj",
//			//				"#Ottd3D.images.trees.pinet1.png",5f);
//			addRandomTrees (transparentItemsVao, treeCount,
//				"images/trees/obj__pinet2.obj",
//				"images/trees/pinet2.png",3f);
//			//			addRandomTrees (transparentItemsVao, treeCount,
//			//				"#Ottd3D.images.trees.obj__tree1.obj",
//			//				"#Ottd3D.images.trees.tree1.png",5f);
//			//			addRandomTrees (transparentItemsVao, treeCount,
//			//				"#Ottd3D.images.trees.obj__tree2.obj",
//			//				"#Ottd3D.images.trees.tree2.png", 5f);
//			//			addRandomTrees (transparentItemsVao, treeCount,
//			//				"#Ottd3D.images.trees.obj__tree3.obj",
//			//				"#Ottd3D.images.trees.tree3.png", 5f);
//
//			//transparentItemsVao.ComputeTangents ();
//			transparentItemsVao.BuildBuffers ();


		}
		void drawScene(){
			GL.Enable (EnableCap.DepthTest);

			material.Update (terrainMat);

			terrain.Render ();

			drawLandItems ();

		}
		void drawLandItems(){
			//			GL.Disable (EnableCap.Blend);

			//material.Update (heolMat);
			material.Update (carMat);

			instancedObjShader.Enable ();
			vaoObjects.Bind ();

			vaoObjects.Render (PrimitiveType.Triangles);


			deformableObjShader.Enable ();
			vaoDeformables.Bind ();
			material.Update (heolMat);
			//			landItemsVao.Render (PrimitiveType.Triangles, trees);
			//			landItemsVao.Render (PrimitiveType.Triangles, treesLeave);
			deformableObjShader.SetBones (heolBones);
			vaoDeformables.Render (PrimitiveType.Triangles, heollow);
//			deformableObjShader.SetBones (pawnBones);
//			vaoDeformables.Render (PrimitiveType.Triangles, pawn);
			vaoDeformables.Unbind ();


			//			GL.Enable (EnableCap.Blend);
			//			GL.Enable (EnableCap.AlphaTest);

			//			transparentItemsVao.Bind ();
			//
			//			//GL.Disable (EnableCap.Blend);
			//			//GL.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.Zero );
			//			//			GL.BlendFunc (BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
			//			//			GL.DepthMask (false);
			//
			//
			//			//			GL.AlphaFunc (AlphaFunction.Greater, 0.0f);
			//			//			GL.DepthMask (false);
			//
			//			GL.Enable (EnableCap.Blend);
			//			//GL.Disable (EnableCap.DepthTest);
			//
			//			transparentItemsVao.Render (PrimitiveType.Triangles);
			//
			//			//GL.Enable (EnableCap.DepthTest);
			//
			//			//			GL.AlphaFunc (AlphaFunction.Equal, 1.0f);
			//			//			GL.DepthMask (true);
			//			//			transparentItemsVao.Render (PrimitiveType.Triangles);
			//
			//			//GL.Disable (EnableCap.Blend);
			//			//GL.Disable (EnableCap.AlphaTest);
			//			//
			//			//			GL.BlendFunc (BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
			//
			//
			//			transparentItemsVao.Unbind ();

			//RailTrack.Render ();			
		}

		Random rnd = new Random ();			
		void addRandomTrees(VertexArrayObject<MeshData, InstancedData> vao,
			int count, string objPath, string diffTexPath, float _scale=1f)
		{			
			VAOItem<InstancedData> vaoi = (VAOItem<InstancedData>)vao.Add (OBJMeshLoader.Load (objPath));
			vaoi.DiffuseTexture = Tetra.Texture.Load(diffTexPath);
			vaoi.InstancedDatas = new Tetra.InstancedData[count];
			for (int i = 0; i < count; i++) {				
				Vector2 pos = new Vector2 ((float)rnd.NextDouble() * terrain.GridSize, (float)rnd.NextDouble() * terrain.GridSize);
				float scale = (float)(rnd.NextDouble () * 0.002f + 0.004f)*_scale;
				vaoi.InstancedDatas[i].modelMats =Matrix4.CreateRotationX (MathHelper.PiOver2) * Matrix4.CreateScale (scale)* Matrix4.CreateTranslation(pos.X, pos.Y, 0f);
			}
			vaoi.UpdateInstancesData ();			
		}

		#region animation
		float heolAngle = 0f, pawnAngle=0f, pawnAngleIncrement=0.05f, pawnAngleLimit=0.5f;
		void animate()
		{

			//			if (updateMatrices) {
			//				heolienne.UpdateModelsMat ();
			//				updateMatrices = false;
			//				gridCacheIsUpToDate = false;
			//			}
			DualQuaternion dq = new DualQuaternion(Quaternion.FromEulerAngles(0f,heolAngle,0f),new Vector3(0f,0f,0f));
//			for (int i = 0; i < heoliennes.InstancedDatas.Length; i++) {
//				heoliennes.InstancedDatas [i].quat0 = new DualQuaternion(Quaternion.FromEulerAngles(heolAngle*0.2f,0f,0f),new Vector3(0f,0f,0f)).m_real;
//				heoliennes.InstancedDatas [i].bpos1 = dq.m_dual;
//				heoliennes.InstancedDatas [i].quat1 = heoliennes.InstancedDatas [i].quat0*dq.m_real;
//			}
//			heoliennes.UpdateInstancesData ();

			for (int i = 0; i < heollow.InstancedDatas.Length; i++) {
				//heollow.InstancedDatas [i].bpos1 = dq.m_dual;
				heollow.InstancedDatas [i].quat1 = dq.m_real;
			}
			heollow.UpdateInstancesData ();

//			for (int i = 0; i < pawn.InstancedDatas.Length; i++) {
//				pawn.InstancedDatas [i].quat1 = Quaternion.FromEulerAngles(pawnAngle,pawnAngle,pawnAngle);
//			}
//			pawn.UpdateInstancesData ();
//
//			pawnAngle += pawnAngleIncrement;
//
//			if (pawnAngleIncrement > 0f){
//				if (pawnAngle > pawnAngleLimit)
//					pawnAngleIncrement = -pawnAngleIncrement;
//			}else if (pawnAngle < -pawnAngleLimit)
//				pawnAngleIncrement = -pawnAngleIncrement;

			heolAngle += MathHelper.Pi * 0.007f;			
		}
		#endregion

		#region Shaders

		public static DeformablesShader deformableObjShader;
		public static InstancedShader instancedObjShader;

		void initShaders()
		{
			deformableObjShader = new DeformablesShader ("Shaders/objects.vert", "Shaders/objects.frag");
			deformableObjShader.ShadowMap = shadowMap;

			instancedObjShader = new InstancedShader ("Shaders/objInstanced.vert", "Shaders/objects.frag");
			instancedObjShader.ShadowMap = shadowMap;
			//objShader.DiffuseTexture = heolienneTex;

			shaderSharedData = new UniformBufferObject<UBOSharedData> (BufferUsageHint.DynamicCopy);
			shaderSharedData.Datas.Color = new Vector4 (0.8f,0.8f,0.8f,1.0f);
			shaderSharedData.Datas.Shared.X = 1.0f;
			shaderSharedData.Datas.SetPass (ShadingPass.Normal);
			shaderSharedData.Bind (0);

			fogData = new UniformBufferObject<UBOFogData> (
				UBOFogData.CreateUBOFogData (), BufferUsageHint.StaticCopy);
			fogData.Bind (1);

			material = new UniformBufferObject<UBOMaterialData> (BufferUsageHint.DynamicCopy);
			material.Bind (2);
		}
			
		void updateShadersMatrices(){
			if (renderLightPOV) {
				terrain.UpdateMVP (lightProjection, lightView, vLook);
				shaderSharedData.Datas.projection = lightProjection;
				shaderSharedData.Datas.view = lightView;
			} else {
				terrain.UpdateMVP (projection, modelview, vLook);
				shaderSharedData.Datas.projection = projection;
				shaderSharedData.Datas.view = modelview;
			}
			shaderSharedData.Datas.normal = modelview.Inverted();
			shaderSharedData.Datas.shadowTexMat = shadowTexMat;
			shaderSharedData.Datas.normal.Transpose ();
			shaderSharedData.Datas.LightPosition = Vector4.Transform(vLight, modelview);

			shaderSharedData.Update ();
		}

		#endregion

		bool queryUpdateGridCache = false;

		public void UpdateViewMatrix()
		{
			Rectangle r = this.ClientRectangle;
			GL.Viewport( r.X, r.Y, r.Width, r.Height);
			projection = Matrix4.CreatePerspectiveFieldOfView (fovY, r.Width / (float)r.Height, zNear, zFar);
			Vector3 vEye = vEyeTarget + vLook * eyeDist;
			modelview = Matrix4.LookAt(vEye, vEyeTarget, Vector3.UnitZ);

			if (vLight.W == 0)
				lightView = Matrix4.LookAt(
					vEyeTarget - vLight.Xyz.Normalized(),
					vEyeTarget, Vector3.UnitZ);
			else
				lightView = Matrix4.LookAt(
					vEyeTarget + vLight.Xyz,
					vEyeTarget, Vector3.UnitZ);
//			Matrix4 tmp = new Matrix4(
//				0.5f, 0.0f, 0.0f, 0.0f, 
//				0.0f, 0.5f, 0.0f, 0.0f,
//				0.0f, 0.0f, 0.5f, 0.0f,
//				0.5f, 0.5f, 0.5f, 1.0f);
			Matrix4 tmp = Matrix4.CreateScale(0.5f) * Matrix4.CreateTranslation(0.5f,0.5f,0.5f);
			lightProjection = Matrix4.CreateOrthographicOffCenter
				(-eyeDist, eyeDist, -eyeDist,eyeDist, -eyeDist, eyeDist);
			
			shadowTexMat = lightView * lightProjection*tmp;

			queryUpdateGridCache = true;
			queryUpdateShadowMap = true;
			queryUpdateShaderMatices = true;
//			if (tDepthSort != null) {
//				killDepthSortThread ();
//			}
//			tDepthSort = new Thread (depthSortThread);
//			tDepthSort.IsBackground = true;
//			tDepthSort.Start ();
//			tDepthSort.Join ();
		}			


		#region shadow map

		const int SHADOW_MAP_SIZE = 2048;
		float bias = 0.0005f;
		Matrix4 shadowTexMat;

		int shadowMap, fboShadow;

		bool queryUpdateShadowMap = false;

		void initShadowMap()
		{
			
			GL.GenTextures(1, out shadowMap);
			GL.BindTexture(TextureTarget.Texture2D, shadowMap);

			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.CompareRToTexture);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareFunc, (int)All.Lequal);
			//GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.DepthTextureMode, (int)All.Intensity);
			GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32f, SHADOW_MAP_SIZE, SHADOW_MAP_SIZE, 0, OpenTK.Graphics.OpenGL.PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);

			GL.BindTexture(TextureTarget.Texture2D, 0);
			GL.ActiveTexture(TextureUnit.Texture0);

			GL.GenFramebuffers(1, out fboShadow);
			GL.BindFramebuffer(FramebufferTarget.Framebuffer, fboShadow);
			GL.DrawBuffer (DrawBufferMode.None);
			GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, shadowMap, 0);

			if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
			{
				throw new Exception(GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer).ToString());
			}
				
			GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
		}

		void updateShadowMap()
		{
			GL.BindFramebuffer(FramebufferTarget.Framebuffer, fboShadow);

			//terrain.UpdateMVP (lightProjection, lightView, vLook);
			shaderSharedData.Datas.projection = lightProjection;
			shaderSharedData.Datas.view = lightView;
			shaderSharedData.Datas.SetPass (ShadingPass.Shadow);
			shaderSharedData.Update ();

			int[] viewport = new int[4];
			GL.GetInteger (GetPName.Viewport, viewport);
			GL.Viewport(0, 0, SHADOW_MAP_SIZE, SHADOW_MAP_SIZE);

			GL.Clear(ClearBufferMask.DepthBufferBit);
			GL.Disable(EnableCap.Normalize);
			GL.ShadeModel(ShadingModel.Flat);
			GL.CullFace(CullFaceMode.Front);


			terrain.RenderForShadowPass ();

			drawLandItems();

			//GL.ColorMask(true, true, true, true);
			GL.ShadeModel(ShadingModel.Smooth);
			GL.CullFace(CullFaceMode.Back);
			GL.Enable(EnableCap.Normalize);
			GL.ShadeModel(ShadingModel.Smooth);

			shaderSharedData.Datas.projection = projection;
			shaderSharedData.Datas.view = modelview;
			shaderSharedData.Datas.SetPass (ShadingPass.Normal);
			shaderSharedData.Update ();

			GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
			GL.Viewport (viewport [0], viewport [1], viewport [2], viewport [3]);
		}
		#endregion

		#region Interface
		void initInterface()
		{
			this.MouseButtonUp += Mouse_ButtonUp;
			this.MouseButtonDown += Mouse_ButtonDown;
			this.MouseWheelChanged += Mouse_WheelChanged;
			this.MouseMove += Mouse_Move;
			this.KeyDown += Ottd3DWindow_KeyDown;

			//LoadInterface("#Ottd3D.ui.menu.goml").DataSource = this;


			CrowInterface.LoadInterface("#Ottd3D.ui.menu.crow").DataSource = this;

			viewedTexture = terrain.gridDepthTex;

			Crow.CompilerServices.ResolveBindings (this.Bindings);

		}


		string viewedImgPath = @"tmp.png";
		int viewedTexture;
		volatile bool
			renderLightPOV = false,
			queryUpdateShaderMatices = false,
			queryTextureViewerUpdate = false, 
			queryGammaUpdate = false,
			autoUpdate = false;

		public bool RenderLightPOV {
			get { return renderLightPOV; }
			set {
				if (value == renderLightPOV)
					return;
				renderLightPOV = value;
				NotifyValueChanged ("RenderLightPOV", renderLightPOV);
				queryUpdateShaderMatices = true;
				queryUpdateGridCache = true;
			}
		}

		public bool AutoUpdate {
			get { return autoUpdate; }
			set {
				if (value == autoUpdate)
					return;
				autoUpdate = value;
				NotifyValueChanged ("AutoUpdate", autoUpdate);
			}
		}

		public float ScreenGamma {
			get { return shaderSharedData.Datas.Shared.X; }
			set {
				if (value == shaderSharedData.Datas.Shared.X)
					return;	
				shaderSharedData.Datas.Shared.X = value;
				NotifyValueChanged ("ScreenGamma", shaderSharedData.Datas.Shared.X);
				queryGammaUpdate = true;
			}
		}

		public string ViewedImgPath {
			get {
				return viewedImgPath;
			}
			set {
				viewedImgPath = value;
				NotifyValueChanged ("ViewedImgPath", viewedImgPath);
			}
		}
			
		#region Mouse
		void Mouse_ButtonDown (object sender, OpenTK.Input.MouseButtonEventArgs e)
		{
			if (e.Button == OpenTK.Input.MouseButton.Left) {				
				switch (CurrentState) {
				case GameState.Playing:
					break;
				case GameState.RailTrackEdition:					
					if (RailTrack.CurrentSegment == null) {
						RailTrack.CurrentSegment = new TrackSegment (terrain.SelCenterPos);
						RailTrack.TrackStarts.Add (RailTrack.CurrentSegment);
					} else {
						TrackSegment newTS = new TrackSegment (RailTrack.CurrentSegment.EndPos, RailTrack.vEnd);
						RailTrack.CurrentSegment.NextSegment.Add (newTS);
						newTS.PreviousSegment.Add (RailTrack.CurrentSegment);
						RailTrack.CurrentSegment = newTS;
					}

					RailTrack.UpdateTrackMesh ();
					break;
				}
			}
		}
		void Mouse_ButtonUp (object sender, OpenTK.Input.MouseButtonEventArgs e)
		{
		}
		void Mouse_Move(object sender, OpenTK.Input.MouseMoveEventArgs e)
		{
			
			if (e.XDelta != 0 || e.YDelta != 0)
			{
				terrain.MouseMove(e);
				NotifyValueChanged("MousePos", MousePos);
				//selection texture has clientRect size and 4 bytes per pixel, so
				switch (CurrentState) {
				case GameState.Playing:
					break;
				case GameState.RailTrackEdition:
					TrackSegment ts = RailTrack.CurrentSegment;
					if (ts != null) {						
						if (terrain.SelCenterPos == ts.StartPos)
							return;
						if (e.Mouse.LeftButton == OpenTK.Input.ButtonState.Pressed) {					
							ts.EndPos = terrain.SelCenterPos;
							ts.vStart = Vector3.Normalize (ts.EndPos - ts.StartPos);
							RailTrack.vEnd = ts.vStart;
						} else {
							ts.EndPos = terrain.SelCenterPos;
							Vector3 vDir = Vector3.Normalize (ts.EndPos - ts.StartPos);
							float dot = Vector3.Dot (ts.vStart, vDir);
							RailTrack.vEnd = -(ts.vStart - 2 * dot * vDir);
						}
						RailTrack.UpdateTrackMesh ();
					}
					break;
				}	
				if (e.Mouse.MiddleButton == OpenTK.Input.ButtonState.Pressed) {
					if (Keyboard [OpenTK.Input.Key.ShiftLeft]) {
						Vector3 v = new Vector3 (
							Vector2.Normalize (vLook.Xy.PerpendicularLeft));
						Vector3 tmp = Vector3.Transform (vLook, 
							Matrix4.CreateRotationZ (-e.XDelta * RotationSpeed) *
							Matrix4.CreateFromAxisAngle (v, -e.YDelta * RotationSpeed));
						tmp.Normalize ();
						if (tmp.Z <= 0f)
							return;
						vLook = tmp;
					} else {
						Vector3 vH = new Vector3(Vector2.Normalize(vLook.Xy.PerpendicularLeft) * e.XDelta * MoveSpeed * eyeDist);
						Vector3 vV = new Vector3(Vector2.Normalize(vLook.Xy) * e.YDelta * MoveSpeed * eyeDist);
						vEyeTarget -= vH + vV;						
					}
					UpdateViewMatrix ();
				}
			}

		}
			
		void Mouse_WheelChanged(object sender, OpenTK.Input.MouseWheelEventArgs e)
		{
			float speed = ZoomSpeed;

			if (Keyboard [OpenTK.Input.Key.ShiftLeft]) {				
				if (e.Delta > 0)
					terrain.SelectionRadius *= 1.25f;
				else
					terrain.SelectionRadius *= 0.8f;
				if (terrain.SelectionRadius > 0.1f)
					terrain.SelectionRadius = 0.1f;
				else if (terrain.SelectionRadius < 1f/1024f)
					terrain.SelectionRadius = 1f/1024f;
				return;
			}
			if (Keyboard[OpenTK.Input.Key.ShiftLeft])
				speed *= 0.1f;
			else if (Keyboard[OpenTK.Input.Key.ControlLeft])
				speed *= 20.0f;

			eyeDistTarget -= e.Delta * speed;
			if (eyeDistTarget < zNear+1)
				eyeDistTarget = zNear+1;
			else if (eyeDistTarget > zFar-6)
				eyeDistTarget = zFar-6;

			//EyeDist = eyeDistTarget;
			Animation.StartAnimation(new Animation<float> (this, "EyeDist", eyeDistTarget, (eyeDistTarget - eyeDist) * 0.1f));
		}
		#endregion

		#region Keyboard
		void Ottd3DWindow_KeyDown (object sender, OpenTK.Input.KeyboardKeyEventArgs e)
		{
			switch (e.Key) {
			case OpenTK.Input.Key.Escape:
				if (CurrentState == GameState.RailTrackEdition) {
					if (RailTrack.CurrentSegment != null) {						
						RailTrack.CurrentSegment = null;
						//RailTrack.TrackStarts.Remove (RailTrack.CurrentSegment);

						RailTrack.UpdateTrackMesh ();
					}
				}
				break;				
			}
		}
		#endregion

		void onShowWindow (object sender, EventArgs e){
			string path = "";
			object data = this;
			bool twoWayBinding = false;
			CheckBox g = sender as CheckBox;
			switch (g.Name) {
			case "checkImgViewer":
				path = "#Ottd3D.ui.imgView.crow";
				twoWayBinding = true;
				queryTextureViewerUpdate = true;
				break;
			case "checkSplatting":
				terrain.CurrentState = Terrain.State.GroundTexturing;
				twoWayBinding = true;
				path = "#Ottd3D.ui.SpattingMenu.goml";
				data = terrain;
				break;
			case "checkHeightMap":
				terrain.CurrentState = Terrain.State.HMEdition;
				twoWayBinding = true;
				path = "#Ottd3D.ui.heightEditionMenu.goml";
				data = terrain;
				break;
			case "checkFps":
				path = "#Ottd3D.ui.fps.crow";
				break;
			case "checkShaderEditor":
				path = "#Ottd3D.ui.ShaderEditor.crow";
				twoWayBinding = true;
				break;
			}
			Window win = CrowInterface.LoadInterface (path) as Window;
			g.Tag = win;
			if (g.Name == "checkSplatting")
				win.Focused += splatWin_Focused;
			else if (g.Name == "checkHeightMap")
				win.Focused += hmWin_Focused;


			win.Tag = g;
			win.Closing += (object winsender, EventArgs wine) => {
				CheckBox cb = (winsender as Window).Tag as CheckBox;
				cb.Tag = null;
				cb.IsChecked = false;
			};
			if (g.Name == "checkShaderEditor")
				data = win;
			win.DataSource = data;
			if (twoWayBinding)
				Crow.CompilerServices.ResolveBindings ((data as IBindable).Bindings);
		}

		void splatWin_Focused (object sender, EventArgs e)
		{
			terrain.CurrentState = Terrain.State.GroundTexturing;
		}
		void hmWin_Focused (object sender, EventArgs e)
		{
			terrain.CurrentState = Terrain.State.HMEdition;
		}
		void onHideWindow (object sender, EventArgs e)
		{
			CheckBox g = sender as CheckBox;
			if (g.Tag == null)
				return;
			Interface.CurrentInterface.DeleteWidget (g.Tag as GraphicObject);
			g.Tag = null;
			terrain.CurrentState = Terrain.State.Play;
		}
		void onSelectViewedTex (object sender, EventArgs e)
		{
			GraphicObject g = sender as GraphicObject;
			switch (g.Name) {
			case "SH":
				viewedTexture = shadowMap;
				break;
			case "HM":
				viewedTexture = terrain.hmGenerator.OutputTex;
				break;
			case "ST":
				viewedTexture = terrain.splattingBrushShader.OutputTex;
				break;
			case "CD":
				viewedTexture = terrain.gridDepthTex;
				break;
			case "CC":
				viewedTexture = terrain.colorTexId;
				break;
			case "CS":
				viewedTexture = terrain.selectionTexId;
				break;
			case "BBC":
				viewedTexture = -1;
				break;
			case "BBD":
				viewedTexture = -2;
				break;
			}
			queryTextureViewerUpdate = true;
		}
		void onGameStateChange (object sender, EventArgs e)
		{
			GraphicObject g = sender as GraphicObject;
			switch (g.Name) {
			case "Play":
				terrain.CurrentState = Terrain.State.Play;
				break;
			case "HMEdition":
				terrain.CurrentState = Terrain.State.HMEdition;
				break;
			case "GroundTexturing":
				terrain.CurrentState = Terrain.State.GroundTexturing;
				break;
			}
		}
		void onReloadImg (object sender, EventArgs e){
			queryTextureViewerUpdate = true;
		}
		#endregion

		#region Depth Sorting
		volatile bool depthSortingDone = false;
		Thread tDepthSort;

		[SecurityPermissionAttribute(SecurityAction.Demand, ControlThread = true)]
		void killDepthSortThread()
		{
			tDepthSort.Abort();
			tDepthSort.Join ();
		}

		void depthSortThread()
		{
			try {
				VAOItem[] transObjs = null;
				lock (transparentItemsVao.Meshes) {
					transObjs = transparentItemsVao.Meshes.ToArray();
				}
				foreach (VAOItem<InstancedData> item in transObjs.OfType<VAOItem<InstancedData>>()) {
					depthSort (item.InstancedDatas);	
				}

			} catch {
				return;
			}
			depthSortingDone = true;
		}
		void depthSort(Tetra.InstancedData[] datas)
		{
			Vector3 vEye = vEyeTarget + vLook * eyeDist;
			Array.Sort(datas,
				delegate(Tetra.InstancedData x, Tetra.InstancedData y) {
					return (new Vector2(y.modelMats.Row3.X, y.modelMats.Row3.Y) - vEye.Xy).LengthFast.
						CompareTo	((new Vector2(x.modelMats.Row3.X, x.modelMats.Row3.Y) - vEye.Xy).LengthFast); });
			
		}
		#endregion

		#region OTK overrides
		protected override void OnLoad (EventArgs e)
		{

			base.OnLoad (e);

			initGL ();

			initShadowMap ();

			initShaders ();

			initScene ();

			initInterface ();


		}

		uint frameCpt;
		protected override void OnUpdateFrame (FrameEventArgs e)
		{
			frameCpt++;
			if (frameCpt == uint.MaxValue)
				frameCpt = 0;
			
			base.OnUpdateFrame (e);

//			if (Keyboard [OpenTK.Input.Key.ShiftLeft]) {
//				float MoveSpeed = 10f;
//				//light movment
//				if (Keyboard [OpenTK.Input.Key.Up])
//					vLight.X -= MoveSpeed;
//				else if (Keyboard [OpenTK.Input.Key.Down])
//					vLight.X += MoveSpeed;
//				else if (Keyboard [OpenTK.Input.Key.Left])
//					vLight.Y -= MoveSpeed;
//				else if (Keyboard [OpenTK.Input.Key.Right])
//					vLight.Y += MoveSpeed;
//				else if (Keyboard [OpenTK.Input.Key.PageUp])
//					vLight.Z += MoveSpeed;
//				else if (Keyboard [OpenTK.Input.Key.PageDown])
//					vLight.Z -= MoveSpeed;
//				//updateShadersMatrices ();
//				//GL.Light (LightName.Light0, LightParameter.Position, vLight);
//			}

//			if (depthSortingDone) {
//				foreach (Tetra.VAOItem<Tetra.VAOInstancedData> item in transparentItemsVao.Meshes) 
//					item.UpdateInstancesData();	
//
//				depthSortingDone = false;
//			}

			Animation.ProcessAnimations ();
			animate ();

			if (queryUpdateShaderMatices) {
				queryUpdateShaderMatices = false;
				updateShadersMatrices ();
			}

			if (queryGammaUpdate) {
				queryGammaUpdate = false;
				shaderSharedData.Update ();
				queryUpdateGridCache = true;
			}

			//if (queryUpdateShadowMap) {
				queryUpdateShadowMap = false;
				
			//}

			updateShadowMap ();

			material.Update (terrainMat);
			terrain.Update (this, queryUpdateGridCache);
			queryUpdateGridCache = false;



			if (queryTextureViewerUpdate || (autoUpdate && frameCpt % 60 == 0)) {
				queryTextureViewerUpdate = false;

				if (viewedTexture < 0) {					
					GL.ReadBuffer (ReadBufferMode.Back);
					if (viewedTexture == -1) {
						// save backbuffer color
						using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap (ClientSize.Width, ClientSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb)) {
							System.Drawing.Imaging.BitmapData bmpData = bmp.LockBits (
								                                            new System.Drawing.Rectangle (0, 0, ClientSize.Width, ClientSize.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
							GL.ReadPixels (0, 0, ClientSize.Width, ClientSize.Height, PixelFormat.Bgra, PixelType.UnsignedByte, bmpData.Scan0);
							SwapBuffers ();
							bmp.UnlockBits (bmpData);
							bmp.RotateFlip (System.Drawing.RotateFlipType.RotateNoneFlipY);
							bmp.Save (ViewedImgPath);
						}
					} else if (viewedTexture == -2) {
						saveBackBufferDepth ();
//						using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap (ClientSize.Width, ClientSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb)) {
//							System.Drawing.Imaging.BitmapData bmpData = bmp.LockBits (
//								new System.Drawing.Rectangle (0, 0, ClientSize.Width, ClientSize.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
//							GL.ReadPixels (0, 0, ClientSize.Width, ClientSize.Height, PixelFormat.DepthComponent, PixelType.UnsignedByte, bmpData.Scan0);
//							SwapBuffers ();
//							bmp.UnlockBits (bmpData);
//							bmp.RotateFlip (System.Drawing.RotateFlipType.RotateNoneFlipY);
//							bmp.Save (ViewedImgPath);
//						}
					}
				} else {
					Tetra.Texture.SaveTextureFromId (viewedTexture, viewedImgPath);
				}

				ViewedImgPath = viewedImgPath;//notify value changed
			}

		}
		protected override void OnResize (EventArgs e)
		{
			base.OnResize (e);
			terrain.CacheSize = ClientRectangle.Size;
			UpdateViewMatrix();
		}
		public override void GLClear ()
		{
			GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
			GL.Clear (ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
		}
		public override void OnRender (FrameEventArgs e)
		{

			drawScene ();
		}
		#endregion

		void saveBackBufferDepth()
		{
			int backbuffDepth, fbo;

			// Create Depth Renderbuffer
			GL.GenTextures(1, out backbuffDepth);
			GL.BindTexture(TextureTarget.Texture2D, backbuffDepth);
			GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32f, ClientSize.Width,ClientSize.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
			//			GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
			//GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.CompareRToTexture);
			//GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareFunc, (int)All.Lequal);
			//GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.DepthTextureMode, (int)All.Luminance);

			GL.GenFramebuffers(1, out fbo);

			GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
			GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
				TextureTarget.Texture2D, backbuffDepth, 0);
			GL.DrawBuffer (DrawBufferMode.None);

			if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
			{
				throw new Exception(GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer).ToString());
			}
			GL.ClearColor (0f, 0f, 0f, 0f);
			GL.Clear (ClearBufferMask.ColorBufferBit| ClearBufferMask.DepthBufferBit);

			drawScene ();
			SwapBuffers ();

			GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

			Tetra.Texture.SaveTextureFromId (backbuffDepth, viewedImgPath);

			GL.DeleteFramebuffer (fbo);
			GL.DeleteTexture (backbuffDepth);
		}

		#region Main and CTOR
		[STAThread]
		static void Main ()
		{
			Console.WriteLine ("starting example");

			using (Ottd3DWindow win = new Ottd3DWindow( )) {
				win.Run (30.0);
			}
		}
		public Ottd3DWindow ()
			: base(1024, 800, 32, 24, 1, 1, "test")
		{
			VSync = VSyncMode.Off;
		}
		#endregion
	}
}