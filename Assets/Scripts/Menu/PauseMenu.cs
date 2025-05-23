using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PauseMenu : Menu
{
	public Button quitButton;
	[SerializeField] private Selectable[] menuItems;
	private int currentIndex = 0;

	void Start()
	{
		quitButton.onClick.AddListener(GameController.ExitToMainMenu);
		if (menuItems == null || menuItems.Length == 0)
		{
			menuItems = new Selectable[] { quitButton };
		}
	}

	void Update()
	{
		if (!IsOpen) return;

		var gamepad = Gamepad.current;
		if (gamepad != null)
		{
			if (gamepad.dpad.up.wasPressedThisFrame)
			{
				NavigateButtons(-1);
			}
			else if (gamepad.dpad.down.wasPressedThisFrame)
			{
				NavigateButtons(1);
			}
			else if (gamepad.buttonSouth.wasPressedThisFrame)
			{
				if (menuItems[currentIndex] is Button button)
				{
					button.onClick.Invoke();
				}
			}
			else if (gamepad.buttonEast.wasPressedThisFrame)
			{
				CloseMenu();
			}
		}
	}

	void NavigateButtons(int direction)
	{
		currentIndex = (currentIndex + direction + menuItems.Length) % menuItems.Length;
		menuItems[currentIndex].Select();
	}

	public void TogglePauseMenu()
	{
		if (IsOpen)
		{
			CloseMenu();
		}
		else
		{
			OpenMenu();
		}
	}

	protected override void OnMenuOpened()
	{
		base.OnMenuOpened();
		GameController.SetPauseState(true);
		
		// Set initial selection
		currentIndex = 0;
		if (menuItems.Length > 0)
		{
			menuItems[currentIndex].Select();
		}
	}

	protected override void OnMenuClosed()
	{
		base.OnMenuClosed();
		GameController.SetPauseState(false);
	}
}
