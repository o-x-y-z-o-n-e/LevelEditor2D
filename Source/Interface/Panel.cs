using ImGuiNET;

namespace L2D; 

public class Panel {

	public string Title;

	private bool open;
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
		if(ImGui.Begin(Title, flags)) {
			Update();
		}
		ImGui.End();
	}

	protected virtual void Update() {
		
	}
	
}