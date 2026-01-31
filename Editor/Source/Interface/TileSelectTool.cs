using System.Drawing;
using System.Numerics;
using IconFonts;
using ImGuiNET;

namespace L2D; 

public class TileSelectTool : CanvasTool {
	
	public Scene Scene => scene;
	public Rectangle Selection => selection;
	
	private Scene scene;
	private Rectangle selection;
	private bool blockSelectUntilRelease;
	private bool resizing;
	private Point resizeTileOrigin;

	public TileSelectTool() {
		DisplayName = $"{Codicons.ScreenFull} Select";
		LayerType = LayerType.Tiles;
		selection = new(0, 0, 0, 0);
	}

	public void SetScene(Scene scene) {
		this.scene = scene;
	}

	public override void OnActive() {
		selection = new(0, 0, 0, 0);
		resizing = false;
		blockSelectUntilRelease = true;
	}

	public override void Update(ImDrawListPtr drawList, Matrix4x4 transform, Rectangle worldBorder, bool movingCamera, bool isHovered) {
		Layer layer = Program.SelectedLayer;
		if(layer == null || layer.Scene != scene || layer.Type != LayerType.Tiles) return;
		
		Vector2 mousePos = ImGui.GetIO().MousePos;
		Matrix4x4.Invert(transform, out var transformInverted);
		Vector2 mousePosTileCoord = Vector2.Transform(mousePos, transformInverted);
		
		Rectangle sceneRegion = new Rectangle(scene.WorldX, scene.WorldY, scene.TileCountX, scene.TileCountY);
		
		int mx = (int)MathF.Floor(mousePosTileCoord.X);
		int my = (int)MathF.Floor(mousePosTileCoord.Y);
		
		if(sceneRegion.Contains(mx, my) && !movingCamera && isHovered) {
			ImGui.SetMouseCursor((ImGuiMouseCursor)10);
		}
		
		if(ImGui.IsKeyPressed(ImGuiKey.LeftShift)) {
			Program.CanvasPanel.SetTool(Program.CanvasPanel.TileEraser);
			return;
		}
		
		bool resize = isHovered && ImGui.IsMouseDown(ImGuiMouseButton.Left) && !blockSelectUntilRelease;
		if(!ImGui.IsMouseDown(ImGuiMouseButton.Left)) blockSelectUntilRelease = false;
		
		if(resize) {
			if(!resizing) {
				resizing = true;
				resizeTileOrigin = new (mx, my);
			}
			selection = new Rectangle(
				int.Min(resizeTileOrigin.X, mx),
				int.Min(resizeTileOrigin.Y, my),
				int.Abs(mx - resizeTileOrigin.X) + 1,
				int.Abs(my - resizeTileOrigin.Y) + 1
			);
		} else if(!resize && resizing) {
			int left = int.Clamp(selection.Left, scene.WorldX, scene.WorldX + scene.TileCountX);
			int right = int.Clamp(selection.Right, scene.WorldX, scene.WorldX + scene.TileCountX);
			int top = int.Clamp(selection.Top, scene.WorldY, scene.WorldY + scene.TileCountY);
			int bottom = int.Clamp(selection.Bottom, scene.WorldY, scene.WorldY + scene.TileCountY);
			selection = new Rectangle(left, top, right - left, bottom - top);
			resizing = false;
		}

		if(selection.Width > 0 && selection.Height > 0) {
			if(ImGui.IsKeyDown(ImGuiKey.LeftCtrl)) {
				if(ImGui.IsKeyPressed(ImGuiKey.X)) {
					Program.CanvasPanel.SetTool(Program.CanvasPanel.TileBrush);
					Program.CanvasPanel.TileBrush.MoveRegion(selection, layer);
					Program.File.MarkDirty();
				}
				if(ImGui.IsKeyPressed(ImGuiKey.C)) {
					Program.CanvasPanel.SetTool(Program.CanvasPanel.TileBrush);
					Program.CanvasPanel.TileBrush.CopyRegion(selection, layer);
				}
			}
			if(ImGui.IsKeyPressed(ImGuiKey.Delete)) {
				Program.CanvasPanel.TileEraser.Erase(selection, layer);
				Program.File.MarkDirty();
			}
		}

		bool inPopup = false;
		Vector2 drag_delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Right);
		if(isHovered && drag_delta.X == 0.0f && drag_delta.Y == 0.0f) {
			if(ImGui.IsMouseClicked(ImGuiMouseButton.Right)) {
				if(selection.Contains(mx, my)) {
					ImGui.OpenPopup("context");
				} else {
					Program.CanvasPanel.SetTool(Program.CanvasPanel.TileBrush);
					return;
				}
			}
		}
		if(ImGui.BeginPopup("context")) {
			inPopup = true;
			if(ImGui.MenuItem("Clear Selection")) {
				selection = new(0, 0, 0, 0);
			}
			if(ImGui.MenuItem("Copy", "Ctrl+C")) {
				Program.CanvasPanel.SetTool(Program.CanvasPanel.TileBrush);
				Program.CanvasPanel.TileBrush.CopyRegion(selection, layer);
			}
			if(ImGui.MenuItem("Move", "Ctrl+X")) {
				Program.CanvasPanel.SetTool(Program.CanvasPanel.TileBrush);
				Program.CanvasPanel.TileBrush.MoveRegion(selection, layer);
			}
			if(ImGui.MenuItem("Flip (Horizontal)")) {
				Program.CanvasPanel.SetTool(Program.CanvasPanel.TileBrush);
				Program.CanvasPanel.TileBrush.MoveRegion(selection, layer);
				Program.CanvasPanel.TileBrush.FlipHorizontal();
			}
			if(ImGui.MenuItem("Flip (Vertical)")) {
				Program.CanvasPanel.SetTool(Program.CanvasPanel.TileBrush);
				Program.CanvasPanel.TileBrush.MoveRegion(selection, layer);
				Program.CanvasPanel.TileBrush.FlipVertical();
			}
			if(ImGui.MenuItem("Rotate (-90)")) {
				Program.CanvasPanel.SetTool(Program.CanvasPanel.TileBrush);
				Program.CanvasPanel.TileBrush.MoveRegion(selection, layer);
				Program.CanvasPanel.TileBrush.RotateLeft();
			}
			if(ImGui.MenuItem("Rotate (+90)")) {
				Program.CanvasPanel.SetTool(Program.CanvasPanel.TileBrush);
				Program.CanvasPanel.TileBrush.MoveRegion(selection, layer);
				Program.CanvasPanel.TileBrush.RotateRight();
			}
			if(ImGui.MenuItem("Fill")) {
				Program.TileFillModal.Open(layer);
			}
			if(ImGui.MenuItem("Erase")) {
				Program.CanvasPanel.TileEraser.Erase(selection, layer);
			}
			if(ImGui.IsKeyPressed(ImGuiKey.Escape)) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		}
		
		if(ImGui.IsKeyPressed(ImGuiKey.Escape) && !inPopup) {
			selection = new(0, 0, 0, 0);
		}

		if(selection.Width == 0 || selection.Height == 0) return;
		
		Vector2 w0 = new Vector2(selection.X, selection.Y);
		Vector2 w1 = new Vector2(selection.X + selection.Width, selection.Y);
		Vector2 w2 = new Vector2(selection.X, selection.Y + selection.Height);
		Vector2 w3 = new Vector2(selection.X + selection.Width, selection.Y + selection.Height);
		
		Vector2 p0 = Vector2.Transform(w0, transform);
		Vector2 p1 = Vector2.Transform(w1, transform);
		Vector2 p2 = Vector2.Transform(w2, transform);
		Vector2 p3 = Vector2.Transform(w3, transform);

		uint borderColorValid = Utilities.GetPackedColor(255, 255, 255, 255);
		uint fillColorValid = Utilities.GetPackedColor(255, 255, 255, 16);
		uint borderColorInvalid = Utilities.GetPackedColor(255, 40, 40, 255);
		uint fillColorInvalid = Utilities.GetPackedColor(255, 40, 40, 64);
		
		// valid tile overlay
		for(int y = 0; y < selection.Height; y++) {
			for(int x = 0; x < selection.Width; x++) {
				int wx = selection.X + x;
				int wy = selection.Y + y;
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
					if(x + 1 >= selection.Width || !inb) {
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
					if(y + 1 >= selection.Height || !inb) {
						drawList.AddLine(
							t2, t3, borderColorValid
						);
					}
				}
			}
		}
		
		
		// invalid tile overlay
		for(int y = 0; y < selection.Height; y++) {
			for(int x = 0; x < selection.Width; x++) {
				int wx = selection.X + x;
				int wy = selection.Y + y;
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
					if(x + 1 >= selection.Width || inb) {
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
					if(y + 1 >= selection.Height || inb) {
						drawList.AddLine(
							t2, t3, borderColorInvalid
						);
					}
				}
			}
		}
	}
	
	public void Dispose() {
		
	}
}