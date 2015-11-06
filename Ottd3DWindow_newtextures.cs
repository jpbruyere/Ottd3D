#define MONO_CAIRO_DEBUG_DISPOSE


using System;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

using System.Diagnostics;

//using GGL;
using go;
using System.Threading;
using GGL;


namespace Ottd3D
{
	class Ottd3DWindow : OpenTKGameWindow, IValueChange
	{
		public enum GameState
		{
			Playing,
		}

		const int _gridSize = 256;
		const int _hmSize = 256;
		const int _splatingSize = 2048;
		const int _circleTexSize = 1024;
		const float heightScale = 50.0f;

		public GameState CurrentState = GameState.Playing;

		#region IValueChange implementation
		public event EventHandler<ValueChangeEventArgs> ValueChanged;
		public void NotifyValueChange(string propName, object newValue)
		{
			ValueChanged.Raise(this, new ValueChangeEventArgs (propName, newValue));
		}
		#endregion

		#region FPS
		int _fps = 0;

		public int fps {
			get { return _fps; }
			set {
				if (_fps == value)
					return;

				_fps = value;

				if (_fps > fpsMax) {
					fpsMax = _fps;
					NotifyValueChange ("fpsMax", fpsMax);
				} else if (_fps < fpsMin) {
					fpsMin = _fps;
					NotifyValueChange ("fpsMin", fpsMin);
				}
					
				NotifyValueChange ("fps", _fps);
				NotifyValueChange ("update",
					this.updateTime.ElapsedMilliseconds.ToString () + " ms");
			}
		}

		public int fpsMin = int.MaxValue;
		public int fpsMax = 0;
		public string update = "";

		void resetFps ()
		{
			fpsMin = int.MaxValue;
			fpsMax = 0;
			_fps = 0;
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

		float eyeDist = 30;
		float eyeDistTarget = 30f;
		float MoveSpeed = 0.01f;
		float ZoomSpeed = 0.2f;
		float RotationSpeed = 0.01f;

		public Vector4 vLight = new Vector4 (-1, -1, -1, 0);
		#endregion

		Vector3 selPos = Vector3.Zero;
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
		public Vector2 MousePos {
			get { return new Vector2 (Mouse.X, Mouse.Y); }
		}
		void updateSelMesh(){
			if (selMesh != null)
				selMesh.Dispose ();
			selMesh = new vaoMesh ((float)Math.Floor(selPos.X)+0.5f, (float)Math.Floor(selPos.Y)+0.5f, selPos.Z, 1.0f, 1.0f);				
		}

		#region Shaders
		public static CircleShader circleShader;
		public static GameLib.VertexDispShader gridShader;
		public static GameLib.Shader simpleTexturedShader;
		public static go.GLBackend.TexturedShader CacheRenderingShader;

		public static SingleLightShader objShader;

		void initShaders()
		{
			circleShader = new CircleShader ("GGL.Shaders.GameLib.red",_circleTexSize, _circleTexSize);
			circleShader.Color = Color.White;
			circleShader.Radius = 0.01f;

			gridShader = new GameLib.VertexDispShader ("Ottd3D.Shaders.VertDisp.vert", "Ottd3D.Shaders.Grid.frag");

			simpleTexturedShader = new GameLib.Shader ();
			CacheRenderingShader = new go.GLBackend.TexturedShader();			

			circleShader.Update ();


			Texture.FlipY = false;
			Texture.DefaultMagFilter = TextureMagFilter.Nearest;
			Texture.DefaultMinFilter = TextureMinFilter.Nearest;

			gridShader.DisplacementMap = Texture.Load ("heightmap.png");
			gridShader.LightPos = vLight;
			gridShader.MapSize = new Vector2 (_gridSize, _gridSize);
			gridShader.HeightScale = heightScale;

			gridShader.SplatTexture = Texture.Load ("splat.png");

			Texture.DefaultMagFilter = TextureMagFilter.Linear;
			Texture.DefaultMinFilter = TextureMinFilter.Linear;
			Texture.FlipY = true;

			objShader = new SingleLightShader ();
			objShader.Color = Color.White;
			objShader.LightPos = vLight;
			objShader.DiffuseTexture = heolienneTex;
			objShader.DisplacementMap = gridShader.DisplacementMap;
			objShader.MapSize = new Vector2 (_gridSize, _gridSize);
			objShader.HeightScale = heightScale;
		}

		void updateShadersMatrices(){
			gridShader.ProjectionMatrix = projection;
			gridShader.ModelViewMatrix = modelview;
			gridShader.ModelMatrix = Matrix4.Identity;

			simpleTexturedShader.ProjectionMatrix = projection;
			simpleTexturedShader.ModelViewMatrix = modelview;
			simpleTexturedShader.ModelMatrix = Matrix4.Identity;

			objShader.ProjectionMatrix = projection;
			objShader.ModelViewMatrix = modelview;
			objShader.ModelMatrix = Matrix4.CreateTranslation (40.5f, 40.5f, 0);
		}
		#endregion

		#region Tests
		vaoMesh tree;
		int treeTex;
		vaoMesh heolienne;
		Texture heolienneTex;

		void initTestMesh(){
			const float tsize = 1;
			tree = new vaoMesh (
				new Vector3[] {
					new Vector3 (-tsize/2f, 0, 0),
					new Vector3 (-tsize/2f, 0, tsize),
					new Vector3 (tsize/2f, 0, 0),
					new Vector3 (tsize/2f, 0, tsize),
					//					new Vector3 (0, -tsize/2f, -tsize/2f),
					//					new Vector3 (0, -tsize/2f, tsize/2f),
					//					new Vector3 (0, tsize/2f, -tsize/2f),
					//					new Vector3 (0, tsize/2f, tsize/2f)
				},
				new Vector2[] {
					new Vector2 (0, 0),
					new Vector2 (0, 1),
					new Vector2 (1, 0),
					new Vector2 (1, 1),
					//					new Vector2 (1, 0),
					//					new Vector2 (1, 1),
					//					new Vector2 (0, 0),
					//					new Vector2 (0, 1)
				},
				new Vector3[] {
					new Vector3 (0, 1, 0),
					new Vector3 (0, 1, 0),
					new Vector3 (0, 1, 0),
					new Vector3 (0, 1, 0),
					//					new Vector3 (1, 0, 0),
					//					new Vector3 (1, 0, 0),
					//					new Vector3 (1, 0, 0),
					//					new Vector3 (1, 0, 0)
				},
				null
			);
			//tree = new vaoMesh(0,0,tsize/2f,tsize,tsize);
			treeTex = Texture.Load ("#Ottd3D.images.trees.tree1.png");

			vaoMesh tmp = vaoMesh.Load ("Meshes/heolienne.obj");
			Matrix4[] modMats = new Matrix4[100*100];
			for (int i = 0; i < 100; i++) {
				for (int j = 0; j < 100; j++) {
					modMats [i*100+j] = Matrix4.CreateTranslation (i * 2, j * 2, 0);
				}
				//modMats [i] = Matrix4.Identity;
			}
			heolienne = new vaoMesh (tmp.positions, tmp.texCoords, tmp.normals, tmp.indices, modMats);
			GL.GetError ();
			heolienneTex = Texture.Load("#Ottd3D.images.brownRock.dds");
			tmp.Dispose ();			
		}
		void drawingTests()
		{
			Matrix4 m = simpleTexturedShader.ModelViewMatrix;
			m.Column0 = Vector4.UnitX;
			//m.Column1 = Vector4.UnitY;
			m.Column2 = Vector4.UnitZ;
			//m.Column3 = new Vector4(m.Column3.X, m.Column3.Y, 0f, m.Column3.W);
			m.Row3 = modelview.Row3;
			//m.Row3 = new Vector4(m.Row3.X, m.Row3.Y, m.Row3.Z, m.Row3.W);
			//			m.ClearRotation();
			//			m.Column1 = modelview.Column1;
			//
			simpleTexturedShader.ModelViewMatrix = m;

			//simpleTexturedShader.ModelMatrix = Matrix4.CreateTranslation (100, 100, 0);
			simpleTexturedShader.Enable ();
			GL.BindTexture (TextureTarget.Texture2D, treeTex);
			tree.Render (PrimitiveType.TriangleStrip);
			GL.BindTexture (TextureTarget.Texture2D, 0);
			simpleTexturedShader.ModelViewMatrix = modelview;
			simpleTexturedShader.ModelMatrix = Matrix4.Identity;

			objShader.Enable ();
			heolienne.Render (PrimitiveType.Triangles, 10000);			
		}
		void testDynShaders()
		{
			DynamicShader ds = new DynamicShader() { 
				floatPrecision = DynamicShader.Precision.highp 
			};
			ds.VertexAttributes.Add(new ShaderData(typeof(Vector3), "in_position"));

			ShaderData<Matrix4> sdProjection = new ShaderData<Matrix4> ("projection");
			ShaderData<Matrix4> sdModelView = new ShaderData<Matrix4> ("modelView");
			ShaderData<Matrix4> sdModel = new ShaderData<Matrix4> ("model");

			ds.Uniforms.Add (sdProjection);
			ds.Uniforms.Add (sdModelView);
			ds.Uniforms.Add (sdModel);
			ds.Uniforms.Add(new ShaderData<Vector4>("lightPos"));
			ds.Uniforms.Add(new ShaderData<float>("heightScale"));
			ds.Uniforms.Add(new ShaderData<Vector2>("mapSize"));

			ds.Compile ();
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

		vaoMesh grid;
		vaoMesh selMesh;

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

			grid = new vaoMesh (positionVboData, texVboData, null);
			grid.indices = indicesVboData;

			Texture.DefaultWrapMode = TextureWrapMode.Repeat;
			gridShader.DiffuseTexture = Texture.Load (TextureTarget.Texture2DArray, groundTextures);
			Texture.DefaultWrapMode = TextureWrapMode.Clamp;
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
		Matrix4 cacheProjection;
		Texture gridCacheTex, gridSelectionTex;
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
			cacheProjection = Matrix4.CreateOrthographicOffCenter 
				(0, ClientRectangle.Width, 0, ClientRectangle.Height, 0, 1);
			initGridFbo ();
		}
		void renderGridCache(){
			bool depthTest = GL.GetBoolean (GetPName.DepthTest);

			GL.Disable (EnableCap.DepthTest);

			CacheRenderingShader.Enable ();
			CacheRenderingShader.ProjectionMatrix = cacheProjection;
			CacheRenderingShader.ModelViewMatrix = Matrix4.Identity;
			CacheRenderingShader.Color = new Vector4(1f,1f,1f,1f);

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

			gridCacheTex = new Texture (cz.Width, cz.Height);

			Texture.DefaultMagFilter = TextureMagFilter.Nearest;
			Texture.DefaultMinFilter = TextureMinFilter.Nearest;
			gridSelectionTex = new Texture (cz.Width, cz.Height);
			Texture.DefaultMagFilter = TextureMagFilter.Linear;
			Texture.DefaultMinFilter = TextureMinFilter.Linear;


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

			gridShader.Enable ();

			//4th component of selection texture is used as coordinate, not as alpha
			GL.Disable (EnableCap.AlphaTest);
			GL.Disable (EnableCap.Blend);

			grid.Render(PrimitiveType.TriangleStrip, grid.indices);

			GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
			GL.DrawBuffer(DrawBufferMode.Back);
			getSelectionTextureData ();

			GL.Enable (EnableCap.AlphaTest);
			GL.Enable (EnableCap.Blend);

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

			gridCacheIsUpToDate = false;
		}			
			
		int ptrHM = 0;

		public int PtrHM{ get { return ptrHM; } }

		void updatePtrHm()
		{
			ptrHM = ((int)Math.Round(SelectionPos.X) + (int)Math.Round(SelectionPos.Y) * _hmSize) * 4 ;
			NotifyValueChange ("PtrHM", ptrHM);
		}

		#region Interface
		void initInterface()
		{
			this.MouseButtonUp += Mouse_ButtonUp;
			this.MouseWheelChanged += new EventHandler<MouseWheelEventArgs>(Mouse_WheelChanged);
			this.MouseMove += new EventHandler<MouseMoveEventArgs>(Mouse_Move);

			LoadInterface("#Ottd3D.ui.fps.goml").DataSource = this;
			//LoadInterface("#Ottd3D.ui.menu.goml").DataSource = this;
		}

		#region Mouse
		void Mouse_Move(object sender, MouseMoveEventArgs e)
		{			
			if (e.XDelta != 0 || e.YDelta != 0)
			{
				NotifyValueChange("MousePos", MousePos);
				int selPtr = (e.X * 4 + (ClientRectangle.Height - e.Y) * ClientRectangle.Width * 4);
				//				SelectionPos = new Vector3 (selectionMap [selPtr], 
				//					selectionMap [selPtr + 1], selectionMap [selPtr + 2]);
				SelectionPos = new Vector3 (
					(float)selectionMap [selPtr] + (float)selectionMap [selPtr + 1] / 255f, 
					(float)selectionMap [selPtr + 2] + (float)selectionMap [selPtr + 3] / 255f, 0f);

				updatePtrHm ();

				if (e.Mouse.MiddleButton == OpenTK.Input.ButtonState.Pressed) {
					if (Keyboard [Key.ShiftLeft]) {
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
		void Mouse_WheelChanged(object sender, MouseWheelEventArgs e)
		{
			float speed = ZoomSpeed * eyeDist;

			if (Keyboard[Key.ControlLeft])
				speed *= 20.0f;

			eyeDistTarget -= e.Delta * speed;
			if (eyeDistTarget < zNear+5)
				eyeDistTarget = zNear+5;
			else if (eyeDistTarget > zFar-100)
				eyeDistTarget = zFar-100;
			Animation.StartAnimation(new Animation<float> (this, "EyeDist", eyeDistTarget, (eyeDistTarget - eyeDist) * 0.2f));
		}
		void Mouse_ButtonUp (object sender, MouseButtonEventArgs e)
		{
			
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

		protected override void OnLoad (EventArgs e)
		{
			base.OnLoad (e);

			//initTestMesh ();

			initInterface ();

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

		}
			
		private int frameCpt = 0;
		protected override void OnUpdateFrame (FrameEventArgs e)
		{
			base.OnUpdateFrame (e);

			fps = (int)RenderFrequency;
			if (frameCpt > 200) {
				resetFps ();
				frameCpt = 0;

			}
			frameCpt++;

			Animation.ProcessAnimations ();


			if (Keyboard [Key.ShiftLeft]) {
				float MoveSpeed = 1f;
				//light movment
				if (Keyboard [Key.Up])
					vLight.X -= MoveSpeed;
				else if (Keyboard [Key.Down])
					vLight.X += MoveSpeed;
				else if (Keyboard [Key.Left])
					vLight.Y -= MoveSpeed;
				else if (Keyboard [Key.Right])
					vLight.Y += MoveSpeed;
				else if (Keyboard [Key.PageUp])
					vLight.Z += MoveSpeed;
				else if (Keyboard [Key.PageDown])
					vLight.Z -= MoveSpeed;
				gridCacheIsUpToDate = false;
				//GL.Light (LightName.Light0, LightParameter.Position, vLight);
			}

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

			//drawingTests ();
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
			//VSync = VSyncMode.On;
		}
		#endregion
	}
}