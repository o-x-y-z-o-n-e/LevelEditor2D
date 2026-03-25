using System.Numerics;
using IconFonts;
using ImGuiNET;

namespace L2D;

public class TemplatesPanel : Panel {

	public Entity SelectedTemplate {
		get => selectedTemplate;
		set => selectedTemplate = value;
	}
	
	private Entity selectedTemplate;
	private int deleteIndex;
	private int copyIndex;
	private string newIdBuffer;

	private FileEditEntry sizeEdit;
	
	public TemplatesPanel() {
		Title = $"{Codicons.Extensions} Templates";
		deleteIndex = -1;
		copyIndex = -1;
		newIdBuffer = "";
	}

	protected override void Update() {
		if(Program.File == null) {
			return;
		}

		World world = Program.File.World;

		ImGui.Columns(2);
		ListView();
		ImGui.NextColumn();
		EditView();
	}
	
	private void ListView() {
		World world = Program.File.World;

		var style = ImGui.GetStyle();

		int selectedIndex = -1;
		int moveUpIndex = -1;
		int moveDownIndex = -1;
		
		if(ImGui.Button(Codicons.DiffAdded)) {
			newIdBuffer = "new entity";
			ImGui.OpenPopup("add-template");
		}
		ImGui.SetItemTooltip("Create");
		
		ImGui.BeginDisabled(selectedTemplate == null);
		
		ImGui.SameLine();
		
		if(ImGui.Button(Codicons.Copy)) {
			newIdBuffer = "new entity";
			copyIndex = selectedIndex;
		}
		ImGui.SetItemTooltip("Copy");
		
		ImGui.SameLine();
		
		if(ImGui.Button(Codicons.Trash)) {
			deleteIndex = selectedIndex;
		}
		ImGui.SetItemTooltip("Delete");

		ImGui.SameLine();

		ImGui.BeginDisabled(selectedIndex == 0);
		if(ImGui.Button(Codicons.ChevronUp)) {
			moveUpIndex = selectedIndex;
		}
		ImGui.SetItemTooltip("Move Up");
		ImGui.EndDisabled();

		ImGui.SameLine();
		
		ImGui.BeginDisabled(selectedIndex == world.Templates.Count - 1);
		if(ImGui.Button(Codicons.ChevronDown)) {
			moveDownIndex = selectedIndex;
		}
		ImGui.SetItemTooltip("Move Down");
		ImGui.EndDisabled();
		
		ImGui.EndDisabled(); // selectedTemplate == null
		
		ImGui.SameLine();
		float gridButtonX = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Grid").X - style.FramePadding.X * 2;
		ImGui.SetCursorPosX(gridButtonX);
		if(ImGui.Button("Grid")) {
			
		}
		ImGui.SameLine();
		ImGui.SetCursorPosX(gridButtonX - ImGui.CalcTextSize("Grid").X - style.FramePadding.X * 2 - style.ItemSpacing.X);
		if(ImGui.Button("List")) {
			
		}
		
		ImGui.BeginChild("list", ImGui.GetContentRegionAvail(), ImGuiChildFlags.Borders);

		for(int i = 0; i < world.Templates.Count; i++) {
			ImGui.PushID(i);
			Entity template = world.Templates.Get(i);

			bool selected = template == selectedTemplate;
			if(ImGui.Selectable(template.Name, selected)) {
				if(selectedTemplate != template) {
					selectedTemplate = template;
				} else {
					selectedTemplate = null;
				}
				selected = true;
			}
			if(selected) {
				selectedIndex = i;
			}
			if(ImGui.BeginPopup("context")) {
				if(ImGui.MenuItem("Copy")) {
					newIdBuffer = "new entity";
					copyIndex = i;
				}
				if(ImGui.MenuItem("Delete")) {
					deleteIndex = i;
				}
				if(ImGui.MenuItem("Move Up")) {
					moveUpIndex = i;
				}
				if(ImGui.MenuItem("Move Down")) {
					moveDownIndex = i;
				}
				ImGui.EndPopup();
			}
			ImGui.PopID();
		}
		
		ImGui.EndChild();
		
		if(ImGui.BeginPopup("add-template")) {
			ImGui.Text("Create new template");
			ImGui.InputText("ID", ref newIdBuffer, 512);
			bool validId = newIdBuffer != "";
			foreach(var s in world.Templates.All) {
				if(s.Name == newIdBuffer) {
					validId = false;
					break;
				}
			}
			if(!validId) {
				ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
				ImGui.Text("Invalid or duplicate id!");
				ImGui.PopStyleColor();
			}
			ImGui.BeginDisabled(!validId);
			if(ImGui.Button("Ok")) {
				Entity template = new Entity(world.Templates);
				template.SetName(newIdBuffer);
				Program.File.ApplyEdit(this, new Entity.AddOperation(world.Templates, template));
				selectedTemplate = template;
				selectedIndex = world.Templates.Count - 1;
				ImGui.CloseCurrentPopup();
				newIdBuffer = "";
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
				newIdBuffer = "";
			}
			ImGui.EndPopup();
		}
		
		
		if(copyIndex >= 0) {
			ImGui.OpenPopup("copy-template");
		}
		if(ImGui.BeginPopup("copy-template")) {
			ImGui.Text("Copy template?");
			ImGui.InputText("ID", ref newIdBuffer, 512);
			bool validId = newIdBuffer != "";
			foreach(var s in world.Templates.All) {
				if(s.Name == newIdBuffer) {
					validId = false;
					break;
				}
			}
			if(!validId) {
				ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
				ImGui.Text("Invalid or duplicate id!");
				ImGui.PopStyleColor();
			}
			ImGui.BeginDisabled(!validId);
			if(ImGui.Button("Ok")) {
				Entity newEntity = world.Templates.Copy(copyIndex);
				newEntity.SetName(newIdBuffer);
				Program.File.ApplyEdit(this, new Entity.AddOperation(world.Templates, newEntity));
				Program.SetSelectedEntity(newEntity);
				copyIndex = -1;
				newIdBuffer = "";
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			ImGui.Dummy(new Vector2(80, 0));
			ImGui.SameLine();
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Cancel").X - ImGui.GetStyle().FramePadding.X * 2);
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
				copyIndex = -1;
				newIdBuffer = "";
			}
			ImGui.EndPopup();
		}
		
		if(deleteIndex >= 0) {
			ImGui.OpenPopup("delete-template");
		}
		if(ImGui.BeginPopup("delete-template")) {
			ImGui.Text("Delete template?");
			if(ImGui.Button("Ok")) {
				if(deleteIndex == selectedIndex) {
					selectedTemplate = null;
				}
				Program.File.ApplyEdit(this, new Entity.RemoveOperation(world.Templates, world.Templates.Get(deleteIndex)));
				ImGui.CloseCurrentPopup();
				deleteIndex = -1;
			}
			ImGui.SameLine();
			ImGui.Dummy(new Vector2(80, 0));
			ImGui.SameLine();
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Cancel").X - ImGui.GetStyle().FramePadding.X * 2);
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
				deleteIndex = -1;
			}
			ImGui.EndPopup();
		}
		
		if(moveUpIndex >= 0) {
			Program.File.ApplyEdit(this, new Entity.MoveOperation(world.Templates, moveUpIndex, moveUpIndex-1));
		}
		
		if(moveDownIndex >= 0) {
			Program.File.ApplyEdit(this, new Entity.MoveOperation(world.Templates, moveDownIndex, moveDownIndex+1));
		}
	}
	
	private void EditView() {
		ImGui.SetCursorPosY(32);
		
		ImGui.BeginChild("preview", ImGui.GetContentRegionAvail(), ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY);

		Vector2 previewArea = ImGui.GetContentRegionAvail();
		Vector2 previewCenter = ImGui.GetWindowPos() + ImGui.GetStyle().WindowPadding + previewArea / 2.0F;

		if(selectedTemplate != null) {
			var drawList = ImGui.GetWindowDrawList();
			uint fillColor = Utilities.GetPackedColor(200, 200, 200, 30);
			uint borderColor = Utilities.GetPackedColor(200, 200, 200, 180);

			float scale = float.Min(
				(previewArea.X / 2.0F) / float.Max(selectedTemplate.Size.X, 1.0F),
				(previewArea.Y / 2.0F) / float.Max(selectedTemplate.Size.Y, 1.0F)
			);
			
			Vector2 e0 = previewCenter - (selectedTemplate.Size / 2.0F) * scale;
			Vector2 e1 = previewCenter + (selectedTemplate.Size / 2.0F) * scale;
			if(selectedTemplate.IsPoint) {
				float size = Entity.POINT_HANDLE_SIZE;
				drawList.AddCircleFilled(e0, size, fillColor);
				drawList.AddCircle(e0, size, borderColor);
				drawList.AddLine(e0 + new Vector2(-size / 3, 0), e0 + new Vector2(size / 3, 0), borderColor);
				drawList.AddLine(e0 + new Vector2(0, -size / 3), e0 + new Vector2(0, size / 3), borderColor);
			} else {
				drawList.AddRectFilled(e0, e1, fillColor);
				drawList.AddRect(e0, e1, borderColor);
			}
			Vector2 textSize = ImGui.CalcTextSize(selectedTemplate.Name);
			Vector2 textPos = new Vector2((e0.X + e1.X) / 2.0F, e0.Y) - (textSize / 2.0F) - new Vector2(0, 16);
			if(textSize.X > 0 && textPos.Y > 0) {
				if(selectedTemplate.IsPoint) textPos.Y -= Entity.POINT_HANDLE_SIZE;
				else if(selectedTemplate == Program.SelectedEntity) textPos.Y -= 14;
				drawList.AddRectFilled(textPos - new Vector2(2, -1), textPos + textSize + new Vector2(8, 4), Utilities.GetPackedColor(10, 10, 10, 64), 4.0F);
				drawList.AddRectFilled(textPos - new Vector2(4, 1), textPos + textSize + new Vector2(6, 2), Utilities.GetPackedColor(180, 180, 180, 192), 4.0F);
				drawList.AddText(textPos + new Vector2(1), Utilities.GetPackedColor(10, 10, 10, 128), selectedTemplate.Name);
				drawList.AddText(textPos, Utilities.GetPackedColor(255, 255, 255, 255), selectedTemplate.Name);
			}
		}

		ImGui.EndChild();
		
		ImGui.BeginChild("edit", ImGui.GetContentRegionAvail(), ImGuiChildFlags.Borders);
		if(selectedTemplate != null) {
			ImGui.SeparatorText("Entity Options");
			string name = selectedTemplate.Name;
			if(ImGui.InputText("Name", ref name, 512, ImGuiInputTextFlags.EnterReturnsTrue)) {
				Program.File.ApplyEdit(this, new Entity.NameOperation(selectedTemplate, name));
			}
			string type = selectedTemplate.Type;
			if(ImGui.InputText("Type", ref type, 512, ImGuiInputTextFlags.EnterReturnsTrue)) {
				Program.File.ApplyEdit(this, new Entity.TypeOperation(selectedTemplate, type));
			}
			Vector2 size = selectedTemplate.Size;
			if(ImGui.DragFloat2("Size", ref size)) {
				if(size.X < 0) size.X = 0;
				if(size.Y < 0) size.Y = 0;
				if(sizeEdit == null || sizeEdit.GetData<Entity.SizeOperation>().Entity != selectedTemplate) {
					sizeEdit = Program.File.BeginEdit(this, new Entity.SizeOperation(selectedTemplate, size));
				} else {
					sizeEdit.GetData<Entity.SizeOperation>().SetSize(size);
				}
			}
			if(ImGui.IsItemDeactivatedAfterEdit() && sizeEdit != null) {
				Program.File.EndEdit(ref sizeEdit, !sizeEdit.GetData<Entity.SizeOperation>().HasChanges());
			}
			PropertyView.Run(selectedTemplate.Properties);
		}
		ImGui.EndChild();
	}

	public void SelectModal(Action<Entity> onSelect) {
		Entity result = null;
		bool open = true;
		ImGui.SetNextWindowSizeConstraints(new Vector2(400, 300), ImGui.GetIO().DisplaySize);
		ImGui.SetNextWindowPos(ImGui.GetIO().DisplaySize / 2.0F, ImGuiCond.Always, new Vector2(0.5F, 0.5F));
		
		if(ImGui.BeginPopupModal("Select Template", ref open)) {
			Vector2 area = ImGui.GetContentRegionAvail();
			var style = ImGui.GetStyle();
			
			// TODO
			/*
			float searchWidth = 300 + ImGui.CalcTextSize("Search").X + style.FramePadding.X * 2.0F;
			float buttonWidth1 = ImGui.CalcTextSize("List").X + style.FramePadding.X * 2.0F;
			float buttonWidth2 = ImGui.CalcTextSize("Grid").X + style.FramePadding.X * 2.0F;
			float widthNeeded = buttonWidth1 + style.ItemSpacing.X + buttonWidth2 + searchWidth + style.FramePadding.X;
		
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
			ImGui.SameLine();
			ImGui.SetNextItemWidth(300);
			if(ImGui.InputText("Search", ref search, 512)) {
				MatchSearch();
			}
			if(ImGui.BeginPopupContextItem()) {
				if(ImGui.MenuItem("Clear")) {
					search = "";
					ImGui.CloseCurrentPopup();
				}
				ImGui.EndPopup();
			}
            
			result = Items(null);
			*/
			
			if(result != null) {
				open = false;
				ImGui.CloseCurrentPopup();
			}
			
			ImGui.EndPopup();
		}
		if(!open) {
			onSelect?.Invoke(result);
		}
	}

}