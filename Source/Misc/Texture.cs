using System.Diagnostics;
using Serilog;
using Silk.NET.OpenGL;
using StbImageSharp;

namespace L2D;

public class Texture {

	public uint Handle => handle;

	public int Width => width;
	public int Height => height;

	private uint handle;
	private int width;
	private int height;
	private bool disposed;

	public Texture(int w, int h) {
		handle = Program.GL.GenTexture();
		width = w;
		height = h;
		disposed = false;
	}

	public static Texture LoadFromFile(string filePath) {
		try {
			byte[] raw = System.IO.File.ReadAllBytes(filePath);
			return LoadFromMemory(raw);
		} catch(Exception e) {
			Log.Error(e, "Failed to load texture from file: {@filePath}", filePath);
			return null;
		}
	}

	public static Texture LoadFromMemory(byte[] raw) {
		try {
			ImageResult image = ImageResult.FromMemory(raw, ColorComponents.RedGreenBlueAlpha);

			Texture texture = new Texture(image.Width, image.Height);

			Program.GL.BindTexture(TextureTarget.Texture2D, texture.handle);

			Program.GL.TexImage2D(
				TextureTarget.Texture2D,
				0,
				InternalFormat.Rgba,
				(uint)image.Width,
				(uint)image.Height,
				0,
				PixelFormat.Rgba,
				PixelType.UnsignedByte,
				new ReadOnlySpan<byte>(image.Data)
			);

			Program.GL.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
			Program.GL.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);

			Program.GL.BindTexture(TextureTarget.Texture2D, 0);

			return texture;
		} catch(Exception e) {
			Log.Error(e, "Failed to load texture from memory");
			return null;
		}
	}
	
	public static Texture LoadFromPixels(byte[] pixels, int width, int height) {
		try {
			Texture texture = new Texture(width, height);

			Program.GL.BindTexture(TextureTarget.Texture2D, texture.handle);

			Program.GL.TexImage2D(
				TextureTarget.Texture2D,
				0,
				InternalFormat.Rgba,
				(uint)width,
				(uint)height,
				0,
				PixelFormat.Rgba,
				PixelType.UnsignedByte,
				new ReadOnlySpan<byte>(pixels)
			);

			Program.GL.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
			Program.GL.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);

			Program.GL.BindTexture(TextureTarget.Texture2D, 0);

			return texture;
		} catch(Exception e) {
			Log.Error(e, "Failed to load texture from memory");
			return null;
		}
	}
	
	public void Bind() {
		Program.GL.BindTexture(GLEnum.Texture2D, handle);
	}
	
	public void UnBind() {
		Program.GL.BindTexture(GLEnum.Texture2D, 0);
	}
	
	public void Dispose() {
		if(disposed) return;
		Program.GL.DeleteTexture(handle);
		disposed = true;
	}

}