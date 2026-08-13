using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayMainGame : MonoBehaviour
{
    public void PlayGame ()
    {
        SceneManager.LoadScene("MainGame");
    }
}
