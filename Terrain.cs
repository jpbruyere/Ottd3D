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
using go;
using GGL;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace Ottd3D
{
	public class Terrain : IValueChange
	{
		#region IValueChange implementation
		public event EventHandler<ValueChangeEventArgs> ValueChanged;
		public void NotifyValueChange(string propName, object newValue)
		{
			ValueChanged.Raise(this, new ValueChangeEventArgs (propName, newValue));
		}
		#endregion

		#region Shaders
		static GameLib.VertexDispShader gridShader;
		static go.GLBackend.TexturedShader CacheRenderingShader;

		void initShaders()
		{
			gridShader = new GameLib.VertexDispShader 
				("Ottd3D.Shaders.VertDisp.vert", "Ottd3D.Shaders.Grid.frag");

			CacheRenderingShader = new go.GLBackend.TexturedShader();			

			gridShader.DisplacementMap = new Texture (HeightMapPath, false);
			gridShader.LightPos = vLight;
			gridShader.MapSize = new Vector2 (_gridSize, _gridSize);
			gridShader.HeightScale = heightScale;

			gridShader.SplatTexture = new Texture (SplatMapPath, false);

			Texture.SetTexFilterNeareast (gridShader.DisplacementMap);
			Texture.SetTexFilterNeareast (gridShader.SplatTexture);
		}

		void updateShadersMatrices(){
			gridShader.ProjectionMatrix = projection;
			gridShader.ModelViewMatrix = modelview;
			gridShader.ModelMatrix = Matrix4.Identity;
		}

		#endregion


		const int _gridSize = 256;
		const int _hmSize = 256;
		const int _splatingSize = 2048;
		const int _circleTexSize = 1024;
		const float heightScale = 50.0f;

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
			
		vaoMesh grid;

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

			gridShader.DiffuseTexture = new TextureArray (groundTextures);

		}
		void drawGrid()
		{
			if (!gridCacheIsUpToDate)
				updateGridFbo ();

			renderGridCache ();
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
		int ptrHM = 0;

		public int PtrHM{ get { return ptrHM; } }

		void updatePtrHm()
		{
			ptrHM = ((int)Math.Round(SelectionPos.X) + (int)Math.Round(SelectionPos.Y) * _hmSize) * 4 ;
			NotifyValueChange ("PtrHM", ptrHM);
		}
		#region Grid Cache
		bool gridCacheIsUpToDate = false,
		heightMapIsUpToDate = true,
		splatTextureIsUpToDate = true;
		QuadVAO cacheQuad;
		Matrix4 cacheProjection;
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
			gridSelectionTex = new Texture (cz.Width, cz.Height);

			Texture.SetTexFilterNeareast (gridSelectionTex);


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


		public string HeightMapPath = "heightmap.png";
		public string SplatMapPath = "splat.png";

		public string[] GroundTextures { get { return groundTextures; }}


		public Terrain ()
		{
			initGrid ();

			createCache ();

			hmData = new byte[_hmSize*_hmSize*4];
			getHeightMapData ();			
		}
	}
}

