using System.Numerics;
using ImGuiNET;

namespace L2D; 

public class ConfirmModal {

	private bool open;
	private string title;
	private string message;
	private Action onConfirm;
	private Action onCancel;

	public ConfirmModal() {
		open = false;
		title = "";
		message = "";
		onConfirm = null;
		onCancel = null;
	}
	
	public void Open(string title, string message, Action onConfirm = null, Action onCancel = null) {
		open = true;
		this.title = title;
		this.message = message;
		this.onConfirm = onConfirm;
		this.onCancel = onCancel;
	}

	internal void Body() {
		if(open) {
			ImGui.OpenPopup(title);
			open = false;
		}
		ImGui.SetNextWindowPos(ImGui.GetIO().DisplaySize / 2.0F, ImGuiCond.Always, new Vector2(0.5F, 0.5F));
		bool o = true;
		if(ImGui.BeginPopupModal(title, ref o, ImGuiWindowFlags.AlwaysAutoResize)) {
			ImGui.Text(message);
			ImGui.Spacing();
			if(ImGui.Button("Confirm")) {
				ImGui.CloseCurrentPopup();
				onConfirm?.Invoke();
			}
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
				onCancel?.Invoke();
			}
			ImGui.EndPopup();
		}
	}
	
}