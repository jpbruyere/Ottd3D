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


namespace Ottd3D
{
	class Ottd3DWindow : OpenTKGameWindow, IValueChange
	{
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
				tmp.fDensity = 0.004f; // For exp and exp2 equation   
				tmp.iEquation = 1; // 0 = linear, 1 = exp, 2 = exp2
				return tmp;
			}
		}

		const int _gridSize = 256;
		const int _hmSize = 256;
		const int _splatingSize = 2048;
		const int _circleTexSize = 1024;
		const float heightScale = 50.0f;

		public GameState CurrentState = GameState.RailTrackEdition;
		public Track RailTrack = new Track();

		#region IValueChange implementation
		public event EventHandler<ValueChangeEventArgs> ValueChanged;
		public void NotifyValueChange(string propName, object newValue)
		{
			ValueChanged.Raise(this, new ValueChangeEventArgs (propName, newValue));
		}
		#endregion


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

		public Vector4 vLight = new Vector4 (0.5f, 0.5f, -1, 0);

		UBOSharedData shaderSharedData;
		UBOFogData fogData;
		int uboShaderSharedData, uboFogData;
		#endregion

		/// <summary> pointer in height map texture </summary>
		int ptrHM = 0;
		/// <summary> selected position in world coordinate </summary>
		Vector3 selPos = Vector3.Zero;
		Point<int> selCase;

		/// <summary> Current case center in world coordinate </summary>
		Vector3 SelCenterPos { 
			get { return new Vector3 ((float)Math.Floor (selPos.X) + 0.5f, (float)Math.Floor (selPos.Y) + 0.5f, selPos.Z); }
		}
		public Vector3 SelectionPos
		{
			get { return selPos; }
			set {
				selPos = value;
				selPos.Z = hmData[((int)selPos.Y * _hmSize + (int)selPos.X) * 4 + 1] / 256f * heightScale;
				updateSelMesh ();
				NotifyValueChange ("SelectionPos", selPos);
			}
		}
		public int PtrHM{ get { return ptrHM; } }
		void updatePtrHm()
		{
			selCase = new Point<int> ((int)Math.Round (SelectionPos.X), (int)Math.Round (SelectionPos.Y));
			ptrHM = (selCase.X + selCase.Y * _hmSize) * 4 ;
			NotifyValueChange ("PtrHM", ptrHM);
		}
		public Vector2 MousePos {
			get { return new Vector2 (Mouse.X, Mouse.Y); }
		}
		void updateSelMesh(){
			selMesh = new vaoMesh ((float)Math.Floor(selPos.X)+0.5f, (float)Math.Floor(selPos.Y)+0.5f, selPos.Z, 1.0f, 1.0f);				
		}

		#region Shaders
		public static CircleShader circleShader;
		public static Ottd3D.VertexDispShader gridShader;
		public static GameLib.Shader simpleTexturedShader;
		public static Tetra.Shader CacheRenderingShader;

		public static Tetra.Mat4InstancedShader objShader;

		void initShaders()
		{
			circleShader = new CircleShader ("GGL.Shaders.GameLib.red",_circleTexSize, _circleTexSize);
			circleShader.Color = new Vector4 (1, 1, 1, 1);
			circleShader.Radius = 0.01f;

			gridShader = new Ottd3D.VertexDispShader ("Ottd3D.Shaders.VertDisp.vert", "Ottd3D.Shaders.Grid.frag");

			simpleTexturedShader = new GameLib.Shader ();
			CacheRenderingShader = new Tetra.Shader();			

			circleShader.Update ();

			gridShader.MapSize = new Vector2 (_gridSize, _gridSize);
			gridShader.HeightScale = heightScale;

			Tetra.Texture.DefaultMagFilter = TextureMagFilter.Nearest;
			Tetra.Texture.DefaultMinFilter = TextureMinFilter.Nearest;
			Tetra.Texture.GenerateMipMaps = false;
			Tetra.Texture.FlipY = false;
			{
				gridShader.DisplacementMap = Tetra.Texture.Load ("heightmap.png");
				gridShader.SplatTexture = Tetra.Texture.Load ("splat.png");
			}
			Tetra.Texture.ResetToDefaultLoadingParams ();

			objShader = new Tetra.Mat4InstancedShader ();
			//objShader.DiffuseTexture = heolienneTex;

			shaderSharedData.Color = new Vector4 (1, 1, 1, 1);
			uboShaderSharedData = GL.GenBuffer ();
			GL.BindBuffer (BufferTarget.UniformBuffer, uboShaderSharedData);
			GL.BufferData(BufferTarget.UniformBuffer,Marshal.SizeOf(shaderSharedData),
					ref shaderSharedData, BufferUsageHint.DynamicCopy);
			GL.BindBuffer (BufferTarget.UniformBuffer, 0);
			GL.BindBufferBase (BufferTarget.UniformBuffer, 0, uboShaderSharedData);

			fogData = UBOFogData.CreateUBOFogData();
			uboFogData = GL.GenBuffer ();
			GL.BindBuffer (BufferTarget.UniformBuffer, uboFogData);
			GL.BufferData(BufferTarget.UniformBuffer,Marshal.SizeOf(fogData),
				ref fogData, BufferUsageHint.StaticCopy);
			GL.BindBuffer (BufferTarget.UniformBuffer, 0);
			GL.BindBufferBase (BufferTarget.UniformBuffer, 10, uboFogData);


		}

		void updateShadersMatrices(){
			skybox.shader.MVP =  Matrix4.CreateRotationX(-MathHelper.PiOver2) *  Matrix4.LookAt(Vector3.Zero, -vLook, Vector3.UnitZ) * projection;

			simpleTexturedShader.ProjectionMatrix = projection;
			simpleTexturedShader.ModelViewMatrix = modelview;
			simpleTexturedShader.ModelMatrix = Matrix4.Identity;

			shaderSharedData.projection = projection;
			shaderSharedData.view = modelview;
			shaderSharedData.normal = modelview.Inverted();
			shaderSharedData.normal.Transpose ();
			shaderSharedData.LightPosition = Vector4.Transform(vLight, modelview);

			GL.BindBuffer (BufferTarget.UniformBuffer, uboShaderSharedData);
			GL.BufferData(BufferTarget.UniformBuffer,Marshal.SizeOf(shaderSharedData),
				ref shaderSharedData, BufferUsageHint.DynamicCopy);
			GL.BindBuffer (BufferTarget.UniformBuffer, 0);
		}

		#endregion

		string[] groundTextures = new string[]
		{
			"#Ottd3D.images.grass2.jpg",
			"#Ottd3D.images.grass.jpg",
			"#Ottd3D.images.brownRock.jpg",
			"#Ottd3D.images.grass_green_d.jpg",
			"#Ottd3D.images.grass_ground_d.jpg",
			"#Ottd3D.images.grass_ground2y_d.jpg",
			"#Ottd3D.images.grass_mix_ylw_d.jpg",
			"#Ottd3D.images.grass_mix_d.jpg",
			"#Ottd3D.images.grass_autumn_orn_d.jpg",
			"#Ottd3D.images.grass_autumn_red_d.jpg",
			"#Ottd3D.images.grass_rocky_d.jpg",
			"#Ottd3D.images.ground_cracks2v_d.jpg",
			"#Ottd3D.images.ground_crackedv_d.jpg",
			"#Ottd3D.images.ground_cracks2y_d.jpg",
			"#Ottd3D.images.ground_crackedo_d.jpg"			
		};

		public string[] GroundTextures { get { return groundTextures; }}

		vaoMesh gridMesh;
		vaoMesh selMesh;

		Tetra.IndexedVAO<Tetra.VAOInstancedData> landItemsVao, transparentItemsVao;

		Tetra.SkyBox skybox;

		public void initGrid()
		{
			const float z = 0.0f;
			const int IdxPrimitiveRestart = int.MaxValue;

			Vector3[] positionVboData;
			int[] indicesVboData;
			Vector2[] texVboData;

			positionVboData = new Vector3[_gridSize * _gridSize];
			texVboData = new Vector2[_gridSize * _gridSize];
			indicesVboData = new int[(_gridSize * 2 + 1) * _gridSize];

			for (int y = 0; y < _gridSize; y++) {
				for (int x = 0; x < _gridSize; x++) {				
					positionVboData [_gridSize * y + x] = new Vector3 (x, y, z);
					texVboData [_gridSize * y + x] = new Vector2 ((float)x*0.5f, (float)y*0.5f);

					if (y < _gridSize-1) {
						indicesVboData [(_gridSize * 2 + 1) * y + x*2] = _gridSize * y + x;
						indicesVboData [(_gridSize * 2 + 1) * y + x*2 + 1] = _gridSize * (y+1) + x;
					}

					if (x == _gridSize-1) {
						indicesVboData [(_gridSize * 2 + 1) * y + x*2 + 2] = IdxPrimitiveRestart;
					}
				}
			}

			gridMesh = new vaoMesh (positionVboData, texVboData, null);
			gridMesh.indices = indicesVboData;

			Tetra.Texture.DefaultWrapMode = TextureWrapMode.Repeat;
			gridShader.DiffuseTexture = Tetra.Texture.Load (TextureTarget.Texture2DArray, groundTextures);
			Tetra.Texture.ResetToDefaultLoadingParams ();
		}
		void drawGrid()
		{
			if (!gridCacheIsUpToDate)
				updateGridFbo ();

			renderGridCache ();
		}
		void drawHoverCase()
		{
			if (selMesh == null)
				return;
			
			simpleTexturedShader.Enable ();

			GL.BindTexture (TextureTarget.Texture2D, circleShader.OutputTex);
			selMesh.Render(PrimitiveType.TriangleStrip);
			GL.BindTexture (TextureTarget.Texture2D, 0);
		}

		byte[] hmData;//height map
		byte[] selectionMap;//has grid positions as colors

		void getHeightMapData()
		{			
			GL.BindTexture (TextureTarget.Texture2D, gridShader.DisplacementMap);

			GL.GetTexImage (TextureTarget.Texture2D, 0, 
				PixelFormat.Rgba, PixelType.UnsignedByte, hmData);

			GL.BindTexture (TextureTarget.Texture2D, 0);
		}
		void updateHeightMap()
		{
			GL.BindTexture (TextureTarget.Texture2D, gridShader.DisplacementMap);

			GL.TexSubImage2D (TextureTarget.Texture2D,
				0, 0, 0, _hmSize, _hmSize, PixelFormat.Bgra, PixelType.UnsignedByte, hmData);

			GL.BindTexture (TextureTarget.Texture2D, 0);
			gridCacheIsUpToDate = false;
			heightMapIsUpToDate = true;
			getHeightMapData ();
			//force update of selection mesh
			SelectionPos = selPos;
		}
		void getSelectionTextureData()
		{
			GL.BindTexture (TextureTarget.Texture2D, gridSelectionTex);

			GL.GetTexImage (TextureTarget.Texture2D, 0, 
				PixelFormat.Rgba, PixelType.UnsignedByte, selectionMap);

			GL.BindTexture (TextureTarget.Texture2D, 0);
		}

		#region Grid Cache
		bool gridCacheIsUpToDate = false,
			heightMapIsUpToDate = true,
			splatTextureIsUpToDate = true;
		QuadVAO cacheQuad;
		int gridCacheTex, gridSelectionTex;
		int fboGrid, depthRenderbuffer;
		DrawBuffersEnum[] dbe = new DrawBuffersEnum[]
		{
			DrawBuffersEnum.ColorAttachment0 ,
			DrawBuffersEnum.ColorAttachment1
		};
		

		void createCache(){
			selectionMap = new byte[ClientRectangle.Width*ClientRectangle.Height*4];

			if (cacheQuad != null)
				cacheQuad.Dispose ();
			cacheQuad = new QuadVAO (0, 0, ClientRectangle.Width, ClientRectangle.Height, 0, 1, 1, -1);
			CacheRenderingShader.MVP = Matrix4.CreateOrthographicOffCenter 
				(0, ClientRectangle.Width, 0, ClientRectangle.Height, 0, 1);
			initGridFbo ();
		}
		void renderGridCache(){
			bool depthTest = GL.GetBoolean (GetPName.DepthTest);

			GL.Disable (EnableCap.DepthTest);

			CacheRenderingShader.Enable ();

			GL.ActiveTexture (TextureUnit.Texture0);
			GL.BindTexture (TextureTarget.Texture2D, gridCacheTex);
			cacheQuad.Render (PrimitiveType.TriangleStrip);
			GL.BindTexture (TextureTarget.Texture2D, 0);

			if (depthTest)
				GL.Enable (EnableCap.DepthTest);
		}

		#region FBO
		void initGridFbo()
		{
			System.Drawing.Size cz = ClientRectangle.Size;

			Tetra.Texture.DefaultMagFilter = TextureMagFilter.Nearest;
			Tetra.Texture.DefaultMinFilter = TextureMinFilter.Nearest;
			Tetra.Texture.GenerateMipMaps = false;
			{
				gridCacheTex = new Tetra.Texture (cz.Width, cz.Height);
				gridSelectionTex = new Tetra.Texture (cz.Width, cz.Height);
			}
			Tetra.Texture.ResetToDefaultLoadingParams ();

			// Create Depth Renderbuffer
			GL.GenRenderbuffers( 1, out depthRenderbuffer );
			GL.BindRenderbuffer( RenderbufferTarget.Renderbuffer, depthRenderbuffer );
			GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, (RenderbufferStorage)All.DepthComponent32, cz.Width, cz.Height);

			GL.GenFramebuffers(1, out fboGrid);

			GL.BindFramebuffer(FramebufferTarget.Framebuffer, fboGrid);
			GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
				TextureTarget.Texture2D, gridCacheTex, 0);
			GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1,
				TextureTarget.Texture2D, gridSelectionTex, 0);
			GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, depthRenderbuffer );


			if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
			{
				throw new Exception(GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer).ToString());
			}

			GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
		}
		void updateGridFbo()
		{						
			GL.BindFramebuffer(FramebufferTarget.Framebuffer, fboGrid);
			GL.DrawBuffers(2, dbe);

			GL.Clear (ClearBufferMask.ColorBufferBit|ClearBufferMask.DepthBufferBit);

			GL.DepthMask (false);
			skybox.Render ();
			GL.DepthMask (true);

			gridShader.Enable ();

			//4th component of selection texture is used as coordinate, not as alpha
			GL.Disable (EnableCap.AlphaTest);
			GL.Disable (EnableCap.Blend);

			gridMesh.Render(PrimitiveType.TriangleStrip, gridMesh.indices);

			GL.DrawBuffers(1, new DrawBuffersEnum[]{DrawBuffersEnum.ColorAttachment0});


			objShader.Enable ();

			landItemsVao.Bind ();
			landItemsVao.Render (PrimitiveType.Triangles);
			landItemsVao.Unbind ();

			//GL.DepthMask (false);
			GL.Enable (EnableCap.AlphaTest);
			GL.Enable (EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

			transparentItemsVao.Bind ();
			transparentItemsVao.Render (PrimitiveType.Triangles);
			transparentItemsVao.Unbind ();


			//GL.DepthMask (true);

			GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
			GL.DrawBuffer(DrawBufferMode.Back);
			getSelectionTextureData ();


			gridCacheIsUpToDate = true;
		}
		#endregion

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

			gridCacheIsUpToDate = false;
		}			
			

		#region Interface
		void initInterface()
		{
			this.MouseButtonUp += Mouse_ButtonUp;
			this.MouseButtonDown += Mouse_ButtonDown;
			this.MouseWheelChanged += Mouse_WheelChanged;
			this.MouseMove += Mouse_Move;
			this.KeyDown += Ottd3DWindow_KeyDown;

			CrowInterface.LoadInterface("#Ottd3D.ui.fps.goml").DataSource = this;
			//LoadInterface("#Ottd3D.ui.menu.goml").DataSource = this;
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
						RailTrack.CurrentSegment = new TrackSegment (SelCenterPos);
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
				NotifyValueChange("MousePos", MousePos);
				//selection texture has clientRect size and 4 bytes per pixel, so
				int selPtr = (e.X * 4 + (ClientRectangle.Height - e.Y) * ClientRectangle.Width * 4);
				if (selPtr + 3 < selectionMap.Length) {
					//selection texture has on each pixel WorldPosition on ground level coded as 2 half floats
					SelectionPos = new Vector3 (
						(float)selectionMap [selPtr] + (float)selectionMap [selPtr + 1] / 255f, 
						(float)selectionMap [selPtr + 2] + (float)selectionMap [selPtr + 3] / 255f, 0f);
				}
				updatePtrHm ();

				switch (CurrentState) {
				case GameState.Playing:
					break;
				case GameState.RailTrackEdition:
					TrackSegment ts = RailTrack.CurrentSegment;
					if (ts != null) {						
						if (SelCenterPos == ts.StartPos)
							return;
						if (e.Mouse.LeftButton == OpenTK.Input.ButtonState.Pressed) {					
							ts.EndPos = SelCenterPos;
							ts.vStart = Vector3.Normalize (ts.EndPos - ts.StartPos);
							RailTrack.vEnd = ts.vStart;
						} else {
							ts.EndPos = SelCenterPos;
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
						RailTrack.vEnd = RailTrack.CurrentSegment.vStart;
						RailTrack.TrackStarts.Remove (RailTrack.CurrentSegment);
						RailTrack.CurrentSegment = null;
						RailTrack.UpdateTrackMesh ();
					}
				}
				break;				
			}
		}
		#endregion
		void onGameStateChange (object sender, ValueChangeEventArgs e)
		{
			if (e.MemberName != "IsChecked" || (bool)e.NewValue != true)
				return;
			
			//force update of position mesh
			SelectionPos = selPos;
		}
		#endregion

//		void matRotThread()
//		{
//			while (true) {
//				if (updateMatrices) {
//					Thread.Sleep (1);
//					continue;
//				}
//				for (int i = 0; i < instances; i++) {
//					for (int j = 0; j < instances; j++) {
//						heolienne.modelMats [i*instances+j] *= Matrix4.CreateRotationZ(0.01f);
//					}
//					//modMats [i] = Matrix4.Identity;
//				}				
//				updateMatrices = true;
//			}
//		}
		Random rnd = new Random ();
		protected override void OnLoad (EventArgs e)
		{

			base.OnLoad (e);

			initInterface ();



			#region test IndexedVAO
			int tcDiffTex = new Texture("/mnt/data/blender/ottd3d/testcube-diff.jpg");
			int tcNormTex = new Texture("/mnt/data/blender/ottd3d/testcube-norm.jpg");

			int houseDiffTex = new Texture("/mnt/data/Images/texture/structures/house-diff.png");
			int houseNormTex = new Texture("/mnt/data/Images/texture/structures/house-norm.png");

			const int nbHeol = 5, heolSpacing = 4;
			Tetra.VAOItem<Tetra.VAOInstancedData> vaoi = null;
			landItemsVao = new Tetra.IndexedVAO<Tetra.VAOInstancedData> ();
				
			//TEST HOUSE
			vaoi = landItemsVao.Add (Tetra.OBJMeshLoader.Load ("/mnt/data/blender/ottd3d/house0.obj"));
			vaoi.DiffuseTexture = houseDiffTex;
			vaoi.NormalMapTexture = houseNormTex;
			vaoi.Datas = new Tetra.VAOInstancedData[nbHeol];
			for (int i = 0; i < nbHeol; i++) {
				Vector2 pos = new Vector2 ((float)rnd.Next(0,_gridSize), (float)rnd.Next(0,_gridSize));
				vaoi.Datas[i].modelMats = Matrix4.CreateTranslation (pos.X-(pos.X % 4f) + 0.5f, pos.Y-(pos.Y % 4f) + 0.5f, 0f);
			}
			vaoi.UpdateInstancesData ();

			//HEOLIENNES
			vaoi = landItemsVao.Add (Tetra.OBJMeshLoader.Load ("/mnt/data/blender/ottd3d/heolienne.obj"));
			vaoi.DiffuseTexture = new Texture("/mnt/data/blender/ottd3d/heolienne.png");
			vaoi.Datas = new Tetra.VAOInstancedData[nbHeol];
			for (int i = 0; i < nbHeol; i++) {
				Vector2 pos = new Vector2 ((float)rnd.Next(0,_gridSize), (float)rnd.Next(0,_gridSize));
				vaoi.Datas[i].modelMats = Matrix4.CreateTranslation (pos.X-(pos.X % 4f) + 0.5f, pos.Y-(pos.Y % 4f) + 0.5f, 0f);
			}
			vaoi.UpdateInstancesData();

			landItemsVao.ComputeTangents();
			landItemsVao.BuildBuffers ();
			#endregion

			const float treezone = 256;
			const int treeCount = 50;
			transparentItemsVao = new Tetra.IndexedVAO<Tetra.VAOInstancedData> ();

			//====TREE1====
//			vaoi = transparentItemsVao.Add (Tetra.OBJMeshLoader.Load ("#Ottd3D.images.trees.obj__pinet1.obj"));
//			vaoi.DiffuseTexture = Tetra.Texture.Load("#Ottd3D.images.trees.pinet1.png");
//			vaoi.modelMats = new Matrix4[treeCount];
//			for (int i = 0; i < treeCount; i++) {				
//				Vector2 pos = new Vector2 ((float)rnd.NextDouble() * treezone, (float)rnd.NextDouble() * treezone);
//				float scale = (float)rnd.NextDouble () * 0.002f + 0.004f;
//				vaoi.modelMats[i] =treeRot * Matrix4.CreateScale (scale)* Matrix4.CreateTranslation(pos.X, pos.Y, 0f);
//			}
//			vaoi.UpdateInstancesData ();

			//====TREE2====
			addRandomTrees (transparentItemsVao, treeCount,
				"#Ottd3D.images.trees.obj__pinet1.obj",
				"#Ottd3D.images.trees.pinet1.png",5f);
			addRandomTrees (transparentItemsVao, treeCount,
				"#Ottd3D.images.trees.obj__pinet2.obj",
				"#Ottd3D.images.trees.pinet2.png",5f);
			addRandomTrees (transparentItemsVao, treeCount,
				"#Ottd3D.images.trees.obj__tree1.obj",
				"#Ottd3D.images.trees.tree1.png",5f);
			addRandomTrees (transparentItemsVao, treeCount,
				"#Ottd3D.images.trees.obj__tree2.obj",
				"#Ottd3D.images.trees.tree2.png", 5f);
			addRandomTrees (transparentItemsVao, treeCount,
				"#Ottd3D.images.trees.obj__tree3.obj",
				"#Ottd3D.images.trees.tree3.png", 5f);

			transparentItemsVao.ComputeTangents ();
			transparentItemsVao.BuildBuffers ();

			initShaders ();

			GL.ClearColor(0.0f, 0.0f, 0.2f, 1.0f);
			GL.Enable(EnableCap.DepthTest);
			GL.DepthFunc(DepthFunction.Less);
			//			GL.Enable(EnableCap.CullFace);
			GL.PrimitiveRestartIndex (int.MaxValue);
			GL.Enable (EnableCap.PrimitiveRestart);

			GL.Enable (EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

			initGrid ();

			createCache ();

			hmData = new byte[_hmSize*_hmSize*4];
			getHeightMapData ();

//			Thread t = new Thread (matRotThread);
//			t.IsBackground = true;
//			t.Start ();

			#region skybox tests
//			string[] sky = new string[] 
//				{
//					@"/mnt/data/Images/texture/skybox/sky1/oright7.jpg",
//					@"/mnt/data/Images/texture/skybox/sky1/oleft7.jpg",
//					@"/mnt/data/Images/texture/skybox/sky1/otop7.jpg",
//					@"/mnt/data/Images/texture/skybox/sky1/otop7.jpg",
//					@"/mnt/data/Images/texture/skybox/sky1/ofront7.jpg",
//					@"/mnt/data/Images/texture/skybox/sky1/oback7.jpg"
//
//					@"/mnt/data/Images/texture/skybox/sky4/px.jpg",
//					@"/mnt/data/Images/texture/skybox/sky4/nx.jpg",
//					@"/mnt/data/Images/texture/skybox/sky4/ny.jpg",
//					@"/mnt/data/Images/texture/skybox/sky4/py.jpg",
//					@"/mnt/data/Images/texture/skybox/sky4/pz.jpg",
//					@"/mnt/data/Images/texture/skybox/sky4/nz.jpg"
//
//					@"/mnt/data/Images/texture/skybox/frozendusk/right.jpg",
//					@"/mnt/data/Images/texture/skybox/frozendusk/left.jpg",
//					@"/mnt/data/Images/texture/skybox/frozendusk/top.jpg",
//					@"/mnt/data/Images/texture/skybox/frozendusk/top.jpg",
//					@"/mnt/data/Images/texture/skybox/frozendusk/front.jpg",
//					@"/mnt/data/Images/texture/skybox/frozendusk/back.jpg"
//				};
			#endregion

			string[] sky = new string[] 
			{
				"#Ottd3D.images.skybox.right.bmp",
				"#Ottd3D.images.skybox.left.bmp",
				"#Ottd3D.images.skybox.top.bmp",
				"#Ottd3D.images.skybox.top.bmp",
				"#Ottd3D.images.skybox.front.bmp",
				"#Ottd3D.images.skybox.back.bmp"
			};

			skybox = new Tetra.SkyBox (sky[0],sky[1],sky[2],sky[3],sky[4],sky[5]);
		}
			
		void addRandomTrees(Tetra.IndexedVAO<Tetra.VAOInstancedData> vao, int count, string objPath, string diffTexPath, float _scale=1f)
		{			
			Tetra.VAOItem<Tetra.VAOInstancedData> vaoi = vao.Add (Tetra.OBJMeshLoader.Load (objPath));
			vaoi.DiffuseTexture = Tetra.Texture.Load(diffTexPath);
			vaoi.Datas = new Tetra.VAOInstancedData[count];
			for (int i = 0; i < count; i++) {				
				Vector2 pos = new Vector2 ((float)rnd.NextDouble() * _gridSize, (float)rnd.NextDouble() * _gridSize);
				float scale = (float)(rnd.NextDouble () * 0.002f + 0.004f)*_scale;
				vaoi.Datas[i].modelMats =Matrix4.CreateRotationX (MathHelper.PiOver2) * Matrix4.CreateScale (scale)* Matrix4.CreateTranslation(pos.X, pos.Y, 0f);
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
				Tetra.VAOItem<Tetra.VAOInstancedData>[] transObjs = null;
				lock (transparentItemsVao.Meshes) {
					transObjs = transparentItemsVao.Meshes.ToArray();
				}
				foreach (Tetra.VAOItem<Tetra.VAOInstancedData> item in transObjs) {
					depthSort (item.Datas);	
				}

			} catch (Exception ex) {
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
		private int frameCpt = 0;
		protected override void OnUpdateFrame (FrameEventArgs e)
		{
			base.OnUpdateFrame (e);

			Animation.ProcessAnimations ();

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
				updateShadersMatrices ();
				gridCacheIsUpToDate = false;
				//GL.Light (LightName.Light0, LightParameter.Position, vLight);
			}

//			if (updateMatrices) {
//				heolienne.UpdateModelsMat ();
//				updateMatrices = false;
//				gridCacheIsUpToDate = false;
//			}

//			if (depthSortingDone) {
//				foreach (Tetra.VAOItem item in transparentItemsVao.Meshes) 
//					item.UpdateInstancesData();	
//
//				depthSortingDone = false;
//				gridCacheIsUpToDate = false;
//			}

			if (heightMapIsUpToDate)
				return;
			
			updateHeightMap ();
		}

		protected override void OnResize (EventArgs e)
		{
			base.OnResize (e);
			UpdateViewMatrix();
		}
		public override void GLClear ()
		{			
			GL.Clear (ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
		}
		public override void OnRender (FrameEventArgs e)
		{			
			drawGrid ();

			drawHoverCase ();

			RailTrack.Render ();
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
			: base(1024, 800,"test")
		{
			VSync = VSyncMode.Off;
		}
		#endregion
	}
}