using System.Drawing;
using System.Numerics;
using IconFonts;
using ImGuiNET;

namespace L2D; 

public class EntityEditTool : CanvasTool {

	private Entity dragEntity;
	private EntityEditMode dragMode;
	private Vector2 dragOrigin;

	public EntityEditTool() {
		DisplayName = $"{Codicons.Layout} Entities";
		LayerType = LayerType.Entities;
		dragEntity = null;
		dragMode = EntityEditMode.Move;
	}

	public override void Update(ImDrawListPtr drawList, Matrix4x4 transform, Rectangle worldBorder, bool movingCamera, bool isHovered) {
		Layer layer = Program.SelectedLayer;
		if(layer == null || layer.Type != LayerType.Entities) return;
		
		if(!layer.Visible) return;

		Matrix4x4 invertedTransform;
		Matrix4x4.Invert(transform, out invertedTransform);
		
		Scene scene = layer.Scene;
		
		ImGui.PushID("entity-edit");
		
		Vector2 mPos = ImGui.GetMousePos();

		float zoom = Program.CanvasPanel.GetZoom();
		
		Vector2 scale = new(1.0F / scene.World.TileWidth, 1.0F / scene.World.TileHeight);
		Vector2 offset = new Vector2(scene.WorldX, scene.WorldY);
		int i = 0;
		foreach(var entity in layer.Entities.All) {
			ImGui.PushID(i);

			Vector2 pos = entity.Position;
			Vector2 size = entity.Size;
			
			if(entity.IsPoint) {
				size = new Vector2(Entity.POINT_HANDLE_SIZE * 2.0F / zoom);
				pos = pos - size / 2;
			}
			
			Vector2 e0 = Vector2.Transform(offset + pos * scale, transform);
			Vector2 e1 = Vector2.Transform(offset + (pos + size) * scale, transform);
			Vector2 ec = (e0 + e1) / 2.0F;
			
			float edgeBorder = 8;

			if(entity != Program.SelectedEntity) {
				if(e1.X - e0.X > 0.0F && e1.Y - e0.Y > 0.0F) {
					ImGui.SetCursorScreenPos(new Vector2(e0.X, e0.Y));
					if(ImGui.InvisibleButton("select", new Vector2(e1.X - e0.X, e1.Y - e0.Y))) {
						if(entity != Program.SelectedEntity) {
							Program.SetSelectedEntity(entity);
						}
					}
					if(ImGui.IsItemHovered()) {
						ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
						Program.CanvasPanel.ShowEntityHighlight(entity);
					}
				}
			} else {
				Program.CanvasPanel.ShowEntitySelect(entity);
				
				if(mPos.X >= e0.X && mPos.X <= e1.X && mPos.Y >= e0.Y && mPos.Y <= e1.Y) {
					ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
					
					if(ImGui.IsMouseClicked(ImGuiMouseButton.Left) && dragEntity == null) {
						dragEntity = entity;
						dragMode = EntityEditMode.Move;
						dragOrigin = mPos;
					}
				}

				if(!entity.IsPoint) {
					uint c = Utilities.GetPackedColor(255, 255, 255, 128);
					float r = 5.0F;
					Vector2 s = new Vector2(r);

					{	// left
						Vector2 p = new Vector2(e0.X - s.X * 2, ec.Y);
						Vector2 min = p - s;
						Vector2 max = p + s;
						drawList.AddRectFilled(min, max, c, r);
						if(mPos.X >= min.X && mPos.X <= max.X && mPos.Y >= min.Y && mPos.Y <= max.Y) {
							ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
							if(ImGui.IsMouseClicked(ImGuiMouseButton.Left) && dragEntity == null) {
								dragEntity = entity;
								dragMode = EntityEditMode.ResizeLeft;
								dragOrigin = mPos;
							}
						}
					}

					{	// right
						Vector2 p = new Vector2(e1.X + s.X * 2, ec.Y);
						Vector2 min = p - s;
						Vector2 max = p + s;
						drawList.AddRectFilled(min, max, c, r);
						if(mPos.X >= min.X && mPos.X <= max.X && mPos.Y >= min.Y && mPos.Y <= max.Y) {
							ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
							if(ImGui.IsMouseClicked(ImGuiMouseButton.Left) && dragEntity == null) {
								dragEntity = entity;
								dragMode = EntityEditMode.ResizeRight;
								dragOrigin = mPos;
							}
						}
					}

					{	// top
						Vector2 p = new Vector2(ec.X, e0.Y - s.Y * 2);
						Vector2 min = p - s;
						Vector2 max = p + s;
						drawList.AddRectFilled(min, max, c, r);
						if(mPos.X >= min.X && mPos.X <= max.X && mPos.Y >= min.Y && mPos.Y <= max.Y) {
							ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS);
							if(ImGui.IsMouseClicked(ImGuiMouseButton.Left) && dragEntity == null) {
								dragEntity = entity;
								dragMode = EntityEditMode.ResizeTop;
								dragOrigin = mPos;
							}
						}
					}

					{	// bottom
						Vector2 p = new Vector2(ec.X, e1.Y + s.Y * 2);
						Vector2 min = p - s;
						Vector2 max = p + s;
						drawList.AddRectFilled(min, max, c, r);
						if(mPos.X >= min.X && mPos.X <= max.X && mPos.Y >= min.Y && mPos.Y <= max.Y) {
							ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS);
							if(ImGui.IsMouseClicked(ImGuiMouseButton.Left) && dragEntity == null) {
								dragEntity = entity;
								dragMode = EntityEditMode.ResizeBottom;
								dragOrigin = mPos;
							}
						}
					}

					{	// left-top
						Vector2 p = new Vector2(e0.X - s.X * 2, e0.Y - s.Y * 2);
						Vector2 min = p - s;
						Vector2 max = p + s;
						drawList.AddRectFilled(min, max, c, r);
						if(mPos.X >= min.X && mPos.X <= max.X && mPos.Y >= min.Y && mPos.Y <= max.Y) {
							ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNWSE);
							if(ImGui.IsMouseClicked(ImGuiMouseButton.Left) && dragEntity == null) {
								dragEntity = entity;
								dragMode = EntityEditMode.ResizeLeftTop;
								dragOrigin = mPos;
							}
						}
					}

					{	// right-top
						Vector2 p = new Vector2(e1.X + s.X * 2, e0.Y - s.Y * 2);
						Vector2 min = p - s;
						Vector2 max = p + s;
						drawList.AddRectFilled(min, max, c, r);
						if(mPos.X >= min.X && mPos.X <= max.X && mPos.Y >= min.Y && mPos.Y <= max.Y) {
							ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNESW);
							if(ImGui.IsMouseClicked(ImGuiMouseButton.Left) && dragEntity == null) {
								dragEntity = entity;
								dragMode = EntityEditMode.ResizeRightTop;
								dragOrigin = mPos;
							}
						}
					}

					{	// left-bottom
						Vector2 p = new Vector2(e0.X - s.X * 2, e1.Y + s.Y * 2);
						Vector2 min = p - s;
						Vector2 max = p + s;
						drawList.AddRectFilled(min, max, c, r);
						if(mPos.X >= min.X && mPos.X <= max.X && mPos.Y >= min.Y && mPos.Y <= max.Y) {
							ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNESW);
							if(ImGui.IsMouseClicked(ImGuiMouseButton.Left) && dragEntity == null) {
								dragEntity = entity;
								dragMode = EntityEditMode.ResizeLeftBottom;
								dragOrigin = mPos;
							}
						}
					}

					{	// right-bottom
						Vector2 p = new Vector2(e1.X + s.X * 2, e1.Y + s.Y * 2);
						Vector2 min = p - s;
						Vector2 max = p + s;
						drawList.AddRectFilled(min, max, c, r);
						if(mPos.X >= min.X && mPos.X <= max.X && mPos.Y >= min.Y && mPos.Y <= max.Y) {
							ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNWSE);
							if(ImGui.IsMouseClicked(ImGuiMouseButton.Left) && dragEntity == null) {
								dragEntity = entity;
								dragMode = EntityEditMode.ResizeRightBottom;
								dragOrigin = mPos;
							}
						}
					}
				}
			}

			ImGui.PopID();
			i++;
		}
		
		if(dragEntity != null) {
			Vector2 drag = (mPos - dragOrigin) / zoom;
			if(!ImGui.IsMouseDown(ImGuiMouseButton.Left)) {
				dragEntity = null;
			} else {
				if(dragMode == EntityEditMode.Move) {
					ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
					dragEntity.Position += drag;
					Vector2 leftOver = new Vector2(dragEntity.Position.X % 1.0F, dragEntity.Position.Y % 1.0F);
					dragEntity.Position = new Vector2((int)dragEntity.Position.X, (int)dragEntity.Position.Y);
					dragOrigin = mPos - leftOver * zoom;
				}
				if(dragMode == EntityEditMode.ResizeLeft) {
					ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
					float p = dragEntity.Position.X + drag.X;
					float leftOver = p % 1.0F;
					p = float.Floor(p);
					dragEntity.Size.X += dragEntity.Position.X - p;
					dragEntity.Position.X = p;
					dragOrigin = mPos - new Vector2(leftOver * zoom, 0);
					if(dragEntity.Size.X < 0) {
						dragEntity.Size.X = 0;
						dragEntity = null;
					}
				}
				if(dragMode == EntityEditMode.ResizeRight) {
					ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
					dragEntity.Size.X += drag.X;
					float leftOver = dragEntity.Size.X % 1.0F;
					dragEntity.Size.X = (int)dragEntity.Size.X;
					dragOrigin = mPos - new Vector2(leftOver * zoom, 0);
					if(dragEntity.Size.X < 0) {
						dragEntity.Size.X = 0;
						dragEntity = null;
					}
				}
				if(dragMode == EntityEditMode.ResizeTop) {
					ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS);
					float p = dragEntity.Position.Y + drag.Y;
					float leftOver = p % 1.0F;
					p = float.Floor(p);
					dragEntity.Size.Y += dragEntity.Position.Y - p;
					dragEntity.Position.Y = p;
					dragOrigin = mPos - new Vector2(0, leftOver * zoom);
					if(dragEntity.Size.Y < 0) {
						dragEntity.Size.Y = 0;
						dragEntity = null;
					}
				}
				if(dragMode == EntityEditMode.ResizeBottom) {
					ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS);
					dragEntity.Size.Y += drag.Y;
					float leftOver = dragEntity.Size.Y % 1.0F;
					dragEntity.Size.Y = (int)dragEntity.Size.Y;
					dragOrigin = mPos - new Vector2(0, leftOver * zoom);
					if(dragEntity.Size.Y < 0) {
						dragEntity.Size.Y = 0;
						dragEntity = null;
					}
				}
				if(dragMode == EntityEditMode.ResizeLeftTop) {
					ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNWSE);
					Vector2 p = dragEntity.Position + drag;
					Vector2 leftOver = new(p.X % 1.0F, p.Y % 1.0F);
					p.X = float.Floor(p.X);
					p.Y = float.Floor(p.Y);
					dragEntity.Size += dragEntity.Position - p;
					dragEntity.Position = p;
					dragOrigin = mPos - leftOver * zoom;
					bool clear = false;
					if(dragEntity.Size.X < 0) {
						dragEntity.Size.X = 0;
						clear = true;
					}
					if(dragEntity.Size.Y < 0) {
						dragEntity.Size.Y = 0;
						clear = true;
					}
					if(clear) dragEntity = null;
				}
				if(dragMode == EntityEditMode.ResizeRightTop) {
					ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNESW);
					float p = dragEntity.Position.Y + drag.Y;
					float s = dragEntity.Size.X + drag.X;
					Vector2 leftOver = new(s % 1.0F, p % 1.0F);
					p = float.Floor(p);
					s = float.Floor(s);
					dragEntity.Size.X = s;
					dragEntity.Size.Y += dragEntity.Position.Y - p;
					dragEntity.Position.Y = p;
					dragOrigin = mPos - leftOver * zoom;
					bool clear = false;
					if(dragEntity.Size.X < 0) {
						dragEntity.Size.X = 0;
						clear = true;
					}
					if(dragEntity.Size.Y < 0) {
						dragEntity.Size.Y = 0;
						clear = true;
					}
					if(clear) dragEntity = null;
				}
				if(dragMode == EntityEditMode.ResizeLeftBottom) {
					ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNESW);
					float p = dragEntity.Position.X + drag.X;
					float s = dragEntity.Size.Y + drag.Y;
					Vector2 leftOver = new(p % 1.0F, s % 1.0F);
					p = float.Floor(p);
					s = float.Floor(s);
					dragEntity.Size.Y = s;
					dragEntity.Size.X += dragEntity.Position.X - p;
					dragEntity.Position.X = p;
					dragOrigin = mPos - leftOver * zoom;
					bool clear = false;
					if(dragEntity.Size.X < 0) {
						dragEntity.Size.X = 0;
						clear = true;
					}
					if(dragEntity.Size.Y < 0) {
						dragEntity.Size.Y = 0;
						clear = true;
					}
					if(clear) dragEntity = null;
				}
				if(dragMode == EntityEditMode.ResizeRightBottom) {
					ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNWSE);
					dragEntity.Size += drag;
					Vector2 leftOver = new Vector2(dragEntity.Size.X % 1.0F, dragEntity.Size.Y % 1.0F);
					dragEntity.Size = new Vector2((int)dragEntity.Size.X, (int)dragEntity.Size.Y);
					dragOrigin = mPos - leftOver * zoom;
					bool clear = false;
					if(dragEntity.Size.X < 0) {
						dragEntity.Size.X = 0;
						clear = true;
					}
					if(dragEntity.Size.Y < 0) {
						dragEntity.Size.Y = 0;
						clear = true;
					}
					if(clear) dragEntity = null;
				}
				Program.File.MarkDirty();
				Program.File.ClearEditHistory(); // TODO: undo/redo
			}	
		}
		
		ImGui.PopID();
	}

}

public enum EntityEditMode {
	Move,
	ResizeLeft,
	ResizeRight,
	ResizeTop,
	ResizeBottom,
	ResizeLeftTop,
	ResizeRightTop,
	ResizeLeftBottom,
	ResizeRightBottom
}