using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _3DConnections
{
    public class LoadSceneAdditive : MonoBehaviour
    {
        private static bool IsSceneLoaded(string sceneName)
        {
            // Iterate through all loaded scenes
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);

                // Check if the scene name matches
                if (scene.name == sceneName && scene.isLoaded)
                {
                    return true;
                }
            }

            // Return false if no matching scene is found
            return false;
        }
        
        private void OnGUI()
        {
            const string sceneNameToCheck = "NewScene";
            //Whereas pressing this Button loads the Additive Scene.
            if (!GUI.Button(new Rect(20, 30, 150, 30), "Other Scene Additive")) return;
            if (!IsSceneLoaded(sceneNameToCheck))
            {
                // Load new Scene in overlapping mode (additive)
                SceneManager.LoadScene("NewScene", LoadSceneMode.Additive);
            }
        }
    }
}