using System.Drawing;
using System.Numerics;
using IconFonts;
using ImGuiNET;

namespace L2D; 

public class TileEraserTool : CanvasTool {

	public Scene Scene => scene;

	private Scene scene;
	private int width;
	private int height;
	private bool resizing;
	private Point resizeTileOrigin;
	private bool disposed;
	
	public TileEraserTool(Scene scene) {
		DisplayName = $"{Codicons.Eraser} Eraser";
		LayerType = LayerType.Tiles;
		this.scene = scene;
		width = 1;
		height = 1;
	}

	private void Erase(int w, int h, Point offset, int mx, int my, Tilemap tilemap) {
		for(int y = 0; y < h; y++) {
			for(int x = 0; x < w; x++) {
				int tx = offset.X + mx - scene.WorldX + x;
				int ty = offset.Y + my - scene.WorldY + y;
				if(tx < 0 || ty < 0 || tx >= scene.TileCountX || ty >= scene.TileCountY) continue;
				tilemap.Grid[tx, ty] = new TileRef(0, 0);
			}
		}
	}

	public void Erase(Rectangle region, Layer layer) {
		Erase(region.Width, region.Height, region.Location, 0, 0, layer.Tilemap);
	}

	public override void Update(ImDrawListPtr drawList, Matrix4x4 transform, Rectangle worldBorder, bool movingCamera, bool isHovered) {
		Layer layer = Program.SelectedLayer;
		if(layer == null || layer.Scene != scene || layer.Type != LayerType.Tiles) return;

		Vector2 mousePos = ImGui.GetIO().MousePos;
		Matrix4x4.Invert(transform, out var transformInverted);
		Vector2 mousePosTileCoord = Vector2.Transform(mousePos, transformInverted);

		int mx = (int)MathF.Floor(mousePosTileCoord.X);
		int my = (int)MathF.Floor(mousePosTileCoord.Y);
		
		Rectangle sceneRegion = new Rectangle(scene.WorldX, scene.WorldY, scene.TileCountX, scene.TileCountY);
		
		if(sceneRegion.Contains(mx, my) && !movingCamera) {
			ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
		}
		
		bool remove = isHovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
		bool resize = isHovered && ImGui.IsMouseDown(ImGuiMouseButton.Right);
		
		if(ImGui.IsKeyPressed(ImGuiKey.Escape)) {
			Program.CanvasPanel.SetTool(Program.CanvasPanel.TileSelect);
			return;
		}
		
		if(ImGui.IsKeyReleased(ImGuiKey.LeftShift)) {
			if(Program.CanvasPanel.TileBrush.IsEmpty()) {
				Program.CanvasPanel.SetTool(Program.CanvasPanel.TileSelect);
			} else {
				Program.CanvasPanel.SetTool(Program.CanvasPanel.TileBrush);
			}
			return;
		}

		if(resize) {
			if(!resizing) {
				resizing = true;
				resizeTileOrigin = new (mx, my);
			}
			
			int sx = int.Abs(mx - resizeTileOrigin.X) + 1;
			int sy = int.Abs(my - resizeTileOrigin.Y) + 1;

			width = sx;
			height = sy;
			
			ImGui.SetMouseCursor((ImGuiMouseCursor)10);
		}
		
		Point offset = resizing ? new(int.Min(resizeTileOrigin.X - mx, 0), int.Min(resizeTileOrigin.Y - my, 0)) : new(-width / 2, -height / 2);

		Rectangle area = new Rectangle(offset.X + mx, offset.Y + my, width, height);
		
		Vector2 w0 = new Vector2(offset.X+mx,       offset.Y+my       );
		Vector2 w1 = new Vector2(offset.X+mx+width, offset.Y+my       );
		Vector2 w2 = new Vector2(offset.X+mx,       offset.Y+my+height);
		Vector2 w3 = new Vector2(offset.X+mx+width, offset.Y+my+height);
		
		Vector2 p0 = Vector2.Transform(w0, transform);
		Vector2 p1 = Vector2.Transform(w1, transform);
		Vector2 p2 = Vector2.Transform(w2, transform);
		Vector2 p3 = Vector2.Transform(w3, transform);

		uint borderColorValid = Utilities.GetPackedColor(255, 255, 40, 255);
		uint fillColorValid = Utilities.GetPackedColor(255, 255, 40, 64);
		uint borderColorInvalid = Utilities.GetPackedColor(255, 40, 40, 255);
		uint fillColorInvalid = Utilities.GetPackedColor(255, 40, 40, 64);
		
		if(remove) {
			Erase(width, height, offset, mx, my, layer.Tilemap);
		} else if(!resize && resizing) {
			Erase(width, height, offset, mx, my, layer.Tilemap);
			width = 1;
			height = 1;
			resizing = false;
		}
		
		// valid tile overlay
		for(int y = 0; y < height; y++) {
			for(int x = 0; x < width; x++) {
				int wx = offset.X + mx + x;
				int wy = offset.Y + my + y;
				if(!sceneRegion.Contains(wx, wy)) continue;
				
				Vector2 t0 = Vector2.Transform(new Vector2(wx, wy), transform);
				Vector2 t1 = Vector2.Transform(new Vector2(wx+1, wy), transform);
				Vector2 t2 = Vector2.Transform(new Vector2(wx, wy+1), transform);
				Vector2 t3 = Vector2.Transform(new Vector2(wx+1, wy+1), transform);
				
				drawList.AddRectFilled(t0, t3, fillColorValid);
				
				{	// left
					bool inb = sceneRegion.Contains(wx - 1, wy);
					if(x - 1 < 0 || !inb) {
						drawList.AddLine(
							t0, t2, borderColorValid
						);
					}
				}
				{	// right
					bool inb = sceneRegion.Contains(wx + 1, wy);
					if(x + 1 >= width || !inb) {
						drawList.AddLine(
							t1, t3, borderColorValid
						);
					}
				}
				{	// top
					bool inb = sceneRegion.Contains(wx, wy - 1);
					if(y - 1 < 0 || !inb) {
						drawList.AddLine(
							t0, t1, borderColorValid
						);
					}
				}
				{	// bottom
					bool inb = sceneRegion.Contains(wx, wy + 1);
					if(y + 1 >= height || !inb) {
						drawList.AddLine(
							t2, t3, borderColorValid
						);
					}
				}
			}
		}
		
		
		// invalid tile overlay
		for(int y = 0; y < height; y++) {
			for(int x = 0; x < width; x++) {
				int wx = offset.X + mx + x;
				int wy = offset.Y + my + y;
				if(sceneRegion.Contains(wx, wy)) continue;
				
				Vector2 t0 = Vector2.Transform(new Vector2(wx, wy), transform);
				Vector2 t1 = Vector2.Transform(new Vector2(wx+1, wy), transform);
				Vector2 t2 = Vector2.Transform(new Vector2(wx, wy+1), transform);
				Vector2 t3 = Vector2.Transform(new Vector2(wx+1, wy+1), transform);
				
				drawList.AddRectFilled(t0, t3, fillColorInvalid);

				{	// left
					bool inb = sceneRegion.Contains(wx - 1, wy);
					if(x - 1 < 0 || inb) {
						drawList.AddLine(
							t0, t2, borderColorInvalid
						);
					}
				}
				{	// right
					bool inb = sceneRegion.Contains(wx + 1, wy);
					if(x + 1 >= width || inb) {
						drawList.AddLine(
							t1, t3, borderColorInvalid
						);
					}
				}
				{	// top
					bool inb = sceneRegion.Contains(wx, wy - 1);
					if(y - 1 < 0 || inb) {
						drawList.AddLine(
							t0, t1, borderColorInvalid
						);
					}
				}
				{	// bottom
					bool inb = sceneRegion.Contains(wx, wy + 1);
					if(y + 1 >= height || inb) {
						drawList.AddLine(
							t2, t3, borderColorInvalid
						);
					}
				}
			}
		}
	}

	public void Dispose() {
		if(disposed) return;
		// TODO
		disposed = true;
	}
	
}