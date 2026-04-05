using System.Numerics;
using ImGuiNET;
using Silk.NET.Input;

namespace E2D;

public class WorldSettingsModal {
	
	private bool open;
	private string worldName;
	private Vector2Int tileSize;
	private string scenesDirectory;
	private string tilesetsDirectory;

	public WorldSettingsModal() {
		open = false;
	}

	public void Open() {
		open = true;
	}

	public void Body() {
		World world = Program.Project?.World;
		
		if(open && world != null) {
			ImGui.OpenPopup("World Settings");
			open = false;
			worldName = world.Name;
			tileSize = new(world.TileWidth, world.TileHeight);
			scenesDirectory = world.ScenesDirectory;
			tilesetsDirectory = ""; // TODO
		}
		ImGui.SetNextWindowPos(ImGui.GetIO().DisplaySize / 2.0F, ImGuiCond.Always, new Vector2(0.5F, 0.5F));
		bool o = true;
		if(ImGui.BeginPopupModal("World Settings", ref o, ImGuiWindowFlags.AlwaysAutoResize)) {
			ImGui.InputText("Name", ref worldName, Program.IMGUI_STRING_MAX);
			
			ImGui.BeginDisabled();
			ImGui.InputInt2("Tile Size", ref tileSize.X);
			ImGui.EndDisabled();
			
			ImGui.InputText("Scenes Directory", ref scenesDirectory, Program.IMGUI_STRING_MAX);

			ImGui.BeginDisabled();
			ImGui.InputText("Tilesets Directory", ref tilesetsDirectory, Program.IMGUI_STRING_MAX);
			ImGui.EndDisabled();
			
			ImGui.Spacing();
			if(ImGui.Button("Apply")) {
				ApplyChanges();
				ImGui.CloseCurrentPopup();
			}
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			
			ImGui.EndPopup();
		}
	}

	private void ApplyChanges() {
		World world = Program.Project.World;
		
		if(worldName != world.Name) {
			world.Name = worldName;
		}

		if(tileSize.X != world.TileWidth || tileSize.Y != world.TileHeight) {
			// TODO: resize everything
		}

		if(scenesDirectory != world.ScenesDirectory) {
			world.ScenesDirectory = scenesDirectory;
			// TODO: update scene files
		}
		
		// TODO: tilesets
	}
	
}