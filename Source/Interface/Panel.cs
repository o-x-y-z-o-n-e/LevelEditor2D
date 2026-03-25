using ImGuiNET;

namespace L2D; 

public class Panel {
	
	public bool IsOpen => isOpen;

	public string Title;

	private bool isOpen;
	protected ImGuiWindowFlags flags;
	private bool focusNextFrame;

	public Panel() {
		flags |= ImGuiWindowFlags.NoCollapse;
		focusNextFrame = false;
	}

	public void Focus() {
		focusNextFrame = true;
	}
	
	public void Execute() {
		if(focusNextFrame) {
			focusNextFrame = false;
			ImGui.SetNextWindowFocus();
		}
		isOpen = ImGui.Begin(Title, flags);
		if(isOpen) {
			Update();
		}
		ImGui.End();
	}

	protected virtual void Update() {
		
	}
	
}