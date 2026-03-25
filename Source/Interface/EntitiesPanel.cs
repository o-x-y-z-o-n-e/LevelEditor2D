using System.Drawing;
using System.Numerics;
using IconFonts;
using ImGuiNET;
using Serilog;

namespace L2D; 

public class EntitiesPanel : Panel {

	private FileEditEntry nameEdit;
	private FileEditEntry typeEdit;
	private FileEditEntry positionEdit;
	private FileEditEntry sizeEdit;

	public EntitiesPanel() {
		Title = $"{Codicons.SymbolMisc} Entities";
		positionEdit = null;
		sizeEdit = null;
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

		var style = ImGui.GetStyle();
		
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

			bool canvasHighlighted = Program.CanvasPanel.IsOpen && entity == Program.CanvasPanel.EntityHighlight;
			
			if(canvasHighlighted) ImGui.PushStyleColor(ImGuiCol.Header, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderHovered]);
			if(ImGui.Selectable(entity.Name, entity == selected || canvasHighlighted, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap)) {
				if(entity != selected) {
					Program.SetSelectedEntity(entity);
				} else {
					Program.SetSelectedEntity(null);
				}
			}
			
			ImGui.PushStyleColor(ImGuiCol.Text, Utilities.GetPackedColor(255, 255, 255, 80));
			float x = ImGui.CalcTextSize(entity.Name).X + ImGui.GetStyle().FramePadding.X + 10;
			
			ImGui.SameLine();
			x = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.GetStyle().FramePadding.X;
			
			x -= ImGui.CalcTextSize(")").X;
			ImGui.SetCursorPosX(x);
			ImGui.Text(")");
			ImGui.SameLine();
			
			string type = entity.Template != null && entity.Type == "" ? entity.Template.Type : entity.Type;
			x -= ImGui.CalcTextSize(type).X;
			ImGui.SetCursorPosX(x);
			ImGui.Text(type);
			ImGui.SameLine();
			
			x -= ImGui.CalcTextSize("(").X;
			ImGui.SetCursorPosX(x);
			ImGui.Text("(");
			ImGui.PopStyleColor();
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
			Entity entity = new Entity(layer.Entities);
			entity.SetName("New Entity");
			entity.SetPosition(
				-Program.CanvasPanel.Camera
				-new Vector2(layer.Scene.WorldX * layer.Scene.World.TileWidth, layer.Scene.WorldY * layer.Scene.World.TileHeight)
			);
			Program.File.ApplyEdit(this, new Entity.AddOperation(layer.Entities, entity));
			Program.SetSelectedEntity(entity);
		}
		ImGui.SetItemTooltip("Create");
		
		ImGui.SameLine();
		if(ImGui.Button(Codicons.Extensions)) {
			moveDownIndex = layer.Entities.IndexOf(Program.SelectedEntity);
		}
		ImGui.SetItemTooltip("Templates");
		
		ImGui.SameLine();
		ImGui.BeginDisabled(Program.SelectedEntity == null);
		
		if(ImGui.Button(Codicons.Copy)) {
			copyIndex = layer.Entities.IndexOf(Program.SelectedEntity);
		}
		ImGui.SetItemTooltip("Copy");

		if(copyIndex >= 0 && copyIndex < layer.Entities.Count) {
			Entity newEntity = layer.Entities.Copy(copyIndex);
			newEntity.SetPosition(newEntity.Position + new Vector2(newEntity.Size.X + 16, 0));
			Program.File.ApplyEdit(this, new Entity.AddOperation(layer.Entities, newEntity));
			Program.SetSelectedEntity(newEntity);
		}
		
		ImGui.SameLine();
		
		if(ImGui.Button(Codicons.Trash)) {
			deleteIndex = layer.Entities.IndexOf(Program.SelectedEntity);
		}
		ImGui.SetItemTooltip("Delete");

		if(deleteIndex >= 0 && deleteIndex < layer.Entities.Count) {
			Program.File.ApplyEdit(this, new Entity.RemoveOperation(layer.Entities, layer.Entities.Get(deleteIndex)));
			if(layer.Entities.Count == 0) {
				Program.SetSelectedEntity(null);
			} else if(deleteIndex >= layer.Entities.Count) {
				Program.SetSelectedEntity(layer.Entities.Get(layer.Entities.Count - 1));
			} else {
				Program.SetSelectedEntity(layer.Entities.Get(deleteIndex));
			}
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
		ImGui.SetItemTooltip("Move Up");
		ImGui.EndDisabled();
		ImGui.BeginDisabled(selectedEntityIndex >= layer.Entities.Count - 1);
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronDown)) {
			moveDownIndex = layer.Entities.IndexOf(Program.SelectedEntity);
		}
		ImGui.SetItemTooltip("Move Down");
		ImGui.EndDisabled();

		if(moveUpIndex >= 1 && moveUpIndex < layer.Entities.Count) {
			Program.File.ApplyEdit(this, new Entity.MoveOperation(layer.Entities, moveUpIndex, moveUpIndex - 1));
		}
		
		if(moveDownIndex >= 0 && moveDownIndex < layer.Entities.Count - 1) {
			Program.File.ApplyEdit(this, new Entity.MoveOperation(layer.Entities, moveDownIndex, moveDownIndex + 1));
		}
		
		ImGui.SameLine();
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(Codicons.OpenInProduct).X - 12);
		if(ImGui.Button(Codicons.OpenInProduct)) {
			Program.CanvasPanel.LocateEntity(Program.SelectedEntity);
			Program.Focus(Program.CanvasPanel);
		}
		ImGui.SetItemTooltip("Locate");
		
		ImGui.EndDisabled();

		if(selected != null) {
			Entity template = selected.Template;
			
			ImGui.SeparatorText("Entity Options");

			{	// Entity Template
				Vector2 cur = ImGui.GetCursorPos();
				Vector2 scur = ImGui.GetCursorScreenPos();
				float w = ImGui.CalcItemWidth();
				float h = ImGui.GetTextLineHeight() + style.FramePadding.Y * 2;
				Vector4 v4 = style.Colors[(int)ImGuiCol.FrameBg];
				if(template == null) v4.W *= style.DisabledAlpha;
				ImGui.GetWindowDrawList().AddRectFilled(scur, scur + new Vector2(w, h), ImGui.ColorConvertFloat4ToU32(v4), style.FrameRounding);
				ImGui.SetCursorPos(new(cur.X + style.FramePadding.X, cur.Y + style.FramePadding.Y));
				if(template != null) {
					if(ImGui.TextLink(template.Name)) {
						Program.TemplatesPanel.SelectedTemplate = template;
						Program.Focus(Program.TemplatesPanel);
					}
				}
				ImGui.BeginDisabled(template == null);
				ImGui.SetCursorPos(cur + new Vector2(w + style.ItemInnerSpacing.X, style.FramePadding.Y));
				ImGui.Text("Template");
				ImGui.EndDisabled();
				ImGui.SetCursorPos(cur + new Vector2(0, h + style.ItemSpacing.Y));
			}

			{	// Entity Name
				bool fallbackOnTemplate = template != null && !selected.HasOwnName;
				if(fallbackOnTemplate) {
					ImGui.PushStyleVar(ImGuiStyleVar.Alpha, style.DisabledAlpha);
				}
				string name = selected.Name;
				if(ImGui.InputText("Name", ref name, 512, ImGuiInputTextFlags.AutoSelectAll)) {
					if(name == "") name = null;
					if(nameEdit == null || nameEdit.GetData<Entity.NameOperation>().Entity != selected) {
						nameEdit = Program.File.BeginEdit(layer, new Entity.NameOperation(selected, name));
					} else {
						nameEdit.GetData<Entity.NameOperation>().SetName(name);
					}
				}
				if(ImGui.IsItemDeactivatedAfterEdit() && nameEdit != null) {
					Program.File.EndEdit(ref nameEdit, !nameEdit.GetData<Entity.NameOperation>().HasChanges());
				}
				if(fallbackOnTemplate) {
					ImGui.PopStyleVar(); // ImGuiStyleVar.Alpha
				} else if(template != null) {
					ImGui.OpenPopupOnItemClick("reset-name", ImGuiPopupFlags.MouseButtonRight);
					if(ImGui.BeginPopup("reset-name")) {
						if(ImGui.MenuItem("Reset")) {
							Program.File.ApplyEdit(layer, new Entity.NameOperation(selected, null));
						}
						ImGui.EndPopup();
					}
				}
			}

			{	// Entity Type
				bool fallbackOnTemplate = template != null && !selected.HasOwnType;
				if(fallbackOnTemplate) {
					ImGui.PushStyleVar(ImGuiStyleVar.Alpha, style.DisabledAlpha);
				}
				string type = selected.Type;
				if(ImGui.InputText("Type", ref type, 512, ImGuiInputTextFlags.AutoSelectAll)) {
					if(type == "") type = null;
					if(typeEdit == null || typeEdit.GetData<Entity.TypeOperation>().Entity != selected) {
						typeEdit = Program.File.BeginEdit(layer, new Entity.TypeOperation(selected, type));
					} else {
						typeEdit.GetData<Entity.TypeOperation>().SetType(type);
					}
				}
				if(ImGui.IsItemDeactivatedAfterEdit() && typeEdit != null) {
					Program.File.EndEdit(ref typeEdit, !typeEdit.GetData<Entity.TypeOperation>().HasChanges());
				}
				if(fallbackOnTemplate) {
					ImGui.PopStyleVar(); // ImGuiStyleVar.Alpha
				} else if(template != null) {
					ImGui.OpenPopupOnItemClick("reset-type", ImGuiPopupFlags.MouseButtonRight);
					if(ImGui.BeginPopup("reset-type")) {
						if(ImGui.MenuItem("Reset")) {
							Program.File.ApplyEdit(layer, new Entity.TypeOperation(selected, null));
						}
						ImGui.EndPopup();
					}
				}
			}

			{	// Entity Position
				Vector2 pos = selected.Position;
				if(ImGui.DragFloat2("Position", ref pos)) {
					if(positionEdit == null || positionEdit.GetData<Entity.PositionOperation>().Entity != selected) {
						positionEdit = Program.File.BeginEdit(layer, new Entity.PositionOperation(selected, pos));
					} else {
						positionEdit.GetData<Entity.PositionOperation>().SetPosition(pos);
					}
				}
				if(ImGui.IsItemDeactivatedAfterEdit() && positionEdit != null) {
					Program.File.EndEdit(ref positionEdit,
						!positionEdit.GetData<Entity.PositionOperation>().HasChanges());
				}
			}

			{	// Entity Size
				bool fallbackOnTemplate = template != null && !selected.HasOwnSize;
				if(fallbackOnTemplate) {
					ImGui.PushStyleVar(ImGuiStyleVar.Alpha, style.DisabledAlpha);
				}
				Vector2 size = selected.Size;
				if(ImGui.DragFloat2("Size", ref size)) {
					if(size.X < 0) size.X = 0;
					if(size.Y < 0) size.Y = 0;
					if(sizeEdit == null || sizeEdit.GetData<Entity.SizeOperation>().Entity != selected) {
						sizeEdit = Program.File.BeginEdit(layer, new Entity.SizeOperation(selected, size));
					} else {
						sizeEdit.GetData<Entity.SizeOperation>().SetSize(size);
					}
				}
				if(ImGui.IsItemDeactivatedAfterEdit() && sizeEdit != null) {
					Program.File.EndEdit(ref sizeEdit, !sizeEdit.GetData<Entity.SizeOperation>().HasChanges());
				}
				if(fallbackOnTemplate) {
					ImGui.PopStyleVar(); // ImGuiStyleVar.Alpha
				} else if(template != null) {
					ImGui.OpenPopupOnItemClick("reset-size", ImGuiPopupFlags.MouseButtonRight);
					if(ImGui.BeginPopup("reset-size")) {
						if(ImGui.MenuItem("Reset")) {
							Program.File.ApplyEdit(layer, new Entity.SizeOperation(selected, null));
						}
						ImGui.EndPopup();
					}
				}
			}
			
			PropertyView.Run(selected.Properties, template?.Properties);
		} else {
			ImGui.Text("No entity selected...");
		}
	}
	
}