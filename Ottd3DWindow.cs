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

		[StructLayout(LayoutKind.Sequential)]
		public struct UBOSharedData
		{
			public Matrix4 projection;
			public Matrix4 view;
			public Matrix4 normal;
			public Vector4 LightPosition;
			public Vector4 Color;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct UBOFogData
		{
			public Vector4 fogColor;
			public float fStart; // This is only for linear fog
			public float fEnd; // This is only for linear fog
			public float fDensity; // For exp and exp2 equation   
			public int iEquation; // 0 = linear, 1 = exp, 2 = exp2

			public static UBOFogData CreateUBOFogData()
			{
				UBOFogData tmp;
				tmp.fogColor = new Vector4(0.7f,0.7f,0.7f,1.0f);
				tmp.fStart = 100.0f; // This is only for linear fog
				tmp.fEnd = 300.0f; // This is only for linear fog
				tmp.fDensity = 0.001f; // For exp and exp2 equation   
				tmp.iEquation = 1; // 0 = linear, 1 = exp, 2 = exp2
				return tmp;
			}
		}



		public GameState CurrentState = GameState.RailTrackEdition;
		public Track RailTrack = new Track();

		#region  scene matrix and vectors
		public static Matrix4 modelview;
		public static Matrix4 projection;
		public static int[] viewport = new int[4];

		public float EyeDist { 
			get { return eyeDist; } 
			set { 
				eyeDist = value; 
				UpdateViewMatrix ();
			} 
		}
		public Vector3 vEyeTarget = new Vector3(32, 32, 0f);
		public Vector3 vLook = Vector3.Normalize(new Vector3(-1f, -1f, 1f));  // Camera vLook Vector
		public float zFar = 512.0f;
		public float zNear = 0.1f;
		public float fovY = (float)Math.PI / 4;

		float eyeDist = 100;
		float eyeDistTarget = 100f;
		float MoveSpeed = 0.004f;
		float ZoomSpeed = 10.0f;
		float RotationSpeed = 0.005f;

		public Vector4 vLight = new Vector4 (0.5f, 0.5f, -0.5f, 0f);

		UBOSharedData shaderSharedData;
		UBOFogData fogData;
		int uboShaderSharedData, uboFogData;
		#endregion

		public Vector2 MousePos {
			get { return new Vector2 (Mouse.X, Mouse.Y); }
		}
		string shaderSource;
		public string ShaderSource {
			get { return shaderSource; }
			set {
				if (string.Equals (value, shaderSource, StringComparison.Ordinal))
					return;
				shaderSource = value;
				NotifyValueChanged ("ShaderSource", shaderSource);
			}
		}

		VertexArrayObject<WeightedMeshData, WeightedInstancedData> landItemsVao, transparentItemsVao;
		Terrain terrain;


		void initGL(){
			GL.Enable(EnableCap.DepthTest);
			GL.DepthFunc(DepthFunction.Less);
			//			GL.Enable(EnableCap.CullFace);
			GL.PrimitiveRestartIndex (int.MaxValue);
			GL.Enable (EnableCap.PrimitiveRestart);

			GL.Enable (EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
		}
		VAOItem<WeightedInstancedData> heoliennes, heollow;
		void initScene(){
			int nbHeol = 5;
			terrain = new Terrain (ClientRectangle.Size);
			landItemsVao = new VertexArrayObject<WeightedMeshData, WeightedInstancedData> ();
			//HEOLIENNES
			heoliennes = (VAOItem<WeightedInstancedData>)landItemsVao.Add (OBJMeshLoader.Load ("Meshes/heolienne.obj"));
			heoliennes.DiffuseTexture = Tetra.Texture.Load ("Meshes/heolienne.png");
			heoliennes.InstancedDatas = new Tetra.WeightedInstancedData[nbHeol];
			for (int i = 0; i < nbHeol; i++) {
				Vector2 pos = new Vector2 ((float)rnd.Next(0,terrain.GridSize), (float)rnd.Next(0,terrain.GridSize));
				heoliennes.InstancedDatas[i].modelMats = Matrix4.CreateTranslation (pos.X-(pos.X % 4f) + 0.5f, pos.Y-(pos.Y % 4f) + 0.5f, 0f);
				heoliennes.InstancedDatas [i].quat0 = Quaternion.Identity;
				heoliennes.InstancedDatas [i].quat1 = Quaternion.Identity;
				heoliennes.InstancedDatas [i].quat2 = Quaternion.Identity;
				heoliennes.InstancedDatas [i].quat3 = Quaternion.Identity;
				heoliennes.InstancedDatas [i].bpos0 = new Vector4 (0f, 0f, 0f, 0f);
				heoliennes.InstancedDatas [i].bpos1 = new Vector4 (0f, 0f, 10f, 0f);
				heoliennes.InstancedDatas [i].bpos2 = new Vector4 (0f, 0f, 0f, 0f);
				heoliennes.InstancedDatas [i].bpos3 = new Vector4 (0f, 0f, 0f, 0f);
			}
			heoliennes.UpdateInstancesData();
			nbHeol = 5;
			heollow = (VAOItem<WeightedInstancedData>)landItemsVao.Add (OBJMeshLoader.Load ("Meshes/heolienne_lod0.obj"));
			heollow.DiffuseTexture = Tetra.Texture.Load ("Meshes/heollow.png");
			heollow.InstancedDatas = new Tetra.WeightedInstancedData[nbHeol];
			for (int i = 0; i < nbHeol; i++) {
				Vector2 pos = new Vector2 ((float)rnd.Next(0,terrain.GridSize), (float)rnd.Next(0,terrain.GridSize));
				heollow.InstancedDatas[i].modelMats = Matrix4.CreateTranslation (pos.X-(pos.X % 4f) + 0.5f, pos.Y-(pos.Y % 4f) + 0.5f, 0f);
				heollow.InstancedDatas [i].quat0 = Quaternion.Identity;
				heollow.InstancedDatas [i].quat1 = Quaternion.Identity;
				heollow.InstancedDatas [i].quat2 = Quaternion.Identity;
				heollow.InstancedDatas [i].quat3 = Quaternion.Identity;
				heollow.InstancedDatas [i].bpos0 = new Vector4 (0f, 0f, 0f, 0f);
				heollow.InstancedDatas [i].bpos1 = new Vector4 (0f, 0f, 10f, 0f);
				heollow.InstancedDatas [i].bpos2 = new Vector4 (0f, 0f, 0f, 0f);
				heollow.InstancedDatas [i].bpos3 = new Vector4 (0f, 0f, 0f, 0f);
			}
			heollow.UpdateInstancesData();


			//landItemsVao.ComputeTangents();
			landItemsVao.BuildBuffers ();

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
			terrain.Render ();

//			GL.Disable (EnableCap.Blend);
			objShader.Enable ();

			landItemsVao.Bind ();
			landItemsVao.Render (PrimitiveType.Triangles);
			landItemsVao.Unbind ();


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

		#region Shaders

		public static Mat4InstancedShader objShader;

		void initShaders()
		{
			objShader = new Mat4InstancedShader ("Shaders/objects.vert", "Shaders/objects.frag");

			//objShader.DiffuseTexture = heolienneTex;

			uboShaderSharedData = GL.GenBuffer ();
			GL.BindBuffer (BufferTarget.UniformBuffer, uboShaderSharedData);
			GL.BufferData(BufferTarget.UniformBuffer,Marshal.SizeOf(shaderSharedData),
					ref shaderSharedData, BufferUsageHint.DynamicCopy);
			GL.BindBuffer (BufferTarget.UniformBuffer, 0);
			GL.BindBufferBase (BufferRangeTarget.UniformBuffer, 0, uboShaderSharedData);

			fogData = UBOFogData.CreateUBOFogData();
			uboFogData = GL.GenBuffer ();
			GL.BindBuffer (BufferTarget.UniformBuffer, uboFogData);
			GL.BufferData(BufferTarget.UniformBuffer,Marshal.SizeOf(fogData),
				ref fogData, BufferUsageHint.StaticCopy);
			GL.BindBuffer (BufferTarget.UniformBuffer, 0);
			GL.BindBufferBase (BufferRangeTarget.UniformBuffer, 1, uboFogData);
		}

		void updateShadersMatrices(){			
			terrain.UpdateMVP (projection, modelview, vLook);

			shaderSharedData.projection = projection;
			shaderSharedData.view = modelview;
			shaderSharedData.normal = modelview.Inverted();
			shaderSharedData.normal.Transpose ();
			shaderSharedData.LightPosition = Vector4.Transform(vLight, modelview);
			shaderSharedData.Color = new Vector4 (1, 1, 1, 1);

			GL.BindBuffer (BufferTarget.UniformBuffer, uboShaderSharedData);
			GL.BufferData(BufferTarget.UniformBuffer,Marshal.SizeOf(shaderSharedData),
				ref shaderSharedData, BufferUsageHint.DynamicCopy);
			GL.BindBuffer (BufferTarget.UniformBuffer, 0);
		}

		#endregion

		public void UpdateViewMatrix()
		{
			Rectangle r = this.ClientRectangle;
			GL.Viewport( r.X, r.Y, r.Width, r.Height);
			projection = Matrix4.CreatePerspectiveFieldOfView (fovY, r.Width / (float)r.Height, zNear, zFar);
			Vector3 vEye = vEyeTarget + vLook * eyeDist;
			modelview = Matrix4.LookAt(vEye, vEyeTarget, Vector3.UnitZ);
			GL.GetInteger(GetPName.Viewport, viewport);

			updateShadersMatrices ();

//			if (tDepthSort != null) {
//				killDepthSortThread ();
//			}
//			tDepthSort = new Thread (depthSortThread);
//			tDepthSort.IsBackground = true;
//			tDepthSort.Start ();
//			tDepthSort.Join ();


		}			
			
		#region Interface
		void initInterface()
		{
			this.MouseButtonUp += Mouse_ButtonUp;
			this.MouseButtonDown += Mouse_ButtonDown;
			this.MouseWheelChanged += Mouse_WheelChanged;
			this.MouseMove += Mouse_Move;
			this.KeyDown += Ottd3DWindow_KeyDown;

			CrowInterface.LoadInterface("#Ottd3D.ui.fps.crow").DataSource = this;
			//LoadInterface("#Ottd3D.ui.menu.goml").DataSource = this;

			CrowInterface.LoadInterface("#Ottd3D.ui.heightEditionMenu.goml").DataSource = terrain;
			CrowInterface.LoadInterface("#Ottd3D.ui.SpattingMenu.goml").DataSource = terrain;
			Crow.CompilerServices.ResolveBindings (terrain.Bindings);

			CrowInterface.LoadInterface("#Ottd3D.ui.menu.crow").DataSource = this;
			CrowInterface.LoadInterface("#Ottd3D.ui.ShaderEditor.crow").DataSource = this;

			Crow.CompilerServices.ResolveBindings (this.Bindings);

			ShaderSource = terrain.gridShader.fragSource;
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
				NotifyValueChanged("MousePos", MousePos);
				//selection texture has clientRect size and 4 bytes per pixel, so

				terrain.MouseMove(e);

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
					return;
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
		void onApplyShader (object sender, EventArgs e){
			terrain.gridShader.fragSource = ShaderSource;
			terrain.gridShader.Compile ();
		}
		#endregion

		Random rnd = new Random ();

			
		void addRandomTrees(VertexArrayObject<MeshData, VAOInstancedData> vao,
			int count, string objPath, string diffTexPath, float _scale=1f)
		{			
			VAOItem<VAOInstancedData> vaoi = (VAOItem<VAOInstancedData>)vao.Add (OBJMeshLoader.Load (objPath));
			vaoi.DiffuseTexture = Tetra.Texture.Load(diffTexPath);
			vaoi.InstancedDatas = new Tetra.VAOInstancedData[count];
			for (int i = 0; i < count; i++) {				
				Vector2 pos = new Vector2 ((float)rnd.NextDouble() * terrain.GridSize, (float)rnd.NextDouble() * terrain.GridSize);
				float scale = (float)(rnd.NextDouble () * 0.002f + 0.004f)*_scale;
				vaoi.InstancedDatas[i].modelMats =Matrix4.CreateRotationX (MathHelper.PiOver2) * Matrix4.CreateScale (scale)* Matrix4.CreateTranslation(pos.X, pos.Y, 0f);
			}
			vaoi.UpdateInstancesData ();			
		}

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
				foreach (VAOItem<VAOInstancedData> item in transObjs.OfType<VAOItem<VAOInstancedData>>()) {
					depthSort (item.InstancedDatas);	
				}

			} catch {
				return;
			}
			depthSortingDone = true;
		}
		void depthSort(Tetra.VAOInstancedData[] datas)
		{
			Vector3 vEye = vEyeTarget + vLook * eyeDist;
			Array.Sort(datas,
				delegate(Tetra.VAOInstancedData x, Tetra.VAOInstancedData y) {
					return (new Vector2(y.modelMats.Row3.X, y.modelMats.Row3.Y) - vEye.Xy).LengthFast.
						CompareTo	((new Vector2(x.modelMats.Row3.X, x.modelMats.Row3.Y) - vEye.Xy).LengthFast); });
			
		}
		#region OTK overrides
		protected override void OnLoad (EventArgs e)
		{

			base.OnLoad (e);

			initGL ();

			initShaders ();

			initScene ();

			initInterface ();
		}
		protected override void OnUpdateFrame (FrameEventArgs e)
		{
			base.OnUpdateFrame (e);

			if (Keyboard [OpenTK.Input.Key.ShiftLeft]) {
				float MoveSpeed = 10f;
				//light movment
				if (Keyboard [OpenTK.Input.Key.Up])
					vLight.X -= MoveSpeed;
				else if (Keyboard [OpenTK.Input.Key.Down])
					vLight.X += MoveSpeed;
				else if (Keyboard [OpenTK.Input.Key.Left])
					vLight.Y -= MoveSpeed;
				else if (Keyboard [OpenTK.Input.Key.Right])
					vLight.Y += MoveSpeed;
				else if (Keyboard [OpenTK.Input.Key.PageUp])
					vLight.Z += MoveSpeed;
				else if (Keyboard [OpenTK.Input.Key.PageDown])
					vLight.Z -= MoveSpeed;
				//updateShadersMatrices ();
				//GL.Light (LightName.Light0, LightParameter.Position, vLight);
			}

//			if (updateMatrices) {
//				heolienne.UpdateModelsMat ();
//				updateMatrices = false;
//				gridCacheIsUpToDate = false;
//			}
			for (int i = 0; i < heoliennes.InstancedDatas.Length; i++) {
				heoliennes.InstancedDatas [i].quat0 = Quaternion.FromEulerAngles(heolAngle*0.1f,0f,0f);
				heoliennes.InstancedDatas [i].quat1 = Quaternion.FromEulerAngles(0f,heolAngle,0f) * heoliennes.InstancedDatas [i].quat0;
			}
			heoliennes.UpdateInstancesData ();

			for (int i = 0; i < heollow.InstancedDatas.Length; i++) {
				heollow.InstancedDatas [i].quat1 = Quaternion.FromEulerAngles(0f,heolAngle,0f);
			}
			heollow.UpdateInstancesData ();
			heolAngle += MathHelper.Pi * 0.007f;
//			if (depthSortingDone) {
//				foreach (Tetra.VAOItem<Tetra.VAOInstancedData> item in transparentItemsVao.Meshes) 
//					item.UpdateInstancesData();	
//
//				depthSortingDone = false;
//			}

			Animation.ProcessAnimations ();

			terrain.Update (this);

		}
		float heolAngle = 0f;
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
			: base(1024, 800, 32, 24, 1, 4, "test")
		{
			VSync = VSyncMode.Off;
		}
		#endregion
	}
}