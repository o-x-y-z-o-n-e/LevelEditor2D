using System.Xml.Linq;
using Silk.NET.OpenGL;

namespace L2D;

public class Tilemap : IDisposable {
	
	public Layer Layer => layer;

	private Layer layer;
	private TileRef[,] grid;
	private int width;
	private int height;
	private bool disposed;
	
	private float[] tileBuffer;
	private uint tileBufferHandle;
	private uint vertexBufferHandle;
	private uint vertexArrayHandle;
	private uint frameBufferHandle;
	private uint frameBufferWidth;
	private uint frameBufferHeight;
	private uint frameBufferTextureHandle;
	
	private static uint shaderProgramHandle;
	
	internal Tilemap(Layer layer) {
		this.layer = layer;
		Resize();
	}

	~Tilemap() {
		Dispose(false);
	}
	
	internal void Parse(XElement tilemapElement) {
		Resize();
		string[] items = tilemapElement.Value.Split(',');
		for(int i = 0; i < items.Length; i++) {
			int tile = 0;
			int tileset = 0;
			string[] parts = items[i].Split(':');
			if(parts.Length > 0) int.TryParse(parts[0].Trim(), out tile);
			if(parts.Length > 1) int.TryParse(parts[1].Trim(), out tileset);
			grid[i % width, i / width] = new(tile, tileset);
		}
	}
	
	public void Resize() {
		if(width == layer.Scene.TileCountX && height == layer.Scene.TileCountY) {
			return;
		}
		width = layer.Scene.TileCountX;
		height = layer.Scene.TileCountY;
		grid = new TileRef[width, height];
	}

	public uint GetFrameBufferTexture() {
		return frameBufferTextureHandle;
	}

	private static unsafe void CreateShaders() {
		if(shaderProgramHandle != 0) return;
		
		GL gl = Program.GL;

		int success;

		uint vert = gl.CreateShader(GLEnum.VertexShader);
		gl.ShaderSource(vert, VERTEX_SRC);
		gl.CompileShader(vert);
		gl.GetShader(vert, GLEnum.CompileStatus, out success);
		if(success == 0) {
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(gl.GetShaderInfoLog(vert));
			Console.ForegroundColor = ConsoleColor.White;
			Environment.Exit(1);
		}
		
		uint frag = gl.CreateShader(GLEnum.FragmentShader);
		gl.ShaderSource(frag, FRAGMENT_SRC);
		gl.CompileShader(frag);
		gl.GetShader(vert, GLEnum.CompileStatus, out success);
		if(success == 0) {
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(gl.GetShaderInfoLog(frag));
			Console.ForegroundColor = ConsoleColor.White;
			Environment.Exit(1);
		}

		shaderProgramHandle = gl.CreateProgram();
		gl.AttachShader(shaderProgramHandle, vert);
		gl.AttachShader(shaderProgramHandle, frag);
		
		gl.LinkProgram(shaderProgramHandle);
		
		gl.DeleteShader(vert);
		gl.DeleteShader(frag);
		
		Console.WriteLine("Tilemap shaders compiled");
	}

	private unsafe void CreateFrameBuffer() {
		GL gl = Program.GL;
		
		uint w = (uint)(width * layer.Scene.World.TileWidth);
		uint h = (uint)(height * layer.Scene.World.TileHeight);

		if(frameBufferWidth == w && frameBufferHeight == h) return;
		
		if(frameBufferHandle != 0) gl.DeleteFramebuffer(frameBufferHandle);
		if(frameBufferTextureHandle != 0) gl.DeleteTexture(frameBufferTextureHandle);
		
		frameBufferHandle = gl.GenFramebuffer();
		gl.BindFramebuffer(GLEnum.Framebuffer, frameBufferHandle);
		if(gl.CheckFramebufferStatus(GLEnum.Framebuffer) != GLEnum.FramebufferComplete) {
			// error
		}

		frameBufferWidth = w;
		frameBufferHeight = h;
		
		frameBufferTextureHandle = gl.GenTexture();
		gl.BindTexture(GLEnum.Texture2D, frameBufferTextureHandle);
		
		gl.TexImage2D(GLEnum.Texture2D, 0, (int)InternalFormat.Rgba32f, frameBufferWidth, frameBufferHeight, 0, GLEnum.Rgba, GLEnum.Float, null);
		
		gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
		gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
		
		gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, TextureTarget.Texture2D, frameBufferTextureHandle, 0);
		
		fixed(GLEnum* buffers = new GLEnum[] {
				GLEnum.ColorAttachment0 
			}) {
			gl.DrawBuffers(1, buffers);
		}
		
		gl.BindFramebuffer(GLEnum.Framebuffer, 0);
		gl.BindTexture(GLEnum.Texture2D, 0);
	}

	private unsafe void CreateTileBuffer() {
		GL gl = Program.GL;

		int count = 4 * width * height;
		
		if(tileBufferHandle == 0) {
			tileBufferHandle = gl.GenBuffer();
		}
		
		gl.BindBuffer(GLEnum.ArrayBuffer, tileBufferHandle);

		if(tileBuffer == null || tileBuffer.Length != count) {
			tileBuffer = new float[count];
			gl.BufferData(GLEnum.ArrayBuffer, (uint)(sizeof(float) * count), null, GLEnum.StaticDraw);
		}

		for(int x = 0; x < width; x++) {
			for(int y = 0; y < height; y++) {
				int i = x + y * width;
				tileBuffer[i * 4 + 0] = x;
				tileBuffer[i * 4 + 1] = y;
				tileBuffer[i * 4 + 2] = grid[x, y].TileID - 1;
				tileBuffer[i * 4 + 3] = grid[x, y].TilesetSlot - 1;
			}
		}

		fixed(void* raw = grid) {
			gl.BufferSubData(GLEnum.ArrayBuffer, 0, (uint)(sizeof(float) * count), raw);
		}
		
		gl.BindBuffer(GLEnum.ArrayBuffer, 0);
	}

	private unsafe void CreateVertexArray() {
		GL gl = Program.GL;

		if(vertexBufferHandle == 0) {
			vertexBufferHandle = gl.GenBuffer();
			gl.BindBuffer(GLEnum.ArrayBuffer, vertexBufferHandle);
			gl.BufferData<float>(GLEnum.ArrayBuffer, VERTICES, GLEnum.StaticDraw);
			gl.BindBuffer(GLEnum.ArrayBuffer, 0);
		}
		
		if(vertexArrayHandle == 0) {
			vertexArrayHandle = gl.GenVertexArray();
		}
		
		gl.BindVertexArray(vertexArrayHandle);
			
		gl.BindBuffer(GLEnum.ArrayBuffer, vertexBufferHandle);
		gl.EnableVertexAttribArray(0);
		gl.VertexAttribPointer(0, 2, GLEnum.Float, false, sizeof(float) * 2, (void*)(0));
			
		gl.BindBuffer(GLEnum.ArrayBuffer, tileBufferHandle);
		gl.EnableVertexAttribArray(1);
		gl.VertexAttribPointer(1, 2, GLEnum.Float, false, sizeof(float) * 4, (void*)(0));
		gl.EnableVertexAttribArray(2);
		gl.VertexAttribPointer(2, 2, GLEnum.Float, false, sizeof(float) * 4, (void*)(2 * sizeof(float)));
		gl.BindBuffer(GLEnum.ArrayBuffer, 0);
			
		gl.VertexAttribDivisor(1, 1);
		gl.VertexAttribDivisor(2, 1);
		
		gl.BindVertexArray(0);
	}

	public void Draw() {
		CreateShaders();
		CreateFrameBuffer();
		CreateTileBuffer();
		CreateVertexArray();
		
		GL gl = Program.GL;
		
		var tsl = gl.GetUniformLocation(shaderProgramHandle, "TileSize");
		
		gl.BindFramebuffer(GLEnum.Framebuffer, frameBufferHandle);
		
		gl.Disable(GLEnum.DepthTest);
		
		gl.ClearColor(0.5f, 0.0f, 0.5f, 1.0f);
		gl.Clear(ClearBufferMask.ColorBufferBit);
		
		gl.UseProgram(shaderProgramHandle);
		gl.Uniform2(tsl, layer.Scene.World.TileWidth, layer.Scene.World.TileHeight);
		gl.BindVertexArray(vertexArrayHandle);
		gl.DrawArraysInstanced(GLEnum.Triangles, 0, 6, (uint)(width * height));
		gl.BindVertexArray(0);
		
		gl.BindFramebuffer(GLEnum.Framebuffer, 0);
	}

	public void Dispose() {
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected void Dispose(bool disposing) {
		if(disposed) return;
		if(disposing) { }
		Program.GL.DeleteFramebuffer(frameBufferHandle);
		Program.GL.DeleteTexture(frameBufferTextureHandle);
		Program.GL.DeleteBuffer(tileBufferHandle);
		Program.GL.DeleteBuffer(vertexArrayHandle);
		disposed = true;
	}
	
#region ==== GLSL SHADER SOURCE ====
	private const string VERTEX_SRC = @"
#version 330 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec2 aOffset;
layout (location = 2) in vec2 aTileRef;

out vec2 TexCoords;
out vec2 TileRef;

uniform vec2 TileSize;

void main()
{
    gl_Position = vec4((aOffset.x + aPos.x) * TileSize.x, (aOffset.y + aPos.y) * TileSize.y, 0.0, 1.0);
    TexCoords = aPos;
	TileRef = aTileRef;
}
";
	
	private const string FRAGMENT_SRC = @"
#version 330 core
out vec4 FragColor;
  
in vec2 TexCoords;
in vec2 TileRef;

// uniform sampler2DArray Tilesets[16];
uniform sampler2DArray Tileset1;

void main() {
	int tileset = int(TileRef.y);
	// Tilesets[tileset]
    // FragColor = texture(Tileset1, vec3(TexCoords.xy, TileRef.x));
	FragColor = vec4(TexCoords.x, TexCoords.y, 0, 1);
}
";
#endregion ==== GLSL SHADER SOURCE ====

	private static readonly float[] VERTICES = {
		0.0f, 0.0f,
		0.0f, 1.0f,
		1.0f, 0.0f,
			
		1.0f, 1.0f,
		1.0f, 0.0f,
		0.0f, 1.0f,
	};
	
}

public struct TileRef {
	public int TileID;
	public int TilesetSlot;
	public TileRef() {
		TileID = 0;
		TilesetSlot = 0;
	}
	public TileRef(int tileID, int tilesetSlot) {
		TileID = tileID;
		TilesetSlot = tilesetSlot;
	}
}

public struct Tile {
	
} 