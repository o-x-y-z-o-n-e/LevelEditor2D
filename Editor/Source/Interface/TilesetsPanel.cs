using System.Drawing;
using System.Globalization;
using System.Numerics;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Rectangle = System.Drawing.Rectangle;

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
	private bool showColliders;

	private bool importOpenModal;
	private string importID;
	private string importGroup;
	private string importPath;
	private Vector2D<int> importOffset;
	private Vector2D<int> importSpacing;
	private Vector2D<int> importTexels;
	private bool reimport;
	private Tileset reimportTileset;
	private Vector2 colliderDragOrigin;
	private bool colliderDrag;
	private int colliderHighlightIndex;
	
	public TilesetsPanel() {
		Title = "Tilesets";

		mode = ViewMode.List;
		idEditBuffer = "";
		lastSelectedTileset = null;
		previewScale = 4;
		showColliders = false;
		colliderDragOrigin = new(0);
		colliderDrag = false;
		colliderHighlightIndex = -1;
		importID = "";
		importGroup = "";
		importPath = "";
		importOffset = new(0);
		importSpacing = new(0);
		importTexels = new(16);
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
		var selected = Items(Program.SelectedTileset);
		if(selected != Program.SelectedTileset) {
			Program.SetSelectedTileset(selected);
		}

		if(importOpenModal) {
			importOpenModal = false;
			ImGui.OpenPopup("Import Tileset");
		}
		
		ImportTilesetModal();
	}

	private void Menu() {
		if(ImGui.Button("Import")) {
			reimport = false;
			importID = "";
			importPath = "";
			importOpenModal = true;
		}
		
		ImGui.SameLine();
		
		// TODO
		ImGui.Checkbox("Show Colliders", ref showColliders);

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

	private Tileset Items(Tileset selected) {
		ImGui.BeginChild("tileset-select");
		
		World world = Program.File.World;
		
		Action displayItemList = () => {
			Vector2 itemSize = new Vector2(ImGui.GetContentRegionAvail().X, 24);
			Vector2 itemSpacing = new Vector2(4, 4);
			
			for(int i = 0; i < world.TilesetCount; i++) {
				Tileset tileset = world.GetTileset(i);
				
				ImGui.TableNextRow();
				ImGui.TableNextColumn();
				if(ImGui.Selectable(tileset.ID, Program.SelectedTileset == tileset, ImGuiSelectableFlags.SpanAllColumns)) {
					if(tileset == selected) {
						selected = null;
					} else {
						selected = tileset;
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
				
				if(tileset == selected) {
					drawList.AddRectFilled(p0, p1, Utilities.GetPackedColor(80, 80, 80, 80));
				}

				if(tileset != null) {
					Texture texSource = tileset.GetTexturePreview();
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
					if(tileset == selected) {
						selected = null;
					} else {
						selected = tileset;
					}
				}
				ImGui.SetItemTooltip(tileset.ID);
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

		return selected;
	}
	
	private void Edit() {
		ImGui.BeginChild("tileset-edit");
		
		World world = Program.File.World;

		Tileset tileset = Program.SelectedTileset;
		
		ImDrawListPtr drawList = ImGui.GetWindowDrawList();
		
		Vector2 areaPos = ImGui.GetCursorScreenPos();
		Vector2 areaSize = ImGui.GetContentRegionAvail();

		ImGui.SetNextWindowSizeConstraints(new(1, 1), new(areaSize.X, areaSize.Y - 100));
		ImGui.BeginChild(
			"tileset-preview",
			new(areaSize.X, areaSize.X), // square, based on width available
			ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY,
			ImGuiWindowFlags.AlwaysHorizontalScrollbar | ImGuiWindowFlags.AlwaysVerticalScrollbar
		);
		Vector2 region = ImGui.GetContentRegionAvail();
		Vector2 p0 = areaPos;
		Vector2 p1 = p0 + region;
		if(tileset != null) {
			Texture texSource = tileset.GetTexturePreview();
			Vector2 border = new(4,4);
			if(texSource != null) {
				Vector2 origin = ImGui.GetCursorPos();
				Vector2 size = new Vector2(texSource.Width, texSource.Height) * previewScale;
				ImGui.Image(new IntPtr(texSource.Handle), size, new(0, 0), new(1, 1), new(1,1,1,1));
				
				int countX = tileset.GetTileCountX();
				int countY = tileset.GetTileCountY();

				bool hoveredTile = false;
				for(int y = 0; y < countY; y++) {
					for(int x = 0; x < countX; x++) {
						int tileID = (y * countX + x) + 1;
						bool selected = tileEditIndex == tileID;
							
						ImGui.SetCursorPos(origin + new Vector2(world.TileWidth * x, world.TileHeight * y) * previewScale);
						ImGui.PushID(tileID);
						Vector2 c = ImGui.GetCursorScreenPos();
						Vector2 s = new Vector2(world.TileWidth, world.TileHeight) * previewScale;
						if(ImGui.InvisibleButton("##tile", s)) {
							tileEditIndex = tileID;
							selected = true;
						}
						
						if(ImGui.IsItemHovered()) {
							ImGui.GetWindowDrawList().AddRectFilled(c, c + s, Utilities.GetPackedColor(200, 200, 200, 50));
							hoveredTile = true;
						}
						if(selected) {
							ImGui.GetWindowDrawList().AddRectFilled(c, c + s, Utilities.GetPackedColor(200, 200, 200, 50));
							ImGui.GetWindowDrawList().AddRect(c, c + s, Utilities.GetPackedColor(255, 255, 255, 255));
						}
						ImGui.PopID();
					}
				}
				if(!hoveredTile) {
					ImGui.SetCursorPos(origin);
					Vector2 c = ImGui.GetContentRegionAvail();
					Vector2 s = new Vector2(float.Max(size.X, c.X), float.Max(size.Y, c.Y));
					if(ImGui.InvisibleButton("##clear", s)) {
						tileEditIndex = 0;
					}
				}
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

		if(ImGui.InputText("ID", ref idEditBuffer, 512, ImGuiInputTextFlags.EnterReturnsTrue)) {
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

		string group = tileset != null ? tileset.Group : "";
		if(ImGui.InputText("Group", ref group, 512, ImGuiInputTextFlags.EnterReturnsTrue)) {
			tileset.Group = group;
		}
		
		if(ImGui.Button("Reimport")) {
			reimport = true;
			reimportTileset = tileset;
			importOpenModal = true;
			importPath = tileset.TextureFilePath;
			importID = tileset.ID;
			importGroup = tileset.Group;
			importOffset = new(tileset.OffsetX, tileset.OffsetY);
			importSpacing = new(tileset.SpacingX, tileset.SpacingY);
			importTexels = new(tileset.SizeX, tileset.SizeY);
		}
		
		if(tileset != null) {
			ImGui.Text($"Path: {tileset.TextureFilePath}");
			ImGui.Text($"Offset: {tileset.OffsetX} {tileset.OffsetY}");
			ImGui.Text($"Spacing: {tileset.SpacingX} {tileset.SpacingY}");
			ImGui.Text($"Texels: {tileset.SizeX} {tileset.SizeY}");
		} else {
			ImGui.Text("Path: --");
			ImGui.Text("Offset: --");
			ImGui.Text("Spacing: --");
			ImGui.Text("Texels: --");
		}

		TileEdit();
		
		ImGui.EndDisabled();
		ImGui.EndChild(); // tileset-controls
		ImGui.EndChild(); // tileset-edit
		
		lastSelectedTileset = tileset;
	}

	private void TileEdit() {
		Tileset tileset = Program.SelectedTileset;

		ImGui.BeginDisabled(tileset == null);

		Vector2 origin = ImGui.GetCursorPos();
		Vector2 areaSize = ImGui.GetContentRegionAvail();
		
		ImGui.BeginChild(
			"tile-preview",
			new(areaSize.Y, areaSize.Y), // square, based on width available
			ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeX
		);
		ImDrawListPtr drawList = ImGui.GetWindowDrawList();
		Vector2 previewSize = ImGui.GetWindowSize();
		Vector2 contentSize = ImGui.GetContentRegionAvail();
		Vector2 contentPos = ImGui.GetCursorScreenPos();

		Vector2 tileSize = contentSize * 0.75F;
		if(tileSize.X < tileSize.Y) {
			tileSize.Y = tileSize.X;
		} else {
			tileSize.X = tileSize.Y;
		}

		if(tileset != null && tileEditIndex > 0) {
			Vector2 p0 = contentPos + (contentSize / 2.0F) - (tileSize / 2.0F);
			Vector2 p1 = p0 + tileSize;
			Texture previewTexture = tileset.GetTexturePreview();
			if(previewTexture != null) {
				Rectangle rect = tileset.GetTileRegion(tileEditIndex - 1);
				Vector2 tilesetSize = new(tileset.GetTextureWidth(),  tileset.GetTextureHeight());
				Vector2 uv0 = new(rect.Left / tilesetSize.X, rect.Top / tilesetSize.Y);
				Vector2 uv1 = new(rect.Right / tilesetSize.X, rect.Bottom / tilesetSize.Y);
				drawList.AddImage((IntPtr)previewTexture.Handle, p0, p1, uv0, uv1);
				drawList.AddRect(p0-new Vector2(1), p1+new Vector2(1), Utilities.GetPackedColor(255, 255, 255, 255));
			}
			var tiledata = tileset.GetTileData(tileEditIndex);
			if(tiledata != null) {
				for(int i = 0; i < tiledata.Shapes.Count; i++) {
					var shape = tiledata.Shapes[i];
					Vector2 s0 = new(shape.Position.X / tileset.SizeX, shape.Position.Y / tileset.SizeY);
					Vector2 s1 = new((shape.Position.X + shape.Size.X) / tileset.SizeX, (shape.Position.Y + shape.Size.Y) / tileset.SizeY);
					Vector2 t0 = new(float.Lerp(p0.X, p1.X, s0.X), float.Lerp(p0.Y, p1.Y, s0.Y));
					Vector2 t1 = new(float.Lerp(p0.X, p1.X, s1.X), float.Lerp(p0.Y, p1.Y, s1.Y));
					if(i == colliderHighlightIndex) {
						drawList.AddRectFilled(t0, t1, Utilities.GetPackedColor(80, 80, 255, 80));
						drawList.AddRect(t0, t1, Utilities.GetPackedColor(40, 40, 255, 200));
					} else {
						drawList.AddRectFilled(t0, t1, Utilities.GetPackedColor(30, 30, 255, 80));
						drawList.AddRect(t0, t1, Utilities.GetPackedColor(30, 30, 255, 200));
					}
				}
			}
			if(ImGui.IsWindowFocused()) {
				Vector2 pos = ImGui.GetMousePos();
				bool drag = ImGui.IsMouseDragging(ImGuiMouseButton.Left, -1.0F);

				if(drag && !colliderDrag) {
					colliderDrag = true;
					colliderDragOrigin = pos;
				}
				
				Vector2 d0 = new(float.Min(pos.X, colliderDragOrigin.X), float.Min(pos.Y, colliderDragOrigin.Y));
				Vector2 d1 = new(float.Max(pos.X, colliderDragOrigin.X), float.Max(pos.Y, colliderDragOrigin.Y));
				Vector2 c0 = new(float.Floor(Utilities.Map(d0.X, p0.X, p1.X, 0.0F, tileset.SizeX)), float.Floor(Utilities.Map(d0.Y, p0.Y, p1.Y, 0.0F, tileset.SizeY)));
				Vector2 c1 = new(float.Ceiling(Utilities.Map(d1.X, p0.X, p1.X, 0.0F, tileset.SizeX)), float.Ceiling(Utilities.Map(d1.Y, p0.Y, p1.Y, 0.0F, tileset.SizeY)));
				
				if(drag) {
					Vector2 t0 = new(float.Lerp(p0.X, p1.X, c0.X / tileset.SizeX), float.Lerp(p0.Y, p1.Y, c0.Y / tileset.SizeY));
					Vector2 t1 = new(float.Lerp(p0.X, p1.X, c1.X / tileset.SizeX), float.Lerp(p0.Y, p1.Y, c1.Y / tileset.SizeY));
					ImGui.GetWindowDrawList().AddRect(t0, t1, Utilities.GetPackedColor(255, 255, 255, 255));
				} else if(colliderDrag) {
					colliderDrag = false;
					if(tiledata == null) {
						tiledata = tileset.AddTileData(tileEditIndex);
					}
					tiledata.Shapes.Add(new(c0, c1 - c0));
				}
			}
		}

		ImGui.EndChild(); // tile-preview
		
		ImGui.SetCursorPos(origin + new Vector2(previewSize.X + 8, 0));

		ImGui.BeginChild("tile-options");
		
		int tileCount = tileset != null ? tileset.GetTileCount() : 0;
		ImGui.SeparatorText("Tile Data");
		ImGui.DragInt("Tile ID", ref tileEditIndex, 0.05F, 0, tileCount+1);
		
		ImGui.BeginDisabled(tileEditIndex == 0);
		
		int count = 0;
		TileData data = null;
		if(tileset != null) {
			data = tileset.GetTileData(tileEditIndex);
			if(data != null) count = data.Shapes.Count;
		}

		if(ImGui.InputInt("Shape Count", ref count, 1, 1)) {
			if(count < 0) count = 0;
			if(data == null) {
				data = tileset.AddTileData(tileEditIndex);
			}
			if(count < data.Shapes.Count) {
				data.Shapes.RemoveRange(count, data.Shapes.Count - count);
			} else {
				while(data.Shapes.Count < count) {
					data.Shapes.Add(new TileShape());
				}
			}
		}
		
		ImGui.Separator();

		colliderHighlightIndex = -1;
		if(data != null) {
			for(int i = 0; i < count; i++) {
				ImGui.PushID(i);

				ImGui.Text($"Shape #{i + 1}");
				
				if(ImGui.IsItemHovered()) {
					colliderHighlightIndex = i;
				}

				bool changed = false;

				Vector2 pos = data.Shapes[i].Position;
				if(ImGui.DragFloat2("Shape Pos", ref pos, 1.0F)) {
					changed = true;
				}

				if(ImGui.IsItemHovered()) {
					colliderHighlightIndex = i;
				}
				
				pos.X = float.Clamp(pos.X, 0.0F, tileset.SizeX);
				pos.Y = float.Clamp(pos.Y, 0.0F, tileset.SizeY);

				Vector2 size = data.Shapes[i].Size;
				if(ImGui.DragFloat2("Shape Size", ref size, 1.0F)) {
					changed = true;
				}
				
				if(ImGui.IsItemHovered()) {
					colliderHighlightIndex = i;
				}
				
				size.X = float.Clamp(size.X, 0.0F, tileset.SizeX - pos.X);
				size.Y = float.Clamp(size.Y, 0.0F, tileset.SizeY - pos.Y);

				if(changed) data.Shapes[i] = new TileShape(pos, size);

				ImGui.PopID();
			}
		}

		ImGui.EndDisabled(); // tileEditIndex == 0
		ImGui.EndChild(); // tile-options
		ImGui.EndDisabled(); // tileset == null
	}

	public void ImportTilesetModal() {
		bool open = true;
		ImGui.SetNextWindowSizeConstraints(new Vector2(400, 300), ImGui.GetIO().DisplaySize);
		ImGui.SetNextWindowPos(ImGui.GetIO().DisplaySize / 2.0F, ImGuiCond.Always, new Vector2(0.5F, 0.5F));
		if(ImGui.BeginPopupModal("Import Tileset", ref open)) {
			Vector2 area = ImGui.GetContentRegionAvail();
			var style = ImGui.GetStyle();

			if(ImGui.Button("Select File")) {
				FileDialog.Open(importPath, "png;bmp;jpg;jpeg", result => {
					if(result != null) {
						importPath = Program.File.GetRelativePath(result);
					}
				});
			}
			
			ImGui.InputText("Path", ref importPath, 512);

			string fullPath = Program.File.GetPath(importPath);
			
			ImGui.SetItemTooltip(fullPath);

			ImGui.BeginDisabled();
			ImGui.Text("Under construction");
			ImGui.InputInt2("Offset", ref importOffset.X);
			ImGui.InputInt2("Spacing", ref importSpacing.X);
			ImGui.InputInt2("Texels", ref importTexels.X);
			ImGui.EndDisabled();
			
			ImGui.Spacing();
			ImGui.Spacing();
			
			ImGui.BeginDisabled(reimport);

			ImGui.InputText("ID", ref importID, 512);
			
			ImGui.InputText("Group", ref importGroup, 512);
			
			ImGui.EndDisabled();
			
			ImGui.Spacing();
			ImGui.Spacing();

			bool valid = System.IO.File.Exists(fullPath) && importID != "";

			if(!reimport) {
				foreach(var t in Program.File.World.Tilesets) {
					if(t.ID == importID) {
						valid = false;
						break;
					}
				}
			}

			ImGui.BeginDisabled(!valid);
			if(ImGui.Button("Import")) {
				Tileset tileset = null;
				if(reimport) {
					tileset = reimportTileset;
				} else {
					tileset = new Tileset(Program.File);
					tileset.ID = importID;
					tileset.Group = importGroup;
				}
				tileset.TextureFilePath = importPath;
				tileset.OffsetX = importOffset.X;
				tileset.OffsetY = importOffset.Y;
				tileset.SpacingX = importSpacing.X;
				tileset.SpacingY = importSpacing.Y;
				tileset.SizeX = importTexels.X;
				tileset.SizeY = importTexels.Y;
				tileset.ReloadTexture();
				Program.File.World.AddTileset(tileset);
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			
			ImGui.EndPopup();
		}
	}

	public void SelectTilesetModal(Action<bool, Tileset> onFinish) {
		bool selected = false;
		Tileset result = null;
		bool open = true;
		ImGui.SetNextWindowSizeConstraints(new Vector2(400, 300), ImGui.GetIO().DisplaySize);
		ImGui.SetNextWindowPos(ImGui.GetIO().DisplaySize / 2.0F, ImGuiCond.Always, new Vector2(0.5F, 0.5F));
		if(ImGui.BeginPopupModal("Select Tileset", ref open)) {
			Vector2 area = ImGui.GetContentRegionAvail();
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
            
			result = Items(null);
			if(result != null) {
				selected = true;
				open = false;
				ImGui.CloseCurrentPopup();
			}
			
			ImGui.EndPopup();
		}
		if(!open) {
			onFinish?.Invoke(selected, result);
		}
	}
	
}