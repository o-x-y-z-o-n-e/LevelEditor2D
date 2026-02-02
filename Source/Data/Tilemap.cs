using System.Numerics;
using System.Text;
using System.Xml.Linq;
using Silk.NET.OpenGL;

namespace L2D;

public class Tilemap : IDisposable {

	private const uint MAX_TILESETS = 16; // TODO: upgrade to 32
	
	public Scene Scene => scene;
	
	public TileRef[,] Grid => grid;

	public int Width => width;
	public int Height => height;
	
	private Scene scene;
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
	private static int shaderTileSizeUniform;
	private static int shaderScreenMatrixUniform;
	private static int[] shaderTextureUniforms;
	
	internal Tilemap(Layer layer) {
		scene = layer.Scene;
		Resize(scene.TileCountX, scene.TileCountY);
	}
	
	internal Tilemap(TileBrushTool brush) {
		scene = brush.Scene;
		Resize(brush.Width, brush.Height);
	}
	
	internal void Parse(XElement tilemapElement) {
		Resize(scene.TileCountX, scene.TileCountY);
		string[] items = tilemapElement.Value.Split(',');
		for(int i = 0; i < items.Length && i < grid.Length; i++) {
			int tile = 0;
			int tileset = 0;
			string[] parts = items[i].Split(':');
			if(parts.Length > 0) int.TryParse(parts[0].Trim(), out tile);
			if(parts.Length > 1) int.TryParse(parts[1].Trim(), out tileset);
			
			grid[i % width, i / width] = new(tile, tileset);
		}
	}

	internal XElement Serialize() {
		var element = new XElement("tilemap");
		StringBuilder strBuilder = new StringBuilder();
		for(int y = 0; y < height; y++) {
			for(int x = 0; x < width; x++) {
				var tile = grid[x, y];
				strBuilder.Append(tile.TileID);
				strBuilder.Append(':');
				strBuilder.Append(tile.TilesetSlot);
				strBuilder.Append(',');
			}
		}
		element.Add(strBuilder.ToString());
		return element;
	}
	
	public void Resize(int w, int h) {
		if(width == w && height == h) return;
		var newGrid = new TileRef[w, h];
		if(grid != null) {
			for(int x = 0; x < width && x < w; x++) {
				for(int y = 0; y < height && y < h; y++) {
					newGrid[x, y] = grid[x, y];
				}
			}
		}
		width = w;
		height = h;
		grid = newGrid;
	}

	public void Set(int x, int y, int tile, int tileset) {
		grid[x, y].TileID = tile;
		grid[x, y].TilesetSlot = tileset;
	}
	
	public void Get(int x, int y, out int tile, out int tileset) {
		tile = grid[x, y].TileID;
		tileset = grid[x, y].TilesetSlot;
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
		
		shaderTileSizeUniform = gl.GetUniformLocation(shaderProgramHandle, "TileSize");
		shaderScreenMatrixUniform = gl.GetUniformLocation(shaderProgramHandle, "ScreenMatrix");

		shaderTextureUniforms = new int[MAX_TILESETS];
		for(int i = 0; i < MAX_TILESETS; i++) {
			shaderTextureUniforms[i] = gl.GetUniformLocation(shaderProgramHandle, $"Tilesets[{i}]");
		}
		
		Console.WriteLine("Tilemap shaders compiled");
	}

	private unsafe void CreateFrameBuffer() {
		GL gl = Program.GL;
		
		uint w = (uint)(width * scene.World.TileWidth);
		uint h = (uint)(height * scene.World.TileHeight);

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
			gl.BufferData(GLEnum.ArrayBuffer, (nuint)(sizeof(float) * count), null, GLEnum.StaticDraw);
		}

		for(int y = 0; y < height; y++) {
			for(int x = 0; x < width; x++) {
				int i = x + y * width;
				tileBuffer[i * 4 + 0] = x;
				tileBuffer[i * 4 + 1] = y;
				tileBuffer[i * 4 + 2] = grid[x, y].TileID - 1;
				tileBuffer[i * 4 + 3] = grid[x, y].TilesetSlot - 1;
			}
		}

		fixed(void* raw = tileBuffer) {
			gl.BufferSubData(GLEnum.ArrayBuffer, 0, (nuint)(sizeof(float) * count), raw);
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

	public unsafe void Render() {
		CreateShaders();
		CreateFrameBuffer();
		CreateTileBuffer();
		CreateVertexArray();
		
		GL gl = Program.GL;

		Matrix4x4 screenMatrix = Matrix4x4.Identity;
		screenMatrix *= Matrix4x4.CreateScale(2.0F / frameBufferWidth, -2.0F / frameBufferHeight, 1.0F);
		screenMatrix *= Matrix4x4.CreateTranslation(-1.0F, 1.0F, 0.0F);
		
		gl.BindFramebuffer(GLEnum.Framebuffer, frameBufferHandle);
		gl.Viewport(0, 0, frameBufferWidth, frameBufferHeight);
		
		gl.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
		gl.Clear(ClearBufferMask.ColorBufferBit);
		
		gl.UseProgram(shaderProgramHandle);
		gl.Uniform2(shaderTileSizeUniform, new Vector2(scene.World.TileWidth, scene.World.TileHeight));
		gl.UniformMatrix4(shaderScreenMatrixUniform, 1, false, (float*)&screenMatrix);

		for(int i = 0; i < MAX_TILESETS; i++) {
			TilesetLink link = null;
			foreach(var l in scene.Tilesets) {
				if(l.Slot - 1 == i && l.Tileset != null) {
					link = l;
					break;
				}
			}
			gl.ActiveTexture(GLEnum.Texture0 + i);
			if(link != null) {
				gl.BindTexture(GLEnum.Texture2DArray, link.Tileset.TextureArray.Handle);
			} else {
				gl.BindTexture(GLEnum.Texture2DArray, 0);
			}
			gl.Uniform1(shaderTextureUniforms[i], i);
		}
		
		gl.BindVertexArray(vertexArrayHandle);
		
		gl.DrawArraysInstanced(GLEnum.Triangles, 0, 6, (uint)(width * height));
		
		gl.BindVertexArray(0);
		
		gl.BindFramebuffer(GLEnum.Framebuffer, 0);
		
		gl.Viewport(0, 0, (uint)Program.FramebufferSize.X, (uint)Program.FramebufferSize.Y);
	}

	public void Dispose() {
		if(disposed) return;
		Program.GL.DeleteTexture(frameBufferTextureHandle);
		Program.GL.DeleteFramebuffer(frameBufferHandle);
		Program.GL.DeleteBuffer(tileBufferHandle);
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

uniform mat4 ScreenMatrix;
uniform vec2 TileSize;

void main() {
    gl_Position = ScreenMatrix * vec4((aOffset.x + aPos.x) * TileSize.x, (aOffset.y + aPos.y) * TileSize.y, 0.0, 1.0);
    TexCoords = aPos;
	TileRef = aTileRef;
}
";
	
	private const string FRAGMENT_SRC = @"
#version 330 core

in vec2 TexCoords;
in vec2 TileRef;

out vec4 FragColor;

uniform sampler2DArray Tilesets[16];
// uniform sampler2DArray Tileset1;
// uniform sampler2DArray Tileset2;
// uniform sampler2DArray Tileset3;
// uniform sampler2DArray Tileset4;
// uniform sampler2DArray Tileset5;
// uniform sampler2DArray Tileset6;
// uniform sampler2DArray Tileset7;
// uniform sampler2DArray Tileset8;

void main() {
	int tileset = int(TileRef.y);
    FragColor = texture(Tilesets[tileset], vec3(TexCoords.xy, TileRef.x));
    // FragColor = texture(Tileset1, vec3(TexCoords.xy, TileRef.x));

	// if(tileset == 0) {
	// 	FragColor = texture(Tileset1, vec3(TexCoords.xy, TileRef.x));
	// } else if(tileset == 1) {
	// 	FragColor = texture(Tileset2, vec3(TexCoords.xy, TileRef.x));
	// } else if(tileset == 2) {
	// 	FragColor = texture(Tileset3, vec3(TexCoords.xy, TileRef.x));
	// } else if(tileset == 3) {
	// 	FragColor = texture(Tileset4, vec3(TexCoords.xy, TileRef.x));
	// }
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