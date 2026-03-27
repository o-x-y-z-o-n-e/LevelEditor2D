using System.Drawing;
using System.Numerics;
using IconFonts;
using ImGuiNET;

namespace L2D;

public class CanvasPanel : Panel {
	
	private bool gridScenesOnly;

	public Vector2 Camera {
		get => camera;
		set => camera = value;
	}

	public float ZoomFactor {
		get => zooming;
		set => zooming = float.Clamp(value, ZOOM_RANGE_MIN, ZOOM_RANGE_MAX);
	}
	
	private Vector2 camera;
	private float zooming;
	private bool isHovered;
	private bool isPressed;
	
	public TileSelectTool TileSelect => tileSelect;
	public TileBrushTool TileBrush => tileBrush;
	public TileEraserTool TileEraser => tileEraser;
	public EntityEditTool EntityEdit => entityEdit;

	public Entity EntityHighlight => entityHighlight;

	private EntityEditTool entityEdit;
	private TileSelectTool tileSelect;
	private TileBrushTool tileBrush;
	private TileEraserTool tileEraser;

	public CanvasTool ActiveTool => activeTool;

	private CanvasTool activeTool;

	private bool previewSceneEnabled;
	private Scene previewSceneExisting;
	private Rectangle previewSceneArea;

	private Entity entityHighlight;
	private Entity entitySelect;
	private float entityOutlineTimer;
	
	private const float ZOOM_RANGE_MIN = -5;
	private const float ZOOM_RANGE_MAX = 15;
	private const float ZOOM_RANGE_SCALE = 0.25F;

	public CanvasPanel() {
		Title = $"{Codicons.Inspect} Canvas";

		flags |= ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar;
		
		camera = new(0, 0);
		zooming = 0;
		gridScenesOnly = true;
		entityEdit = new EntityEditTool();
		tileSelect = new TileSelectTool();
		tileBrush = new TileBrushTool();
		tileEraser = new TileEraserTool();
		activeTool = null;
	}

	public float GetZoom() {
		return MathF.Exp(zooming * ZOOM_RANGE_SCALE);
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

		Layer selectedLayer = Program.SelectedLayer;

		ToolBar();

		ImGui.BeginChild("view", ImGui.GetContentRegionAvail(), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
		
		Vector2 canvas_p0 = ImGui.GetCursorScreenPos();      // ImDrawList API uses screen coordinates!
		Vector2 canvas_sz = ImGui.GetContentRegionAvail();   // Resize canvas to what's available
		if(canvas_sz.X < 50.0f) canvas_sz.X = 50.0f;
		if(canvas_sz.Y < 50.0f) canvas_sz.Y = 50.0f;
		Vector2 canvas_p1 = new Vector2(canvas_p0.X + canvas_sz.X, canvas_p0.Y + canvas_sz.Y);
		
		ImDrawListPtr drawList = ImGui.GetWindowDrawList();
		drawList.AddRectFilled(canvas_p0, canvas_p1, Color.FromArgb(255, 50, 50, 50).GetPackedValue());
		drawList.AddRect(canvas_p0, canvas_p1, Color.FromArgb(255, 180, 180, 180).GetPackedValue());
		
		// ImGui.InvisibleButton("canvas", canvas_sz, ImGuiButtonFlags.MouseButtonRight);
		ImGui.Dummy(canvas_sz);
		isHovered = ImGui.IsItemHovered();  // Hovered
		//isPressed = ImGui.IsItemActive();   // Held

		float zoom = GetZoom();
		
		Matrix4x4 transform = Matrix4x4.Identity;

		Vector2 tileSize = new(world.TileWidth, world.TileHeight);
		Vector2 zoomScale = tileSize * zoom;

		if(previewSceneEnabled) {
			Vector2 p = new(previewSceneArea.X + previewSceneArea.Width / 2.0F, previewSceneArea.Y + previewSceneArea.Height / 2.0F);
			camera = -p * tileSize;
		}

		transform *= Matrix4x4.CreateTranslation(canvas_sz.X / 2 / zoomScale.X, canvas_sz.Y / 2 / zoomScale.Y, 0);
		transform *= Matrix4x4.CreateScale(zoomScale.X, zoomScale.Y, 1);
		transform *= Matrix4x4.CreateTranslation(camera.X * zoom, camera.Y * zoom, 0);
		transform *= Matrix4x4.CreateTranslation(canvas_p0.X, canvas_p0.Y, 0);
		
		// TODO: mouse center zoom option

		bool movingCamera = false;
		
		if(isHovered) {
			// ImGui.SetMouseCursor((ImGuiMouseCursor)10);
			// if(ImGui.IsMouseDragging(ImGuiMouseButton.Middle, -1.0F)) {
			if(ImGui.IsMouseDown(ImGuiMouseButton.Middle)) {
				movingCamera = true;
				ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
				camera.X += io.MouseDelta.X / zoom;
				camera.Y += io.MouseDelta.Y / zoom;
			}
			zooming += io.MouseWheel;
			if(io.MouseWheel != 0.0F) {
				zooming = float.Clamp(float.Round(zooming), ZOOM_RANGE_MIN, ZOOM_RANGE_MAX);
			}
		}

		// Vector2 drag_delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Right);
		// if(drag_delta.X == 0.0f && drag_delta.Y == 0.0f) {
		// 	ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags.MouseButtonRight);
		// }
		// if(ImGui.BeginPopup("context")) {
		// 	if(ImGui.MenuItem("yo mama", null, false, true)) { }
		// 	ImGui.EndPopup();
		// }
		
		drawList.PushClipRect(canvas_p0 + new Vector2(1), canvas_p1 - new Vector2(1), true);

		Rectangle worldBorder = DrawWorldBorder(world, drawList, transform);
		
		for(int i = 0; i < world.SceneCount; i++) {
			ImGui.PushID(i);
			DrawScene(world.GetScene(i), drawList, transform);
			ImGui.PopID();
		}

		if(entityHighlight != null && entityHighlight != entitySelect) {
			DrawEntityOutline(drawList, transform, entityHighlight, false);
			entityHighlight = null;
		}
		
		if(entitySelect != null) {
			DrawEntityOutline(drawList, transform, entitySelect, true);
			entitySelect = null;
		}
		
		Scene activeScene = Program.SelectedScene;
		
		tileSelect.SetScene(activeScene);
		tileBrush.SetScene(activeScene);
		tileEraser.SetScene(activeScene);
		entityEdit.SetScene(activeScene);

		if(selectedLayer == null) {
			SetTool(null);
		} else if(selectedLayer.Type == LayerType.Tiles) {
			if(activeTool == null || activeTool.LayerType != LayerType.Tiles) {
				SetTool(tileSelect);
			}
		} else if(selectedLayer.Type == LayerType.Entities) {
			if(activeTool == null || activeTool.LayerType != LayerType.Entities) {
				SetTool(entityEdit);
			}
		}
		
		activeTool?.Update(drawList, transform, worldBorder, movingCamera, isHovered);
		
		DrawEntityLabels(drawList, transform, world);

		if(previewSceneEnabled) {
			DrawScenePreview(drawList, transform);
		}
		
		drawList.PopClipRect();
		
		ImGui.EndChild();
	}

	private void ToolBar() {
		Layer selectedLayer = Program.SelectedLayer;
		
		ImGui.SetNextItemWidth(150);
		if(ImGui.BeginCombo("Active Tool", activeTool?.DisplayName ?? "None")) {
			ImGui.BeginDisabled(selectedLayer == null || selectedLayer.Type == LayerType.Entities);
			if(ImGui.Selectable(tileSelect.DisplayName, activeTool == tileSelect)) {
				SetTool(tileSelect);
			}
			ImGui.BeginDisabled(tileBrush.IsEmpty());
			if(ImGui.Selectable(tileBrush.DisplayName, activeTool == tileBrush)) {
				SetTool(tileBrush);
			}
			ImGui.EndDisabled();
			if(ImGui.Selectable(tileEraser.DisplayName, activeTool == tileEraser)) {
				SetTool(tileEraser);
			}
			ImGui.EndDisabled();
			ImGui.BeginDisabled(selectedLayer == null || selectedLayer.Type == LayerType.Tiles);
			if(ImGui.Selectable(entityEdit.DisplayName, activeTool == entityEdit)) {
				SetTool(entityEdit);
			}
			ImGui.EndDisabled();
			ImGui.EndCombo();
		}
		
		ImGui.SameLine();

		ImGui.Checkbox("Grid Scenes Only", ref gridScenesOnly);
		
		ImGui.SameLine();
		
		ImGui.SetNextItemWidth(400);
		ImGui.SliderFloat("Zoom", ref zooming, ZOOM_RANGE_MIN, ZOOM_RANGE_MAX);
		ImGui.OpenPopupOnItemClick("zoom-menu", ImGuiPopupFlags.MouseButtonRight);
		if(ImGui.BeginPopup("zoom-menu")) {
			if(ImGui.MenuItem("Reset", null, false, true)) {
				zooming = 0;
			}
			ImGui.EndPopup();
		}
	}

	private Rectangle DrawWorldBorder(World world, ImDrawListPtr drawList, Matrix4x4 transform) {
		if(world.SceneCount == 0) return new Rectangle(0,0,0,0);
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
		Vector2 p0 = Vector2.Transform(new Vector2(minX, minY), transform);
		Vector2 p1 = Vector2.Transform(new Vector2(maxX, minY), transform);
		Vector2 p2 = Vector2.Transform(new Vector2(minX, maxY), transform);
		Vector2 p3 = Vector2.Transform(new Vector2(maxX, maxY), transform);

		Rectangle region = new Rectangle(minX, minY, maxX - minX, maxY - minY);

		if(!gridScenesOnly) {
			DrawGrid(region, drawList, transform);
		}

		uint boundryLineColor = Color.FromArgb(255, 180, 180, 180).GetPackedValue();
		int lineSize = 1;
		int halfLineSize = lineSize / 2;
		drawList.AddLine(
			p0 + new Vector2(0, halfLineSize),
			p1 + new Vector2(0, halfLineSize),
			boundryLineColor,
			lineSize
		);
		drawList.AddLine(
			p0 + new Vector2(halfLineSize, 0),
			p2 + new Vector2(halfLineSize, 0),
			boundryLineColor,
			lineSize
		);
		drawList.AddLine(
			p3 + new Vector2(-halfLineSize, 0),
			p1 + new Vector2(-halfLineSize, 0),
			boundryLineColor,
			lineSize
		);
		drawList.AddLine(
			p3 + new Vector2(0, -halfLineSize),
			p2 + new Vector2(0, -halfLineSize),
			boundryLineColor,
			lineSize
		);

		return region;
	}

	private void DrawScene(Scene scene, ImDrawListPtr drawList, Matrix4x4 transform) {
		Vector2 p0 = Vector2.Transform(new Vector2(scene.WorldX, scene.WorldY), transform);
		Vector2 p1 = Vector2.Transform(new Vector2(scene.WorldX + scene.TileCountX, scene.WorldY), transform);
		Vector2 p2 = Vector2.Transform(new Vector2(scene.WorldX, scene.WorldY + scene.TileCountY), transform);
		Vector2 p3 = Vector2.Transform(new Vector2(scene.WorldX + scene.TileCountX, scene.WorldY + scene.TileCountY), transform);
		
		// ID label
		Vector2 idTextSize = ImGui.CalcTextSize(scene.ID);
		Vector2 idTextPos = p0 - Vector2.UnitY * idTextSize.Y;
		uint idTextColor = scene == Program.SelectedScene ? Color.FromArgb(255, 20, 220, 20).GetPackedValue() : 0xFFFFFFFF;
		
		drawList.AddText(idTextPos, idTextColor, scene.ID);
		drawList.AddRectFilled(idTextPos, idTextPos + idTextSize, Color.FromArgb(40, 180, 180, 180).GetPackedValue());
		
		//ImGui.PushID(scene.ID);
		ImGui.SetCursorScreenPos(idTextPos);
		if(ImGui.InvisibleButton("select-scene", idTextSize)) {
			Program.SetSelectedScene(scene);
		}
		if(ImGui.IsItemHovered()) {
			ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
		}
		//ImGui.PopID();
		
		// Layers
		foreach(var layer in scene.GetAllLayers()) {
			bool hide = !layer.IsGloballyVisible;
			if(Program.LayersPanel.IsolateLayerView && scene == Program.SelectedScene) {
				hide = layer != Program.SelectedLayer;
			}
			if(hide) continue;
			if(layer.Type == LayerType.Tiles) {
				layer.Tilemap.Render();
				uint tex = layer.Tilemap.GetFrameBufferTexture();
				drawList.AddImage((nint)tex, p0, p3, new(0,0), new(1,1));
			}
			if(layer.Type == LayerType.Entities) {
				Vector2 scale = new(1.0F / scene.World.TileWidth, 1.0F / scene.World.TileHeight);
				Vector2 offset = new Vector2(scene.WorldX, scene.WorldY);
				foreach(var entity in layer.Entities.All) {
					uint fillColor = Utilities.GetPackedColor(200, 200, 200, 30);
					uint borderColor = Utilities.GetPackedColor(200, 200, 200, 180);
					Vector2 e0 = Vector2.Transform(offset + entity.Position * scale, transform);
					Vector2 e1 = Vector2.Transform(offset + (entity.Position + entity.Size) * scale, transform);
					if(entity.IsPoint) {
						float size = Entity.POINT_HANDLE_SIZE;
						drawList.AddCircleFilled(e0, size, fillColor);
						drawList.AddCircle(e0, size, borderColor);
						drawList.AddLine(e0 + new Vector2(-size/3, 0), e0 + new Vector2(size/3, 0), borderColor);
						drawList.AddLine(e0 + new Vector2(0, -size/3), e0 + new Vector2(0, size/3), borderColor);
					} else {
						drawList.AddRectFilled(e0, e1, fillColor);
						drawList.AddRect(e0, e1, borderColor);
					}
				}
			}
		}
		
		// Grid
		if(gridScenesOnly) {
			DrawGrid(new Rectangle(scene.WorldX, scene.WorldY, scene.TileCountX, scene.TileCountY), drawList, transform);
		}
		
		// Boundry lines
		uint boundryLineColor = Color.FromArgb(255, 0, 0, 0).GetPackedValue();
		int lineSize = 1;
		int halfLineSize = lineSize / 2;
		drawList.AddLine(
			p0 + new Vector2(0, halfLineSize),
			p1 + new Vector2(0, halfLineSize),
			boundryLineColor,
			lineSize
		);
		drawList.AddLine(
			p0 + new Vector2(halfLineSize, 0),
			p2 + new Vector2(halfLineSize, 0),
			boundryLineColor,
			lineSize
		);
		drawList.AddLine(
			p3 + new Vector2(-halfLineSize, 0),
			p1 + new Vector2(-halfLineSize, 0),
			boundryLineColor,
			lineSize
		);
		drawList.AddLine(
			p3 + new Vector2(0, -halfLineSize),
			p2 + new Vector2(0, -halfLineSize),
			boundryLineColor,
			lineSize
		);
	}

	private void DrawGrid(Rectangle region, ImDrawListPtr drawList, Matrix4x4 transform) {
		int gridAlpha = (int)(20 * Utilities.Map(zooming, ZOOM_RANGE_MIN / 2.0F, 0, 0.0F, 1.0F));
		for(int x = region.X; x <= region.X + region.Width; x++) {
			drawList.AddLine(
				Vector2.Transform(new Vector2(x, region.Y), transform),
				Vector2.Transform(new Vector2(x, region.Y + region.Height), transform),
				Color.FromArgb(gridAlpha, 200, 200, 200).GetPackedValue()
			);
		}
		for(int y = region.Y; y <= region.Y + region.Height; y++) {
			drawList.AddLine(
				Vector2.Transform(new Vector2(region.X, y), transform),
				Vector2.Transform(new Vector2(region.X + region.Width, y), transform),
				Color.FromArgb(gridAlpha, 200, 200, 200).GetPackedValue()
			);
		}
	}

	private void DrawEntityLabels(ImDrawListPtr drawList, Matrix4x4 transform, World world) {
		for(int s = 0; s < world.SceneCount; s++) {
			ImGui.PushID(s);
			Scene scene = world.GetScene(s);
			int l = 0;
			foreach(var layer in scene.GetAllLayers()) {
				bool hide = !layer.IsGloballyVisible;
				if(Program.LayersPanel.IsolateLayerView && scene == Program.SelectedScene) {
					hide = layer != Program.SelectedLayer;
				}
				if(hide) {
					l++;
					continue;
				}
				ImGui.PushID(l);
				if(layer.Type == LayerType.Entities) {
					Vector2 scale = new(1.0F / scene.World.TileWidth, 1.0F / scene.World.TileHeight);
					Vector2 offset = new Vector2(scene.WorldX, scene.WorldY);
					int e = 0;
					foreach(var entity in layer.Entities.All) {
						ImGui.PushID(e);
						Vector2 e0 = Vector2.Transform(offset + entity.Position * scale, transform);
						Vector2 e1 = Vector2.Transform(offset + (entity.Position + entity.Size) * scale, transform);
						Vector2 textSize = ImGui.CalcTextSize(entity.Name);
						Vector2 textPos = new Vector2((e0.X + e1.X) / 2.0F, e0.Y) - (textSize / 2.0F) - new Vector2(0, 16);
						if(textSize.X > 0 && textPos.Y > 0) {
							if(entity.IsPoint) textPos.Y -= Entity.POINT_HANDLE_SIZE;
							else if(entity == Program.SelectedEntity) textPos.Y -= 14;
							drawList.AddRectFilled(textPos - new Vector2(2, -1), textPos + textSize + new Vector2(8, 4), Utilities.GetPackedColor(10, 10, 10, 64), 4.0F);
							drawList.AddRectFilled(textPos - new Vector2(4, 1), textPos + textSize + new Vector2(6, 2), Utilities.GetPackedColor(180, 180, 180, 192), 4.0F);
							drawList.AddText(textPos + new Vector2(1), Utilities.GetPackedColor(10, 10, 10, 128), entity.Name);
							drawList.AddText(textPos, Utilities.GetPackedColor(255, 255, 255, 255), entity.Name);
							
							ImGui.SetCursorScreenPos(textPos);
							
							if(ImGui.InvisibleButton("select-entity", textSize)) {
								Program.SetSelectedScene(entity.Collection.Layer.Scene);
								Program.SetSelectedLayer(entity.Collection.Layer);
								Program.SetSelectedEntity(entity);
							}
							if(ImGui.IsItemHovered()) {
								ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
								ShowEntityHighlight(entity);
							}
						}
						ImGui.PopID();
						e++;
					}
				}
				ImGui.PopID();
				l++;
			}
			ImGui.PopID();
		}
	}

	private void DrawEntityOutline(ImDrawListPtr drawList, Matrix4x4 transform, Entity entity, bool selected) {
		int thick = 3;
		float step = 8;

		uint fillColor = Utilities.GetPackedColor(220, 220, 220, 30);
		uint lineBaseColor = Utilities.GetPackedColor(230, 230, 230, 220);
		uint lineDashColor = Utilities.GetPackedColor(80, 80, 80, 255);

		Scene scene = entity.Collection.Layer.Scene;
		
		Vector2 scale = new(1.0F / scene.World.TileWidth, 1.0F / scene.World.TileHeight);
		Vector2 offset = new Vector2(scene.WorldX, scene.WorldY);

		Vector2 pos = entity.Position;
		Vector2 size = entity.Size;
		
		if(entity.IsPoint) {
			size = new Vector2(Entity.POINT_HANDLE_SIZE * 2 / GetZoom());
			pos = pos - size / 2.0F;
		}
		
		Vector2 e0 = Vector2.Transform(offset + pos * scale, transform);
		Vector2 e1 = Vector2.Transform(offset + (pos + size) * scale, transform);
		e0.X = (int)e0.X;
		e0.Y = (int)e0.Y;
		e1.X = (int)e1.X;
		e1.Y = (int)e1.Y;
		drawList.AddRectFilled(e0, e1, fillColor);
		
		float t = 0.0F;

		if(selected) {
			entityOutlineTimer = (entityOutlineTimer + Program.DeltaTime) % 1.0F;
			t = entityOutlineTimer;
			lineDashColor = Utilities.GetPackedColor(40, 40, 40, 255);
		}

		float hs = step / 2.0F;
		float s1 = step * t;
		float s2 = hs + s1;
		
		ImGui.PushClipRect(e0-new Vector2(1), e1+new Vector2(2), true);
		drawList.AddLine(new Vector2(e0.X, e0.Y), new Vector2(e1.X, e0.Y), lineBaseColor, thick);
		drawList.AddLine(new Vector2(e0.X, e0.Y), new Vector2(e0.X, e1.Y), lineBaseColor, thick);
		drawList.AddLine(new Vector2(e0.X, e1.Y), new Vector2(e1.X, e1.Y), lineBaseColor, thick);
		drawList.AddLine(new Vector2(e1.X, e0.Y), new Vector2(e1.X, e1.Y), lineBaseColor, thick);
		for(float x = e0.X; x <= e1.X + step; x += step) {
			drawList.AddLine(new Vector2((int)(x + s1 - step), e0.Y), new Vector2((int)(x + s2 - step), e0.Y), lineDashColor, thick);
			drawList.AddLine(new Vector2((int)(x - s1 + step), e1.Y), new Vector2((int)(x - s2 + step), e1.Y), lineDashColor, thick);
		}
		for(float y = e0.Y; y <= e1.Y + step; y += step) {
			drawList.AddLine(new Vector2(e0.X, (int)(y - s1 + step)), new Vector2(e0.X, (int)(y - s2 + step)), lineDashColor, thick);
			drawList.AddLine(new Vector2(e1.X, (int)(y + s1 - step)), new Vector2(e1.X, (int)(y + s2 - step)), lineDashColor, thick);
		}
		ImGui.PopClipRect();
	}

	private void DrawScenePreview(ImDrawListPtr drawList, Matrix4x4 transform) {
		Vector2 p0 = Vector2.Transform(new Vector2(previewSceneArea.X, previewSceneArea.Y), transform);
		Vector2 p3 = Vector2.Transform(new Vector2(previewSceneArea.X+previewSceneArea.Width, previewSceneArea.Y+previewSceneArea.Height), transform);

		bool overlaps = false;

		foreach(var scene in Program.File.World.Scenes) {
			if(scene == previewSceneExisting) continue;
			Rectangle area = new(scene.WorldX, scene.WorldY, scene.TileCountX, scene.TileCountY);
			if(area.IntersectsWith(previewSceneArea)) {
				overlaps = true;
				break;
			}
		}

		if(overlaps) {
			drawList.AddRectFilled(p0, p3, Utilities.GetPackedColor(255, 10, 10, 128));
			drawList.AddRect(p0, p3, Utilities.GetPackedColor(255, 10, 10, 255));
		} else {
			drawList.AddRectFilled(p0, p3, Utilities.GetPackedColor(20, 255, 20, 128));
			drawList.AddRect(p0, p3, Utilities.GetPackedColor(20, 255, 20, 255));
		}
	}

	public void EnableScenePreview(Rectangle area, Scene existingScene = null) {
		previewSceneEnabled = true;
		previewSceneArea = area;
		previewSceneExisting = existingScene;
	}

	public void DisableScenePreview() {
		previewSceneEnabled = false;
		previewSceneArea = default;
		previewSceneExisting = null;
	}

	public void ShowEntityHighlight(Entity entity) {
		entityHighlight = entity;
	}
	
	public void ShowEntitySelect(Entity entity) {
		entitySelect = entity;
	}

	public void LocateScene(Scene scene) {
		float zoom = GetZoom();
		Vector2 p = new(scene.WorldX + scene.TileCountX / 2.0F, scene.WorldY + scene.TileCountY / 2.0F);
		camera = -p * new Vector2(scene.World.TileWidth, scene.World.TileHeight);
	}

	public void LocateEntity(Entity entity) {
		Scene scene = entity.Collection.Layer.Scene;
		float zoom = GetZoom();
		Vector2 p = entity.Position + entity.Size / 2.0F;
		p += new Vector2(scene.WorldX * scene.World.TileWidth, scene.WorldY * scene.World.TileHeight);
		camera = -p;
	}

	public void SetTool(CanvasTool tool) {
		if(activeTool == tool) return;
		activeTool = tool;
		activeTool?.OnActive();
	}
}

public class CanvasTool {
	public string DisplayName => displayName;
	public LayerType LayerType => layerType;

	public Scene Scene => scene;

	private string displayName;
	private LayerType layerType;
	private Scene scene;

	protected CanvasTool(string displayName, LayerType type) {
		this.displayName = displayName;
		this.layerType = type;
	}

	public virtual void OnActive() {
		
	}
	public virtual void Update(ImDrawListPtr drawList, Matrix4x4 transform, Rectangle worldBorder, bool movingCamera, bool isHovered) {
		
	}
	public virtual void SetScene(Scene scene) {
		this.scene = scene;
	}
}