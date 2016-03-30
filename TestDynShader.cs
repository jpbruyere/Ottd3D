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

namespace test
{
	public class TestDynShader : OpenTKGameWindow
	{
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
		public Vector3 vEyeTarget = new Vector3(0f, 0f, 0f);
		public Vector3 vLook = Vector3.Normalize(new Vector3(-1f, -1f, 1f));  // Camera vLook Vector
		public float zFar = 512.0f;
		public float zNear = 0.1f;
		public float fovY = (float)Math.PI / 4;

		float eyeDist = 10;
		float eyeDistTarget = 10f;
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

		vaoMesh mesh;
		int tex;

		void initGL(){
			GL.Enable(EnableCap.DepthTest);
			GL.DepthFunc(DepthFunction.Less);
			//			GL.Enable(EnableCap.CullFace);
			GL.PrimitiveRestartIndex (int.MaxValue);
			GL.Enable (EnableCap.PrimitiveRestart);

			GL.Enable (EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

			mesh = new vaoMesh (0f, 0f, 0f, 100f, 100f);
			tex = Tetra.Texture.Load ("images/test.jpg");
		}

		void drawScene(){
			shader.Enable ();
			GL.ActiveTexture (TextureUnit.Texture0);
			GL.BindTexture (TextureTarget.Texture2D, tex);
			mesh.Render (PrimitiveType.TriangleStrip);
			GL.BindTexture (TextureTarget.Texture2D, 0);
		}

		#region Shaders

		public static ShadingProgram shader;

		void initShaders()
		{
			shader = new ShadingProgram ();

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
			shader.MVP = modelview * projection;

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
		}			
			
		#region Interface
		void initInterface()
		{
			this.MouseButtonUp += Mouse_ButtonUp;
			this.MouseButtonDown += Mouse_ButtonDown;
			this.MouseWheelChanged += Mouse_WheelChanged;
			this.MouseMove += Mouse_Move;
			this.KeyDown += Ottd3DWindow_KeyDown;
		}
		#region Mouse
		void Mouse_ButtonDown (object sender, OpenTK.Input.MouseButtonEventArgs e)
		{}
		void Mouse_ButtonUp (object sender, OpenTK.Input.MouseButtonEventArgs e)
		{}
		void Mouse_Move(object sender, OpenTK.Input.MouseMoveEventArgs e)
		{
			if (e.XDelta != 0 || e.YDelta != 0)
			{	
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
				break;				
			}
		}
		#endregion
		#endregion

		#region OTK overrides
		protected override void OnLoad (EventArgs e)
		{

			base.OnLoad (e);

			initGL ();

			initShaders ();

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

			Animation.ProcessAnimations ();
		}

		protected override void OnResize (EventArgs e)
		{
			base.OnResize (e);
			UpdateViewMatrix();
		}
		public override void GLClear ()
		{
			GL.ClearColor(0.0f, 0.0f, 0.2f, 1.0f);
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

			using (TestDynShader win = new TestDynShader( )) {
				win.Run (30.0);
			}
		}
		public TestDynShader ()
			: base(1024, 800, 32, 24, 1, 4, "test")
		{
			VSync = VSyncMode.Off;
		}
		#endregion
	}
}