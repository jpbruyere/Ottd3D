//
//  Grid.cs
//
//  Author:
//       Jean-Philippe Bruyère <jp.bruyere@hotmail.com>
//
//  Copyright (c) 2015 jp
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using GGL;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Crow;
using System.Diagnostics;
using System.Collections.Generic;

namespace Ottd3D
{
	public class Terrain : IValueChange, IDisposable, IBindable
	{
		public enum State
		{
			Play,
			HMEdition,
			ClearHM,
			ClearSplatting,
			LoadMap,
			GroundTexturing
		}

		#region IValueChange implementation
		public event EventHandler<ValueChangeEventArgs> ValueChanged;
		public void NotifyValueChanged(string propName, object newValue)
		{
			ValueChanged.Raise(this, new ValueChangeEventArgs (propName, newValue));
		}
		#endregion

		#region IBindable implementation
		List<Binding> bindings = new List<Binding> ();
		public List<Binding> Bindings {
			get { return bindings; }
		}
		#endregion

		int _gridSize = 256;
		int _hmSize = 256;
		int _splatingSize = 2048;
		float heightScale = 20.0f;
		System.Drawing.Size cacheSize;
		float selectionRadius = 1f/1024f;
		State currentState = State.ClearHM;
		Vector4 splatBrush = new Vector4(0f, 1f/255f, 100f/255f, 1f);

		bool wireframe = false;

		/// <summary> pointer in height map texture bitmap</summary>
		int ptrHM = 0;

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

		byte[] hmData;//height map
		byte[] selectionMap;//has grid positions as colors

		Matrix4	modelview,
				projection;

		/// <summary> selected position in world coordinate </summary>
		Vector3 selPos = Vector3.Zero;
		Point<int> selCase;

		public bool Wireframe {
			get { return wireframe; }
			set {
				if (value == wireframe)
					return;
				wireframe = value;
				gridCacheIsUpToDate = false;
				NotifyValueChanged ("Wireframe", wireframe);
			}
		}


		public State CurrentState {
			get { return currentState; }
			set {
				if (currentState == value)
					return;

				currentState = value;

				switch (currentState) {
				case State.Play:					
					gridShader.SelectionColor = new Vector4 (1f, 0f, 0f, 1.0f);
					gridShader.SelectionCenter = SelectionPos.Xy / GridSize;
					SelectionRadius = selectionRadius;
					break;
				case State.HMEdition:					
					gridShader.SelectionColor = new Vector4 (1, 1, 1, 1);
					gridShader.SelectionCenter = SelectionPos.Xy / GridSize;
					SelectionRadius = selectionRadius;
					break;
				case State.ClearHM:
					break;
				case State.GroundTexturing:
					break;
				default:
					break;
				}
			}
		}
		public string[] GroundTextures { get { return groundTextures; }}

		public int SplatBrushSplat1 {
			get { return (int)(splatBrush.X * 255); }
			set {
				if (value == SplatBrushSplat1)
					return;
				splatBrush.X = (float)value / 255f;
				NotifyValueChanged ("SplatBrushSplat1", SplatBrushSplat1);
			}
		}
		public int SplatBrushSplat2 {
			get { return (int)(splatBrush.Y * 255); }
			set {
				if (value == SplatBrushSplat2)
					return;
				splatBrush.Y = (float)value / 255f;
				NotifyValueChanged ("SplatBrushSplat2", SplatBrushSplat2);
			}
		}
		public int SplatBrushPressure {
			get { return (int)(splatBrush.Z * 255); }
			set {
				if (value == SplatBrushPressure)
					return;
				splatBrush.Z = (float)value / 255f;
				NotifyValueChanged ("SplatBrushPressure", SplatBrushPressure);
			}
		}
		public System.Drawing.Size CacheSize {
			get { return cacheSize; }
			set {
				if (value == cacheSize)
					return;
				cacheSize = value;
				createCache ();
			}
		}
		public int GridSize {
			get { return _gridSize; }
			set { _gridSize = value; }
		}
		public int HmSize {
			get { return _hmSize; }
			set {
				_hmSize = value;
			}
		}
		public float HeightScale {
			get { return heightScale; }
			set {
				if (value == heightScale)
					return;
				heightScale = value;
				NotifyValueChanged ("HeightScale", heightScale);
			}
		}

		public float SelectionRadius {
			get { return selectionRadius; }
			set {
				selectionRadius = value;
				gridShader.SelectionRadius = selectionRadius;
				gridCacheIsUpToDate = false;
			}
		}
		/// <summary> Current case center in world coordinate </summary>
		public Vector3 SelCenterPos { 
			get { return new Vector3 ((float)Math.Floor (selPos.X) + 0.5f, (float)Math.Floor (selPos.Y) + 0.5f, selPos.Z); }
		}
		public Vector3 SelectionPos
		{
			get { return selPos; }
			set {
				selPos = value;
				selPos.Z = hmData[((int)selPos.Y * _hmSize + (int)selPos.X) * 4 + 1] / 256f * heightScale;
				NotifyValueChanged ("SelectionPos", selPos);
			}
		}
		public int PtrHM{ get { return ptrHM; } }

		#region GL
		Tetra.SkyBox skybox;
		vaoMesh gridMesh;

		BrushShader	hmGenerator,
					splattingBrushShader;

		public Ottd3D.VertexDispShader gridShader;
		Tetra.Shader cacheShader;

		void draw(){
			GL.Clear (ClearBufferMask.ColorBufferBit|ClearBufferMask.DepthBufferBit);

			GL.DepthMask (false);
			skybox.Render ();
			GL.DepthMask (true);

			gridShader.Enable ();

			//4th component of selection texture is used as coordinate, not as alpha
			GL.Disable (EnableCap.AlphaTest);
			GL.Disable (EnableCap.Blend);
			GL.Enable (EnableCap.CullFace);
			GL.CullFace (CullFaceMode.Back);

			if (wireframe)				
				gridMesh.Render(PrimitiveType.LineStrip);
			else
				gridMesh.Render(PrimitiveType.TriangleStrip);
			GL.Disable (EnableCap.CullFace);
		}
		void initShaders(){
			gridShader = new Ottd3D.VertexDispShader 
				("Shaders/VertDisp.vert", "Shaders/Grid.frag");
			
			gridShader.MapSize = new Vector2 (_gridSize, _gridSize);
			gridShader.HeightScale = heightScale;

			initGridMaps ();

			hmGenerator = new BrushShader (null,"Shaders/hmBrush.frag",_hmSize, _hmSize, gridShader.DisplacementMap);
			splattingBrushShader = new BrushShader (null,"Shaders/brush.frag", _splatingSize, _splatingSize, gridShader.SplatTexture);

			Tetra.Texture.ResetToDefaultLoadingParams ();

			cacheShader = new Tetra.Shader();			
		}
		void initGridMaps(){

			if (GL.IsTexture (gridShader.DisplacementMap))
				GL.DeleteTexture (gridShader.DisplacementMap);
			if (GL.IsTexture (gridShader.SplatTexture))
				GL.DeleteTexture (gridShader.SplatTexture);
			if (hmGenerator != null)
				hmGenerator.Dispose ();
			if (splattingBrushShader != null)
				splattingBrushShader.Dispose ();
			
			Tetra.Texture.DefaultMagFilter = TextureMagFilter.Nearest;
			Tetra.Texture.DefaultMinFilter = TextureMinFilter.Nearest;
			Tetra.Texture.GenerateMipMaps = false;
			Tetra.Texture.FlipY = false;
			{
				try {
					gridShader.DisplacementMap = Tetra.Texture.Load ("heightmap.png");	
				} catch {
					gridShader.DisplacementMap = new Tetra.Texture (HmSize, HmSize);
				}
				try {
					gridShader.SplatTexture = Tetra.Texture.Load ("splat.png");	
				} catch{
					gridShader.SplatTexture = new Tetra.Texture (_splatingSize, _splatingSize);
				}
			}
		}
		void initGrid()
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
						indicesVboData [(_gridSize * 2 + 1) * y + x*2+ 1] = _gridSize * y + x;
						indicesVboData [(_gridSize * 2 + 1) * y + x*2] = _gridSize * (y+1) + x;
					}

					if (x == _gridSize-1) {
						indicesVboData [(_gridSize * 2 + 1) * y + x*2 + 2] = IdxPrimitiveRestart;
					}
				}
			}

			gridMesh = new vaoMesh (positionVboData, texVboData, indicesVboData);

			Tetra.Texture.DefaultWrapMode = TextureWrapMode.Repeat;
			gridShader.DiffuseTexture = Tetra.Texture.Load (TextureTarget.Texture2DArray, groundTextures);
			Tetra.Texture.ResetToDefaultLoadingParams ();

			hmData = new byte[_hmSize*_hmSize*4];
			getHeightMapData ();
		}
		void drawGridCache()
		{
			renderGridCache ();
		}

		#region Grid Cache
		bool	gridCacheIsUpToDate = false,
		heightMapIsUpToDate = true,
		splatTextureIsUpToDate = true;
		QuadVAO cacheQuad;
		int gridCacheTex,
		gridSelectionTex;
		int fboGrid,
		depthRenderbuffer;
		DrawBuffersEnum[] dbe = new DrawBuffersEnum[]
		{
			DrawBuffersEnum.ColorAttachment0 ,
			DrawBuffersEnum.ColorAttachment1
		};


		void createCache(){
			this.Dispose();

			selectionMap = new byte[CacheSize.Width * CacheSize.Height*4];

			cacheQuad = new QuadVAO (0, 0, CacheSize.Width, CacheSize.Height, 0, 1, 1, -1);
			cacheShader.MVP = Matrix4.CreateOrthographicOffCenter 
				(0, CacheSize.Width, 0, CacheSize.Height, 0, 1);

			initGridFbo ();
		}
		void renderGridCache(){
			bool depthTest = GL.GetBoolean (GetPName.DepthTest);

			GL.Disable (EnableCap.DepthTest);

			cacheShader.Enable ();

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
			Tetra.Texture.DefaultMagFilter = TextureMagFilter.Nearest;
			Tetra.Texture.DefaultMinFilter = TextureMinFilter.Nearest;
			Tetra.Texture.GenerateMipMaps = false;
			{
				gridCacheTex = new Tetra.Texture (CacheSize.Width, CacheSize.Height);
				gridSelectionTex = new Tetra.Texture (CacheSize.Width, CacheSize.Height);
			}
			Tetra.Texture.ResetToDefaultLoadingParams ();

			// Create Depth Renderbuffer
			GL.GenRenderbuffers( 1, out depthRenderbuffer );
			GL.BindRenderbuffer( RenderbufferTarget.Renderbuffer, depthRenderbuffer );
			GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, (RenderbufferStorage)All.DepthComponent32, CacheSize.Width, CacheSize.Height);

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

			draw ();

			GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
			GL.DrawBuffer(DrawBufferMode.Back);
			getSelectionTextureData ();


			gridCacheIsUpToDate = true;
		}
		#endregion

		#endregion

		#endregion

		#region CTOR
		public Terrain(System.Drawing.Size _cacheSize){
			initShaders();
			initGrid ();
			CacheSize = _cacheSize;

			skybox = new Tetra.SkyBox (
				"#Ottd3D.images.skybox.right.bmp",
				"#Ottd3D.images.skybox.left.bmp",
				"#Ottd3D.images.skybox.top.bmp",
				"#Ottd3D.images.skybox.top.bmp",
				"#Ottd3D.images.skybox.front.bmp",
				"#Ottd3D.images.skybox.back.bmp");

			CurrentState = State.Play;
		}
		#endregion

		public void Update(Ottd3DWindow win){
			switch (CurrentState) {
			case State.Play:
				if (win.CrowInterface.hoverWidget != null)
					break;				
				break;
			case State.HMEdition:
				if (win.CrowInterface.hoverWidget != null)
					break;
				if (win.Mouse [OpenTK.Input.MouseButton.Left]) {
					hmGenerator.Color = new Vector4 (0f, 1f / 255f, 0f, 1f);
					updateHeightMap ();
				} else if (win.Mouse [OpenTK.Input.MouseButton.Right]) {
					hmGenerator.Color = new Vector4 (0f, -1f / 255f, 0f, 1f);
					updateHeightMap ();
				}
				if (!heightMapIsUpToDate)
					updateHeightMap ();			
				break;
			case State.ClearHM:
				hmGenerator.Clear ();
				getHeightMapData ();
				gridShader.DisplacementMap = hmGenerator.OutputTex;
				gridCacheIsUpToDate = false;
				CurrentState = State.HMEdition;
				break;
			case State.ClearSplatting:
				splattingBrushShader.Clear ();
				gridShader.SplatTexture = splattingBrushShader.OutputTex;
				gridCacheIsUpToDate = false;
				CurrentState = State.GroundTexturing;
				break;
			case State.LoadMap:
				initGridMaps ();
				gridCacheIsUpToDate = false;
				currentState = State.Play;
				break;
			case State.GroundTexturing:
				if (win.CrowInterface.hoverWidget != null)
					break;
				if (win.Mouse [OpenTK.Input.MouseButton.Left]) {
					splattingBrushShader.Color = splatBrush;
					updateSplatting ();
				} else if (win.Mouse [OpenTK.Input.MouseButton.Right]) {
					splattingBrushShader.Color = new Vector4 (splatBrush.X, splatBrush.Y, -splatBrush.Z, 1f);
					updateSplatting ();
				}				
				break;
			}

			if (!gridCacheIsUpToDate)
				updateGridFbo ();
		}

		public void UpdateMVP(Matrix4 _projection, Matrix4 _modelview, Vector3 vLook){
			projection = _projection;
			modelview = _modelview;

			skybox.shader.MVP =  Matrix4.CreateRotationX(-MathHelper.PiOver2) *  Matrix4.LookAt(Vector3.Zero, -vLook, Vector3.UnitZ) * projection;

			gridCacheIsUpToDate = false;
		}
		public void Render(){
			drawGridCache ();
		}

		void updatePtrHm()
		{
			selCase = new Point<int> ((int)Math.Round (SelectionPos.X), (int)Math.Round (SelectionPos.Y));
			ptrHM = (selCase.X + selCase.Y * _hmSize) * 4 ;
			NotifyValueChanged ("PtrHM", ptrHM);
		}			
		void getHeightMapData()
		{			
			GL.BindTexture (TextureTarget.Texture2D, gridShader.DisplacementMap);

			GL.GetTexImage (TextureTarget.Texture2D, 0, 
				PixelFormat.Rgba, PixelType.UnsignedByte, hmData);

			GL.BindTexture (TextureTarget.Texture2D, 0);
		}
		void updateSplatting()
		{			
			splattingBrushShader.Radius = SelectionRadius;
			splattingBrushShader.Center = SelectionPos.Xy * 4f / (float)(_splatingSize);
			//splattingBrushShader.Center = gridShader.SelectionCenter;
			splattingBrushShader.Update ();
			gridShader.SplatTexture = splattingBrushShader.OutputTex;
			gridCacheIsUpToDate = false;
		}
		void updateHeightMap()
		{			
			//float radiusDiv = 40f / (float)_hmSize;
			hmGenerator.Radius = SelectionRadius * 2f;
			hmGenerator.Center = gridShader.SelectionCenter;
			hmGenerator.Update ();
			getHeightMapData ();
			gridShader.DisplacementMap = hmGenerator.OutputTex;
			gridCacheIsUpToDate = false;

		}
		void getSelectionTextureData()
		{
			GL.BindTexture (TextureTarget.Texture2D, gridSelectionTex);

			GL.GetTexImage (TextureTarget.Texture2D, 0, 
				PixelFormat.Rgba, PixelType.UnsignedByte, selectionMap);

			GL.BindTexture (TextureTarget.Texture2D, 0);
		}

		#region Interface
		public void MouseMove(OpenTK.Input.MouseMoveEventArgs e){
			int selPtr = (e.X * 4 + (CacheSize.Height - e.Y) * CacheSize.Width * 4);
			if (selPtr + 3 < selectionMap.Length) {
				//selection texture has on each pixel WorldPosition on ground level coded as 2 half floats
				SelectionPos = new Vector3 (
					(float)selectionMap [selPtr] + (float)selectionMap [selPtr + 1] / 255f, 
					(float)selectionMap [selPtr + 2] + (float)selectionMap [selPtr + 3] / 255f, 0f);
				gridShader.SelectionCenter = SelectionPos.Xy / GridSize;
				gridCacheIsUpToDate = false;
			}
			updatePtrHm ();

			if (CurrentState == State.GroundTexturing) {					
				//				if (Mouse [OpenTK.Input.MouseButton.Left]) {
				//					splattingBrushShader.Color = splatBrush;
				//					updateSplatting ();
				//				} else if (Mouse [OpenTK.Input.MouseButton.Right]) {
				//					splattingBrushShader.Color = new Vector4 (splatBrush.X, splatBrush.Y, -1f / 255f, 1f);
				//					updateSplatting ();
				//				}
			} else if (CurrentState == State.HMEdition) {					

			}
		}
		void onSave(object sender, Crow.MouseButtonEventArgs e){
			Texture.Save (splattingBrushShader.OutputTex, @"splat.png");
			Texture.Save (hmGenerator.OutputTex, @"heightmap.png");
		}
		void onLoad(object sender, Crow.MouseButtonEventArgs e){
			CurrentState = State.LoadMap;
		}
		void onClearHM(object sender, Crow.MouseButtonEventArgs e){
			CurrentState = State.ClearHM;
		}
		void onClearSplatting(object sender, Crow.MouseButtonEventArgs e){
			CurrentState = State.ClearSplatting;
		}
		#endregion

		#region IDisposable implementation

		public void Dispose ()
		{
			if (cacheQuad != null)
				cacheQuad.Dispose ();
			if (GL.IsTexture (gridCacheTex))
				GL.DeleteTexture (gridCacheTex);
			if (GL.IsTexture (gridSelectionTex))
				GL.DeleteTexture (gridSelectionTex);
			if (GL.IsRenderbuffer (depthRenderbuffer))
				GL.DeleteRenderbuffer (depthRenderbuffer);
			if (GL.IsFramebuffer (fboGrid))
				GL.DeleteFramebuffer (fboGrid);			
		}

		#endregion
	}
}

