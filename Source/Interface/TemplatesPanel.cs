using System.Numerics;
using System.Text.RegularExpressions;
using IconFonts;
using ImGuiNET;

namespace E2D;

public class TemplatesPanel : Panel {

	private bool addPopup;
	private bool copyPopup;
	private Entity copyTarget;
	private bool deletePopup;
	private Entity deleteTarget;
	
	private string newIdBuffer;

	private string search;
	private bool searchDirty;
	private List<Entity> searchMatches;
	
	private bool gridView;

	private FileEditEntry sizeEdit;
	
	public TemplatesPanel() {
		Title = $"{Codicons.Extensions} Templates";
		addPopup = false;
		copyPopup = false;
		copyTarget = null;
		deletePopup = false;
		deleteTarget = null;
		newIdBuffer = "";
		gridView = false;
		search = "";
		searchDirty = false;
		searchMatches = new();
	}

	protected override void Update() {
		if(Program.Project == null) {
			return;
		}

		World world = Program.Project.World;

		ImGui.Columns(2);
		ListView();
		ImGui.NextColumn();
		EditView();
	}
	
	private void ListView() {
		World world = Program.Project.World;
		
		var style = ImGui.GetStyle();

		Entity.MoveOperation moveOperation = null;
		
		if(ImGui.Button(Codicons.DiffAdded)) {
			addPopup = true;
		}
		ImGui.SetItemTooltip("Create");
		
		ImGui.BeginDisabled(Program.SelectedTemplate == null);
		
		ImGui.SameLine();
		if(ImGui.Button(Codicons.Copy)) {
			copyPopup = true;
			copyTarget = Program.SelectedTemplate;
		}
		ImGui.SetItemTooltip("Copy");
		
		ImGui.SameLine();
		if(ImGui.Button(Codicons.Trash)) {
			deletePopup = true;
			deleteTarget = Program.SelectedTemplate;
		}
		ImGui.SetItemTooltip("Delete");

		int selectedIndex = Program.SelectedTemplate != null ? world.Templates.IndexOf(Program.SelectedTemplate) : -1;
		ImGui.BeginDisabled(selectedIndex == 0);
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronUp)) {
			moveOperation = new Entity.MoveOperation(world.Templates, selectedIndex, selectedIndex - 1);
		}
		ImGui.SetItemTooltip("Move Up");
		ImGui.EndDisabled();

		ImGui.BeginDisabled(selectedIndex == world.Templates.Count - 1);
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronDown)) {
			moveOperation = new Entity.MoveOperation(world.Templates, selectedIndex, selectedIndex + 1);
		}
		ImGui.SetItemTooltip("Move Down");
		ImGui.EndDisabled();
		
		ImGui.EndDisabled(); // selectedTemplate == null
		
		float gridButtonX = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Grid").X - style.FramePadding.X * 2;
		ImGui.SameLine();
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

		Program.SelectedTemplate = Items(ref moveOperation, false, template => {
			ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags.MouseButtonRight);
			if(ImGui.BeginPopup("context")) {
				if(ImGui.MenuItem("Copy")) {
					copyPopup = true;
					copyTarget = template;
				}
				if(ImGui.MenuItem("Delete")) {
					deletePopup = true;
					deleteTarget = template;
				}
				ImGui.EndPopup();
			}
		});
		
		AddPopup();
		CopyPopup();
		DeletePopup();

		if(moveOperation != null) {
			Program.Project.ApplyEdit(moveOperation);
		}
	}
	
	private void EditView() {
		ImGui.SetCursorPosY(32);
		
		ImGui.BeginChild("preview", ImGui.GetContentRegionAvail(), ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY);

		if(Program.SelectedTemplate != null) {
			Vector2 areaMin = ImGui.GetWindowPos() + ImGui.GetStyle().WindowPadding;
			Vector2 areaMax = areaMin + ImGui.GetContentRegionAvail();
			DrawPreview(Program.SelectedTemplate, areaMin, areaMax);
		}

		ImGui.EndChild();
		
		ImGui.BeginChild("edit", ImGui.GetContentRegionAvail(), ImGuiChildFlags.Borders);
		if(Program.SelectedTemplate != null) {
			ImGui.SeparatorText("Entity Options");
			string name = Program.SelectedTemplate.Name;
			if(ImGui.InputText("Name", ref name, Program.IMGUI_STRING_MAX)) {}
			if(ImGui.IsItemDeactivatedAfterEdit()) {
				bool invalidName = name == "";
				foreach(var t in Program.SelectedTemplate.Collection.All) {
					if(t.Name == name) {
						invalidName = true;
						break;
					}
				}
				if(!invalidName) {
					Program.Project.ApplyEdit(this, new Entity.NameOperation(Program.SelectedTemplate, name));
				}
			}
			string type = Program.SelectedTemplate.Type;
			if(ImGui.InputText("Type", ref type, Program.IMGUI_STRING_MAX)) {}
			if(ImGui.IsItemDeactivatedAfterEdit()) {
				Program.Project.ApplyEdit(this, new Entity.TypeOperation(Program.SelectedTemplate, type));
			}
			Vector2 size = Program.SelectedTemplate.Size;
			if(ImGui.DragFloat2("Size", ref size)) {
				if(size.X < 0) size.X = 0;
				if(size.Y < 0) size.Y = 0;
				if(sizeEdit == null || sizeEdit.GetData<Entity.SizeOperation>().Entity != Program.SelectedTemplate) {
					sizeEdit = Program.Project.BeginEdit(new Entity.SizeOperation(Program.SelectedTemplate, size));
				} else {
					sizeEdit.GetData<Entity.SizeOperation>().SetSize(size);
				}
			}
			if(ImGui.IsItemDeactivatedAfterEdit() && sizeEdit != null) {
				Program.Project.EndEdit(ref sizeEdit, !sizeEdit.GetData<Entity.SizeOperation>().HasChanges());
			}
			PropertyView.Run(Program.SelectedTemplate.Properties);
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

	private unsafe Entity Items(ref Entity.MoveOperation moveOperation, bool selectOnly, Action<Entity> contextPopup = null) {
		World world = Program.Project.World;

		Entity selected = selectOnly ? null : Program.SelectedTemplate;
		
		ImGui.BeginChild("items", ImGui.GetContentRegionAvail(), ImGuiChildFlags.Borders);
		
		var drawList = ImGui.GetWindowDrawList();
		Vector2 areaPos = ImGui.GetCursorScreenPos();
		Vector2 areaSize = ImGui.GetContentRegionAvail();
		Vector2 areaOffset = new Vector2(0, 0);
		Vector2 itemSize = new Vector2(200, 200);
		Vector2 itemSpacing = new Vector2(4, 4);

		int columns = (int)(areaSize.X / (itemSize.X + itemSpacing.X));
		itemSize = new Vector2(areaSize.X / columns - itemSpacing.X);

		Vector2 cur = ImGui.GetCursorPos();
		int count = selectOnly ? searchMatches.Count : world.Templates.Count;
		for(int i = 0; i < count; i++) {
			ImGui.PushID(i);
			Entity template = selectOnly ? searchMatches[i] : world.Templates.Get(i);

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
				cur = ImGui.GetCursorPos();
				if(ImGui.InvisibleButton("select", itemSize)) {
					if(selected != template) {
						selected = template;
					} else {
						selected = null;
					}
				}
				if(template.Type != "") {
					ImGui.SetItemTooltip(template.Type);
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
				cur = ImGui.GetCursorPos();
				if(ImGui.Selectable(template.Name, template == selected)) {
					if(selected != template) {
						selected = template;
					} else {
						selected = null;
					}
				}
			}
			Vector2 nextCur = ImGui.GetCursorPos();

			if(!selectOnly) {
				contextPopup?.Invoke(template);

				if(ImGui.BeginDragDropSource()) {
					ImGui.Text(template.Name);
					ImGui.SetDragDropPayload("MOVE_TEMPLATE_DATA", (IntPtr)(&i), sizeof(int));
					ImGui.EndDragDropSource();
				}
				Vector2 scur = Vector2.Zero;
				if(gridView) {
					ImGui.SetCursorPos(cur - new Vector2(3, 0));
					scur = ImGui.GetCursorScreenPos();
					ImGui.Dummy(new Vector2(6, itemSize.Y));
				} else {
					ImGui.SetCursorPos(cur - new Vector2(0, 4));
					scur = ImGui.GetCursorScreenPos();
					ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 6));
					ImGui.SetCursorPos(nextCur);
				}
				if(moveOperation == null) {
					if(ImGui.BeginDragDropTarget()) {
						ImGuiPayloadPtr payloadPtr = ImGui.AcceptDragDropPayload("MOVE_TEMPLATE_DATA", ImGuiDragDropFlags.AcceptNoDrawDefaultRect | ImGuiDragDropFlags.AcceptBeforeDelivery);
						if(payloadPtr.NativePtr != null) {
							if(payloadPtr.IsPreview()) {
								if(gridView) {
									ImGui.GetWindowDrawList().AddRectFilled(
										scur,
										scur + new Vector2(3, itemSize.Y),
										Utilities.GetPackedColor(50, 80, 220, 255)
									);
								} else {
									ImGui.GetWindowDrawList().AddRectFilled(
										scur,
										scur + new Vector2(ImGui.GetContentRegionAvail().X, 3),
										Utilities.GetPackedColor(50, 80, 220, 255)
									);
								}
							}
							if(payloadPtr.IsDelivery()) {
								int srcIndex = ((int*)payloadPtr.Data)[0];
								int insertIndex = i;
								if(srcIndex < i) insertIndex--;
								if(srcIndex != insertIndex) {
									moveOperation = new Entity.MoveOperation(world.Templates, srcIndex, insertIndex);
								}
							}
						}
						ImGui.EndDragDropTarget();
					}
				}
			}

			if(!gridView) {
				ImGui.PushStyleColor(ImGuiCol.Text, Utilities.GetPackedColor(255, 255, 255, 80));
				float x = ImGui.CalcTextSize(template.Name).X + ImGui.GetStyle().FramePadding.X + 10;
			
				ImGui.SetCursorPosY(cur.Y);
				x = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.GetStyle().FramePadding.X;
			
				x -= ImGui.CalcTextSize(")").X;
				ImGui.SetCursorPosX(x);
				ImGui.Text(")");
				ImGui.SetCursorPosY(cur.Y);

				x -= ImGui.CalcTextSize(template.Type).X;
				ImGui.SetCursorPosX(x);
				ImGui.Text(template.Type);
				ImGui.SetCursorPosY(cur.Y);

				x -= ImGui.CalcTextSize("(").X;
				ImGui.SetCursorPosX(x);
				ImGui.Text("(");
				ImGui.PopStyleColor(); // ImGuiCol.Text
				
				if(i + 1 < count) ImGui.SetCursorPos(nextCur);
			}
			
			ImGui.PopID();
		}
		if(!selectOnly && count > 0) {
			Vector2 scur = Vector2.Zero;
			if(gridView) {
				ImGui.SetCursorScreenPos(areaPos + areaOffset - new Vector2(3, 0));
				scur = ImGui.GetCursorScreenPos();
				ImGui.Dummy(new Vector2(6, itemSize.Y));
			} else {
				float height = ImGui.GetCursorPosY() - cur.Y;
				ImGui.SetCursorPos(cur + new Vector2(0, height - 4));
				scur = ImGui.GetCursorScreenPos();
				ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 6));
			}
			if(moveOperation == null) {
				if(ImGui.BeginDragDropTarget()) {
					ImGuiPayloadPtr payloadPtr = ImGui.AcceptDragDropPayload("MOVE_TEMPLATE_DATA", ImGuiDragDropFlags.AcceptNoDrawDefaultRect | ImGuiDragDropFlags.AcceptBeforeDelivery);
					if(payloadPtr.NativePtr != null) {
						if(payloadPtr.IsPreview()) {
							if(gridView) {
								ImGui.GetWindowDrawList().AddRectFilled(
									scur,
									scur + new Vector2(3, itemSize.Y),
									Utilities.GetPackedColor(50, 80, 220, 255)
								);
							} else {
								ImGui.GetWindowDrawList().AddRectFilled(
									scur,
									scur + new Vector2(ImGui.GetContentRegionAvail().X, 3),
									Utilities.GetPackedColor(50, 80, 220, 255)
								);
							}
						}
						if(payloadPtr.IsDelivery()) {
							int srcIndex = ((int*)payloadPtr.Data)[0];
							if(srcIndex < world.Templates.Count - 1) {
								moveOperation = new Entity.MoveOperation(world.Templates, srcIndex, world.Templates.Count - 1);
							}
						}
					}
					ImGui.EndDragDropTarget();
				}
			}
		}
		
		ImGui.EndChild();

		return selected;
	}

	private void AddPopup() {
		World world = Program.Project.World;
		if(addPopup) {
			addPopup = false;
			newIdBuffer = "new entity";
			ImGui.OpenPopup("add-template");
		}
		if(ImGui.BeginPopup("add-template")) {
			ImGui.Text("Create new template");
			ImGui.InputText("ID", ref newIdBuffer, Program.IMGUI_STRING_MAX);
			bool emptyName = newIdBuffer == "";
			bool duplicateName = world.Templates.All.Any(t => t.Name == newIdBuffer);
			bool invalid = emptyName || duplicateName;
			ImGui.BeginDisabled(invalid);
			if(ImGui.Button("Confirm")) {
				Entity template = new Entity(world.Templates);
				template.SetName(newIdBuffer);
				Program.Project.ApplyEdit(this, new Entity.AddOperation(world.Templates, template));
				Program.SelectedTemplate = template;
				ImGui.CloseCurrentPopup();
			}
			if(invalid && ImGui.BeginItemTooltip()) {
				ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 0, 1));
				if(emptyName) {
					ImGui.Text($"Template name is empty!");
				}
				if(duplicateName) {
					ImGui.Text($"Template name already exists!");
				}
				ImGui.PopStyleColor();
				ImGui.EndTooltip();
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		}
	}

	private void CopyPopup() {
		World world = Program.Project.World;
		if(copyPopup) {
			copyPopup = false;
			newIdBuffer = "new entity";
			if(copyTarget != null) {
				ImGui.OpenPopup("copy-template");
			}
		}
		if(ImGui.BeginPopup("copy-template")) {
			ImGui.Text("Copy selected template");
			ImGui.InputText("ID", ref newIdBuffer, Program.IMGUI_STRING_MAX);
			bool emptyName = newIdBuffer == "";
			bool duplicateName = world.Templates.All.Any(t => t.Name == newIdBuffer);
			bool invalid = emptyName || duplicateName;
			ImGui.BeginDisabled(invalid);
			if(ImGui.Button("Confirm")) {
				Entity template = world.Templates.Copy(copyTarget);
				template.SetName(newIdBuffer);
				Program.Project.ApplyEdit(this, new Entity.AddOperation(world.Templates, template));
				Program.SelectedTemplate = template;
				ImGui.CloseCurrentPopup();
			}
			if(invalid && ImGui.BeginItemTooltip()) {
				ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 0, 1));
				if(emptyName) {
					ImGui.Text($"Template name is empty!");
				}
				if(duplicateName) {
					ImGui.Text($"Template name already exists!");
				}
				ImGui.PopStyleColor();
				ImGui.EndTooltip();
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		} else {
			copyTarget = null;
		}
	}

	private void DeletePopup() {
		World world = Program.Project.World;
		if(deletePopup) {
			deletePopup = false;
			if(deleteTarget != null) {
				ImGui.OpenPopup("delete-template");
			}
		}
		if(ImGui.BeginPopup("delete-template")) {
			ImGui.Text("Delete selected template");
			if(ImGui.Button("Confirm")) {
				Program.Project.ApplyEdit(this, new Entity.RemoveOperation(world.Templates, deleteTarget));
				if(deleteTarget == Program.SelectedTemplate) {
					Program.SelectedTemplate = null;
				}
				ImGui.CloseCurrentPopup();
			}
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		} else {
			deleteTarget = null;
		}
	}

	public void SelectModal(bool open, Action<Entity> onSelect) {
		Entity result = null;

		if(open) {
			ImGui.OpenPopup("Select Template");
			searchDirty = true;
		}

		open = true;
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
			
			float searchWidth = 300;
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - searchWidth - ImGui.CalcTextSize("Search").X - ImGui.GetStyle().FramePadding.X);
			ImGui.SetNextItemWidth(searchWidth);
			ImGui.InputText("Search", ref search, Program.IMGUI_STRING_MAX);
			if(ImGui.IsItemDeactivatedAfterEdit()) {
				searchDirty = true;
			}

			if(searchDirty) {
				SearchMatches();
			}

			Entity.MoveOperation moveOperation = null;
			result = Items(ref moveOperation, true, null);
			
			if(result != null) {
				ImGui.CloseCurrentPopup();
			}
			
			ImGui.EndPopup();
		}
		
		if(result != null) {
			onSelect?.Invoke(result);
		}
	}
	
	private void SearchMatches() {
		World world = Program.Project.World;
		var regex = new Regex(Regex.Escape(search));
		searchMatches.Clear();
		for(int i = 0; i < world.Templates.Count; i++) {
			Entity template = world.Templates.Get(i);
			if(search != "") {
				var nameMatch = regex.Match(template.Name);
				if(nameMatch.Success) {
					searchMatches.Add(template);
				}
			} else {
				searchMatches.Add(template);
			}
		}
	}

}