using System.Numerics;
using IconFonts;
using ImGuiNET;

namespace L2D; 

public class EntitiesPanel : Panel {

	public EntitiesPanel() {
		Title = "Entities";
	}

	protected override void Update() {
		if(Program.File == null) {
			return;
		}
		
		if(Program.SelectedScene == null) {
			ImGui.Text("No scene selected...");
			return;
		}
		
		if(Program.SelectedLayer == null) {
			ImGui.Text("No layer selected...");
			return;
		}

		if(Program.SelectedLayer.Type != LayerType.Entities) {
			ImGui.Text("Selected layer is not used for entities...");
			return;
		}

		Layer layer = Program.SelectedLayer;
		Entity selected = Program.SelectedEntity;
		
		Vector2 listSize = ImGui.GetContentRegionAvail();
		listSize.Y -= 200;
		ImGui.BeginChild("entity-list", listSize, ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY);

		int copyIndex = -1;
		int deleteIndex = -1;
		int moveUpIndex = -1;
		int moveDownIndex = -1;

		int index = 0;
		foreach(var entity in layer.Entities.All) {
			ImGui.PushID(index);

			bool canvasHighlighted = entity == Program.CanvasPanel.EntityHighlight;
			
			if(canvasHighlighted) ImGui.PushStyleColor(ImGuiCol.Header, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderHovered]);
			if(ImGui.Selectable(entity.Name, entity == selected || canvasHighlighted)) {
				if(entity != selected) {
					Program.SetSelectedEntity(entity);
				} else {
					Program.SetSelectedEntity(null);
				}
			}
			if(canvasHighlighted) ImGui.PopStyleColor();
			
			if(ImGui.IsItemHovered()) {
				Program.CanvasPanel.ShowEntityHighlight(entity);
			}
			ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags.MouseButtonRight);
			if(ImGui.BeginPopup("context")) {
				if(ImGui.MenuItem("Copy")) {
					copyIndex = index;
				}
				if(ImGui.MenuItem("Delete")) {
					deleteIndex = index;
				}
				if(ImGui.MenuItem("Move Up")) {
					moveUpIndex = index;
				}
				if(ImGui.MenuItem("Move Down")) {
					moveDownIndex = index;
				}
				if(ImGui.MenuItem("Locate")) {
					Program.CanvasPanel.LocateEntity(entity);
				}
				ImGui.EndPopup();
			}
			ImGui.PopID();
			index++;
		}
		
		ImGui.EndChild();
		
		if(ImGui.Button(Codicons.DiffAdded)) {
			Entity entity = layer.Entities.Add();
			entity.Name = "New Entity";
			entity.Position = -Program.CanvasPanel.Camera - new Vector2(layer.Scene.WorldX * layer.Scene.World.TileWidth, layer.Scene.WorldY * layer.Scene.World.TileHeight);
			Program.SetSelectedEntity(entity);
			Program.File.MarkDirty();
		}
		
		ImGui.SameLine();
		ImGui.BeginDisabled(Program.SelectedEntity == null);
		
		if(ImGui.Button(Codicons.Copy)) {
			copyIndex = layer.Entities.IndexOf(Program.SelectedEntity);
		}

		if(copyIndex >= 0 && copyIndex < layer.Entities.Count) {
			Entity newEntity = layer.Entities.Copy(copyIndex);
			newEntity.Position += new Vector2(newEntity.Size.X + 16, 0);
			Program.File.MarkDirty();
		}
		
		ImGui.SameLine();
		
		if(ImGui.Button(Codicons.Trash)) {
			deleteIndex = layer.Entities.IndexOf(Program.SelectedEntity);
		}

		if(deleteIndex >= 0 && deleteIndex < layer.Entities.Count) {
			layer.Entities.Remove(deleteIndex);
			if(layer.Entities.Count == 0) {
				Program.SetSelectedEntity(null);
			} else if(deleteIndex >= layer.Entities.Count) {
				Program.SetSelectedEntity(layer.Entities.Get(layer.Entities.Count - 1));
			} else {
				Program.SetSelectedEntity(layer.Entities.Get(deleteIndex));
			}
			Program.File.MarkDirty();
		}
		
		ImGui.SameLine();
		
		int selectedEntityIndex = -1;
		if(Program.SelectedEntity != null) {
			selectedEntityIndex = layer.Entities.IndexOf(Program.SelectedEntity);
		}
		ImGui.BeginDisabled(selectedEntityIndex <= 0);
		if(ImGui.Button(Codicons.ChevronUp)) {
			moveUpIndex = layer.Entities.IndexOf(Program.SelectedEntity);
		}
		ImGui.EndDisabled();
		ImGui.BeginDisabled(selectedEntityIndex >= layer.Entities.Count - 1);
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronDown)) {
			moveDownIndex = layer.Entities.IndexOf(Program.SelectedEntity);
		}
		ImGui.EndDisabled();

		if(moveUpIndex >= 1 && moveUpIndex < layer.Entities.Count) {
			layer.Entities.Move(moveUpIndex, moveUpIndex - 1);
			Program.File.MarkDirty();
		}
		
		if(moveDownIndex >= 0 && moveDownIndex < layer.Entities.Count - 1) {
			layer.Entities.Move(moveDownIndex, moveDownIndex + 1);
			Program.File.MarkDirty();
		}
		
		ImGui.SameLine();
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(Codicons.OpenInProduct).X - 12);
		if(ImGui.Button(Codicons.OpenInProduct)) {
			Program.CanvasPanel.LocateEntity(Program.SelectedEntity);
		}
		
		ImGui.EndDisabled();

		if(selected != null) {
			ImGui.SeparatorText("Entity Options");
			if(ImGui.InputText("Name", ref selected.Name, 512)) {
				Program.File.MarkDirty();    
            }
			if(ImGui.InputText("Type", ref selected.Type, 512)) {
				Program.File.MarkDirty();    
            }
			if(ImGui.DragFloat2("Position", ref selected.Position)) {
				Program.File.MarkDirty();
			}
			if(ImGui.DragFloat2("Size", ref selected.Size)) {
				if(selected.Size.X < 0) selected.Size.X = 0;
				if(selected.Size.Y < 0) selected.Size.Y = 0;
				Program.File.MarkDirty();
			}
			PropertyView.Run(selected.Properties);
		} else {
			ImGui.Text("No entity selected...");
		}
	}
}