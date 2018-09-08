using System.Collections;
using UnityEngine;

namespace MassOfVoice
{
    public class TextObjectFactory : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 1.0f)] float appearanceInterval = 0.1f;

        public IEnumerator CreateTextObjects(string text)
        {
            FlyingText.addRigidbodies = true;
            var textObjects = FlyingText.GetObjectsArray(text);

            foreach(var textObject in textObjects)
            {
                textObject.GetComponent<Renderer>().enabled = false;
                textObject.GetComponent<Rigidbody>().useGravity = false;
            }

            foreach(var textObject in textObjects)
            {
                yield return new WaitForSeconds(appearanceInterval);
                textObject.AddComponent<TextObjectController>().Initialize();
            }
        }
    }
}