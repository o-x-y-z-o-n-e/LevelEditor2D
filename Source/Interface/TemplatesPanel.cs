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
	private bool gridView;

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
		
		if(selectedTemplate != null) {
			selectedIndex = world.Templates.IndexOf(selectedTemplate);
		}
		
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
		ImGui.BeginDisabled(gridView);
		if(ImGui.Button("Grid")) {
			gridView = true;
		}
		ImGui.EndDisabled();
		ImGui.SameLine();
		ImGui.SetCursorPosX(gridButtonX - ImGui.CalcTextSize("Grid").X - style.FramePadding.X * 2 - style.ItemSpacing.X);
		ImGui.BeginDisabled(!gridView);
		if(ImGui.Button("List")) {
			gridView = false;
		}
		ImGui.EndDisabled();

		selectedTemplate = Items(selectedTemplate, index => {
			ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags.MouseButtonRight);
			if(ImGui.BeginPopup("context")) {
				if(ImGui.MenuItem("Copy")) {
					newIdBuffer = "new entity";
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
				ImGui.EndPopup();
			}
		});

		if(selectedTemplate != null) {
			selectedIndex = world.Templates.IndexOf(selectedTemplate);
		}
		
		if(ImGui.BeginPopup("add-template")) {
			ImGui.Text("Create new template");
			ImGui.InputText("ID", ref newIdBuffer, Program.IMGUI_STRING_MAX);
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
			ImGui.InputText("ID", ref newIdBuffer, Program.IMGUI_STRING_MAX);
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

		if(selectedTemplate != null) {
			Vector2 areaMin = ImGui.GetWindowPos() + ImGui.GetStyle().WindowPadding;
			Vector2 areaMax = areaMin + ImGui.GetContentRegionAvail();
			DrawPreview(selectedTemplate, areaMin, areaMax);
		}

		ImGui.EndChild();
		
		ImGui.BeginChild("edit", ImGui.GetContentRegionAvail(), ImGuiChildFlags.Borders);
		if(selectedTemplate != null) {
			ImGui.SeparatorText("Entity Options");
			string name = selectedTemplate.Name;
			if(ImGui.InputText("Name", ref name, Program.IMGUI_STRING_MAX)) {}
			if(ImGui.IsItemDeactivatedAfterEdit()) {
				bool invalidName = name == "";
				foreach(var t in selectedTemplate.Collection.All) {
					if(t.Name == name) {
						invalidName = true;
						break;
					}
				}
				if(!invalidName) {
					Program.File.ApplyEdit(this, new Entity.NameOperation(selectedTemplate, name));
				}
			}
			string type = selectedTemplate.Type;
			if(ImGui.InputText("Type", ref type, Program.IMGUI_STRING_MAX)) {}
			if(ImGui.IsItemDeactivatedAfterEdit()) {
				Program.File.ApplyEdit(this, new Entity.TypeOperation(selectedTemplate, type));
			}
			Vector2 size = selectedTemplate.Size;
			if(ImGui.DragFloat2("Size", ref size)) {
				if(size.X < 0) size.X = 0;
				if(size.Y < 0) size.Y = 0;
				if(sizeEdit == null || sizeEdit.GetData<Entity.SizeOperation>().Entity != selectedTemplate) {
					sizeEdit = Program.File.BeginEdit(new Entity.SizeOperation(selectedTemplate, size));
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
	
	private void DrawPreview(Entity template, Vector2 areaMin, Vector2 areaMax, float areaUsed = 0.5F) {
		if(template == null) return;

		areaUsed = float.Clamp(areaUsed, 0.1F, 1.0F);
		
		Vector2 previewSize = areaMax - areaMin;
		Vector2 previewCenter = (areaMax + areaMin) / 2.0F;

		var drawList = ImGui.GetWindowDrawList();
		uint fillColor = Utilities.GetPackedColor(200, 200, 200, 30);
		uint borderColor = Utilities.GetPackedColor(200, 200, 200, 180);

		float scale = float.Min(
			(previewSize.X * areaUsed) / float.Max(template.Size.X, 1.0F),
			(previewSize.Y * areaUsed) / float.Max(template.Size.Y, 1.0F)
		);
			
		Vector2 e0 = previewCenter - (template.Size / 2.0F) * scale;
		Vector2 e1 = previewCenter + (template.Size / 2.0F) * scale;
		if(template.IsPoint) {
			float size = Entity.POINT_HANDLE_SIZE;
			drawList.AddCircleFilled(e0, size, fillColor);
			drawList.AddCircle(e0, size, borderColor);
			drawList.AddLine(e0 + new Vector2(-size / 3, 0), e0 + new Vector2(size / 3, 0), borderColor);
			drawList.AddLine(e0 + new Vector2(0, -size / 3), e0 + new Vector2(0, size / 3), borderColor);
		} else {
			drawList.AddRectFilled(e0, e1, fillColor);
			drawList.AddRect(e0, e1, borderColor);
		}
		Vector2 textSize = ImGui.CalcTextSize(template.Name);
		Vector2 textPos = new Vector2((e0.X + e1.X) / 2.0F, e0.Y) - (textSize / 2.0F) - new Vector2(0, 16);
		if(textSize.X > 0 && textPos.Y > 0) {
			if(template.IsPoint) textPos.Y -= Entity.POINT_HANDLE_SIZE;
			drawList.AddRectFilled(textPos - new Vector2(2, -1), textPos + textSize + new Vector2(8, 4), Utilities.GetPackedColor(10, 10, 10, 64), 4.0F);
			drawList.AddRectFilled(textPos - new Vector2(4, 1), textPos + textSize + new Vector2(6, 2), Utilities.GetPackedColor(180, 180, 180, 192), 4.0F);
			drawList.AddText(textPos + new Vector2(1), Utilities.GetPackedColor(10, 10, 10, 128), template.Name);
			drawList.AddText(textPos, Utilities.GetPackedColor(255, 255, 255, 255), template.Name);
		}
	}

	private Entity Items(Entity selected, Action<int> contextPopup = null) {
		World world = Program.File.World;
		
		ImGui.BeginChild("items", ImGui.GetContentRegionAvail(), ImGuiChildFlags.Borders);
		
		var drawList = ImGui.GetWindowDrawList();
		Vector2 areaPos = ImGui.GetCursorScreenPos();
		Vector2 areaSize = ImGui.GetContentRegionAvail();
		Vector2 areaOffset = new Vector2(0, 0);
		Vector2 itemSize = new Vector2(200, 200);
		Vector2 itemSpacing = new Vector2(4, 4);

		int columns = (int)(areaSize.X / (itemSize.X + itemSpacing.X));
		itemSize = new Vector2(areaSize.X / columns - itemSpacing.X);
		
		for(int i = 0; i < world.Templates.Count; i++) {
			ImGui.PushID(i);
			Entity template = world.Templates.Get(i);

			if(gridView) {
				Vector2 p0 = areaPos + areaOffset;
				Vector2 p1 = p0 + itemSize;
				
				drawList.AddRectFilled(p0, p1, Utilities.GetPackedColor(40, 40, 40, 255));
				drawList.AddRect(p0, p1, Utilities.GetPackedColor(80, 80, 80, 255));
				
				ImGui.SetCursorScreenPos(areaPos + areaOffset);
				ImGui.PushClipRect(p0, p1, true);
				if(template == selected) {
					drawList.AddRectFilled(p0, p1, Utilities.GetPackedColor(80, 80, 80, 80));
				}
				DrawPreview(template, p0, p1, 0.65F);
				if(ImGui.InvisibleButton("select", itemSize)) {
					if(selected != template) {
						selected = template;
					} else {
						selected = null;
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
			} else {
				if(ImGui.Selectable(template.Name, template == selected)) {
					if(selected != template) {
						selected = template;
					} else {
						selected = null;
					}
				}
			}

			contextPopup?.Invoke(i);
			
			ImGui.PopID();
		}
		
		ImGui.EndChild();

		return selected;
	}

	public void SelectModal(Action<Entity> onSelect) {
		Entity result = null;
		bool open = true;
		ImGui.SetNextWindowSizeConstraints(new Vector2(400, 300), ImGui.GetIO().DisplaySize);
		ImGui.SetNextWindowPos(ImGui.GetIO().DisplaySize / 2.0F, ImGuiCond.Always, new Vector2(0.5F, 0.5F));
		
		if(ImGui.BeginPopupModal("Select Template", ref open)) {
			Vector2 area = ImGui.GetContentRegionAvail();
			var style = ImGui.GetStyle();

			ImGui.BeginDisabled(!gridView);
			if(ImGui.Button("List")) {
				gridView = false;
			}
			ImGui.EndDisabled();
			
			ImGui.SameLine();
			ImGui.BeginDisabled(gridView);
			if(ImGui.Button("Grid")) {
				gridView = true;
			}
			ImGui.EndDisabled();
			
			ImGui.SameLine();
			
			string search = "";
			float searchWidth = 300;
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - searchWidth - ImGui.CalcTextSize("Search").X - ImGui.GetStyle().FramePadding.X);
			ImGui.SetNextItemWidth(searchWidth);
			ImGui.InputText("Search", ref search, Program.IMGUI_STRING_MAX);
			// TODO: searching

			result = Items(null);
			
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