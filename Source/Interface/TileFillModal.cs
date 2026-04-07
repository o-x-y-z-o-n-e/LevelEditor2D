using System.Drawing;
using System.Numerics;
using System.Text.RegularExpressions;
using ImGuiNET;

namespace E2D; 

public class TileFillModal {
	
	private bool open;
	private Layer layer;
	private List<TileFillEntry> entries;
	private float emptyTileWeight;

	public TileFillModal() {
		entries = new();
		open = false;
		layer = null;
		emptyTileWeight = 0.0F;
	}

	public void Open(Layer layer) {
		this.layer = layer;
		open = true;
	}
	
	internal void Body() {
		if(open) {
			ImGui.OpenPopup("Tile Fill");
			open = false;
		}
		ImGui.SetNextWindowSizeConstraints(new Vector2(400, 400), ImGui.GetIO().DisplaySize);
		bool o = true;
		if(ImGui.BeginPopupModal("Tile Fill", ref o)) {
			var style = ImGui.GetStyle();
			Vector2 optionsArea = ImGui.GetContentRegionAvail() - new Vector2(0, style.WindowPadding.Y + style.FramePadding.Y + ImGui.GetFontSize());
			ImGui.BeginChild("options", optionsArea);
			ImGui.Columns(2);
			ImGui.BeginChild("list");
			List();
			ImGui.EndChild();
			ImGui.NextColumn();
			ImGui.BeginChild("grid");
			Grid();
			ImGui.EndChild();
			ImGui.EndChild();
			if(ImGui.Button("Confirm")) {
				Fill();
				ImGui.CloseCurrentPopup();
			}
			ImGui.SameLine();
			if(ImGui.Button("Clear")) {
				entries.Clear();
				emptyTileWeight = 0.0F;
			}
			ImGui.EndPopup();
		}
	}
	
	private void List() {
		var drawList = ImGui.GetWindowDrawList();
		var style = ImGui.GetStyle();
		
		Vector2 size = new Vector2(ImGui.GetContentRegionAvail().X, 100);
		Vector2 iconSize = new Vector2(size.Y, size.Y) - style.WindowPadding * 2;
		float fontSize = ImGui.GetFontSize();

		Vector2 removeButtonSize = ImGui.CalcTextSize("Remove") + style.FramePadding * 2;

		float sum = GetTotalWeight();
		
		ImGui.BeginChild("empty", size, ImGuiChildFlags.Borders);
        
		Vector2 cur = ImGui.GetCursorScreenPos();
		uint emptyCrossColor = Utilities.GetPackedColor(180, 180, 180, 255);
		drawList.AddLine(cur, cur + iconSize, emptyCrossColor);
		drawList.AddLine(cur + new Vector2(iconSize.X, 0), cur + new Vector2(0, iconSize.Y), emptyCrossColor);
		drawList.AddRect(cur - new Vector2(1), cur + new Vector2(1) + iconSize, Utilities.GetPackedColor(255, 255, 255, 255));
		ImGui.Dummy(iconSize);
		ImGui.SameLine();
		ImGui.DragFloat("Weight", ref emptyTileWeight, 1.0F, 0.0F, float.MaxValue);
		
		ImGui.SetCursorPos(new Vector2(style.WindowPadding.X + iconSize.X + style.ItemSpacing.X, size.Y - style.WindowPadding.Y - fontSize * 2 - style.ItemSpacing.Y));
		ImGui.Text("Distributed Weight:");
		ImGui.SameLine();
		if(entries.Count > 0) {
			ImGui.TextUnformatted((emptyTileWeight / sum).ToString("P2"));
		} else {
			ImGui.TextUnformatted("100.00%");
		}
		ImGui.SetCursorPos(new Vector2(style.WindowPadding.X + iconSize.X + style.ItemSpacing.X, size.Y - style.WindowPadding.Y - fontSize));
		ImGui.Text("Empty Tile");
		
		ImGui.EndChild();
		
		ImGui.Separator();

		int removeIndex = -1;

		for(int i = 0; i < entries.Count; i++) {
			ImGui.Spacing();
			ImGui.PushID(i);
			ImGui.BeginChild("entry", size, ImGuiChildFlags.Borders);

			Tileset tileset = null;
			foreach(var link in layer.Scene.Tilesets) {
				if(link.Slot == entries[i].TilesetSlot) {
					tileset = link.Tileset;
					break;
				}
			}

			if(tileset != null && tileset.GetTexturePreview() != null) {
				Texture texture = tileset.GetTexturePreview();
				Rectangle rect = tileset.GetTileRegion(entries[i].TileID - 1);
				Vector2 uvMin = new Vector2(rect.Left / (float)texture.Width, rect.Top / (float)texture.Height);
				Vector2 uvMax = new Vector2(rect.Right / (float)texture.Width, rect.Bottom / (float)texture.Height);
				cur = ImGui.GetCursorScreenPos();
				ImGui.Image((IntPtr)texture.Handle, iconSize, uvMin, uvMax);
				drawList.AddRect(cur - new Vector2(1), cur + new Vector2(1) + iconSize, Utilities.GetPackedColor(255, 255, 255, 255));
			} else {
				ImGui.Dummy(iconSize);
			}
			
			ImGui.SameLine();
			
			ImGui.DragFloat("Weight", ref entries[i].Weight, 1.0F, 1.0F, float.MaxValue);
			
			ImGui.SetCursorPos(new Vector2(style.WindowPadding.X + iconSize.X + style.ItemSpacing.X, size.Y - style.WindowPadding.Y - fontSize * 2 - style.ItemSpacing.Y));
			ImGui.Text("Distributed Weight:");
			ImGui.SameLine();
			ImGui.TextUnformatted((entries[i].Weight / sum).ToString("P2"));
			ImGui.SetCursorPos(new Vector2(style.WindowPadding.X + iconSize.X + style.ItemSpacing.X, size.Y - style.WindowPadding.Y - fontSize));
			ImGui.Text("Tileset:");
			ImGui.SameLine();
			ImGui.Text(tileset?.ID ?? "--");
			
			ImGui.SetCursorPos(size - removeButtonSize - style.WindowPadding);
			if(ImGui.Button("Remove", removeButtonSize)) {
				removeIndex = i;
			}
			
			ImGui.EndChild();
			ImGui.PopID();
		}

		if(removeIndex >= 0) {
			entries.RemoveAt(removeIndex);
		}
	}

	private void Grid() {
		int scale = 4;
		foreach(var link in layer.Scene.Tilesets) {
			ImGui.PushID(link.Slot);
			string label = $"Slot [{link.Slot}]: {link.Tileset?.ID ?? "--"}";
			bool open = ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.DefaultOpen);
			var tileset = link.Tileset;
			if(open && tileset != null) {
				Texture texSource = tileset?.GetTexturePreview();
				Vector2 areaAvailable = ImGui.GetContentRegionAvail();
				Vector2 areaPos = ImGui.GetCursorScreenPos();
				Vector2 areaSize = new(areaAvailable.X, texSource?.Height * scale + 34 ?? areaAvailable.X);
				ImGui.BeginChild(
					"tile-select",
					areaSize,
					ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.Borders,
					ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.AlwaysHorizontalScrollbar
				);
				Vector2 origin = ImGui.GetCursorPos();
				Vector2 size = new Vector2(texSource.Width, texSource.Height) * scale;
				ImGui.Image(new IntPtr(texSource.Handle), size, new(0, 0), new(1, 1), new(1,1,1,1));

				int countX = tileset.GetTileCountX();
				int countY = tileset.GetTileCountY();
				for(int y = 0; y < countY; y++) {
					for(int x = 0; x < countX; x++) {
						int tileID = (y * countX + x) + 1;
						int entryIndex = -1;
						for(int i = 0; i < entries.Count; i++) {
							if(entries[i].TilesetSlot == link.Slot && entries[i].TileID == tileID) {
								entryIndex = i;
								break;
							}
						}
							
						ImGui.SetCursorPos(origin + new Vector2(layer.Scene.World.TileWidth * x, layer.Scene.World.TileHeight * y) * scale);
						ImGui.PushID(tileID);
						Vector2 c = ImGui.GetCursorScreenPos();
						Vector2 s = new Vector2(layer.Scene.World.TileWidth, layer.Scene.World.TileHeight) * scale;
						if(ImGui.InvisibleButton("##tile", s)) {
							if(entryIndex < 0) {
								entries.Add(new(link.Slot, tileID, 1.0F));
								entryIndex = entries.Count - 1;
							} else {
								entries.RemoveAt(entryIndex);
								entryIndex = -1;
							}
						}
							
						if(ImGui.IsItemHovered()) {
							ImGui.GetWindowDrawList().AddRectFilled(c, c + s, Utilities.GetPackedColor(200, 200, 200, 50));
						}
						if(entryIndex >= 0) {
							ImGui.GetWindowDrawList().AddRectFilled(c, c + s, Utilities.GetPackedColor(200, 200, 200, 50));
							ImGui.GetWindowDrawList().AddRect(c, c + s, Utilities.GetPackedColor(255, 255, 255, 255));
						}
						ImGui.PopID();
					}
				}
				
				ImGui.EndChild();
			}
			ImGui.PopID();
		}
	}

	private float GetTotalWeight() {
		float sum = emptyTileWeight;
		for(int i = 0; i < entries.Count; i++) {
			sum += entries[i].Weight;
		}
		return sum;
	}

	private int Sample() {
		float sum = GetTotalWeight();
		float random = Random.Shared.NextSingle() * sum;
		if(random < emptyTileWeight) return 0;
		random -= emptyTileWeight;
		for(int i = 0; i < entries.Count; i++) {
			if(random < entries[i].Weight) {
				return i + 1;
			}
			random -= entries[i].Weight;
		}
		return 0;
	}

	private void Fill() {
		Rectangle selection = Program.CanvasPanel.TileSelect.Selection;
		
		// TODO: update to TileEditOperation

		var operation = new TileFillOperation(layer.Tilemap, selection);
		
		for(int y = selection.Top; y < selection.Bottom; y++) {
			for(int x = selection.Left; x < selection.Right; x++) {
				int sx = x - layer.Scene.WorldX;
				int sy = y - layer.Scene.WorldY;
				if(sx >= 0 && sx < layer.Scene.TileCountX && sy >= 0 && sy < layer.Scene.TileCountY) {
					int sampledEntry = Sample();
					if(sampledEntry > 0) {
						operation.NextState[x - selection.Left, y - selection.Top] =
							new TileRef(entries[sampledEntry - 1].TileID, entries[sampledEntry - 1].TilesetSlot);
					} else {
						operation.NextState[x - selection.Left, y - selection.Top] =
							new TileRef(0, 0);
					}
				}
			}
		}
		
		var edit = Program.Project.BeginEdit(Program.CanvasPanel, layer.Scene, operation,
			redo: entry => {
				var data = entry.GetData<TileFillOperation>();
				var area = data.Area;
				for(int y = area.Top; y < area.Bottom; y++) {
					for(int x = area.Left; x < area.Right; x++) {
						int sx = x - data.Tilemap.Scene.WorldX;
						int sy = y - data.Tilemap.Scene.WorldY;
						if(sx >= 0 && sx < data.Tilemap.Scene.TileCountX && sy >= 0 && sy < data.Tilemap.Scene.TileCountY) {
							data.Tilemap.Set(sx, sy, data.NextState[x - area.Left, y - area.Top]);
						}
					}
				}
			},
			undo: entry => {
				var data = entry.GetData<TileFillOperation>();
				var area = data.Area;
				for(int y = area.Top; y < area.Bottom; y++) {
					for(int x = area.Left; x < area.Right; x++) {
						int sx = x - data.Tilemap.Scene.WorldX;
						int sy = y - data.Tilemap.Scene.WorldY;
						if(sx >= 0 && sx < data.Tilemap.Scene.TileCountX && sy >= 0 && sy < data.Tilemap.Scene.TileCountY) {
							data.Tilemap.Set(sx, sy, data.PrevState[x - area.Left, y - area.Top]);
						}
					}
				}
			}
		);
		
		Program.Project.EndEdit(ref edit);
	}

	public class TileFillOperation {
		public Tilemap Tilemap;
		public Rectangle Area;
		public TileRef[,] PrevState;
		public TileRef[,] NextState;
		public TileFillOperation(Tilemap tilemap, Rectangle area) {
			Tilemap = tilemap;
			Area = area;
			PrevState = new TileRef[area.Width,area.Height];
			NextState = new TileRef[area.Width,area.Height];
			for(int y = area.Top; y < area.Bottom; y++) {
				for(int x = area.Left; x < area.Right; x++) {
					int sx = x - tilemap.Scene.WorldX;
					int sy = y - tilemap.Scene.WorldY;
					if(sx >= 0 && sx < tilemap.Scene.TileCountX && sy >= 0 && sy < tilemap.Scene.TileCountY) {
						PrevState[x - area.Left, y - area.Top] = tilemap.Get(sx, sy);
						NextState[x - area.Left, y - area.Top] = tilemap.Get(sx, sy);
					}
				}
			}
		}
	}
	
}

public class TileFillEntry {
	public float Weight;
	public int TilesetSlot;
	public int TileID;
	public TileFillEntry(int tileset, int tile, float weight) {
		Weight = weight;
		TilesetSlot = tileset;
		TileID = tile;
	}
}