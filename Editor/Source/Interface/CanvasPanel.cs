using System.Drawing;
using System.Numerics;
using ImGuiNET;

namespace L2D;

public class CanvasPanel : Panel {
	
	private Vector2 scrolling;
	private float zooming;

	public CanvasPanel() {
		Title = "Canvas";

		flags |= ImGuiWindowFlags.NoScrollWithMouse;
		
		scrolling = new(64, 64);
		zooming = 0;
	}

	protected override void Update() {
		if(Program.File == null) {
			ImGui.Text("No file loaded...");
			return;
		}
		if(Program.File.World == null) {
			ImGui.Text("No world active...");
			return;
		}
		
		var io = ImGui.GetIO();

		World world = Program.File.World;

		float zoom_range_min = -5;
		float zoom_range_max = 15;
		float zoom_range_scale = 0.25F;
		ImGui.SetNextItemWidth(400);
		ImGui.SliderFloat("Zoom", ref zooming, zoom_range_min, zoom_range_max);
		ImGui.OpenPopupOnItemClick("zoom-menu", ImGuiPopupFlags.MouseButtonRight);
		if(ImGui.BeginPopup("zoom-menu")) {
			if(ImGui.MenuItem("Reset", null, false, true)) {
				zooming = 0;
			}
			ImGui.EndPopup();
		}
		
		Vector2 canvas_p0 = ImGui.GetCursorScreenPos();      // ImDrawList API uses screen coordinates!
		Vector2 canvas_sz = ImGui.GetContentRegionAvail();   // Resize canvas to what's available
		if(canvas_sz.X < 50.0f) canvas_sz.X = 50.0f;
		if(canvas_sz.Y < 50.0f) canvas_sz.Y = 50.0f;
		Vector2 canvas_p1 = new Vector2(canvas_p0.X + canvas_sz.X, canvas_p0.Y + canvas_sz.Y);
		
		ImDrawListPtr drawList = ImGui.GetWindowDrawList();
		drawList.AddRectFilled(canvas_p0, canvas_p1, Color.FromArgb(255, 50, 50, 50).GetPackedValue());
		drawList.AddRect(canvas_p0, canvas_p1, Color.FromArgb(255, 180, 180, 180).GetPackedValue());
		
		ImGui.InvisibleButton("canvas", canvas_sz, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);
		bool is_hovered = ImGui.IsItemHovered(); // Hovered
		bool is_active = ImGui.IsItemActive();   // Held
		float zoom = MathF.Exp(zooming * zoom_range_scale);
		Vector2 origin = new(canvas_p0.X + scrolling.X, canvas_p0.Y + scrolling.Y); // Lock scrolled origin
		Vector2 scale = new Vector2(world.TileWidth, world.TileHeight) * zoom;
		Vector2 mouse_pos_in_canvas = new(io.MousePos.X - origin.X, io.MousePos.Y - origin.Y);
		
		// TODO: clamp scroll
		// TODO: center zoom
		
		if(is_active && ImGui.IsMouseDragging(ImGuiMouseButton.Right, -1.0F)) {
			scrolling.X += io.MouseDelta.X;
			scrolling.Y += io.MouseDelta.Y;
		}
		if(is_hovered) {
			zooming += io.MouseWheel;
			if(io.MouseWheel != 0.0F) {
				zooming = float.Clamp(float.Round(zooming), zoom_range_min, zoom_range_max);
			}
		}

		Vector2 drag_delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Right);
		if(drag_delta.X == 0.0f && drag_delta.Y == 0.0f) {
			ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags.MouseButtonRight);
		}
		if(ImGui.BeginPopup("context")) {
			if(ImGui.MenuItem("yo mama", null, false, true)) { }
			ImGui.EndPopup();
		}
		
		drawList.PushClipRect(canvas_p0 + new Vector2(1), canvas_p1 - new Vector2(1), true);

		DrawWorldBorder(world, drawList, origin, scale);
		
		for(int i = 0; i < world.SceneCount; i++) {
			DrawScene(world.GetScene(i), drawList, origin, scale);
		}
		
		int grid_alpha = (int)(20 * Utilities.Map(zooming, zoom_range_min / 2.0F, 0, 0.0F, 1.0F));
		Vector2 grid_step = scale;
		for(float x = scrolling.X % grid_step.X; x < canvas_sz.X; x += grid_step.X) {
			drawList.AddLine(
				new Vector2(canvas_p0.X + x, canvas_p0.Y),
				new Vector2(canvas_p0.X + x, canvas_p1.Y),
				Color.FromArgb(grid_alpha, 200, 200, 200).GetPackedValue()
			);
		}
		for(float y = scrolling.Y % grid_step.Y; y < canvas_sz.Y; y += grid_step.Y) {
			drawList.AddLine(
				new Vector2(canvas_p0.X, canvas_p0.Y + y),
				new Vector2(canvas_p1.X, canvas_p0.Y + y),
				Color.FromArgb(grid_alpha, 200, 200, 200).GetPackedValue()
			);
		}
		
		drawList.PopClipRect();
	}

	private void DrawWorldBorder(World world, ImDrawListPtr drawList, Vector2 origin, Vector2 scale) {
		if(world.SceneCount == 0) return;
		int minX = int.MaxValue;
		int minY = int.MaxValue;
		int maxX = int.MinValue;
		int maxY = int.MinValue;
		for(int i = 0; i < world.SceneCount; i++) {
			Scene scene = world.GetScene(i);
			if(scene.WorldX < minX) minX = scene.WorldX;
			if(scene.WorldY < minY) minY = scene.WorldY;
			if(scene.WorldX+scene.TileCountX > maxX) maxX = scene.WorldX + scene.TileCountX;
			if(scene.WorldY+scene.TileCountY > maxY) maxY = scene.WorldY + scene.TileCountY;
		}
		
		minX--;
		minY--;
		maxX++;
		maxY++;

		Vector2[] points = {
			origin + scale * new Vector2(minX, minY),
			origin + scale * new Vector2(maxX, minY),
			origin + scale * new Vector2(minX, maxY),
			origin + scale * new Vector2(maxX, maxY)
		};

		uint boundryLineColor = Color.FromArgb(255, 180, 180, 180).GetPackedValue();
		int lineSize = 1;
		int halfLineSize = lineSize / 2;
		drawList.AddLine(
			points[0] + new Vector2(0, halfLineSize),
			points[1] + new Vector2(0, halfLineSize),
			boundryLineColor,
			lineSize
		);
		drawList.AddLine(
			points[0] + new Vector2(halfLineSize, 0),
			points[2] + new Vector2(halfLineSize, 0),
			boundryLineColor,
			lineSize
		);
		drawList.AddLine(
			points[3] + new Vector2(-halfLineSize, 0),
			points[1] + new Vector2(-halfLineSize, 0),
			boundryLineColor,
			lineSize
		);
		drawList.AddLine(
			points[3] + new Vector2(0, -halfLineSize),
			points[2] + new Vector2(0, -halfLineSize),
			boundryLineColor,
			lineSize
		);
	}

	private void DrawScene(Scene scene, ImDrawListPtr drawList, Vector2 origin, Vector2 scale) {
		Vector2 scenePos = scale * new Vector2(scene.WorldX, scene.WorldY);
		
		Vector2[] points = {
			origin + scale * new Vector2(scene.WorldX, scene.WorldY),
			origin + scale * new Vector2(scene.WorldX+scene.TileCountX, scene.WorldY),
			origin + scale * new Vector2(scene.WorldX, scene.WorldY+scene.TileCountY),
			origin + scale * new Vector2(scene.WorldX+scene.TileCountX, scene.WorldY+scene.TileCountY)
		};
		
		// ID label
		Vector2 idTextSize = ImGui.CalcTextSize(scene.ID);
		Vector2 idTextPos = origin + scenePos - Vector2.UnitY * idTextSize.Y;
		uint idTextColor = scene == Program.SelectedScene ? Color.FromArgb(255, 20, 220, 20).GetPackedValue() : 0xFFFFFFFF;
		drawList.AddText(idTextPos, idTextColor, scene.ID);
		drawList.AddRectFilled(idTextPos, idTextPos + idTextSize, Color.FromArgb(40, 180, 180, 180).GetPackedValue());

		// Tilemap layers
		for(int i = 0; i < scene.LayerCount; i++) {
			if(!scene.Layers[i].Visible || !scene.Layers[i].HasTilemap) continue;
			scene.Layers[i].Tilemap.Draw();
			uint tex = scene.Layers[i].Tilemap.GetFrameBufferTexture();
			drawList.AddImage((nint)tex, points[0], points[3], new(0,1), new(1,0));
		}
		
		// Boundry lines
		uint boundryLineColor = Color.FromArgb(255, 0, 0, 0).GetPackedValue();
		int lineSize = 1;
		int halfLineSize = lineSize / 2;
		drawList.AddLine(
			points[0] + new Vector2(0, halfLineSize),
			points[1] + new Vector2(0, halfLineSize),
			boundryLineColor,
			lineSize
		);
		drawList.AddLine(
			points[0] + new Vector2(halfLineSize, 0),
			points[2] + new Vector2(halfLineSize, 0),
			boundryLineColor,
			lineSize
		);
		drawList.AddLine(
			points[3] + new Vector2(-halfLineSize, 0),
			points[1] + new Vector2(-halfLineSize, 0),
			boundryLineColor,
			lineSize
		);
		drawList.AddLine(
			points[3] + new Vector2(0, -halfLineSize),
			points[2] + new Vector2(0, -halfLineSize),
			boundryLineColor,
			lineSize
		);
	}
}