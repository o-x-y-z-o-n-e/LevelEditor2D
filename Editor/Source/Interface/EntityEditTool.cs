using System.Drawing;
using System.Numerics;
using IconFonts;
using ImGuiNET;

namespace L2D; 

public class EntityEditTool : CanvasTool {

	public EntityEditTool() {
		DisplayName = $"{Codicons.Layout} Entities";
		LayerType = LayerType.Entities;
	}

	public override void Update(ImDrawListPtr drawList, Matrix4x4 transform, Rectangle worldBorder, bool movingCamera, bool isHovered) {
		Layer layer = Program.SelectedLayer;
		if(layer == null || layer.Type != LayerType.Entities) return;
		
		if(!layer.Visible) return;

		Scene scene = layer.Scene;
		
		ImGui.PushID("entity-edit");
		
		Vector2 scale = new(1.0F / scene.World.TileWidth, 1.0F / scene.World.TileHeight);
		Vector2 offset = new Vector2(scene.WorldX, scene.WorldY);
		int i = 0;
		foreach(var entity in layer.Entities.All) {
			ImGui.PushID(i);

			Vector2 pos = entity.Position;
			Vector2 size = entity.Size;
			
			if(entity.IsPoint) {
				size = new Vector2(Entity.POINT_HANDLE_SIZE * 2.0F / Program.CanvasPanel.GetZoom());
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
				
				// TODO: move & resize
				
				Vector2 mPos = ImGui.GetMousePos();
				if(mPos.X >= e0.X && mPos.X <= e1.X && mPos.Y >= e0.Y && mPos.Y <= e1.Y) {
					ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
				}

				// ImGui.SetCursorScreenPos(new Vector2(e0.X + edgeBorder, e0.Y + edgeBorder));
				// if(ImGui.InvisibleButton("move", new Vector2(e1.X - e0.X - edgeBorder * 2, e1.Y - e0.Y - edgeBorder * 2))) {
				// 	
				// }
				// if(ImGui.IsItemHovered()) {
				// 	
				// }

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
						}
					}

					{	// right
						Vector2 p = new Vector2(e1.X + s.X * 2, ec.Y);
						Vector2 min = p - s;
						Vector2 max = p + s;
						drawList.AddRectFilled(min, max, c, r);
						if(mPos.X >= min.X && mPos.X <= max.X && mPos.Y >= min.Y && mPos.Y <= max.Y) {
							ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
						}
					}

					{	// top
						Vector2 p = new Vector2(ec.X, e0.Y - s.Y * 2);
						Vector2 min = p - s;
						Vector2 max = p + s;
						drawList.AddRectFilled(min, max, c, r);
						if(mPos.X >= min.X && mPos.X <= max.X && mPos.Y >= min.Y && mPos.Y <= max.Y) {
							ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS);
						}
					}

					{	// bottom
						Vector2 p = new Vector2(ec.X, e1.Y + s.Y * 2);
						Vector2 min = p - s;
						Vector2 max = p + s;
						drawList.AddRectFilled(min, max, c, r);
						if(mPos.X >= min.X && mPos.X <= max.X && mPos.Y >= min.Y && mPos.Y <= max.Y) {
							ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS);
						}
					}

					{	// left-top
						Vector2 p = new Vector2(e0.X - s.X * 2, e0.Y - s.Y * 2);
						Vector2 min = p - s;
						Vector2 max = p + s;
						drawList.AddRectFilled(min, max, c, r);
						if(mPos.X >= min.X && mPos.X <= max.X && mPos.Y >= min.Y && mPos.Y <= max.Y) {
							ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNWSE);
						}
					}

					{	// right-top
						Vector2 p = new Vector2(e1.X + s.X * 2, e0.Y - s.Y * 2);
						Vector2 min = p - s;
						Vector2 max = p + s;
						drawList.AddRectFilled(min, max, c, r);
						if(mPos.X >= min.X && mPos.X <= max.X && mPos.Y >= min.Y && mPos.Y <= max.Y) {
							ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNESW);
						}
					}

					{	// left-bottom
						Vector2 p = new Vector2(e0.X - s.X * 2, e1.Y + s.Y * 2);
						Vector2 min = p - s;
						Vector2 max = p + s;
						drawList.AddRectFilled(min, max, c, r);
						if(mPos.X >= min.X && mPos.X <= max.X && mPos.Y >= min.Y && mPos.Y <= max.Y) {
							ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNESW);
						}
					}

					{	// right-bottom
						Vector2 p = new Vector2(e1.X + s.X * 2, e1.Y + s.Y * 2);
						Vector2 min = p - s;
						Vector2 max = p + s;
						drawList.AddRectFilled(min, max, c, r);
						if(mPos.X >= min.X && mPos.X <= max.X && mPos.Y >= min.Y && mPos.Y <= max.Y) {
							ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNWSE);
						}
					}
				}
			}

			ImGui.PopID();
			i++;
		}
		
		ImGui.PopID();
	}

}