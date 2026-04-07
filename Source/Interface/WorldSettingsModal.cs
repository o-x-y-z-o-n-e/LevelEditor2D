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
			tilesetsDirectory = world.TilesetsDirectory;
		}
		ImGui.SetNextWindowPos(ImGui.GetIO().DisplaySize / 2.0F, ImGuiCond.Always, new Vector2(0.5F, 0.5F));
		bool o = true;
		if(ImGui.BeginPopupModal("World Settings", ref o, ImGuiWindowFlags.AlwaysAutoResize)) {
			bool changes = false;
			bool clearUndoRedo = false;
			
			ImGui.InputText("World Name", ref worldName, Program.IMGUI_STRING_MAX);
			
			ImGui.BeginDisabled();
			ImGui.InputInt2("Tile Size", ref tileSize.X);
			ImGui.EndDisabled();
			
			ImGui.InputText("Scenes Directory", ref scenesDirectory, Program.IMGUI_STRING_MAX);
			ImGui.SetItemTooltip("Default location where external scene files are saved");

			ImGui.BeginDisabled();
			ImGui.InputText("Tilesets Directory", ref tilesetsDirectory, Program.IMGUI_STRING_MAX);
			ImGui.EndDisabled();
			ImGui.SetItemTooltip("Default location where external tileset files are saved");

			changes |= worldName != world.Name;
			changes |= tileSize.X != world.TileWidth;
			changes |= tileSize.Y != world.TileHeight;
			changes |= scenesDirectory != world.ScenesDirectory;
			changes |= tilesetsDirectory != world.TilesetsDirectory;
			
			clearUndoRedo |= tileSize.X != world.TileWidth;
			clearUndoRedo |= tileSize.Y != world.TileHeight;
			
			ImGui.Spacing();
			ImGui.BeginDisabled(!changes);
			if(ImGui.Button("Apply")) {
				ApplyChanges();
				ImGui.CloseCurrentPopup();
			}
			if(clearUndoRedo) {
				ImGui.SetItemTooltip("Warning! Applying changes will clear project undo/redo edit history");
			}
			ImGui.EndDisabled();
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
			foreach(var scene in world.Scenes) {
				if(!scene.IsEmbedded) {
					world.Project.DeleteFileOnSave(scene.FileAbsolutePath);
					scene.UpdateFilePath();
				}
			}
			world.Project.MarkDirty();
		}
		
		if(tilesetsDirectory != world.TilesetsDirectory) {
			world.TilesetsDirectory = tilesetsDirectory;
			foreach(var tileset in world.Tilesets) {
				if(!tileset.IsEmbedded) {
					// TODO
					// world.Project.DeleteFileOnSave(tileset.FileAbsolutePath);
					// tileset.UpdateFileWatcher();
				}
			}
			world.Project.MarkDirty();
		}
	}
	
}