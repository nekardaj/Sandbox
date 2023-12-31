using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum UIAction
{
    Play,
    Quit,
    Menu,
    Continue
}

public class ButtonController : MonoBehaviour
{
    [SerializeField] private UIAction action;
    [SerializeField] private TMPro.TMP_InputField inputField;
    [SerializeField] private GameObject menu;
    public void OnClick()
    {
        if (action == UIAction.Play)
        {
            Debug.Log(inputField.text);
            if (int.TryParse(inputField.text, out int seed))
            {
                UnityEngine.Random.InitState(seed);
            }
            else
            {
                // If user does not enter a valid seed, use the current time as a seed
                UnityEngine.Random.InitState(System.DateTime.Now.Millisecond);
            }
            Chunk.InitializeSeed();
            Time.timeScale = 1.0f;
            SceneManager.LoadScene(1);
        }
        else if (action == UIAction.Quit)
        {
            Application.Quit();
        }
        else if (action == UIAction.Menu)
        {
            SceneManager.LoadScene(0);
        }
        else if (action == UIAction.Continue)
        {
            menu.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Time.timeScale = 1;
        }
    }
}
