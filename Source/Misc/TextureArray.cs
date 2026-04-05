using Silk.NET.OpenGL;

namespace E2D; 

public class TextureArray {
	
	public uint Handle => handle;

	private uint handle;
	private bool disposed;
	
	public TextureArray() {
		handle = Program.GL.GenTexture();
		disposed = false;
	}

	public void Bind() {
		Program.GL.BindTexture(GLEnum.Texture2DArray, handle);
	}
	
	public void UnBind() {
		Program.GL.BindTexture(GLEnum.Texture2DArray, 0);
	}

	public void Dispose() {
		if(disposed) return;
		Program.GL.DeleteTexture(handle);
		disposed = true;
	}
	
}