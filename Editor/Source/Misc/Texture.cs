using System.Diagnostics;
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

	~Texture() {
		Dispose(false);
	}

	public static Texture Load(string filePath) {
		try {
			byte[] raw = System.IO.File.ReadAllBytes(filePath);
			
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
			
			Program.GL.TextureParameter(texture.handle, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
			Program.GL.TextureParameter(texture.handle, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
			
			Program.GL.BindTexture(TextureTarget.Texture2D, 0);
			
			return texture;
		} catch(Exception e) {
			Console.Error.WriteLine(e);
			return null;
		}
	}
	
	public void Dispose() {
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected void Dispose(bool disposing) {
		if(disposed) return;
		if(disposing) { }
		Program.GL.DeleteTexture(handle);
		disposed = true;
	}

}