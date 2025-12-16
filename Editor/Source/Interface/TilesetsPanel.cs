using System.Globalization;
using System.Numerics;
using ImGuiNET;
using Silk.NET.Maths;

namespace L2D; 

public class TilesetsPanel : Panel {

	private enum ViewMode {
		List,
		Grid
	}

	private static ViewMode mode;
	
	// Temp editing data, controlled by Edit()
	private static string idEditBuffer;
	private static int tileEditIndex;
	private Tileset lastSelectedTileset;
	private int previewScale;

	public TilesetsPanel() {
		Title = "Tilesets";

		mode = ViewMode.List;
		idEditBuffer = "";
		lastSelectedTileset = null;
		previewScale = 4;
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
		Menu();
		ImGui.Columns(2);
		Edit();
		ImGui.NextColumn();
		Items();
	}

	private void Menu() {
		if(ImGui.Button("Import")) {
			// TODO
		}
		
		ImGui.SameLine();

		ImGui.SetNextItemWidth(300);
		ImGui.SliderInt("Preview Scale", ref previewScale, 1, 10);
		
		ImGui.SameLine();
		
		var style = ImGui.GetStyle();
		
		float buttonWidth1 = ImGui.CalcTextSize("List").X + style.FramePadding.X * 2.0F;
		float buttonWidth2 = ImGui.CalcTextSize("Grid").X + style.FramePadding.X * 2.0F;
		float widthNeeded = buttonWidth1 + style.ItemSpacing.X + buttonWidth2;
		
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - widthNeeded);
		
		ImGui.BeginDisabled(mode == ViewMode.List);
		if(ImGui.Button("List")) {
			mode = ViewMode.List;
		}
		ImGui.EndDisabled();
		ImGui.SameLine();
		ImGui.BeginDisabled(mode == ViewMode.Grid);
		if(ImGui.Button("Grid")) {
			mode = ViewMode.Grid;
		}
		ImGui.EndDisabled();
	}

	private void Items() {
		ImGui.BeginChild("tileset-select");
		
		World world = Program.File.World;
		
		Action displayItemList = () => {
			Vector2 itemSize = new Vector2(ImGui.GetContentRegionAvail().X, 24);
			Vector2 itemSpacing = new Vector2(4, 4);
			
			for(int i = 0; i < world.TilesetCount; i++) {
				Tileset tileset = world.GetTileset(i);

				// ImDrawListPtr drawList = ImGui.GetWindowDrawList();
				// 
				// Vector2 p0 = areaPos + areaOffset;
				// Vector2 p1 = p0 + itemSize;
				// 
				// drawList.AddRectFilled(p0, p1, Utilities.GetPackedColor(50, 50, 50, 255));
				// drawList.AddRect(p0, p1, Utilities.GetPackedColor(180, 180, 180, 255));
				// 
				// Vector2 textSize = ImGui.CalcTextSize(tileset.ID);
				// ImGui.SetCursorScreenPos(p0 + new Vector2(6, (itemSize.Y / 2) - (textSize.Y / 2)));
				// ImGui.Text(tileset.ID);
				// areaOffset.Y += itemSize.Y + itemSpacing.Y;
				
				ImGui.TableNextRow();
				ImGui.TableNextColumn();
				if(ImGui.Selectable(tileset.ID, Program.SelectedTileset == tileset, ImGuiSelectableFlags.SpanAllColumns)) {
					if(Program.SelectedTileset == tileset) {
						Program.SetSelectedTileset(null);
					} else {
						Program.SetSelectedTileset(tileset);
					}
				}
				ImGui.TableNextColumn();
				ImGui.Text(tileset.Group);
				ImGui.TableNextColumn();
				ImGui.Text(tileset.TextureFilePath);
			}
		};
		
		Action displayItemGrid = () => {
			Vector2 itemSize = new Vector2(300, 300);
			Vector2 itemSpacing = new Vector2(4, 4);

			// areaOffset.Y += 18;

			ImGui.BeginChild("tileset-grid", ImGui.GetContentRegionAvail());
			
			Vector2 areaPos = ImGui.GetCursorScreenPos();
			Vector2 areaSize = ImGui.GetContentRegionAvail();
			Vector2 areaOffset = new Vector2(0, 0);
			
			for(int i = 0; i < world.TilesetCount; i++) {
				ImGui.PushID(i);
				Tileset tileset = world.GetTileset(i);

				ImDrawListPtr drawList = ImGui.GetWindowDrawList();
			
				Vector2 p0 = areaPos + areaOffset;
				Vector2 p1 = p0 + itemSize;
				
				drawList.AddRectFilled(p0, p1, Utilities.GetPackedColor(40, 40, 40, 255));
				drawList.AddRect(p0, p1, Utilities.GetPackedColor(80, 80, 80, 255));
				
				ImGui.SetCursorScreenPos(areaPos + areaOffset);
				ImGui.PushClipRect(p0, p1, true);
				
				if(Program.SelectedTileset == tileset) {
					drawList.AddRectFilled(p0, p1, Utilities.GetPackedColor(80, 80, 80, 80));
				}

				if(tileset != null) {
					Texture texSource = tileset.GetTexture();
					Vector2 header = new(0, 12);
					Vector2 border = new(4,4);
					if(texSource != null) {
						Vector2 origin = p0 + border + header;
						Vector2 maxSize = p1 - p0 - border * 2 - header;
						Vector2 size  = new(texSource.Width, texSource.Height);
						float scale = 1.0F;
						if(size.Y > size.X) {
							scale = maxSize.Y / size.Y;
						} else {
							scale = maxSize.X / size.X;
						}
						drawList.AddImage(new IntPtr(texSource.Handle), origin, origin + size * scale, new(0, 0), new(1, 1));
					} else {
						drawList.AddRectFilled(p0 + border + header, p1 - border, Utilities.GetPackedColor(255, 0, 255, 255));
					}
				}
				
				if(ImGui.InvisibleButton("select", itemSize)) {
					if(Program.SelectedTileset == tileset) {
						Program.SetSelectedTileset(null);
					} else {
						Program.SetSelectedTileset(tileset);
					}
				}

				if(ImGui.IsItemHovered()) {
					drawList.AddRectFilled(p0, p1, Utilities.GetPackedColor(120, 120, 120, 40));
				}
				
				ImGui.PopClipRect();
				
				// Calculate next item offset from areaPos
				areaOffset.X += itemSize.X + itemSpacing.X;
				if(areaSize.X - areaOffset.X < itemSize.X) {
					areaOffset.X = 0;
					areaOffset.Y += itemSize.Y + itemSpacing.Y;
				}
				ImGui.PopID();
			}
			
			ImGui.SetCursorScreenPos(areaPos);
			ImGui.Dummy(areaOffset + itemSize);
			
			ImGui.EndChild();
		};

		ImGuiTableFlags tableFlags = ImGuiTableFlags.Borders;
		
		if(ImGui.BeginTable("tileset-table", 3, tableFlags)) {
			ImGui.TableSetupColumn("ID");
			ImGui.TableSetupColumn("Group");
			ImGui.TableSetupColumn("Path");
			ImGui.TableHeadersRow();
			
			if(mode == ViewMode.List) {
				displayItemList();
			}
			
			ImGui.EndTable();
		}
		
		// grid items outside of table layout, but still use table column sorting.
		if(mode == ViewMode.Grid) {
			displayItemGrid();
		}

		ImGui.EndChild();
	}
	
	private void Edit() {
		ImGui.BeginChild("tileset-edit");
		
		World world = Program.File.World;

		Tileset tileset = Program.SelectedTileset;
		
		ImDrawListPtr drawList = ImGui.GetWindowDrawList();
		
		Vector2 areaPos = ImGui.GetCursorScreenPos();
		Vector2 areaSize = ImGui.GetContentRegionAvail();

		Vector2 previewArea = new Vector2(areaSize.X, areaSize.X); // square, based on width available
		
		ImGui.BeginChild(
			"tileset-preview",
			previewArea,
			ImGuiChildFlags.AlwaysUseWindowPadding,
			ImGuiWindowFlags.AlwaysHorizontalScrollbar | ImGuiWindowFlags.AlwaysVerticalScrollbar
		);
		Vector2 p0 = areaPos;
		Vector2 p1 = p0 + previewArea - new Vector2(16);
		drawList.AddRectFilled(areaPos, p1, Utilities.GetPackedColor(50, 50, 50, 255)); // background
		drawList.AddRect(p0, p1, Utilities.GetPackedColor(180, 180, 180, 255)); // border
		if(tileset != null) {
			Texture texSource = tileset.GetTexture();
			Vector2 border = new(4,4);
			if(texSource != null) {
				Vector2 size = new Vector2(texSource.Width, texSource.Height) * previewScale;
				ImGui.PushClipRect(p0 + new Vector2(1), p1 - new Vector2(1), true);
				ImGui.Image(new IntPtr(texSource.Handle), size, new(0, 0), new(1, 1), new(1,1,1,1));
				ImGui.PopClipRect();
			}
		} else {
			ImGui.Text("No tileset selected...");
		}
		ImGui.EndChild();
		
		ImGui.BeginChild("tileset-controls");
		ImGui.BeginDisabled(tileset == null);

		if(tileset != lastSelectedTileset) {
			idEditBuffer = tileset != null ? tileset.ID : "";
			tileEditIndex = 0;
		}

		if(ImGui.InputText("Tileset ID", ref idEditBuffer, 128, ImGuiInputTextFlags.EnterReturnsTrue)) {
			bool allowed = idEditBuffer != "";
			foreach(var ts in Program.File.World.Tilesets) {
				if(ts.ID == idEditBuffer) {
					allowed = false;
					break;
				}
			}
			if(allowed) {
				tileset.ID = idEditBuffer;
			} else {
				idEditBuffer = tileset.ID;
			}
		}

		Vector2D<int> spacing = tileset != null ? new(tileset.SpacingX, tileset.SpacingY) : new(0,0);
		if(ImGui.InputInt2("Tileset Spacing", ref spacing.X)) {
			tileset.SpacingX = spacing.X;
			tileset.SpacingY = spacing.Y;
		}
		
		Vector2D<int> offset = tileset != null ? new(tileset.OffsetX, tileset.OffsetY) : new(0,0);
		if(ImGui.InputInt2("Tileset Offset", ref offset.X)) {
			tileset.OffsetX = offset.X;
			tileset.OffsetY = offset.Y;
		}
		
		string fileDisplayString = tileset != null ? tileset.TextureFilePath : "...";
		if(ImGui.Button(fileDisplayString)) {
			// TODO: open file select dialog
		}

		int tileCount = tileset != null ? tileset.GetTileCount() : 0;
		tileCount = 10;

		ImGui.SeparatorText("Tile Data");
		ImGui.DragInt("Tile Index", ref tileEditIndex, 0.05F, 0, tileCount);
		
		int count = 3;

		ImGui.DragInt("Shape Count", ref count, 0.05F, 0, 16);
		ImGui.Separator();

		for(int i = 0; i < count; i++) {
			ImGui.PushID(i);
			
			ImGui.Text($"Shape #{i+1}");

			Vector2D<int> pos = new(0, 0);
			ImGui.InputInt2("Shape Pos", ref pos.X);
		
			Vector2D<int> size = new(0, 0);
			ImGui.InputInt2("Shape Size", ref size.X);
			
			ImGui.PopID();
		}
		
		ImGui.EndDisabled();
		ImGui.EndChild(); // tileset-controls
		ImGui.EndChild(); // tileset-edit
		
		lastSelectedTileset = tileset;
	}
	
}